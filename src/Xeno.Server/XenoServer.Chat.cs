namespace Xeno.Server
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using WatsonWebserver.Core;
    using Xeno.Core.Chat;
    using Xeno.Core.Chat.Wire;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Server chat: conversations, streaming turns, feedback, and chat history for signed-in users of the server web
    /// dashboard. Turns run through the same chat engine as the Xeno client and reach models through the server's
    /// network relay, so routing, request history, and credits work as for any network request. No tools.
    /// </summary>
    public sealed partial class XenoServer
    {
        #region Private-Members

        private const string ChatResource = "Chat";
        private const string ChatPath = "/v1/chat/completions";
        private readonly ConcurrentDictionary<string, KeyValuePair<string, ChatEngine>> _ActiveChatTurns = new ConcurrentDictionary<string, KeyValuePair<string, ChatEngine>>(StringComparer.Ordinal);

        #endregion

        #region Internal-Methods

        /// <summary>
        /// Handle a chat route.
        /// </summary>
        /// <param name="context">HTTP context.</param>
        /// <param name="requestContext">Request context.</param>
        /// <param name="action">Route action name.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        internal async Task HandleChatAsync(HttpContextBase context, RequestContext requestContext, string action, CancellationToken token)
        {
            if (_Database?.Chat == null) throw new Xeno.Core.Exceptions.HttpStatusException(503, "ChatUnavailable", "Chat storage is not available.");
            string? id = context.Request.Url.Parameters.Get("id");
            switch (action)
            {
                case "models":
                    await RequirePermissionAsync(requestContext, ChatResource, OperationTypeEnum.Read, token).ConfigureAwait(false);
                    await SendJsonAsync(context, await ChatModelsAsync(requestContext, token).ConfigureAwait(false)).ConfigureAwait(false);
                    return;
                case "models.prepare":
                {
                    await RequirePermissionAsync(requestContext, ChatResource, OperationTypeEnum.Read, token).ConfigureAwait(false);
                    ChatModelPrepareRequest body = ReadJsonBody<ChatModelPrepareRequest>(context);
                    await SendJsonAsync(context, new ChatModelReadiness { Model = body.Model, Route = "network", Message = "Xeno network models load on the machine that serves them when first asked." }).ConfigureAwait(false);
                    return;
                }
                case "conversations.list":
                    await RequirePermissionAsync(requestContext, ChatResource, OperationTypeEnum.Read, token).ConfigureAwait(false);
                    await SendJsonAsync(context, await OwnStore(requestContext).ListConversationsAsync(ChatQuery(context, "search"), ParseChatEnum<ChatSurfaceEnum>(ChatQuery(context, "surface")), ParseIntegerQuery(context, "limit", 200, 1, 1000), token).ConfigureAwait(false)).ConfigureAwait(false);
                    return;
                case "conversations.create":
                {
                    await RequirePermissionAsync(requestContext, ChatResource, OperationTypeEnum.Write, token).ConfigureAwait(false);
                    ChatConversationRequest body = ReadJsonBody<ChatConversationRequest>(context);
                    ChatConversation created = await OwnStore(requestContext).CreateConversationAsync(new ChatConversation
                    {
                        Title = String.IsNullOrWhiteSpace(body.Title) ? "New chat" : body.Title.Trim(),
                        Model = body.Model?.Trim() ?? String.Empty,
                        SystemPrompt = String.IsNullOrWhiteSpace(body.SystemPrompt) ? null : body.SystemPrompt,
                        Pinned = body.Pinned ?? false,
                        Surface = ChatSurfaceEnum.Server
                    }, token).ConfigureAwait(false);
                    await SendStatusJsonAsync(context, 201, created).ConfigureAwait(false);
                    return;
                }
                case "conversations.get":
                {
                    await RequirePermissionAsync(requestContext, ChatResource, OperationTypeEnum.Read, token).ConfigureAwait(false);
                    ChatConversation conversation = await OwnStore(requestContext).GetConversationAsync(RequireId(id), true, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("Conversation not found.");
                    await SendJsonAsync(context, conversation).ConfigureAwait(false);
                    return;
                }
                case "conversations.update":
                {
                    await RequirePermissionAsync(requestContext, ChatResource, OperationTypeEnum.Write, token).ConfigureAwait(false);
                    IChatStore store = OwnStore(requestContext);
                    ChatConversationRequest body = ReadJsonBody<ChatConversationRequest>(context);
                    ChatConversation conversation = await store.GetConversationAsync(RequireId(id), false, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("Conversation not found.");
                    if (body.Title != null) conversation.Title = String.IsNullOrWhiteSpace(body.Title) ? "New chat" : body.Title.Trim();
                    if (body.Model != null) conversation.Model = body.Model.Trim();
                    if (body.SystemPrompt != null) conversation.SystemPrompt = String.IsNullOrWhiteSpace(body.SystemPrompt) ? null : body.SystemPrompt;
                    if (body.Pinned.HasValue) conversation.Pinned = body.Pinned.Value;
                    await store.UpdateConversationAsync(conversation, token).ConfigureAwait(false);
                    await SendJsonAsync(context, conversation).ConfigureAwait(false);
                    return;
                }
                case "conversations.delete":
                    await RequirePermissionAsync(requestContext, ChatResource, OperationTypeEnum.Delete, token).ConfigureAwait(false);
                    await SendJsonAsync(context, new ChatMutationResponse { Changed = await OwnStore(requestContext).DeleteConversationAsync(RequireId(id), token).ConfigureAwait(false) }).ConfigureAwait(false);
                    return;
                case "messages.send":
                    await RequirePermissionAsync(requestContext, ChatResource, OperationTypeEnum.Execute, token).ConfigureAwait(false);
                    await StreamChatTurnAsync(context, requestContext, RequireId(id), ReadJsonBody<ChatSendRequest>(context), token).ConfigureAwait(false);
                    return;
                case "turns.cancel":
                {
                    await RequirePermissionAsync(requestContext, ChatResource, OperationTypeEnum.Execute, token).ConfigureAwait(false);
                    bool cancelled = _ActiveChatTurns.TryGetValue(RequireId(id), out KeyValuePair<string, ChatEngine> active) && String.Equals(active.Key, OwnerKey(requestContext), StringComparison.Ordinal) && active.Value.Cancel(id!);
                    await SendJsonAsync(context, new ChatMutationResponse { Changed = cancelled }).ConfigureAwait(false);
                    return;
                }
                case "feedback.set":
                    await RequirePermissionAsync(requestContext, ChatResource, OperationTypeEnum.Write, token).ConfigureAwait(false);
                    await SendJsonAsync(context, await OwnStore(requestContext).SetFeedbackAsync(RequireId(id), ReadJsonBody<ChatFeedbackRequest>(context), token).ConfigureAwait(false)).ConfigureAwait(false);
                    return;
                case "feedback.delete":
                    await RequirePermissionAsync(requestContext, ChatResource, OperationTypeEnum.Write, token).ConfigureAwait(false);
                    await SendJsonAsync(context, new ChatMutationResponse { Changed = await OwnStore(requestContext).DeleteFeedbackAsync(RequireId(id), token).ConfigureAwait(false) }).ConfigureAwait(false);
                    return;
                case "feedback.list":
                {
                    IChatStore store = await ReadScopeAsync(context, requestContext, token).ConfigureAwait(false);
                    await SendJsonAsync(context, await store.QueryFeedbackAsync(new ChatFeedbackQuery
                    {
                        Rating = ParseNullableChatInt(ChatQuery(context, "rating")),
                        Model = ChatQuery(context, "model"),
                        Route = ChatQuery(context, "route"),
                        HasComment = ParseNullableChatBool(ChatQuery(context, "hasComment")),
                        Search = ChatQuery(context, "search"),
                        FromUtc = ParseDateQuery(context, "fromUtc"),
                        ToUtc = ParseDateQuery(context, "toUtc"),
                        Page = ParseIntegerQuery(context, "page", 1, 1, 100000),
                        PageSize = ParseIntegerQuery(context, "pageSize", 50, 1, 500)
                    }, token).ConfigureAwait(false)).ConfigureAwait(false);
                    return;
                }
                case "history.list":
                {
                    IChatStore store = await ReadScopeAsync(context, requestContext, token).ConfigureAwait(false);
                    await SendJsonAsync(context, await store.QueryTurnsAsync(new ChatHistoryQuery
                    {
                        Model = ChatQuery(context, "model"),
                        Route = ChatQuery(context, "route"),
                        Provider = ChatQuery(context, "provider"),
                        Status = ParseChatEnum<ChatTurnStatusEnum>(ChatQuery(context, "status")),
                        Surface = ParseChatEnum<ChatSurfaceEnum>(ChatQuery(context, "surface")),
                        Rating = ParseNullableChatInt(ChatQuery(context, "rating")),
                        ConversationId = ChatQuery(context, "conversationId"),
                        Search = ChatQuery(context, "search"),
                        FromUtc = ParseDateQuery(context, "fromUtc"),
                        ToUtc = ParseDateQuery(context, "toUtc"),
                        Page = ParseIntegerQuery(context, "page", 1, 1, 100000),
                        PageSize = ParseIntegerQuery(context, "pageSize", 50, 1, 500)
                    }, token).ConfigureAwait(false)).ConfigureAwait(false);
                    return;
                }
                case "history.get":
                {
                    IChatStore store = await ReadScopeAsync(context, requestContext, token).ConfigureAwait(false);
                    ChatTurn turn = await store.GetTurnAsync(RequireId(id), token).ConfigureAwait(false) ?? throw new KeyNotFoundException("Turn not found.");
                    await SendJsonAsync(context, turn).ConfigureAwait(false);
                    return;
                }
                case "analytics":
                {
                    IChatStore store = await ReadScopeAsync(context, requestContext, token).ConfigureAwait(false);
                    DateTime to = ParseDateQuery(context, "toUtc") ?? DateTime.UtcNow;
                    DateTime from = ParseDateQuery(context, "fromUtc") ?? to.AddHours(-24);
                    if (to <= from) throw new ArgumentException("toUtc must be after fromUtc.");
                    await SendJsonAsync(context, await store.AnalyticsAsync(from, to, BucketSeconds(context), token).ConfigureAwait(false)).ConfigureAwait(false);
                    return;
                }
                case "analytics.usage":
                {
                    IChatStore store = await ReadScopeAsync(context, requestContext, token).ConfigureAwait(false);
                    await HandleChatUsageAnalyticsAsync(context, requestContext, store, token).ConfigureAwait(false);
                    return;
                }
                default:
                    throw new KeyNotFoundException("Chat route not found.");
            }
        }

        #endregion

        #region Private-Methods

        private IChatStore OwnStore(RequestContext requestContext)
        {
            return _Database!.Chat!.For(requestContext.TenantId, Owner(requestContext));
        }

        private static string Owner(RequestContext requestContext)
        {
            string? owner = requestContext.UserId ?? requestContext.PrincipalId;
            if (String.IsNullOrWhiteSpace(owner)) throw new UnauthorizedAccessException("Sign in to chat.");
            return owner;
        }

        private static string OwnerKey(RequestContext requestContext)
        {
            return requestContext.TenantId + "/" + Owner(requestContext);
        }

        private async Task<IChatStore> ReadScopeAsync(HttpContextBase context, RequestContext requestContext, CancellationToken token)
        {
            if (String.Equals(ChatQuery(context, "scope"), "tenant", StringComparison.OrdinalIgnoreCase))
            {
                await RequirePermissionAsync(requestContext, "ChatAdmin", OperationTypeEnum.Read, token).ConfigureAwait(false);
                return _Database!.Chat!.For(requestContext.TenantId, null);
            }

            await RequirePermissionAsync(requestContext, ChatResource, OperationTypeEnum.Read, token).ConfigureAwait(false);
            return OwnStore(requestContext);
        }

        private async Task<ChatModelList> ChatModelsAsync(RequestContext requestContext, CancellationToken token)
        {
            List<XenoClientRecord> clients = await VisibleClientsForTenantAsync(requestContext.TenantId, token).ConfigureAwait(false);
            List<ChatModelOption> models = clients
                .Where(item => item.Connected && item.Active)
                .SelectMany(item => item.Models)
                .Where(item => !ChatModelNames.IsEmbeddingOnly(item.Name, item.SupportsCompletions, item.SupportsEmbeddings))
                .GroupBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .Select(group => new ChatModelOption
                {
                    Name = group.First().Name,
                    OnNetwork = true,
                    SupportsCompletions = group.Any(item => item.SupportsCompletions),
                    SupportsEmbeddings = group.Any(item => item.SupportsEmbeddings)
                })
                .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new ChatModelList { Entries = models, NetworkMessage = models.Count == 0 ? "No connected client serves a model to this tenant right now." : String.Empty };
        }

        private async Task StreamChatTurnAsync(HttpContextBase context, RequestContext requestContext, string conversationId, ChatSendRequest request, CancellationToken token)
        {
            if (String.IsNullOrWhiteSpace(request.Content)) throw new ArgumentException("Type a message to send.");
            IChatStore store = OwnStore(requestContext);
            ChatConversation conversation = await store.GetConversationAsync(conversationId, false, token).ConfigureAwait(false) ?? throw new KeyNotFoundException("Conversation not found.");
            if (String.IsNullOrWhiteSpace(request.Model) && String.IsNullOrWhiteSpace(conversation.Model)) throw new ArgumentException("Choose a model for this conversation.");
            request.Surface = ChatSurfaceEnum.Server;

            string ownerKey = OwnerKey(requestContext);
            ChatEngine engine = new ChatEngine(store, new RelayTransport(this, requestContext), () => new DefaultChatLimits());
            context.Response.StatusCode = 200;
            context.Response.ContentType = ChatSse.ContentType;
            context.Response.Headers.Add("Cache-Control", "no-store");
            context.Response.Headers.Add("X-Accel-Buffering", "no");
            context.Response.ChunkedTransfer = true;
            SemaphoreSlim gate = new SemaphoreSlim(1, 1);
            string? turnId = null;
            try
            {
                await engine.SendAsync(conversationId, request, async item =>
                {
                    if (item.Kind == ChatStreamEventKindEnum.Turn && item.TurnId != null)
                    {
                        turnId = item.TurnId;
                        _ActiveChatTurns[turnId] = new KeyValuePair<string, ChatEngine>(ownerKey, engine);
                    }

                    byte[] bytes = Encoding.UTF8.GetBytes(ChatSse.Format(item));
                    await gate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                    try
                    {
                        await context.Response.SendChunk(bytes, false, token).ConfigureAwait(false);
                    }
                    finally
                    {
                        gate.Release();
                    }
                }, token).ConfigureAwait(false);
            }
            finally
            {
                if (turnId != null) _ActiveChatTurns.TryRemove(turnId, out _);
                try
                {
                    await context.Response.SendChunk(Array.Empty<byte>(), true, CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is IOException || ex is ObjectDisposedException || ex is InvalidOperationException || ex is OperationCanceledException)
                {
                }

                gate.Dispose();
            }
        }

        /// <summary>
        /// Send one chat completion through the network relay: route, dispatch to the chosen client, stream the answer
        /// bytes to a sink, and record request history (and so credits) as for any network request. Retries once on
        /// another client when the first is busy.
        /// </summary>
        private async Task<ChatTransportResult> RelayChatCompletionAsync(RequestContext caller, ChatCompletionRequestDto request, Func<byte[], int, Task> sink, CancellationToken token)
        {
            RequestContext requestContext = JsonUtil.Deserialize<RequestContext>(JsonUtil.Serialize(caller)) ?? new RequestContext();
            requestContext.RequestId = IdGenerator.NewRequestHistoryId();
            requestContext.StartedUtc = DateTime.UtcNow;
            requestContext.RequestMethod = "POST";
            requestContext.RequestPath = ChatPath;
            string body = JsonUtil.Serialize(request);
            RoutingDecision decision = await RequireRouting().RouteAsync(requestContext, request.Model, false, token).ConfigureAwait(false);
            if (!decision.Success)
            {
                await RecordAndSaveHistoryAsync(requestContext, "POST", ChatPath, decision, token).ConfigureAwait(false);
                return new ChatTransportResult { Route = "unavailable", StatusCode = decision.HttpStatusCode, Error = decision.Message, FailureKind = decision.HttpStatusCode == 504 ? ChatFailureKindEnum.Timeout : decision.HttpStatusCode >= 500 || decision.HttpStatusCode == 404 || decision.HttpStatusCode == 429 ? ChatFailureKindEnum.Unavailable : (ChatFailureKindEnum?)null };
            }

            ChatRelayOutcome first = await RelayChatOnceAsync(requestContext, decision, body, sink, token, true).ConfigureAwait(false);
            if (!first.Busy) return first.Result;
            RoutingDecision retry = await RequireRouting().RouteAsync(requestContext, request.Model, false, null, token, decision.SelectedClientId).ConfigureAwait(false);
            retry.Timeline.Insert(0, "ClientBusyRetry");
            if (!retry.Success)
            {
                retry.HttpStatusCode = 503;
                retry.OutcomeCode = "AllClientsBusy";
                retry.Message = "Every client that serves this model is at its concurrency limit. Try again shortly.";
                await RecordAndSaveHistoryAsync(requestContext, "POST", ChatPath, retry, token).ConfigureAwait(false);
                return new ChatTransportResult { Route = "network", StatusCode = 503, Error = retry.Message, FailureKind = ChatFailureKindEnum.Unavailable };
            }

            requestContext.RequestId = IdGenerator.NewRequestHistoryId();
            return (await RelayChatOnceAsync(requestContext, retry, body, sink, token, false).ConfigureAwait(false)).Result;
        }

        private async Task<ChatRelayOutcome> RelayChatOnceAsync(RequestContext requestContext, RoutingDecision decision, string body, Func<byte[], int, Task> sink, CancellationToken token, bool allowBusy)
        {
            ChatTransportResult result = new ChatTransportResult { Route = "network" };
            XenoClientRecord? client = await ResolveSelectedClientAsync(requestContext.TenantId, decision.SelectedClientId, token).ConfigureAwait(false);
            ClientConnection? connection = client == null ? null : GetClientConnection(decision.SelectedClientId);
            if (client == null || connection == null || !connection.Session.IsConnected)
            {
                decision.Success = false;
                decision.HttpStatusCode = client == null ? 502 : 503;
                decision.OutcomeCode = client == null ? "ClientMissing" : "ClientNotConnected";
                decision.Message = client == null ? "The selected client no longer exists." : "The selected client is not connected.";
                decision.Timeline.Add(decision.OutcomeCode);
                await RecordAndSaveHistoryAsync(requestContext, "POST", ChatPath, decision, token).ConfigureAwait(false);
                result.StatusCode = decision.HttpStatusCode;
                result.Error = decision.Message;
                result.FailureKind = ChatFailureKindEnum.Unavailable;
                return new ChatRelayOutcome(result, false);
            }

            result.Provider = client.Name;
            string requestId = requestContext.RequestId;
            PendingClientResponse pending = new PendingClientResponse();
            lock (_PendingResponsesLock)
            {
                _PendingClientResponses[requestId] = pending;
            }

            XenoClientModel? selectedModel = FindSelectedModel(client, decision, false);
            client.InFlightRequests++;
            await _Database!.Clients!.AdjustInFlightAsync(client.TenantId, client.Id, 1, token).ConfigureAwait(false);
            ClientInFlightSlot slot = new ClientInFlightSlot(_Database!.Clients!, client);
            // The same forwarding headers the HTTP relay sends, built from the chat caller's request context.
            string requestorIp = SanitizeHeaderValue(requestContext.SourceIp);
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "application/json" };
            AddForwardedHeader(headers, "X-Forwarded-For", requestorIp);
            AddForwardedHeader(headers, "X-Real-IP", requestorIp);
            AddForwardedHeader(headers, "X-Xeno-Requestor-IP", requestorIp);
            AddForwardedHeader(headers, "X-Xeno-Request-ID", requestContext.RequestId);
            AddForwardedHeader(headers, "X-Xeno-Tenant-ID", requestContext.TenantId);
            DateTime started = DateTime.UtcNow;
            bool dispatched = false;
            try
            {
                ServerRequestMessage payload = new ServerRequestMessage
                {
                    Type = "server.request",
                    RequestId = requestId,
                    TenantId = requestContext.TenantId,
                    RequestorTenantId = requestContext.TenantId,
                    RequestorPrincipalType = requestContext.PrincipalType.ToString(),
                    RequestorPrincipalId = requestContext.PrincipalId,
                    ClientId = client.Id,
                    ClientName = client.Name,
                    ApiType = selectedModel?.Provider.ToString(),
                    Source = selectedModel?.Source,
                    Method = "POST",
                    RelativePath = NormalizeOpenAiPath(ChatPath),
                    Model = decision.EffectiveModel,
                    Embeddings = false,
                    Stream = true,
                    RequestorIp = requestorIp,
                    Headers = headers,
                    Body = body
                };
                decision.Timeline.Add("ClientDispatch");
                await SendToClientAsync(connection, JsonUtil.Serialize(payload), token).ConfigureAwait(false);
                dispatched = true;
                ClientResponseMessage first = await WaitForClientFirstResponseAsync(requestId, pending, EffectiveRequestTimeoutMs(decision), token).ConfigureAwait(false);
                if (allowBusy && first.Busy)
                {
                    decision.Timeline.Add("ClientBusy");
                    return new ChatRelayOutcome(result, true);
                }

                RequestHistoryEntry history = RequireRouting().RecordHistory(requestContext, ChatPath, "POST", decision);
                ClientResponseMessage final;
                if (IsClientResponseStart(first))
                {
                    result.StatusCode = first.StatusCode ?? (first.Success ? 200 : 502);
                    using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                    {
                        timeout.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, _Settings.WebSocket.RequestTimeoutMs)));
                        using (timeout.Token.Register(delegate { pending.Cancel(timeout.Token); }))
                        {
                            while (await pending.Chunks.Reader.WaitToReadAsync(timeout.Token).ConfigureAwait(false))
                            {
                                while (pending.Chunks.Reader.TryRead(out ClientResponseChunkMessage? chunk))
                                {
                                    if (chunk == null) continue;
                                    byte[] bytes = chunk.Decode();
                                    if (bytes.Length > 0) await sink(bytes, bytes.Length).ConfigureAwait(false);
                                }
                            }

                            final = await pending.Final.Task.ConfigureAwait(false);
                        }
                    }
                }
                else
                {
                    final = first;
                    byte[] whole = Encoding.UTF8.GetBytes(first.Body ?? String.Empty);
                    if (whole.Length > 0) await sink(whole, whole.Length).ConfigureAwait(false);
                }

                await slot.ReleaseAsync().ConfigureAwait(false);
                decision.ClientRoundTripMs = Math.Max(0, (long)(DateTime.UtcNow - started).TotalMilliseconds);
                decision.Timeline.Add("ClientResponseComplete");
                int statusCode = final.StatusCode ?? result.StatusCode ?? (final.Success ? 200 : 502);
                decision.Success = final.Success;
                decision.HttpStatusCode = statusCode;
                decision.OutcomeCode = final.Success ? "Completed" : "ClientRequestFailed";
                decision.Message = final.Success ? "Client completed the chat request." : final.Error ?? "Client request failed.";
                PopulateRequestHistory(history, requestContext, body, headers, final.Body ?? String.Empty, ExtractHeaders(final), final, decision);
                await TryEnqueueLedgerEventAsync(history, token).ConfigureAwait(false);
                await InsertRequestHistoryAsync(history, CancellationToken.None).ConfigureAwait(false);
                result.StatusCode = statusCode;
                result.Completed = final.Success;
                if (!final.Success) result.Error = String.IsNullOrWhiteSpace(final.Error) ? "The client could not answer." : final.Error;
                return new ChatRelayOutcome(result, false);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                decision.Success = false;
                decision.HttpStatusCode = 499;
                decision.OutcomeCode = "CallerDisconnected";
                decision.Message = "The chat turn was stopped before the client finished.";
                decision.Timeline.Add("CallerDisconnected");
                if (dispatched)
                {
                    try
                    {
                        await SendRequestCancellationAsync(connection, requestId, "CallerDisconnected", requestorIp, "POST", NormalizeOpenAiPath(ChatPath), selectedModel?.Source, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is InvalidOperationException || ex is IOException || ex is WebSocketException)
                    {
                    }
                }

                await RecordAndSaveHistoryAsync(requestContext, "POST", ChatPath, decision, CancellationToken.None).ConfigureAwait(false);
                throw;
            }
            catch (OperationCanceledException)
            {
                decision.Success = false;
                decision.HttpStatusCode = 504;
                decision.OutcomeCode = "ClientTimeout";
                decision.Message = "The selected client did not respond before the timeout.";
                decision.Timeline.Add("ClientTimeout");
                await RecordAndSaveHistoryAsync(requestContext, "POST", ChatPath, decision, CancellationToken.None).ConfigureAwait(false);
                result.StatusCode = 504;
                result.Error = decision.Message;
                result.FailureKind = ChatFailureKindEnum.Timeout;
                return new ChatRelayOutcome(result, false);
            }
            finally
            {
                await slot.ReleaseAsync().ConfigureAwait(false);
                lock (_PendingResponsesLock)
                {
                    _PendingClientResponses.Remove(requestId);
                }

            }
        }

        private static string? ChatQuery(HttpContextBase context, string name)
        {
            string value = DecodeQueryValue(context.Request.Query.Elements.Get(name));
            return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static string RequireId(string? id)
        {
            if (String.IsNullOrWhiteSpace(id)) throw new ArgumentException("An id is required.");
            return Uri.UnescapeDataString(id);
        }

        private static int? ParseNullableChatInt(string? value)
        {
            if (value == null) return null;
            if (Int32.TryParse(value, out int parsed)) return parsed;
            throw new ArgumentException("'" + value + "' is not a number.");
        }

        private static bool? ParseNullableChatBool(string? value)
        {
            if (value == null) return null;
            if (Boolean.TryParse(value, out bool parsed)) return parsed;
            throw new ArgumentException("'" + value + "' is not true or false.");
        }

        private static T? ParseChatEnum<T>(string? value) where T : struct, Enum
        {
            if (value == null) return null;
            if (Enum.TryParse(value, true, out T parsed) && Enum.IsDefined(parsed)) return parsed;
            throw new ArgumentException("'" + value + "' is not one of: " + String.Join(", ", Enum.GetNames<T>()) + ".");
        }

        #endregion

        #region Private-Classes

        private sealed class ChatRelayOutcome
        {
            internal ChatRelayOutcome(ChatTransportResult result, bool busy)
            {
                Result = result;
                Busy = busy;
            }

            internal ChatTransportResult Result { get; }

            internal bool Busy { get; }
        }

        private sealed class RelayTransport : IChatCompletionTransport
        {
            private readonly XenoServer _Server;
            private readonly RequestContext _Caller;

            internal RelayTransport(XenoServer server, RequestContext caller)
            {
                _Server = server;
                _Caller = caller;
            }

            public Task<ChatTransportResult> SendAsync(ChatCompletionRequestDto request, Func<byte[], int, Task> onBytes, CancellationToken token)
            {
                return _Server.RelayChatCompletionAsync(_Caller, request, onBytes, token);
            }
        }

        #endregion

        // Chat activity bucket size in seconds: bucketSeconds, or bucketMinutes for older callers.
        private static int BucketSeconds(HttpContextBase context)
        {
            string? seconds = context.Request.Query.Elements.Get("bucketSeconds");
            if (!String.IsNullOrWhiteSpace(seconds)) return ParseIntegerQuery(context, "bucketSeconds", 3600, 1, 6000000);
            return ParseIntegerQuery(context, "bucketMinutes", 60, 1, 100000) * 60;
        }
    }
}

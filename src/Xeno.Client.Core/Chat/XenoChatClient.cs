namespace Xeno.Client.Chat
{
    using System;
    using Xeno.Core.Chat;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Services;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Calls the running client's chat API. The CLI, desktop app, and terminal app use it; the local web dashboard
    /// calls the same routes from the browser. The endpoint comes from <see cref="LocalApiEndpoint"/>, read on every
    /// call so a client restart is picked up. Thread safe.
    /// </summary>
    public sealed class XenoChatClient : IDisposable
    {
        #region Public-Members

        /// <summary>
        /// Message shown when the client's local API is not running.
        /// </summary>
        public const string NotRunningMessage = "Chat needs the Xeno client running with its local dashboard enabled. Start the client and try again.";

        /// <summary>
        /// True when a running client published its local API.
        /// </summary>
        public bool IsAvailable
        {
            get { return Endpoint() != null; }
        }

        #endregion

        #region Private-Members

        private readonly HttpClient _Http;
        private readonly Func<LocalApiEndpoint?> _Endpoint;
        private bool _Disposed = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the client.
        /// </summary>
        /// <param name="endpoint">Returns the local API endpoint, or null to read ~/.xeno/local-api.json.</param>
        /// <param name="handler">HTTP handler, for tests; null for the default.</param>
        public XenoChatClient(Func<LocalApiEndpoint?>? endpoint = null, HttpMessageHandler? handler = null)
        {
            _Endpoint = endpoint ?? LocalApiEndpoint.Read;
            _Http = handler == null ? new HttpClient() : new HttpClient(handler, false);
            _Http.Timeout = Timeout.InfiniteTimeSpan;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Models available for chat: this machine's first, then the Xeno network's.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Catalog.</returns>
        public Task<ModelCatalog> ListModelsAsync(CancellationToken token)
        {
            return GetAsync<ModelCatalog>("chat/models", token);
        }

        /// <summary>
        /// Load a model on this machine before chatting with it, and learn whether it can use tools.
        /// </summary>
        /// <param name="model">Model.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Readiness.</returns>
        public Task<ChatModelReadiness> PrepareModelAsync(string model, CancellationToken token)
        {
            return SendJsonAsync<ChatModelReadiness>(HttpMethod.Post, "chat/models/prepare", new ChatModelPrepareRequest { Model = model }, token);
        }

        /// <summary>
        /// Xeno tools the chat model can use when a conversation has tools on.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Tools.</returns>
        public Task<List<ChatToolInfo>> ListToolsAsync(CancellationToken token)
        {
            return GetAsync<List<ChatToolInfo>>("chat/tools", token);
        }

        /// <summary>
        /// List conversations.
        /// </summary>
        /// <param name="search">Text to find, or null.</param>
        /// <param name="limit">Maximum to return.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversations without messages.</returns>
        public Task<List<ChatConversation>> ListConversationsAsync(string? search, int limit, CancellationToken token)
        {
            return GetAsync<List<ChatConversation>>("chat/conversations" + QueryString(new Dictionary<string, string?> { ["search"] = search, ["limit"] = limit.ToString(CultureInfo.InvariantCulture) }), token);
        }

        /// <summary>
        /// Start a conversation.
        /// </summary>
        /// <param name="request">Title, model, and options.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversation.</returns>
        public Task<ChatConversation> CreateConversationAsync(ChatConversationRequest request, CancellationToken token)
        {
            return SendJsonAsync<ChatConversation>(HttpMethod.Post, "chat/conversations", request, token);
        }

        /// <summary>
        /// Read a conversation with its messages.
        /// </summary>
        /// <param name="id">Conversation id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversation.</returns>
        /// <exception cref="KeyNotFoundException">The conversation does not exist.</exception>
        public Task<ChatConversation> GetConversationAsync(string id, CancellationToken token)
        {
            return GetAsync<ChatConversation>("chat/conversations/" + Uri.EscapeDataString(id), token);
        }

        /// <summary>
        /// Change a conversation.
        /// </summary>
        /// <param name="id">Conversation id.</param>
        /// <param name="request">Changes; null members stay as they are.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Conversation.</returns>
        public Task<ChatConversation> UpdateConversationAsync(string id, ChatConversationRequest request, CancellationToken token)
        {
            return SendJsonAsync<ChatConversation>(HttpMethod.Put, "chat/conversations/" + Uri.EscapeDataString(id), request, token);
        }

        /// <summary>
        /// Delete a conversation.
        /// </summary>
        /// <param name="id">Conversation id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when it existed.</returns>
        public async Task<bool> DeleteConversationAsync(string id, CancellationToken token)
        {
            ChatMutationResponse response = await SendJsonAsync<ChatMutationResponse>(HttpMethod.Delete, "chat/conversations/" + Uri.EscapeDataString(id), null, token).ConfigureAwait(false);
            return response.Changed;
        }

        /// <summary>
        /// Send a message and receive the answer as it streams.
        /// </summary>
        /// <param name="conversationId">Conversation id.</param>
        /// <param name="request">Message.</param>
        /// <param name="onEvent">Receives each event, or null.</param>
        /// <param name="token">Cancellation token; cancelling stops the turn.</param>
        /// <returns>The finished turn, or null when the stream ended without one.</returns>
        public async Task<ChatTurn?> SendAsync(string conversationId, ChatSendRequest request, Func<ChatStreamEvent, Task>? onEvent, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            using (HttpRequestMessage message = Build(HttpMethod.Post, "chat/conversations/" + Uri.EscapeDataString(conversationId) + "/messages", request))
            {
                message.Headers.Accept.ParseAdd(ChatSse.ContentType);
                HttpResponseMessage response = await SendRawAsync(message, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
                using (response)
                {
                    if (!response.IsSuccessStatusCode) await ThrowForStatusAsync(response, token).ConfigureAwait(false);
                    ChatTurn? finished = null;
                    StringBuilder buffer = new StringBuilder();
                    using (Stream stream = await response.Content.ReadAsStreamAsync(token).ConfigureAwait(false))
                    using (StreamReader reader = new StreamReader(stream, new UTF8Encoding(false)))
                    {
                        char[] chars = new char[4096];
                        while (true)
                        {
                            int read = await reader.ReadAsync(chars.AsMemory(0, chars.Length), token).ConfigureAwait(false);
                            if (read <= 0) break;
                            buffer.Append(chars, 0, read);
                            foreach (ChatStreamEvent item in ChatSse.Drain(buffer))
                            {
                                if (item.Kind == ChatStreamEventKindEnum.Done && item.Turn != null) finished = item.Turn;
                                if (onEvent != null) await onEvent(item).ConfigureAwait(false);
                            }
                        }
                    }

                    buffer.Append("\n\n");
                    foreach (ChatStreamEvent item in ChatSse.Drain(buffer))
                    {
                        if (item.Kind == ChatStreamEventKindEnum.Done && item.Turn != null) finished = item.Turn;
                        if (onEvent != null) await onEvent(item).ConfigureAwait(false);
                    }

                    return finished;
                }
            }
        }

        /// <summary>
        /// Stop a running turn.
        /// </summary>
        /// <param name="turnId">Turn id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when it was running.</returns>
        public async Task<bool> CancelAsync(string turnId, CancellationToken token)
        {
            ChatMutationResponse response = await SendJsonAsync<ChatMutationResponse>(HttpMethod.Post, "chat/turns/" + Uri.EscapeDataString(turnId) + "/cancel", null, token).ConfigureAwait(false);
            return response.Changed;
        }

        /// <summary>
        /// Allow or deny a tool call waiting for approval.
        /// </summary>
        /// <param name="turnId">Turn id.</param>
        /// <param name="toolCallId">Tool call record id.</param>
        /// <param name="allow">True to run it.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        /// <exception cref="KeyNotFoundException">The call is not waiting.</exception>
        public async Task ApproveAsync(string turnId, string toolCallId, bool allow, CancellationToken token)
        {
            await SendJsonAsync<ChatMutationResponse>(HttpMethod.Post, "chat/turns/" + Uri.EscapeDataString(turnId) + "/tool-calls/" + Uri.EscapeDataString(toolCallId) + "/approval", new ChatToolApprovalRequest { Allow = allow }, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Rate an answer.
        /// </summary>
        /// <param name="messageId">Assistant message id.</param>
        /// <param name="request">Rating and comment.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Saved feedback.</returns>
        public Task<ChatFeedback> SetFeedbackAsync(string messageId, ChatFeedbackRequest request, CancellationToken token)
        {
            return SendJsonAsync<ChatFeedback>(HttpMethod.Put, "chat/messages/" + Uri.EscapeDataString(messageId) + "/feedback", request, token);
        }

        /// <summary>
        /// Remove a rating.
        /// </summary>
        /// <param name="messageId">Message id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when there was one.</returns>
        public async Task<bool> DeleteFeedbackAsync(string messageId, CancellationToken token)
        {
            ChatMutationResponse response = await SendJsonAsync<ChatMutationResponse>(HttpMethod.Delete, "chat/messages/" + Uri.EscapeDataString(messageId) + "/feedback", null, token).ConfigureAwait(false);
            return response.Changed;
        }

        /// <summary>
        /// Query feedback.
        /// </summary>
        /// <param name="query">Filters and paging.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Page.</returns>
        public Task<ChatPage<ChatFeedback>> QueryFeedbackAsync(ChatFeedbackQuery query, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(query);
            return GetAsync<ChatPage<ChatFeedback>>("chat/feedback" + QueryString(new Dictionary<string, string?>
            {
                ["rating"] = query.Rating?.ToString(CultureInfo.InvariantCulture),
                ["model"] = query.Model,
                ["route"] = query.Route,
                ["hasComment"] = query.HasComment?.ToString().ToLowerInvariant(),
                ["search"] = query.Search,
                ["fromUtc"] = Utc(query.FromUtc),
                ["toUtc"] = Utc(query.ToUtc),
                ["page"] = query.Page.ToString(CultureInfo.InvariantCulture),
                ["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture)
            }), token);
        }

        /// <summary>
        /// Query chat request history.
        /// </summary>
        /// <param name="query">Filters and paging.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Page.</returns>
        public Task<ChatPage<ChatTurn>> QueryHistoryAsync(ChatHistoryQuery query, CancellationToken token)
        {
            return GetAsync<ChatPage<ChatTurn>>("chat/history" + HistoryQueryString(query), token);
        }

        /// <summary>
        /// Read one turn with its tool calls.
        /// </summary>
        /// <param name="turnId">Turn id.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Turn.</returns>
        public Task<ChatTurn> GetTurnAsync(string turnId, CancellationToken token)
        {
            return GetAsync<ChatTurn>("chat/history/" + Uri.EscapeDataString(turnId), token);
        }

        /// <summary>
        /// Delete the history rows matching a query's filters.
        /// </summary>
        /// <param name="query">Filters.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rows removed.</returns>
        public async Task<int> DeleteHistoryAsync(ChatHistoryQuery query, CancellationToken token)
        {
            ChatMutationResponse response = await SendJsonAsync<ChatMutationResponse>(HttpMethod.Delete, "chat/history" + HistoryQueryString(query), null, token).ConfigureAwait(false);
            return response.Removed;
        }

        /// <summary>
        /// Chat activity over a range.
        /// </summary>
        /// <param name="fromUtc">Start, UTC.</param>
        /// <param name="toUtc">End, UTC.</param>
        /// <param name="bucketSeconds">Bucket size in seconds.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Analytics.</returns>
        public Task<ChatAnalytics> AnalyticsAsync(DateTime fromUtc, DateTime toUtc, int bucketSeconds, CancellationToken token)
        {
            return GetAsync<ChatAnalytics>("chat/analytics" + QueryString(new Dictionary<string, string?>
            {
                ["fromUtc"] = Utc(fromUtc),
                ["toUtc"] = Utc(toUtc),
                ["bucketSeconds"] = bucketSeconds.ToString(CultureInfo.InvariantCulture)
            }), token);
        }

        /// <summary>
        /// Dispose.
        /// </summary>
        public void Dispose()
        {
            if (_Disposed) return;
            _Disposed = true;
            _Http.Dispose();
        }

        #endregion

        #region Private-Methods

        private LocalApiEndpoint? Endpoint()
        {
            return _Endpoint();
        }

        private HttpRequestMessage Build(HttpMethod method, string relative, object? body)
        {
            LocalApiEndpoint endpoint = Endpoint() ?? throw new InvalidOperationException(NotRunningMessage);
            HttpRequestMessage message = new HttpRequestMessage(method, endpoint.Url.TrimEnd('/') + "/api/v1/" + relative);
            message.Headers.Add("X-Xeno-Local-Token", endpoint.Token);
            if (body != null) message.Content = new StringContent(JsonUtil.Serialize(body), Encoding.UTF8, "application/json");
            return message;
        }

        private async Task<T> GetAsync<T>(string relative, CancellationToken token) where T : class
        {
            return await SendJsonAsync<T>(HttpMethod.Get, relative, null, token).ConfigureAwait(false);
        }

        private async Task<T> SendJsonAsync<T>(HttpMethod method, string relative, object? body, CancellationToken token) where T : class
        {
            using (HttpRequestMessage message = Build(method, relative, body))
            using (HttpResponseMessage response = await SendRawAsync(message, HttpCompletionOption.ResponseContentRead, token).ConfigureAwait(false))
            {
                if (!response.IsSuccessStatusCode) await ThrowForStatusAsync(response, token).ConfigureAwait(false);
                string json = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
                return JsonUtil.Deserialize<T>(json) ?? throw new InvalidDataException("The client returned an empty response.");
            }
        }

        private async Task<HttpResponseMessage> SendRawAsync(HttpRequestMessage message, HttpCompletionOption completion, CancellationToken token)
        {
            try
            {
                return await _Http.SendAsync(message, completion, token).ConfigureAwait(false);
            }
            catch (HttpRequestException)
            {
                throw new InvalidOperationException(NotRunningMessage);
            }
        }

        private static async Task ThrowForStatusAsync(HttpResponseMessage response, CancellationToken token)
        {
            string text = await response.Content.ReadAsStringAsync(token).ConfigureAwait(false);
            string message = response.ReasonPhrase ?? "Request failed.";
            if (JsonUtil.TryDeserialize<LocalDashboardErrorResponse>(text, out LocalDashboardErrorResponse? error) && !String.IsNullOrWhiteSpace(error?.Message)) message = error!.Message;
            switch (response.StatusCode)
            {
                case HttpStatusCode.NotFound:
                    throw new KeyNotFoundException(message);
                case HttpStatusCode.BadRequest:
                    throw new ArgumentException(message);
                case HttpStatusCode.Unauthorized:
                    throw new UnauthorizedAccessException("The client's local API rejected the token. Restart the client and try again.");
                default:
                    throw new InvalidOperationException(message);
            }
        }

        private static string HistoryQueryString(ChatHistoryQuery query)
        {
            ArgumentNullException.ThrowIfNull(query);
            return QueryString(new Dictionary<string, string?>
            {
                ["model"] = query.Model,
                ["route"] = query.Route,
                ["provider"] = query.Provider,
                ["status"] = query.Status?.ToString(),
                ["surface"] = query.Surface?.ToString(),
                ["rating"] = query.Rating?.ToString(CultureInfo.InvariantCulture),
                ["fallback"] = query.Fallback?.ToString().ToLowerInvariant(),
                ["conversationId"] = query.ConversationId,
                ["search"] = query.Search,
                ["fromUtc"] = Utc(query.FromUtc),
                ["toUtc"] = Utc(query.ToUtc),
                ["page"] = query.Page.ToString(CultureInfo.InvariantCulture),
                ["pageSize"] = query.PageSize.ToString(CultureInfo.InvariantCulture)
            });
        }

        private static string QueryString(Dictionary<string, string?> values)
        {
            List<string> parts = new List<string>();
            foreach (KeyValuePair<string, string?> pair in values)
            {
                if (String.IsNullOrWhiteSpace(pair.Value)) continue;
                parts.Add(pair.Key + "=" + Uri.EscapeDataString(pair.Value));
            }

            return parts.Count == 0 ? String.Empty : "?" + String.Join("&", parts);
        }

        private static string? Utc(DateTime? value)
        {
            return value.HasValue ? DateTime.SpecifyKind(value.Value.ToUniversalTime(), DateTimeKind.Utc).ToString("O", CultureInfo.InvariantCulture) : null;
        }

        #endregion
    }
}

namespace Xeno.Core.Chat
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// Runs chat turns: saves the user's message, sends the conversation to the model through an
    /// <see cref="IChatCompletionTransport"/>, streams the answer as <see cref="ChatStreamEvent"/>s, runs tool calls
    /// (asking first for tools that change something), and records the turn in chat request history.
    /// Thread safe; turns in different conversations run in parallel.
    /// </summary>
    public sealed class ChatEngine
    {
        #region Public-Members

        /// <summary>
        /// Longest title made from a first message.
        /// </summary>
        public const int TitleLength = 60;

        /// <summary>
        /// Turns running now, by turn id.
        /// </summary>
        public IReadOnlyCollection<string> ActiveTurns
        {
            get { return _Active.Keys.ToList(); }
        }

        #endregion

        #region Private-Members

        private readonly IChatStore _Store;
        private readonly IChatCompletionTransport _Transport;
        private readonly Func<IChatLimits> _Settings;
        private readonly IChatToolHost? _Tools;
        private readonly ConcurrentDictionary<string, CancellationTokenSource> _Active = new ConcurrentDictionary<string, CancellationTokenSource>(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> _Approvals = new ConcurrentDictionary<string, TaskCompletionSource<bool>>(StringComparer.Ordinal);
        private DateTime _LastPruneUtc = DateTime.MinValue;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the engine.
        /// </summary>
        /// <param name="store">Chat storage.</param>
        /// <param name="transport">Model transport.</param>
        /// <param name="settings">Returns the current chat limits.</param>
        /// <param name="tools">Tools the model may call, or null for none.</param>
        /// <exception cref="ArgumentNullException">store, transport, or settings is null.</exception>
        public ChatEngine(IChatStore store, IChatCompletionTransport transport, Func<IChatLimits> settings, IChatToolHost? tools = null)
        {
            _Store = store ?? throw new ArgumentNullException(nameof(store));
            _Transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Tools = tools;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Run one turn. Events go to <paramref name="emit"/> as they happen; a failing emit (a caller that went away)
        /// does not stop the turn from being saved.
        /// </summary>
        /// <param name="conversationId">Conversation id.</param>
        /// <param name="request">User message.</param>
        /// <param name="emit">Receives events, or null.</param>
        /// <param name="token">Cancellation token; cancelling stops the turn and saves what arrived.</param>
        /// <returns>The finished turn.</returns>
        /// <exception cref="ArgumentNullException">request is null.</exception>
        /// <exception cref="ArgumentException">The message is empty or no model is chosen.</exception>
        /// <exception cref="KeyNotFoundException">The conversation does not exist.</exception>
        public async Task<ChatTurn> SendAsync(string conversationId, ChatSendRequest request, Func<ChatStreamEvent, Task>? emit, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (String.IsNullOrWhiteSpace(request.Content)) throw new ArgumentException("Type a message to send.");
            ChatConversation conversation = await _Store.GetConversationAsync(conversationId, true, token).ConfigureAwait(false)
                ?? throw new KeyNotFoundException("Conversation not found.");
            string model = String.IsNullOrWhiteSpace(request.Model) ? conversation.Model : request.Model.Trim();
            if (String.IsNullOrWhiteSpace(model)) throw new ArgumentException("Choose a model for this conversation.");
            IChatLimits settings = _Settings() ?? new DefaultChatLimits();
            await PruneIfDueAsync(settings, token).ConfigureAwait(false);

            bool changed = false;
            if (!String.Equals(conversation.Model, model, StringComparison.Ordinal))
            {
                conversation.Model = model;
                changed = true;
            }

            if (conversation.Messages.Count == 0 && String.Equals(conversation.Title, "New chat", StringComparison.Ordinal))
            {
                conversation.Title = MakeTitle(request.Content);
                changed = true;
            }

            if (changed) await _Store.UpdateConversationAsync(conversation, token).ConfigureAwait(false);

            string turnId = Xeno.Core.Helpers.IdGenerator.NewId("trn_");
            ChatMessage user = await _Store.AddMessageAsync(new ChatMessage { ConversationId = conversation.Id, Role = ChatRoleEnum.User, Content = request.Content, TurnId = turnId }, token).ConfigureAwait(false);
            List<ChatMessage> history = conversation.Messages.Concat(new[] { user }).ToList();
            ChatTurn turn = new ChatTurn
            {
                Id = turnId,
                ConversationId = conversation.Id,
                ConversationTitle = conversation.Title,
                UserMessageId = user.Id,
                Model = model,
                Surface = request.Surface,
                Status = ChatTurnStatusEnum.Running,
                CreatedUtc = DateTime.UtcNow
            };
            await _Store.UpsertTurnAsync(turn, CancellationToken.None).ConfigureAwait(false);

            using (CancellationTokenSource turnSource = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                _Active[turn.Id] = turnSource;
                try
                {
                    await SafeEmitAsync(emit, new ChatStreamEvent { Kind = ChatStreamEventKindEnum.Turn, ConversationId = conversation.Id, TurnId = turn.Id, MessageId = user.Id, Turn = turn }).ConfigureAwait(false);
                    await RunTurnAsync(conversation, turn, history, settings, request.Stream, emit, turnSource.Token).ConfigureAwait(false);
                }
                finally
                {
                    _Active.TryRemove(turn.Id, out _);
                    foreach (string key in _Approvals.Keys.Where(key => key.StartsWith(turn.Id + "/", StringComparison.Ordinal)).ToList())
                    {
                        if (_Approvals.TryRemove(key, out TaskCompletionSource<bool>? pending)) pending.TrySetResult(false);
                    }
                }
            }

            return turn;
        }

        /// <summary>
        /// Stop a running turn.
        /// </summary>
        /// <param name="turnId">Turn id.</param>
        /// <returns>True when the turn was running.</returns>
        public bool Cancel(string turnId)
        {
            if (String.IsNullOrWhiteSpace(turnId) || !_Active.TryGetValue(turnId, out CancellationTokenSource? source)) return false;
            try
            {
                source.Cancel();
                return true;
            }
            catch (ObjectDisposedException)
            {
                return false;
            }
        }

        /// <summary>
        /// Answer a tool call that is waiting for approval.
        /// </summary>
        /// <param name="turnId">Turn id.</param>
        /// <param name="toolCallId">Tool call record id.</param>
        /// <param name="allow">True to run the tool, false to deny it.</param>
        /// <returns>True when the call was waiting.</returns>
        public bool Approve(string turnId, string toolCallId, bool allow)
        {
            if (!_Approvals.TryRemove(turnId + "/" + toolCallId, out TaskCompletionSource<bool>? pending)) return false;
            return pending.TrySetResult(allow);
        }

        #endregion

        #region Private-Methods

        private async Task RunTurnAsync(ChatConversation conversation, ChatTurn turn, List<ChatMessage> history, IChatLimits settings, bool stream, Func<ChatStreamEvent, Task>? emit, CancellationToken token)
        {
            Stopwatch clock = Stopwatch.StartNew();
            bool toolsOn = conversation.ToolsEnabled && _Tools != null && _Tools.Definitions.Count > 0;
            int maxCalls = toolsOn ? settings.MaxToolIterations : 1;
            int toolCalls = 0;
            bool retriedWithoutTools = false;
            ChatMessage? lastAssistant = null;
            ChatFailure? failure = null;
            bool failedAfterOutput = false;
            try
            {
                for (int iteration = 1; iteration <= maxCalls; iteration++)
                {
                    bool offerTools = toolsOn && iteration < maxCalls && toolCalls < settings.MaxToolCallsPerTurn;
                    ChatCompletionRequestDto request = BuildRequest(conversation, turn.Model, history, offerTools, stream);
                    ChatCompletionAccumulator accumulator = new ChatCompletionAccumulator();
                    List<ChatStreamEvent> pending = new List<ChatStreamEvent>();
                    Stopwatch call = Stopwatch.StartNew();
                    double? firstToken = null;
                    double? lastToken = null;
                    accumulator.ContentReceived += text =>
                    {
                        if (!firstToken.HasValue) firstToken = clock.Elapsed.TotalMilliseconds;
                        lastToken = clock.Elapsed.TotalMilliseconds;
                        pending.Add(new ChatStreamEvent { Kind = ChatStreamEventKindEnum.Chunk, ConversationId = conversation.Id, TurnId = turn.Id, Text = text });
                    };
                    accumulator.ThinkingReceived += text => pending.Add(new ChatStreamEvent { Kind = ChatStreamEventKindEnum.Thinking, ConversationId = conversation.Id, TurnId = turn.Id, Text = text });

                    ChatTransportResult result;
                    bool cancelled = false;
                    try
                    {
                        result = await _Transport.SendAsync(request, async (bytes, count) =>
                        {
                            accumulator.Append(bytes, count);
                            await FlushAsync(emit, pending).ConfigureAwait(false);
                        }, token).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (token.IsCancellationRequested)
                    {
                        result = new ChatTransportResult { Error = "Stopped." };
                        cancelled = true;
                    }
                    catch (OperationCanceledException)
                    {
                        result = new ChatTransportResult { Error = "Xeno stopped waiting for the model after " + Math.Round(call.Elapsed.TotalSeconds) + " seconds.", FailureKind = ChatFailureKindEnum.Timeout };
                    }

                    accumulator.Complete();
                    await FlushAsync(emit, pending).ConfigureAwait(false);
                    turn.ModelCalls++;
                    turn.Route = result.Route;
                    turn.Provider = result.Provider;
                    turn.Fallback = turn.Fallback || result.Fallback;
                    turn.StatusCode = result.StatusCode;
                    if (!turn.TimeToFirstTokenMs.HasValue && firstToken.HasValue) turn.TimeToFirstTokenMs = firstToken;
                    if (accumulator.PromptTokens.HasValue) turn.PromptTokens = (turn.PromptTokens ?? 0) + accumulator.PromptTokens.Value;
                    if (accumulator.CompletionTokens.HasValue) turn.CompletionTokens = (turn.CompletionTokens ?? 0) + accumulator.CompletionTokens.Value;
                    if (accumulator.CachedPromptTokens.HasValue) turn.CachedPromptTokens = (turn.CachedPromptTokens ?? 0) + accumulator.CachedPromptTokens.Value;
                    turn.ModelMs += call.Elapsed.TotalMilliseconds;
                    if (firstToken.HasValue && lastToken.HasValue) turn.StreamingMs = (turn.StreamingMs ?? 0) + Math.Max(0, lastToken.Value - firstToken.Value);

                    string? error = cancelled ? null : DescribeError(result, accumulator);
                    if (error != null && offerTools && !retriedWithoutTools && !accumulator.ReceivedOutput && RejectsTools(error))
                    {
                        // The model does not take tools: turn them off for this conversation and ask again without them.
                        retriedWithoutTools = true;
                        toolsOn = false;
                        conversation.ToolsEnabled = false;
                        await _Store.UpdateConversationAsync(conversation, CancellationToken.None).ConfigureAwait(false);
                        maxCalls = iteration + 1;
                        continue;
                    }

                    List<ChatCompletionToolCallDto> requested = error == null && !cancelled && offerTools ? accumulator.ToolCalls.Where(item => !String.IsNullOrWhiteSpace(item.Function?.Name)).ToList() : new List<ChatCompletionToolCallDto>();
                    if (accumulator.ReceivedOutput || requested.Count > 0)
                    {
                        lastAssistant = await _Store.AddMessageAsync(new ChatMessage
                        {
                            ConversationId = conversation.Id,
                            Role = ChatRoleEnum.Assistant,
                            Content = accumulator.Content,
                            Thinking = accumulator.Thinking,
                            Model = turn.Model,
                            Route = result.Route,
                            Fallback = result.Fallback,
                            PromptTokens = accumulator.PromptTokens,
                            CompletionTokens = accumulator.CompletionTokens,
                            TimeToFirstTokenMs = firstToken,
                            TotalMs = call.Elapsed.TotalMilliseconds,
                            Incomplete = cancelled || error != null,
                            TurnId = turn.Id,
                            Stats = new ChatMessageStats
                            {
                                Model = turn.Model,
                                Route = result.Route,
                                Provider = result.Provider,
                                Streamed = stream,
                                TimeToFirstTokenMs = firstToken,
                                StreamingMs = firstToken.HasValue && lastToken.HasValue ? lastToken.Value - firstToken.Value : null,
                                ModelMs = call.Elapsed.TotalMilliseconds,
                                PromptTokens = accumulator.PromptTokens,
                                CachedPromptTokens = accumulator.CachedPromptTokens,
                                CompletionTokens = accumulator.CompletionTokens,
                                ReasoningTokens = accumulator.ReasoningTokens,
                                TokensPerSecond = TokensPerSecond(accumulator.CompletionTokens, firstToken, lastToken, call.Elapsed.TotalMilliseconds, stream)
                            }
                        }, CancellationToken.None).ConfigureAwait(false);
                        history.Add(lastAssistant);
                        turn.AssistantMessageId = lastAssistant.Id;
                    }

                    bool wroteThisCall = accumulator.ReceivedOutput || requested.Count > 0;
                    if (cancelled)
                    {
                        turn.Status = ChatTurnStatusEnum.Cancelled;
                        turn.Error = "Stopped.";
                        failure = new ChatFailure { Kind = ChatFailureKindEnum.Stopped, Reason = accumulator.ReceivedOutput ? "You stopped the answer." : "You stopped the turn before the model wrote anything." };
                        failedAfterOutput = wroteThisCall;
                        break;
                    }

                    if (error != null)
                    {
                        failure = Classify(result, accumulator, error);
                        failedAfterOutput = wroteThisCall;
                        turn.Status = ChatTurnStatusEnum.Error;
                        turn.Error = error;
                        await SafeEmitAsync(emit, new ChatStreamEvent { Kind = ChatStreamEventKindEnum.Error, ConversationId = conversation.Id, TurnId = turn.Id, Text = failure.Describe(failedAfterOutput), Failure = failure }).ConfigureAwait(false);
                        break;
                    }

                    if (requested.Count == 0 && String.IsNullOrWhiteSpace(accumulator.Content))
                    {
                        // The model finished cleanly but wrote no answer (it may have only reasoned).
                        failure = new ChatFailure
                        {
                            Kind = ChatFailureKindEnum.NoResponse,
                            Reason = String.IsNullOrWhiteSpace(accumulator.Thinking) ? "The model finished without writing anything." : "The model reasoned but finished without writing an answer.",
                            StatusCode = result.StatusCode
                        };
                        failedAfterOutput = wroteThisCall;
                        turn.Status = ChatTurnStatusEnum.Error;
                        turn.Error = failure.Reason;
                        await SafeEmitAsync(emit, new ChatStreamEvent { Kind = ChatStreamEventKindEnum.Error, ConversationId = conversation.Id, TurnId = turn.Id, Text = failure.Describe(failedAfterOutput), Failure = failure }).ConfigureAwait(false);
                        break;
                    }

                    if (requested.Count == 0 || lastAssistant == null)
                    {
                        turn.Status = ChatTurnStatusEnum.Success;
                        break;
                    }

                    foreach (ChatCompletionToolCallDto requestedCall in requested)
                    {
                        if (toolCalls >= settings.MaxToolCallsPerTurn) break;
                        toolCalls++;
                        ChatToolCallRecord record = await RunToolAsync(conversation, turn, lastAssistant, requestedCall, settings, emit, token).ConfigureAwait(false);
                        lastAssistant.ToolCalls.Add(record);
                        turn.ToolCallCount++;
                        turn.ToolMs += record.DurationMs;
                        turn.ApprovalMs += record.ApprovalMs;
                        if (record.Approval == ChatToolApprovalEnum.Denied || record.Approval == ChatToolApprovalEnum.TimedOut) turn.ToolDenied++;
                        else if (!record.Success) turn.ToolFailures++;
                        ChatMessage toolMessage = await _Store.AddMessageAsync(new ChatMessage
                        {
                            ConversationId = conversation.Id,
                            Role = ChatRoleEnum.Tool,
                            Content = record.Result ?? ToolErrorJson(record),
                            TurnId = turn.Id,
                            ToolCallId = record.CallId
                        }, CancellationToken.None).ConfigureAwait(false);
                        history.Add(toolMessage);
                    }

                    if (token.IsCancellationRequested)
                    {
                        turn.Status = ChatTurnStatusEnum.Cancelled;
                        turn.Error = "Stopped.";
                        failure = new ChatFailure { Kind = ChatFailureKindEnum.Stopped, Reason = "You stopped the turn while its tools ran." };
                        break;
                    }

                    // The last iteration offers no tools, so the model has to answer in words.
                    turn.Status = ChatTurnStatusEnum.Success;
                }
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is System.IO.IOException || ex is System.Net.Http.HttpRequestException || ex is TimeoutException)
            {
                failure = new ChatFailure { Kind = ex is TimeoutException ? ChatFailureKindEnum.Timeout : ex is System.Net.Http.HttpRequestException ? ChatFailureKindEnum.Unavailable : ChatFailureKindEnum.Failed, Reason = ex.Message };
                failedAfterOutput = false;
                turn.Status = ChatTurnStatusEnum.Error;
                turn.Error = ex.Message;
                await SafeEmitAsync(emit, new ChatStreamEvent { Kind = ChatStreamEventKindEnum.Error, ConversationId = conversation.Id, TurnId = turn.Id, Text = failure.Describe(false), Failure = failure }).ConfigureAwait(false);
            }
            finally
            {
                if (turn.Status == ChatTurnStatusEnum.Running) turn.Status = token.IsCancellationRequested ? ChatTurnStatusEnum.Cancelled : ChatTurnStatusEnum.Error;
                if (failure == null && turn.Status == ChatTurnStatusEnum.Cancelled) failure = new ChatFailure { Kind = ChatFailureKindEnum.Stopped, Reason = "You stopped the turn." };
                if (failure == null && turn.Status == ChatTurnStatusEnum.Error) failure = new ChatFailure { Kind = ChatFailureKindEnum.Failed, Reason = turn.Error ?? "The turn ended unexpectedly." };
                turn.TotalMs = clock.Elapsed.TotalMilliseconds;
                turn.CompletedUtc = DateTime.UtcNow;
                if (failure != null)
                {
                    if (failedAfterOutput && lastAssistant != null)
                    {
                        // Part of the answer arrived: keep it and say why it stopped.
                        lastAssistant.Failure = failure;
                        lastAssistant.Incomplete = true;
                    }
                    else
                    {
                        // Nothing arrived: save a stand-in answer that says so, so the conversation shows it later too.
                        lastAssistant = await _Store.AddMessageAsync(new ChatMessage
                        {
                            ConversationId = conversation.Id,
                            Role = ChatRoleEnum.Assistant,
                            Content = String.Empty,
                            Model = turn.Model,
                            Route = turn.Route,
                            Fallback = turn.Fallback,
                            TotalMs = turn.TotalMs,
                            Incomplete = true,
                            TurnId = turn.Id,
                            Failure = failure,
                            Stats = new ChatMessageStats { Model = turn.Model, Route = turn.Route, Provider = turn.Provider, Streamed = stream }
                        }, CancellationToken.None).ConfigureAwait(false);
                        turn.AssistantMessageId = lastAssistant.Id;
                    }
                }

                await _Store.UpsertTurnAsync(turn, CancellationToken.None).ConfigureAwait(false);
                if (lastAssistant != null)
                {
                    ChatMessageStats stats = lastAssistant.Stats ?? new ChatMessageStats { Model = turn.Model, Route = turn.Route, Provider = turn.Provider, Streamed = stream };
                    stats.ToolMs = turn.ToolMs;
                    stats.ToolCalls = turn.ToolCallCount;
                    stats.ModelCalls = turn.ModelCalls;
                    stats.TurnMs = turn.TotalMs;
                    stats.TurnPromptTokens = turn.PromptTokens;
                    stats.TurnCompletionTokens = turn.CompletionTokens;
                    lastAssistant.Stats = stats;
                    await _Store.UpdateMessageAsync(lastAssistant, CancellationToken.None).ConfigureAwait(false);
                }
            }

            await SafeEmitAsync(emit, new ChatStreamEvent { Kind = ChatStreamEventKindEnum.Usage, ConversationId = conversation.Id, TurnId = turn.Id, MessageId = turn.AssistantMessageId, Turn = turn }).ConfigureAwait(false);
            await SafeEmitAsync(emit, new ChatStreamEvent { Kind = ChatStreamEventKindEnum.Done, ConversationId = conversation.Id, TurnId = turn.Id, MessageId = turn.AssistantMessageId, Turn = turn }).ConfigureAwait(false);
        }

        private async Task<ChatToolCallRecord> RunToolAsync(ChatConversation conversation, ChatTurn turn, ChatMessage assistant, ChatCompletionToolCallDto requested, IChatLimits settings, Func<ChatStreamEvent, Task>? emit, CancellationToken token)
        {
            string name = requested.Function?.Name ?? String.Empty;
            ChatToolCallRecord record = new ChatToolCallRecord
            {
                Id = Xeno.Core.Helpers.IdGenerator.NewId("tcl_"),
                CallId = String.IsNullOrWhiteSpace(requested.Id) ? "call_" + Guid.NewGuid().ToString("N").Substring(0, 12) : requested.Id,
                TurnId = turn.Id,
                MessageId = assistant.Id,
                ToolName = name,
                Arguments = String.IsNullOrWhiteSpace(requested.Function?.Arguments) ? "{}" : requested.Function!.Arguments!,
                Mutating = _Tools!.IsMutating(name),
                CreatedUtc = DateTime.UtcNow
            };

            if (record.Mutating)
            {
                record.Approval = ChatToolApprovalEnum.Pending;
                TaskCompletionSource<bool> answer = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                _Approvals[turn.Id + "/" + record.Id] = answer;
                await _Store.UpsertToolCallAsync(record, CancellationToken.None).ConfigureAwait(false);
                await SafeEmitAsync(emit, ToolEvent(conversation, turn, record)).ConfigureAwait(false);
                Stopwatch waiting = Stopwatch.StartNew();
                Task finished = await Task.WhenAny(answer.Task, Task.Delay(TimeSpan.FromSeconds(settings.ToolApprovalTimeoutSeconds), token)).ConfigureAwait(false);
                record.ApprovalMs = waiting.Elapsed.TotalMilliseconds;
                _Approvals.TryRemove(turn.Id + "/" + record.Id, out _);
                if (finished != answer.Task)
                {
                    record.Approval = token.IsCancellationRequested ? ChatToolApprovalEnum.Denied : ChatToolApprovalEnum.TimedOut;
                    record.Error = token.IsCancellationRequested ? "The turn was stopped before the tool was approved." : "Nobody approved the tool in time, so it did not run.";
                }
                else if (!answer.Task.Result)
                {
                    record.Approval = ChatToolApprovalEnum.Denied;
                    record.Error = "The user denied this tool call.";
                }
                else
                {
                    record.Approval = ChatToolApprovalEnum.Allowed;
                }
            }

            if (record.Approval == ChatToolApprovalEnum.Auto || record.Approval == ChatToolApprovalEnum.Allowed)
            {
                await SafeEmitAsync(emit, ToolEvent(conversation, turn, record)).ConfigureAwait(false);
                Stopwatch clock = Stopwatch.StartNew();
                try
                {
                    ChatToolContext context = new ChatToolContext { ConversationId = conversation.Id, TurnId = turn.Id, WorkingDirectory = conversation.WorkingDirectory };
                    record.Result = await _Tools.InvokeAsync(name, record.Arguments, context, token).ConfigureAwait(false);
                    record.Success = true;
                }
                catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is KeyNotFoundException || ex is System.IO.IOException || ex is System.Net.Http.HttpRequestException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is TimeoutException || ex is System.ComponentModel.Win32Exception || ex is System.Text.Json.JsonException || (ex is OperationCanceledException && !token.IsCancellationRequested))
                {
                    record.Error = ex.Message;
                }
                catch (OperationCanceledException)
                {
                    record.Error = "The turn was stopped while the tool ran.";
                }

                record.DurationMs = clock.Elapsed.TotalMilliseconds;
            }

            await _Store.UpsertToolCallAsync(record, CancellationToken.None).ConfigureAwait(false);
            await SafeEmitAsync(emit, ToolEvent(conversation, turn, record)).ConfigureAwait(false);
            return record;
        }

        private static bool RejectsTools(string error)
        {
            return error.Contains("does not support tools", StringComparison.OrdinalIgnoreCase)
                || error.Contains("tools are not supported", StringComparison.OrdinalIgnoreCase)
                || error.Contains("tool use is not supported", StringComparison.OrdinalIgnoreCase);
        }

        private static double? TokensPerSecond(int? completionTokens, double? firstToken, double? lastToken, double callMs, bool stream)
        {
            if (!completionTokens.HasValue || completionTokens.Value <= 0) return null;
            double ms = stream && firstToken.HasValue && lastToken.HasValue && lastToken.Value > firstToken.Value ? lastToken.Value - firstToken.Value : callMs;
            if (ms <= 0) return null;
            return Math.Round(completionTokens.Value / (ms / 1000.0), 1);
        }

        /// <summary>
        /// The system note sent with every request that offers tools, so the model knows what it can call even when
        /// asked about its tools in plain words.
        /// </summary>
        /// <param name="definitions">Tool definitions.</param>
        /// <param name="workingDirectory">The conversation's working directory, or null.</param>
        /// <returns>Text.</returns>
        public static string ToolsNote(IReadOnlyList<ChatCompletionToolDto> definitions, string? workingDirectory = null)
        {
            ArgumentNullException.ThrowIfNull(definitions);
            StringBuilder note = new StringBuilder("You are chatting inside Xeno. You can call these Xeno tools (also served to agents over MCP) when they help answer; call a tool instead of guessing about this machine, its models, routing, or chat history:");
            foreach (ChatCompletionToolDto tool in definitions)
            {
                if (tool.Function == null || String.IsNullOrWhiteSpace(tool.Function.Name)) continue;
                note.Append("\n- ").Append(tool.Function.Name);
                if (!String.IsNullOrWhiteSpace(tool.Function.Description)) note.Append(": ").Append(tool.Function.Description);
            }

            if (!String.IsNullOrWhiteSpace(workingDirectory)) note.Append("\nFile and process tools work in ").Append(workingDirectory).Append(" and cannot reach outside it; relative paths are relative to it.");
            return note.ToString();
        }

        private ChatCompletionRequestDto BuildRequest(ChatConversation conversation, string model, List<ChatMessage> history, bool offerTools, bool stream)
        {
            ChatCompletionRequestDto request = new ChatCompletionRequestDto
            {
                Model = model,
                Stream = stream,
                StreamOptions = stream ? new ChatCompletionStreamOptionsDto { IncludeUsage = true } : null,
                Tools = offerTools ? _Tools!.Definitions.ToList() : null
            };
            if (!String.IsNullOrWhiteSpace(conversation.SystemPrompt)) request.Messages.Add(new ChatCompletionMessageDto { Role = "system", Content = conversation.SystemPrompt });
            if (offerTools) request.Messages.Add(new ChatCompletionMessageDto { Role = "system", Content = ToolsNote(_Tools!.Definitions, conversation.WorkingDirectory) });
            HashSet<string> answered = new HashSet<string>(history.Where(item => item.Role == ChatRoleEnum.Tool && !String.IsNullOrEmpty(item.ToolCallId)).Select(item => item.ToolCallId!), StringComparer.Ordinal);
            foreach (ChatMessage message in history)
            {
                switch (message.Role)
                {
                    case ChatRoleEnum.System:
                        request.Messages.Add(new ChatCompletionMessageDto { Role = "system", Content = message.Content });
                        break;
                    case ChatRoleEnum.User:
                        request.Messages.Add(new ChatCompletionMessageDto { Role = "user", Content = message.Content });
                        break;
                    case ChatRoleEnum.Assistant:
                        List<ChatToolCallRecord> calls = message.ToolCalls.Where(call => answered.Contains(call.CallId)).ToList();
                        if (String.IsNullOrEmpty(message.Content) && calls.Count == 0) break;
                        request.Messages.Add(new ChatCompletionMessageDto
                        {
                            Role = "assistant",
                            Content = message.Content,
                            ToolCalls = calls.Count == 0 ? null : calls.Select(call => new ChatCompletionToolCallDto
                            {
                                Id = call.CallId,
                                Type = "function",
                                Function = new ChatCompletionFunctionCallDto { Name = call.ToolName, Arguments = call.Arguments }
                            }).ToList()
                        });
                        break;
                    case ChatRoleEnum.Tool:
                        request.Messages.Add(new ChatCompletionMessageDto { Role = "tool", Content = message.Content, ToolCallId = message.ToolCallId });
                        break;
                }
            }

            return request;
        }

        private async Task PruneIfDueAsync(IChatLimits settings, CancellationToken token)
        {
            DateTime now = DateTime.UtcNow;
            if (now - _LastPruneUtc < TimeSpan.FromHours(1)) return;
            _LastPruneUtc = now;
            await _Store.PruneTurnsAsync(now.AddDays(-settings.HistoryRetentionDays), token).ConfigureAwait(false);
        }

        private static string? DescribeError(ChatTransportResult result, ChatCompletionAccumulator accumulator)
        {
            if (!String.IsNullOrWhiteSpace(accumulator.Error)) return accumulator.Error;
            if (result.StatusCode.HasValue && result.StatusCode.Value >= 400) return result.Error ?? "The model returned HTTP " + result.StatusCode.Value + ".";
            if (!String.IsNullOrWhiteSpace(result.Error)) return result.Error;
            if (!accumulator.ReceivedOutput && !accumulator.Finished) return "The model returned nothing.";
            return null;
        }

        private static ChatFailure Classify(ChatTransportResult result, ChatCompletionAccumulator accumulator, string error)
        {
            ChatFailureKindEnum kind;
            if (result.FailureKind.HasValue) kind = result.FailureKind.Value;
            else if (!String.IsNullOrWhiteSpace(accumulator.Error)) kind = ChatFailureKindEnum.ModelError;
            else if (result.StatusCode == 408 || result.StatusCode == 504) kind = ChatFailureKindEnum.Timeout;
            else if (result.StatusCode == 429 || result.StatusCode == 502 || result.StatusCode == 503) kind = ChatFailureKindEnum.Unavailable;
            else if (result.StatusCode.HasValue && result.StatusCode.Value >= 400) kind = ChatFailureKindEnum.ModelError;
            else if (accumulator.ReceivedOutput) kind = ChatFailureKindEnum.Interrupted;
            else if (!String.IsNullOrWhiteSpace(result.Error)) kind = ChatFailureKindEnum.Failed;
            else kind = ChatFailureKindEnum.NoResponse;
            if (kind == ChatFailureKindEnum.Timeout || kind == ChatFailureKindEnum.Unavailable)
            {
                if (accumulator.ReceivedOutput && kind == ChatFailureKindEnum.Unavailable) kind = ChatFailureKindEnum.Interrupted;
            }

            return new ChatFailure { Kind = kind, Reason = error, StatusCode = result.StatusCode };
        }

        private static ChatStreamEvent ToolEvent(ChatConversation conversation, ChatTurn turn, ChatToolCallRecord record)
        {
            return new ChatStreamEvent
            {
                Kind = ChatStreamEventKindEnum.ToolCall,
                ConversationId = conversation.Id,
                TurnId = turn.Id,
                MessageId = record.MessageId,
                ToolCall = new ChatToolCallRecord
                {
                    Id = record.Id,
                    CallId = record.CallId,
                    TurnId = record.TurnId,
                    MessageId = record.MessageId,
                    ToolName = record.ToolName,
                    Arguments = record.Arguments,
                    Result = record.Result,
                    Mutating = record.Mutating,
                    Approval = record.Approval,
                    Success = record.Success,
                    Error = record.Error,
                    DurationMs = record.DurationMs,
                    CreatedUtc = record.CreatedUtc
                }
            };
        }

        private static string ToolErrorJson(ChatToolCallRecord record)
        {
            return Xeno.Core.Serialization.JsonUtil.Serialize(new ChatToolErrorResult { Error = record.Error ?? "The tool did not run." });
        }

        private static string MakeTitle(string content)
        {
            string flat = String.Join(" ", content.Split(new[] { ' ', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries));
            if (flat.Length <= TitleLength) return flat;
            return flat.Substring(0, TitleLength - 3).TrimEnd() + "...";
        }

        private static async Task FlushAsync(Func<ChatStreamEvent, Task>? emit, List<ChatStreamEvent> pending)
        {
            if (pending.Count == 0) return;
            List<ChatStreamEvent> events = pending.ToList();
            pending.Clear();
            foreach (ChatStreamEvent item in events) await SafeEmitAsync(emit, item).ConfigureAwait(false);
        }

        private static async Task SafeEmitAsync(Func<ChatStreamEvent, Task>? emit, ChatStreamEvent item)
        {
            if (emit == null) return;
            try
            {
                await emit(item).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is ObjectDisposedException || ex is InvalidOperationException || ex is OperationCanceledException || ex is System.Net.Sockets.SocketException)
            {
                // The caller went away; the turn still finishes and is saved.
            }
        }

        #endregion
    }
}

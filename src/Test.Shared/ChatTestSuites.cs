namespace Test.Shared
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client;
    using Xeno.Client.Chat;
    using Xeno.Core.Chat;
    using Xeno.Core.Chat.Wire;
    using Xeno.Client.LocalDashboard;
    using Xeno.Client.LocalDashboard.Database;
    using Xeno.Client.LocalInference;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Chat: the OpenAI stream reader, chat storage, the turn engine and its tool loop, routed model calls, and the
    /// local chat API through <see cref="XenoChatClient"/>.
    /// </summary>
    public static class ChatTestSuites
    {
        #region Public-Methods

        /// <summary>
        /// Chat suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor ChatSuite()
        {
            const string suite = "Chat";
            return new TestSuiteDescriptor(suite, "Chat", new List<TestCaseDescriptor>
            {
                Case(suite, "AccumulatorReadsStreamsAndBodies", "The stream reader collects text, reasoning, tool calls, usage, and errors", () =>
                {
                    string stream = Sse(
                        "{\"choices\":[{\"delta\":{\"reasoning\":\"hm\"}}]}",
                        "{\"choices\":[{\"delta\":{\"content\":\"Hel\"}}]}",
                        "{\"choices\":[{\"delta\":{\"content\":\"lo\",\"tool_calls\":[{\"index\":0,\"id\":\"c1\",\"function\":{\"name\":\"xeno_status\",\"arguments\":\"{\\\"a\\\":\"}}]}}]}",
                        "{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"function\":{\"arguments\":\"1}\"}}]},\"finish_reason\":\"tool_calls\"}]}",
                        "{\"choices\":[],\"usage\":{\"prompt_tokens\":7,\"completion_tokens\":3}}") + "data: [DONE]\n\n";
                    ChatCompletionAccumulator split = new ChatCompletionAccumulator();
                    List<string> pieces = new List<string>();
                    split.ContentReceived += text => pieces.Add(text);
                    byte[] bytes = Encoding.UTF8.GetBytes(stream);
                    for (int i = 0; i < bytes.Length; i += 5) split.Append(bytes.Skip(i).Take(5).ToArray(), Math.Min(5, bytes.Length - i));
                    split.Complete();
                    Check(split.Content == "Hello" && String.Join("|", pieces) == "Hel|lo", "Content is wrong: " + split.Content);
                    Check(split.Thinking == "hm", "Reasoning is wrong.");
                    Check(split.ToolCalls.Count == 1 && split.ToolCalls[0].Id == "c1" && split.ToolCalls[0].Function!.Name == "xeno_status" && split.ToolCalls[0].Function!.Arguments == "{\"a\":1}", "Tool call fragments were not joined.");
                    Check(split.PromptTokens == 7 && split.CompletionTokens == 3 && split.Finished && split.FinishReason == "tool_calls", "Usage or finish was not read.");

                    ChatCompletionAccumulator whole = new ChatCompletionAccumulator();
                    whole.Append("{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"Hi there\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":2,\"completion_tokens\":2}}");
                    whole.Complete();
                    Check(whole.Content == "Hi there" && whole.CompletionTokens == 2 && whole.Finished, "A whole JSON answer was not read.");

                    ChatCompletionAccumulator error = new ChatCompletionAccumulator();
                    error.Append("{\"error\":{\"message\":\"model not found\",\"type\":\"invalid_request_error\"}}");
                    error.Complete();
                    Check(error.Error == "model not found" && !error.ReceivedOutput, "An error body was not read.");

                    ChatCompletionAccumulator text = new ChatCompletionAccumulator();
                    text.Append("{\"error\":\"model 'x' not found\"}");
                    text.Complete();
                    Check(text.Error == "model 'x' not found", "A plain-text error body was not read: " + text.Error);
                    return Task.CompletedTask;
                }),
                Case(suite, "StoreKeepsConversationsFeedbackAndHistory", "Conversations, messages, feedback, history filters, paging, pruning, and analytics", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        SqliteChatStore store = await NewStoreAsync(home).ConfigureAwait(false);
                        ChatConversation first = await store.CreateConversationAsync(new ChatConversation { Title = "Alpha", Model = "llama3.1:8b" }, CancellationToken.None).ConfigureAwait(false);
                        ChatConversation second = await store.CreateConversationAsync(new ChatConversation { Title = "Beta", Model = "qwen3", Pinned = true, Surface = ChatSurfaceEnum.Cli }, CancellationToken.None).ConfigureAwait(false);
                        Check(first.Id.StartsWith("cnv_", StringComparison.Ordinal), "Conversation id has the wrong prefix.");
                        List<ChatConversation> listed = await store.ListConversationsAsync(null, null, 10, CancellationToken.None).ConfigureAwait(false);
                        Check(listed.Count == 2 && listed[0].Id == second.Id, "Pinned conversations are not listed first.");
                        Check((await store.ListConversationsAsync("alp", null, 10, CancellationToken.None).ConfigureAwait(false)).Single().Id == first.Id, "Search did not find the title.");
                        Check((await store.ListConversationsAsync(null, ChatSurfaceEnum.Cli, 10, CancellationToken.None).ConfigureAwait(false)).Single().Id == second.Id, "The surface filter did not work.");
                        Check((await store.ListConversationsAsync("100%", null, 10, CancellationToken.None).ConfigureAwait(false)).Count == 0, "A % in the search was treated as a wildcard.");

                        ChatMessage user = await store.AddMessageAsync(new ChatMessage { ConversationId = first.Id, Role = ChatRoleEnum.User, Content = "hi" }, CancellationToken.None).ConfigureAwait(false);
                        ChatMessage answer = await store.AddMessageAsync(new ChatMessage { ConversationId = first.Id, Role = ChatRoleEnum.Assistant, Content = "hello", Model = "llama3.1:8b", Route = "local:ollama", CompletionTokens = 5, TotalMs = 120, TurnId = "trn_a" }, CancellationToken.None).ConfigureAwait(false);
                        Check(user.Sequence == 1 && answer.Sequence == 2, "Messages were not numbered in order.");

                        ExpectThrows<ArgumentException>(() => store.SetFeedbackAsync(answer.Id, new ChatFeedbackRequest { Rating = 3 }, CancellationToken.None).GetAwaiter().GetResult());
                        ExpectThrows<ArgumentException>(() => store.SetFeedbackAsync(user.Id, new ChatFeedbackRequest { Rating = 1 }, CancellationToken.None).GetAwaiter().GetResult());
                        ExpectThrows<KeyNotFoundException>(() => store.SetFeedbackAsync("msg_missing", new ChatFeedbackRequest { Rating = 1 }, CancellationToken.None).GetAwaiter().GetResult());
                        ChatFeedback up = await store.SetFeedbackAsync(answer.Id, new ChatFeedbackRequest { Rating = 1 }, CancellationToken.None).ConfigureAwait(false);
                        ChatFeedback down = await store.SetFeedbackAsync(answer.Id, new ChatFeedbackRequest { Rating = -1, Comment = "  too short  " }, CancellationToken.None).ConfigureAwait(false);
                        Check(up.Id == down.Id && down.Rating == -1 && down.Comment == "too short" && down.Model == "llama3.1:8b" && down.CompletionTokens == 5, "Rating again did not replace the first rating.");
                        ChatConversation read = (await store.GetConversationAsync(first.Id, true, CancellationToken.None).ConfigureAwait(false))!;
                        Check(read.Messages.Count == 2 && read.Messages[1].Feedback?.Rating == -1, "The conversation does not carry the feedback.");
                        ChatPage<ChatFeedback> negative = await store.QueryFeedbackAsync(new ChatFeedbackQuery { Rating = -1, HasComment = true }, CancellationToken.None).ConfigureAwait(false);
                        Check(negative.Total == 1 && negative.Items[0].ConversationTitle == "Alpha" && negative.Items[0].Excerpt == "hello", "The feedback query is wrong.");
                        Check((await store.QueryFeedbackAsync(new ChatFeedbackQuery { HasComment = false }, CancellationToken.None).ConfigureAwait(false)).Total == 0, "The has-comment filter is wrong.");

                        DateTime now = DateTime.UtcNow;
                        await store.UpsertTurnAsync(new ChatTurn { Id = "trn_a", ConversationId = first.Id, AssistantMessageId = answer.Id, Model = "llama3.1:8b", Route = "local:ollama", Provider = "ollama", Status = ChatTurnStatusEnum.Success, TotalMs = 100, TimeToFirstTokenMs = 20, CompletionTokens = 5, CreatedUtc = now.AddMinutes(-30) }, CancellationToken.None).ConfigureAwait(false);
                        await store.UpsertTurnAsync(new ChatTurn { Id = "trn_b", ConversationId = second.Id, Model = "qwen3", Route = "network", Status = ChatTurnStatusEnum.Success, TotalMs = 300, Fallback = true, CreatedUtc = now.AddMinutes(-20), Surface = ChatSurfaceEnum.Cli }, CancellationToken.None).ConfigureAwait(false);
                        await store.UpsertTurnAsync(new ChatTurn { Id = "trn_c", ConversationId = second.Id, Model = "qwen3", Route = "network", Status = ChatTurnStatusEnum.Error, Error = "boom", TotalMs = 50, CreatedUtc = now.AddMinutes(-10) }, CancellationToken.None).ConfigureAwait(false);
                        await store.UpsertTurnAsync(new ChatTurn { Id = "trn_old", ConversationId = second.Id, Model = "qwen3", Route = "network", Status = ChatTurnStatusEnum.Success, CreatedUtc = now.AddDays(-40) }, CancellationToken.None).ConfigureAwait(false);

                        Check((await store.QueryTurnsAsync(new ChatHistoryQuery(), CancellationToken.None).ConfigureAwait(false)).Items.First().Id == "trn_c", "History is not newest first.");
                        Check((await store.QueryTurnsAsync(new ChatHistoryQuery { Route = "local" }, CancellationToken.None).ConfigureAwait(false)).Items.Single().Id == "trn_a", "The local route filter is wrong.");
                        Check((await store.QueryTurnsAsync(new ChatHistoryQuery { Model = "QWEN3", Status = ChatTurnStatusEnum.Error }, CancellationToken.None).ConfigureAwait(false)).Items.Single().Id == "trn_c", "Model and status filters are wrong.");
                        Check((await store.QueryTurnsAsync(new ChatHistoryQuery { Rating = -1 }, CancellationToken.None).ConfigureAwait(false)).Items.Single().FeedbackRating == -1, "The rating filter is wrong.");
                        Check((await store.QueryTurnsAsync(new ChatHistoryQuery { Rating = 0 }, CancellationToken.None).ConfigureAwait(false)).Total == 3, "The unrated filter is wrong.");
                        Check((await store.QueryTurnsAsync(new ChatHistoryQuery { Fallback = true, Surface = ChatSurfaceEnum.Cli }, CancellationToken.None).ConfigureAwait(false)).Items.Single().Id == "trn_b", "Fallback and surface filters are wrong.");
                        Check((await store.QueryTurnsAsync(new ChatHistoryQuery { Search = "boom" }, CancellationToken.None).ConfigureAwait(false)).Items.Single().Id == "trn_c", "Search did not match the error.");
                        ChatPage<ChatTurn> paged = await store.QueryTurnsAsync(new ChatHistoryQuery { Page = 2, PageSize = 2 }, CancellationToken.None).ConfigureAwait(false);
                        Check(paged.Total == 4 && paged.Items.Count == 2 && paged.Items[0].Id == "trn_a", "Paging is wrong.");

                        ChatAnalytics analytics = await store.AnalyticsAsync(now.AddHours(-1), now, 900, CancellationToken.None).ConfigureAwait(false);
                        Check(analytics.Turns == 3 && analytics.Local == 1 && analytics.Network == 1 && analytics.Errors == 1 && analytics.Fallbacks == 1, "Analytics totals are wrong.");
                        Check(analytics.P50Ms == 100 && analytics.P95Ms == 300 && analytics.P50TimeToFirstTokenMs == 20, "Percentiles are wrong.");
                        Check(analytics.ThumbsDown == 1 && analytics.NegativeRate == 1.0 && analytics.Buckets.Count == 4 && analytics.Buckets.Sum(item => item.Local + item.Network + item.Errors) == 3, "Feedback or buckets are wrong.");
                        Check(analytics.Models[0].Model == "qwen3" && analytics.Models[0].Turns == 2 && analytics.Models[0].Errors == 1, "The model breakdown is wrong.");

                        Check(await store.PruneTurnsAsync(now.AddDays(-30), CancellationToken.None).ConfigureAwait(false) == 1, "Pruning did not remove the old turn.");
                        Check(await store.DeleteTurnsAsync(new ChatHistoryQuery { Status = ChatTurnStatusEnum.Error }, CancellationToken.None).ConfigureAwait(false) == 1, "Delete by filter is wrong.");
                        Check(await store.DeleteFeedbackAsync(answer.Id, CancellationToken.None).ConfigureAwait(false), "The rating was not removed.");
                        Check(await store.DeleteConversationAsync(first.Id, CancellationToken.None).ConfigureAwait(false) && await store.GetConversationAsync(first.Id, true, CancellationToken.None).ConfigureAwait(false) == null, "The conversation was not deleted.");
                        Check(await store.GetMessageAsync(answer.Id, CancellationToken.None).ConfigureAwait(false) == null, "Messages outlived their conversation.");
                        Check((await store.QueryTurnsAsync(new ChatHistoryQuery { ConversationId = first.Id }, CancellationToken.None).ConfigureAwait(false)).Total == 1, "History rows should stay after the conversation is deleted.");
                    }
                }),
                Case(suite, "EngineStreamsAndRecordsTurns", "A turn streams events, saves the answer, titles the conversation, and records history", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        SqliteChatStore store = await NewStoreAsync(home).ConfigureAwait(false);
                        ScriptedTransport transport = new ScriptedTransport();
                        transport.Responses.Enqueue(Sse(
                            "{\"choices\":[{\"delta\":{\"reasoning_content\":\"think\"}}]}",
                            "{\"choices\":[{\"delta\":{\"content\":\"Hi \"}}]}",
                            "{\"choices\":[{\"delta\":{\"content\":\"you\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":11,\"completion_tokens\":2}}") + "data: [DONE]\n\n");
                        ChatEngine engine = new ChatEngine(store, transport, () => new ClientChatSettings());
                        ChatConversation conversation = await store.CreateConversationAsync(new ChatConversation { Model = "llama3.1:8b", SystemPrompt = "Be brief." }, CancellationToken.None).ConfigureAwait(false);
                        List<ChatStreamEvent> events = new List<ChatStreamEvent>();
                        ChatTurn turn = await engine.SendAsync(conversation.Id, new ChatSendRequest { Content = "Say hi to me please", Surface = ChatSurfaceEnum.Desktop }, item => { events.Add(item); return Task.CompletedTask; }, CancellationToken.None).ConfigureAwait(false);
                        string kinds = String.Join(",", events.Select(item => item.Kind));
                        Check(kinds == "Turn,Thinking,Chunk,Chunk,Usage,Done", "Event order is wrong: " + kinds);
                        Check(turn.Status == ChatTurnStatusEnum.Success && turn.Route == "local:test" && turn.PromptTokens == 11 && turn.CompletionTokens == 2 && turn.ModelCalls == 1 && turn.TimeToFirstTokenMs.HasValue, "The turn record is wrong.");
                        ChatCompletionRequestDto sent = transport.Requests.Single();
                        Check(sent.Stream && sent.StreamOptions?.IncludeUsage == true && sent.Tools == null, "The request options are wrong.");
                        Check(sent.Messages.Count == 2 && sent.Messages[0].Role == "system" && sent.Messages[1].Content == "Say hi to me please", "The request messages are wrong.");
                        ChatConversation saved = (await store.GetConversationAsync(conversation.Id, true, CancellationToken.None).ConfigureAwait(false))!;
                        Check(saved.Title == "Say hi to me please", "The first message did not become the title.");
                        Check(saved.Messages.Count == 2 && saved.Messages[1].Content == "Hi you" && saved.Messages[1].Thinking == "think" && saved.Messages[1].Route == "local:test" && saved.Messages[1].TurnId == turn.Id, "The answer was not saved.");
                        ChatTurn? stored = await store.GetTurnAsync(turn.Id, CancellationToken.None).ConfigureAwait(false);
                        Check(stored != null && stored.Surface == ChatSurfaceEnum.Desktop && stored.AssistantMessageId == saved.Messages[1].Id && stored.CompletedUtc.HasValue, "The history row is wrong.");

                        transport.Responses.Enqueue(Sse("{\"choices\":[{\"delta\":{\"content\":\"Again\"},\"finish_reason\":\"stop\"}]}"));
                        await engine.SendAsync(conversation.Id, new ChatSendRequest { Content = "again", Model = "qwen3" }, null, CancellationToken.None).ConfigureAwait(false);
                        ChatCompletionRequestDto second = transport.Requests.Last();
                        Check(second.Model == "qwen3" && second.Messages.Count == 4 && second.Messages[2].Role == "assistant" && second.Messages[2].Content == "Hi you", "The second turn did not carry the history.");
                        Check((await store.GetConversationAsync(conversation.Id, false, CancellationToken.None).ConfigureAwait(false))!.Model == "qwen3", "Choosing a model did not stick to the conversation.");

                        await ExpectThrowsAsync<ArgumentException>(() => engine.SendAsync(conversation.Id, new ChatSendRequest { Content = " " }, null, CancellationToken.None)).ConfigureAwait(false);
                        await ExpectThrowsAsync<KeyNotFoundException>(() => engine.SendAsync("cnv_missing", new ChatSendRequest { Content = "x" }, null, CancellationToken.None)).ConfigureAwait(false);
                        ChatConversation noModel = await store.CreateConversationAsync(new ChatConversation(), CancellationToken.None).ConfigureAwait(false);
                        await ExpectThrowsAsync<ArgumentException>(() => engine.SendAsync(noModel.Id, new ChatSendRequest { Content = "x" }, null, CancellationToken.None)).ConfigureAwait(false);
                    }
                }),
                Case(suite, "EngineRecordsErrorsAndCancels", "A model error and a stopped turn are recorded, keeping the partial answer", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        SqliteChatStore store = await NewStoreAsync(home).ConfigureAwait(false);
                        ScriptedTransport transport = new ScriptedTransport();
                        transport.Responses.Enqueue("{\"error\":{\"message\":\"model 'nope' not found\"}}");
                        transport.StatusCodes.Enqueue(404);
                        ChatEngine engine = new ChatEngine(store, transport, () => new ClientChatSettings());
                        ChatConversation conversation = await store.CreateConversationAsync(new ChatConversation { Model = "nope" }, CancellationToken.None).ConfigureAwait(false);
                        List<ChatStreamEvent> events = new List<ChatStreamEvent>();
                        ChatTurn failed = await engine.SendAsync(conversation.Id, new ChatSendRequest { Content = "hi" }, item => { events.Add(item); return Task.CompletedTask; }, CancellationToken.None).ConfigureAwait(false);
                        Check(failed.Status == ChatTurnStatusEnum.Error && failed.Error == "model 'nope' not found" && failed.StatusCode == 404 && failed.AssistantMessageId != null, "The error turn is wrong: " + failed.Error);
                        Check(events.Any(item => item.Kind == ChatStreamEventKindEnum.Error && item.Failure?.Kind == ChatFailureKindEnum.ModelError && item.Text == "The model returned an error: model 'nope' not found.") && events.Last().Kind == ChatStreamEventKindEnum.Done, "The error was not streamed.");
                        ChatMessage? standIn = await store.GetMessageAsync(failed.AssistantMessageId!, CancellationToken.None).ConfigureAwait(false);
                        Check(standIn != null && standIn.Content == String.Empty && standIn.Failure?.Kind == ChatFailureKindEnum.ModelError && standIn.Failure.StatusCode == 404, "The failure was not saved in the conversation.");

                        transport.Responses.Enqueue(Sse("{\"choices\":[{\"delta\":{\"content\":\"partial\"}}]}"));
                        transport.HoldOpen = true;
                        Task<ChatTurn> running = engine.SendAsync(conversation.Id, new ChatSendRequest { Content = "long" }, null, CancellationToken.None);
                        await WaitUntilAsync(() => engine.ActiveTurns.Count == 1 && transport.Holding).ConfigureAwait(false);
                        Check(engine.Cancel(engine.ActiveTurns.Single()), "Cancel did not find the running turn.");
                        ChatTurn stopped = await running.ConfigureAwait(false);
                        Check(stopped.Status == ChatTurnStatusEnum.Cancelled && engine.ActiveTurns.Count == 0, "The turn was not cancelled.");
                        ChatMessage? partial = await store.GetMessageAsync(stopped.AssistantMessageId ?? String.Empty, CancellationToken.None).ConfigureAwait(false);
                        Check(partial != null && partial.Content == "partial" && partial.Incomplete && partial.Failure?.Kind == ChatFailureKindEnum.Stopped && ChatFormat.FailureLine(partial) == "Stopped: You stopped the answer.", "The partial answer was not kept as incomplete.");
                        Check(!engine.Cancel("trn_missing"), "Cancelling an unknown turn reported success.");
                    }
                }),
                Case(suite, "EngineSaysWhyTheModelDidNotAnswer", "No answer, a timeout, an unreachable model, and a cut-off answer are saved in the conversation with the reason", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        SqliteChatStore store = await NewStoreAsync(home).ConfigureAwait(false);
                        ScriptedTransport transport = new ScriptedTransport();
                        ChatEngine engine = new ChatEngine(store, transport, () => new ClientChatSettings());
                        ChatConversation conversation = await store.CreateConversationAsync(new ChatConversation { Model = "llama3.1:8b" }, CancellationToken.None).ConfigureAwait(false);

                        // The model finishes cleanly without writing anything.
                        transport.Responses.Enqueue(Sse("{\"choices\":[{\"delta\":{},\"finish_reason\":\"stop\"}]}") + "data: [DONE]\n\n");
                        List<ChatStreamEvent> events = new List<ChatStreamEvent>();
                        ChatTurn empty = await engine.SendAsync(conversation.Id, new ChatSendRequest { Content = "one" }, item => { events.Add(item); return Task.CompletedTask; }, CancellationToken.None).ConfigureAwait(false);
                        ChatMessage emptyAnswer = (await store.GetMessageAsync(empty.AssistantMessageId ?? String.Empty, CancellationToken.None).ConfigureAwait(false))!;
                        Check(empty.Status == ChatTurnStatusEnum.Error && emptyAnswer.Failure?.Kind == ChatFailureKindEnum.NoResponse && ChatFormat.FailureLine(emptyAnswer) == "No response from the model: The model finished without writing anything.", "An empty answer was not reported: " + ChatFormat.FailureLine(emptyAnswer));
                        Check(events.Any(item => item.Kind == ChatStreamEventKindEnum.Error && item.Failure?.Kind == ChatFailureKindEnum.NoResponse), "No error event for an empty answer.");

                        // The transport times out before anything arrives.
                        transport.Results.Enqueue(new ChatTransportResult { Route = "local:test", StatusCode = 504, Error = "Provider 'test' did not respond within 300 seconds.", FailureKind = ChatFailureKindEnum.Timeout });
                        ChatTurn slow = await engine.SendAsync(conversation.Id, new ChatSendRequest { Content = "two" }, null, CancellationToken.None).ConfigureAwait(false);
                        ChatMessage slowAnswer = (await store.GetMessageAsync(slow.AssistantMessageId ?? String.Empty, CancellationToken.None).ConfigureAwait(false))!;
                        Check(slowAnswer.Failure?.Kind == ChatFailureKindEnum.Timeout && ChatFormat.FailureLine(slowAnswer) == "The model timed out: Provider 'test' did not respond within 300 seconds.", "A timeout was not reported: " + ChatFormat.FailureLine(slowAnswer));

                        // The transport gives up on its own (an HTTP timeout surfacing as a cancellation that is not the caller's).
                        transport.Throw = new TaskCanceledException("HttpClient timed out.");
                        ChatTurn thrown = await engine.SendAsync(conversation.Id, new ChatSendRequest { Content = "three" }, null, CancellationToken.None).ConfigureAwait(false);
                        ChatMessage thrownAnswer = (await store.GetMessageAsync(thrown.AssistantMessageId ?? String.Empty, CancellationToken.None).ConfigureAwait(false))!;
                        Check(thrown.Status == ChatTurnStatusEnum.Error && thrownAnswer.Failure?.Kind == ChatFailureKindEnum.Timeout, "A transport timeout escaped or was not reported.");

                        // The model cannot be reached.
                        transport.Results.Enqueue(new ChatTransportResult { Route = "network", StatusCode = 502, Error = "This client is not connected to the Xeno network." });
                        ChatTurn down = await engine.SendAsync(conversation.Id, new ChatSendRequest { Content = "four" }, null, CancellationToken.None).ConfigureAwait(false);
                        Check((await store.GetMessageAsync(down.AssistantMessageId ?? String.Empty, CancellationToken.None).ConfigureAwait(false))!.Failure?.Kind == ChatFailureKindEnum.Unavailable, "An unreachable model was not reported.");

                        // Part of the answer arrives, then the provider stops.
                        transport.Responses.Enqueue(Sse("{\"choices\":[{\"delta\":{\"content\":\"Half an\"}}]}"));
                        transport.Results.Enqueue(new ChatTransportResult { Route = "local:test", StatusCode = 200, Error = "Provider 'test' stopped in the middle of its response." });
                        ChatTurn cut = await engine.SendAsync(conversation.Id, new ChatSendRequest { Content = "five" }, null, CancellationToken.None).ConfigureAwait(false);
                        ChatMessage cutAnswer = (await store.GetMessageAsync(cut.AssistantMessageId ?? String.Empty, CancellationToken.None).ConfigureAwait(false))!;
                        Check(cutAnswer.Content == "Half an" && cutAnswer.Incomplete && cutAnswer.Failure?.Kind == ChatFailureKindEnum.Interrupted, "A cut-off answer was not reported on the answer itself.");

                        // Failures show in the terminal transcript and are never sent back to the model.
                        ChatConversation saved = (await store.GetConversationAsync(conversation.Id, true, CancellationToken.None).ConfigureAwait(false))!;
                        string transcript = ChatTranscript.Build(saved, null, null, null);
                        Check(transcript.Contains("No response from the model", StringComparison.Ordinal) && transcript.Contains("The model timed out", StringComparison.Ordinal) && transcript.Contains("The answer was cut off", StringComparison.Ordinal), "The transcript does not say why answers are missing.");
                        transport.Responses.Enqueue(Sse("{\"choices\":[{\"delta\":{\"content\":\"ok\"},\"finish_reason\":\"stop\"}]}"));
                        await engine.SendAsync(conversation.Id, new ChatSendRequest { Content = "six" }, null, CancellationToken.None).ConfigureAwait(false);
                        List<ChatCompletionMessageDto> sentBack = transport.Requests.Last().Messages;
                        Check(sentBack.Count(item => item.Role == "assistant") == 1 && sentBack.Single(item => item.Role == "assistant").Content == "Half an", "Failure stand-ins were sent to the model.");
                    }
                }),
                Case(suite, "EngineRunsToolsWithApproval", "Read-only tools run, tools that change things wait for Allow or Deny, and results go back to the model", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        SqliteChatStore store = await NewStoreAsync(home).ConfigureAwait(false);
                        ScriptedTransport transport = new ScriptedTransport();
                        transport.Responses.Enqueue(Sse("{\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"a\",\"function\":{\"name\":\"read_it\",\"arguments\":\"{}\"}},{\"index\":1,\"id\":\"b\",\"function\":{\"name\":\"change_it\",\"arguments\":\"{\\\"x\\\":1}\"}},{\"index\":2,\"id\":\"c\",\"function\":{\"name\":\"change_it\",\"arguments\":\"{\\\"x\\\":2}\"}}]},\"finish_reason\":\"tool_calls\"}]}"));
                        transport.Responses.Enqueue(Sse("{\"choices\":[{\"delta\":{\"content\":\"Done.\"},\"finish_reason\":\"stop\"}]}"));
                        FakeTools tools = new FakeTools();
                        ChatEngine engine = new ChatEngine(store, transport, () => new ClientChatSettings(), tools);
                        ChatConversation conversation = await store.CreateConversationAsync(new ChatConversation { Model = "m", ToolsEnabled = true }, CancellationToken.None).ConfigureAwait(false);
                        int pendingSeen = 0;
                        List<ChatStreamEvent> events = new List<ChatStreamEvent>();
                        ChatTurn turn = await engine.SendAsync(conversation.Id, new ChatSendRequest { Content = "go" }, item =>
                        {
                            events.Add(item);
                            if (item.Kind == ChatStreamEventKindEnum.ToolCall && item.ToolCall!.Approval == ChatToolApprovalEnum.Pending)
                            {
                                pendingSeen++;
                                bool allow = pendingSeen == 1;
                                _ = Task.Run(() => engine.Approve(item.TurnId!, item.ToolCall.Id, allow));
                            }

                            return Task.CompletedTask;
                        }, CancellationToken.None).ConfigureAwait(false);
                        Check(turn.Status == ChatTurnStatusEnum.Success && turn.ModelCalls == 2 && turn.ToolCallCount == 3, "The tool turn is wrong: " + turn.Status + " " + turn.Error);
                        Check(String.Join(",", tools.Calls) == "read_it:{},change_it:{\"x\":1}", "The wrong tools ran: " + String.Join(",", tools.Calls));
                        Check(transport.Requests[0].Tools != null && transport.Requests[0].Tools!.Count == 2, "Tools were not offered.");
                        ChatCompletionMessageDto? toolsNote = transport.Requests[0].Messages.FirstOrDefault(item => item.Role == "system");
                        Check(toolsNote != null && toolsNote.Content!.Contains("- read_it", StringComparison.Ordinal) && toolsNote.Content.Contains("- change_it", StringComparison.Ordinal), "The model was not told which tools it has.");
                        ChatCompletionRequestDto followUp = transport.Requests[1];
                        List<ChatCompletionMessageDto> toolMessages = followUp.Messages.Where(item => item.Role == "tool").ToList();
                        Check(toolMessages.Count == 3 && toolMessages[0].ToolCallId == "a" && toolMessages[2].Content!.Contains("denied", StringComparison.OrdinalIgnoreCase), "Tool results were not sent back.");
                        Check(followUp.Messages.Single(item => item.Role == "assistant").ToolCalls!.Count == 3, "The assistant message did not carry its tool calls.");
                        ChatTurn stored = (await store.GetTurnAsync(turn.Id, CancellationToken.None).ConfigureAwait(false))!;
                        Check(stored.ToolCalls.Select(item => item.Approval).SequenceEqual(new[] { ChatToolApprovalEnum.Auto, ChatToolApprovalEnum.Allowed, ChatToolApprovalEnum.Denied }), "Approvals were not recorded.");
                        Check(stored.ToolCalls[0].Success && stored.ToolCalls[0].Result == "{\"ok\":true}" && !stored.ToolCalls[2].Success, "Tool results were not recorded.");
                        ChatConversation saved = (await store.GetConversationAsync(conversation.Id, true, CancellationToken.None).ConfigureAwait(false))!;
                        Check(saved.Messages.Last().Content == "Done." && saved.Messages.Count(item => item.Role == ChatRoleEnum.Tool) == 3 && saved.Messages[1].ToolCalls.Count == 3, "The conversation does not show the tool calls.");
                        Check(events.Count(item => item.Kind == ChatStreamEventKindEnum.ToolCall) >= 6, "Tool call events were not streamed.");
                    }
                }),
                Case(suite, "OllamaToolSupportNeedsATemplateThatRendersTools", "A model counts as using tools only when Ollama lists tools and its renderer or template renders them", () =>
                {
                    Check(new OllamaShowCapabilitiesDto { Capabilities = new List<string> { "completion", "tools" }, Template = "{{ .Prompt }}", Renderer = "gemma4-large" }.UsesTools, "A built-in renderer handles tools.");
                    Check(new OllamaShowCapabilitiesDto { Capabilities = new List<string> { "completion", "tools" }, Template = "{{- if .Tools }}{{ .Tools }}{{ end }}{{ .Prompt }}" }.UsesTools, "A Go template with .Tools handles tools.");
                    Check(new OllamaShowCapabilitiesDto { Capabilities = new List<string> { "completion", "tools" }, Template = "{% if tools %}{{ tools }}{% endif %}" }.UsesTools, "A Jinja template with tools handles tools.");
                    Check(!new OllamaShowCapabilitiesDto { Capabilities = new List<string> { "completion", "tools", "thinking" }, Template = "{% set ns = namespace(is_tool=false) %}{% for message in messages %}{{ message['content'] }}{% endfor %}" }.UsesTools, "A template that drops tools (deepseek-r1) must not count.");
                    Check(!new OllamaShowCapabilitiesDto { Capabilities = new List<string> { "completion" }, Renderer = "x" }.UsesTools, "Without the tools capability there is no tool support.");
                    string note = ChatEngine.ToolsNote(new List<ChatCompletionToolDto> { new ChatCompletionToolDto { Function = new ChatCompletionFunctionDto { Name = "xeno_status", Description = "Client status." } } });
                    Check(note.Contains("Xeno", StringComparison.Ordinal) && note.Contains("- xeno_status: Client status.", StringComparison.Ordinal), "The tools note is wrong: " + note);
                    return Task.CompletedTask;
                }),
                Case(suite, "ChatActivityBucketsMatchEachRange", "Chat activity uses 60, 96, 84, and 120 buckets for the last hour, day, week, and month", () =>
                {
                    DateTime to = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
                    List<ChatTurn> turns = new List<ChatTurn> { new ChatTurn { CreatedUtc = to.AddSeconds(-45), Status = ChatTurnStatusEnum.Success, Route = "local:ollama" } };
                    Xeno.Client.Services.HistoryRangeEnum[] ranges = new[] { Xeno.Client.Services.HistoryRangeEnum.Hour, Xeno.Client.Services.HistoryRangeEnum.Day, Xeno.Client.Services.HistoryRangeEnum.Week, Xeno.Client.Services.HistoryRangeEnum.Month };
                    int[] counts = new[] { 60, 96, 84, 120 };
                    for (int i = 0; i < ranges.Length; i++)
                    {
                        Xeno.Client.Services.HistoryRangeEnum range = ranges[i];
                        int expected = counts[i];
                        ChatAnalytics analytics = ChatAnalyticsBuilder.Build(turns, Xeno.Client.Services.HistoryRanges.StartUtc(range, to), to, Xeno.Client.Services.HistoryRanges.ChatBucketSeconds(range));
                        Check(analytics.Buckets.Count == expected, range + " has " + analytics.Buckets.Count + " buckets, not " + expected + ".");
                        Check(analytics.Buckets.Sum(item => item.Local) == 1, range + " lost the turn.");
                    }

                    ChatAnalytics hour = ChatAnalyticsBuilder.Build(turns, to.AddHours(-1), to, 60);
                    Check(hour.BucketSeconds == 60 && hour.Buckets[59].Local == 1, "The 1-minute bucket for the turn is wrong.");
                    return Task.CompletedTask;
                }),
                Case(suite, "WaitQuipsDoNotRepeat", "Wait quips are short, end in an ellipsis, and none repeats within 20 picks", () =>
                {
                    Check(Xeno.Client.Presentation.ChatWaitMessages.All.Count == 200 && Xeno.Client.Presentation.ChatWaitMessages.All.Distinct().Count() == 200, "The quip list is wrong.");
                    Check(Xeno.Client.Presentation.ChatWaitMessages.All.All(item => item.EndsWith("...", StringComparison.Ordinal) && item.Length <= 60), "A quip is long or lacks an ellipsis.");
                    Xeno.Client.Presentation.ChatWaitMessages picker = new Xeno.Client.Presentation.ChatWaitMessages(7);
                    List<string> picks = Enumerable.Range(0, 500).Select(index => picker.Next()).ToList();
                    for (int i = 0; i + 20 < picks.Count; i++) Check(picks.Skip(i).Take(21).Distinct().Count() == 21, "A quip repeated within 20 picks at " + i + ".");
                    return Task.CompletedTask;
                }),
                Case(suite, "RoutedTransportFollowsTheRouter", "Chat calls go to the local provider, and report when the network or a rule is unavailable", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (FakeProviderServer provider = new FakeProviderServer(request => new FakeProviderResponse { ContentType = "text/event-stream", Chunks = new List<string> { Sse("{\"choices\":[{\"delta\":{\"content\":\"local\"},\"finish_reason\":\"stop\"}]}"), "data: [DONE]\n\n" } }))
                    {
                        LocalInferenceTunnelBridge.Clear();
                        ClientSettings settings = new ClientSettings { Providers = new List<ClientRunnerSettings> { ListenerRoutingHarness.Provider("box", "Ollama", provider.BaseUrl, "llama3.1:8b") } };
                        using (RoutedChatTransport transport = new RoutedChatTransport(() => settings, null, () => null))
                        {
                            StringBuilder body = new StringBuilder();
                            ChatTransportResult local = await transport.SendAsync(new ChatCompletionRequestDto { Model = "llama3.1:8b" }, (bytes, count) => { body.Append(Encoding.UTF8.GetString(bytes, 0, count)); return Task.CompletedTask; }, CancellationToken.None).ConfigureAwait(false);
                            Check(local.Route == "local:box" && local.Provider == "box" && local.StatusCode == 200 && local.Completed && body.ToString().Contains("local", StringComparison.Ordinal), "The local route is wrong: " + local.Route + " " + local.Error);
                            Check(provider.Requests.Single().Path == "/v1/chat/completions" && JsonUtil.Deserialize<ChatCompletionRequestDto>(provider.Requests.Single().Body)!.Model == "llama3.1:8b", "The provider did not get an OpenAI request.");

                            ChatTransportResult network = await transport.SendAsync(new ChatCompletionRequestDto { Model = "deepseek-r1:671b" }, (bytes, count) => Task.CompletedTask, CancellationToken.None).ConfigureAwait(false);
                            Check(network.Route == "network" && network.StatusCode == 502 && network.Error!.Contains("not connected", StringComparison.Ordinal), "A missing tunnel was not reported: " + network.Error);

                            settings.LocalInferenceServer.RoutingMode = LocalInferenceRoutingModeEnum.LocalOnly;
                            ChatTransportResult unavailable = await transport.SendAsync(new ChatCompletionRequestDto { Model = "deepseek-r1:671b" }, (bytes, count) => Task.CompletedTask, CancellationToken.None).ConfigureAwait(false);
                            Check(unavailable.Route == "unavailable" && unavailable.StatusCode == 404, "LocalOnly did not refuse a network model.");
                        }
                    }
                }),
                Case(suite, "LocalApiServesChat", "The local chat API streams turns, keeps conversations, feedback, and history, and the shared client reads it", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (FakeProviderServer provider = new FakeProviderServer(request => new FakeProviderResponse { ContentType = "text/event-stream", Chunks = new List<string> { Sse("{\"choices\":[{\"delta\":{\"content\":\"Hello \"}}]}"), Sse("{\"choices\":[{\"delta\":{\"content\":\"there\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":4,\"completion_tokens\":2}}"), "data: [DONE]\n\n" } }))
                    {
                        int port = FakeProviderServer.GetFreePort();
                        ClientSettings settings = new ClientSettings { ClientId = "xcl_chat", Name = "chat", Providers = new List<ClientRunnerSettings> { ListenerRoutingHarness.Provider("box", "Ollama", provider.BaseUrl, "llama3.1:8b") } };
                        settings.LocalDashboard.Host = "127.0.0.1";
                        settings.LocalDashboard.Port = port;
                        settings.LocalDashboard.Database.Filename = Path.Combine(home.Directory, "local-dashboard.db");
                        settings.Save();
                        LocalDashboardDatabaseDriverBase driver = LocalDashboardDatabaseDriverFactory.Create(settings.LocalDashboard.Database);
                        await driver.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
                        SqliteChatStore store = new SqliteChatStore(settings.LocalDashboard.Database);
                        await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
                        using (XenoChatClient missing = new XenoChatClient())
                        {
                            Check(!missing.IsAvailable, "Chat looked available before the client published its API.");
                            InvalidOperationException notRunning = await ExpectThrowsAsync<InvalidOperationException>(() => missing.ListConversationsAsync(null, 10, CancellationToken.None)).ConfigureAwait(false);
                            Check(notRunning.Message == XenoChatClient.NotRunningMessage, "The not-running message is wrong.");
                        }

                        using (RoutedChatTransport transport = new RoutedChatTransport(() => settings, null, () => null))
                        using (LocalDashboardService service = new LocalDashboardService(settings, driver.RequestHistory, driver.TelemetryHistory, null, driver.ProviderTelemetryHistory, null, store, new ChatEngine(store, transport, () => settings.Chat)))
                        using (XenoChatClient chat = new XenoChatClient())
                        {
                            await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
                            LocalApiEndpoint.Publish(new LocalApiEndpoint { Url = service.DashboardUrl, Token = service.ApiToken, ProcessId = Environment.ProcessId });
                            Check(chat.IsAvailable, "The published endpoint was not found.");
                            if (!OperatingSystem.IsWindows()) Check((File.GetUnixFileMode(LocalApiEndpoint.FilePath) & (UnixFileMode.GroupRead | UnixFileMode.OtherRead)) == 0, "local-api.json is readable by other users.");

                            ChatConversation conversation = await chat.CreateConversationAsync(new ChatConversationRequest { Model = "llama3.1:8b", Surface = ChatSurfaceEnum.Cli }, CancellationToken.None).ConfigureAwait(false);
                            StringBuilder streamed = new StringBuilder();
                            List<ChatStreamEventKindEnum> kinds = new List<ChatStreamEventKindEnum>();
                            ChatTurn? turn = await chat.SendAsync(conversation.Id, new ChatSendRequest { Content = "hello", Surface = ChatSurfaceEnum.Cli }, item =>
                            {
                                kinds.Add(item.Kind);
                                if (item.Kind == ChatStreamEventKindEnum.Chunk) streamed.Append(item.Text);
                                return Task.CompletedTask;
                            }, CancellationToken.None).ConfigureAwait(false);
                            Check(streamed.ToString() == "Hello there" && kinds.First() == ChatStreamEventKindEnum.Turn && kinds.Last() == ChatStreamEventKindEnum.Done, "The stream is wrong: " + streamed + " " + String.Join(",", kinds));
                            Check(turn != null && turn.Status == ChatTurnStatusEnum.Success && turn.Route == "local:box" && turn.CompletionTokens == 2 && turn.Surface == ChatSurfaceEnum.Cli, "The finished turn is wrong.");

                            ChatConversation read = await chat.GetConversationAsync(conversation.Id, CancellationToken.None).ConfigureAwait(false);
                            ChatMessage answer = read.Messages.Last();
                            Check(read.Messages.Count == 2 && answer.Content == "Hello there", "The conversation was not saved.");
                            ChatFeedback feedback = await chat.SetFeedbackAsync(answer.Id, new ChatFeedbackRequest { Rating = -1, Comment = "meh" }, CancellationToken.None).ConfigureAwait(false);
                            Check(feedback.Rating == -1, "Feedback was not saved.");
                            Check((await chat.QueryFeedbackAsync(new ChatFeedbackQuery { Rating = -1 }, CancellationToken.None).ConfigureAwait(false)).Items.Single().Comment == "meh", "The feedback query is wrong.");
                            ChatPage<ChatTurn> history = await chat.QueryHistoryAsync(new ChatHistoryQuery { Route = "local", Rating = -1 }, CancellationToken.None).ConfigureAwait(false);
                            Check(history.Total == 1 && history.Items[0].ConversationTitle == "hello", "The history query is wrong.");
                            Check((await chat.GetTurnAsync(history.Items[0].Id, CancellationToken.None).ConfigureAwait(false)).Id == turn!.Id, "Reading a turn failed.");
                            ChatAnalytics analytics = await chat.AnalyticsAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddMinutes(1), 600, CancellationToken.None).ConfigureAwait(false);
                            Check(analytics.Turns == 1 && analytics.Local == 1 && analytics.ThumbsDown == 1, "Analytics are wrong.");
                            Check((await chat.ListConversationsAsync("hel", 10, CancellationToken.None).ConfigureAwait(false)).Single().MessageCount == 2, "The conversation list is wrong.");
                            Check((await chat.UpdateConversationAsync(conversation.Id, new ChatConversationRequest { Title = "Renamed", Pinned = true }, CancellationToken.None).ConfigureAwait(false)).Title == "Renamed", "Renaming failed.");

                            await ExpectThrowsAsync<ArgumentException>(() => chat.SendAsync(conversation.Id, new ChatSendRequest { Content = "" }, null, CancellationToken.None)).ConfigureAwait(false);
                            await ExpectThrowsAsync<KeyNotFoundException>(() => chat.GetConversationAsync("cnv_missing", CancellationToken.None)).ConfigureAwait(false);
                            await ExpectThrowsAsync<KeyNotFoundException>(() => chat.ApproveAsync(turn.Id, "tcl_missing", true, CancellationToken.None)).ConfigureAwait(false);

                            using (HttpClient raw = new HttpClient())
                            using (HttpResponseMessage noToken = await raw.GetAsync(service.DashboardUrl + "/api/v1/chat/conversations").ConfigureAwait(false))
                            {
                                Check((int)noToken.StatusCode == 401, "The chat API answered without the local token.");
                            }

                            Check(await chat.DeleteHistoryAsync(new ChatHistoryQuery { ConversationId = conversation.Id }, CancellationToken.None).ConfigureAwait(false) == 1, "Deleting history failed.");
                            Check(await chat.DeleteConversationAsync(conversation.Id, CancellationToken.None).ConfigureAwait(false), "Deleting the conversation failed.");
                            LocalApiEndpoint.Withdraw(Environment.ProcessId);
                            Check(!chat.IsAvailable, "Withdrawing the endpoint did not remove it.");
                        }
                    }
                }),
                Case(suite, "CliChats", "xeno chat asks once, reads piped input, and lists, rates, and queries history", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (FakeProviderServer provider = new FakeProviderServer(request => new FakeProviderResponse { ContentType = "text/event-stream", Chunks = new List<string> { Sse("{\"choices\":[{\"delta\":{\"content\":\"Hello from CLI\"},\"finish_reason\":\"stop\"}],\"usage\":{\"prompt_tokens\":3,\"completion_tokens\":3}}"), "data: [DONE]\n\n" } }))
                    {
                        CliResult notRunning = await RunCliAsync(home.Directory, null, "chat", "-m", "llama3.1:8b", "hello").ConfigureAwait(false);
                        Check(notRunning.ExitCode == 1 && (notRunning.Output + notRunning.Error).Contains("xeno start", StringComparison.Ordinal), "Chat without a client did not explain itself: " + notRunning.Output + notRunning.Error);

                        int port = FakeProviderServer.GetFreePort();
                        ClientSettings settings = new ClientSettings { ClientId = "xcl_cli", Name = "cli", Providers = new List<ClientRunnerSettings> { ListenerRoutingHarness.Provider("box", "Ollama", provider.BaseUrl, "llama3.1:8b") } };
                        settings.LocalDashboard.Host = "127.0.0.1";
                        settings.LocalDashboard.Port = port;
                        settings.LocalDashboard.Database.Filename = Path.Combine(home.Directory, "local-dashboard.db");
                        settings.Save();
                        LocalDashboardDatabaseDriverBase driver = LocalDashboardDatabaseDriverFactory.Create(settings.LocalDashboard.Database);
                        await driver.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
                        SqliteChatStore store = new SqliteChatStore(settings.LocalDashboard.Database);
                        await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
                        using (RoutedChatTransport transport = new RoutedChatTransport(() => settings, null, () => null))
                        using (LocalDashboardService service = new LocalDashboardService(settings, driver.RequestHistory, driver.TelemetryHistory, null, driver.ProviderTelemetryHistory, null, store, new ChatEngine(store, transport, () => settings.Chat)))
                        {
                            await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
                            LocalApiEndpoint.Publish(new LocalApiEndpoint { Url = service.DashboardUrl, Token = service.ApiToken, ProcessId = Environment.ProcessId });

                            CliResult once = await RunCliAsync(home.Directory, null, "chat", "-m", "llama3.1:8b", "--json", "Say", "hello").ConfigureAwait(false);
                            Check(once.ExitCode == 0, "One-shot chat failed: " + once.Error);
                            Xeno.Client.Tools.XenoChatToolResult? result = JsonUtil.Deserialize<Xeno.Client.Tools.XenoChatToolResult>(once.Output);
                            Check(result != null && result.Answer == "Hello from CLI" && result.Route == "local:box" && result.Status == "Success", "The JSON result is wrong: " + once.Output);

                            CliResult piped = await RunCliAsync(home.Directory, "What is two plus two?", "chat", "-c", result!.ConversationId).ConfigureAwait(false);
                            Check(piped.ExitCode == 0 && piped.Output.Contains("Hello from CLI", StringComparison.Ordinal), "Piped chat failed: " + piped.Output + piped.Error);

                            ChatConversation saved = (await store.GetConversationAsync(result.ConversationId, true, CancellationToken.None).ConfigureAwait(false))!;
                            Check(saved.Surface == ChatSurfaceEnum.Cli && saved.Messages.Count == 4 && saved.Messages[0].Content == "Say hello" && saved.Messages[2].Content == "What is two plus two?", "The conversation was not saved as expected.");
                            string answerId = saved.Messages[1].Id;
                            CliResult rate = await RunCliAsync(home.Directory, null, "chat", "rate", answerId, "down", "too", "short").ConfigureAwait(false);
                            Check(rate.ExitCode == 0 && (await store.GetMessageAsync(answerId, CancellationToken.None).ConfigureAwait(false))!.Feedback?.Comment == "too short", "Rating from the CLI failed: " + rate.Error);

                            CliResult list = await RunCliAsync(home.Directory, null, "chat", "list").ConfigureAwait(false);
                            Check(list.ExitCode == 0 && list.Output.Contains(result.ConversationId, StringComparison.Ordinal), "chat list is wrong: " + list.Output);
                            CliResult history = await RunCliAsync(home.Directory, null, "chat", "history", "--rating", "down", "--json").ConfigureAwait(false);
                            ChatPage<ChatTurn>? page = JsonUtil.Deserialize<ChatPage<ChatTurn>>(history.Output);
                            Check(history.ExitCode == 0 && page != null && page.Total == 1 && page.Items[0].Surface == ChatSurfaceEnum.Cli, "chat history is wrong: " + history.Output);
                            CliResult feedback = await RunCliAsync(home.Directory, null, "chat", "feedback", "--comments").ConfigureAwait(false);
                            Check(feedback.ExitCode == 0 && feedback.Output.Contains("too short", StringComparison.Ordinal), "chat feedback is wrong: " + feedback.Output);
                            CliResult show = await RunCliAsync(home.Directory, null, "chat", "show", result.ConversationId).ConfigureAwait(false);
                            Check(show.ExitCode == 0 && show.Output.Contains("What is two plus two?", StringComparison.Ordinal) && show.Output.Contains("Rated down", StringComparison.Ordinal), "chat show is wrong: " + show.Output);
                            CliResult bad = await RunCliAsync(home.Directory, null, "chat", "history", "--status", "Exploded").ConfigureAwait(false);
                            Check(bad.ExitCode == 1 && (bad.Output + bad.Error).Contains("Success", StringComparison.Ordinal), "A bad filter was not explained: " + bad.Output + bad.Error);
                            CliResult deleted = await RunCliAsync(home.Directory, null, "chat", "delete", result.ConversationId).ConfigureAwait(false);
                            Check(deleted.ExitCode == 0 && await store.GetConversationAsync(result.ConversationId, false, CancellationToken.None).ConfigureAwait(false) == null, "chat delete failed.");
                            LocalApiEndpoint.Withdraw(Environment.ProcessId);
                        }
                    }
                }),
                Case(suite, "ChatSettingsAreBackfilledAndClamped", "client.json gains the Chat section, and limits stay in range", () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        File.WriteAllText(ClientSettings.SettingsFile, "{\"Name\":\"old\"}");
                        ClientSettings loaded = ClientSettings.Load();
                        Check(loaded.Chat != null && loaded.Chat.HistoryRetentionDays == 30 && File.ReadAllText(ClientSettings.SettingsFile).Contains("\"Chat\"", StringComparison.Ordinal), "The Chat section was not backfilled.");
                        ClientChatSettings chat = new ClientChatSettings { HistoryRetentionDays = 0, MaxToolIterations = 99, MaxToolCallsPerTurn = -1, ToolApprovalTimeoutSeconds = 1 };
                        Check(chat.HistoryRetentionDays == 1 && chat.MaxToolIterations == 20 && chat.MaxToolCallsPerTurn == 1 && chat.ToolApprovalTimeoutSeconds == 10, "Limits were not clamped.");
                    }

                    return Task.CompletedTask;
                })
            });
        }

        #endregion

        #region Private-Methods

        private static async Task<SqliteChatStore> NewStoreAsync(ClientHomeScope home)
        {
            SqliteChatStore store = new SqliteChatStore(new ClientLocalDatabaseSettings { Filename = Path.Combine(home.Directory, "chat-" + Guid.NewGuid().ToString("N") + ".db") });
            await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
            return store;
        }

        private static string Sse(params string[] chunks)
        {
            return String.Concat(chunks.Select(chunk => "data: " + chunk + "\n\n"));
        }

        private static async Task<CliResult> RunCliAsync(string home, string? input, params string[] args)
        {
            System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo("dotnet")
            {
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            info.ArgumentList.Add(typeof(Xeno.Client.Program).Assembly.Location);
            foreach (string arg in args) info.ArgumentList.Add(arg);
            info.Environment["XENO_CLIENT_HOME"] = home;
            info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(info) ?? throw new InvalidOperationException("Could not start xeno."))
            {
                if (input != null) await process.StandardInput.WriteAsync(input).ConfigureAwait(false);
                process.StandardInput.Close();
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> error = process.StandardError.ReadToEndAsync();
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
                {
                    await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                }

                return new CliResult { ExitCode = process.ExitCode, Output = await output.ConfigureAwait(false), Error = await error.ConfigureAwait(false) };
            }
        }

        private static async Task WaitUntilAsync(Func<bool> condition)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(10);
            while (!condition())
            {
                if (DateTime.UtcNow > deadline) throw new TimeoutException("The condition was not met in time.");
                await Task.Delay(20).ConfigureAwait(false);
            }
        }

        private static TestCaseDescriptor Case(string suite, string id, string name, Func<Task> body)
        {
            return new TestCaseDescriptor(suite, id, name, executeAsync: async delegate
            {
                await body().ConfigureAwait(false);
            });
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static T ExpectThrows<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T ex)
            {
                return ex;
            }

            throw new InvalidOperationException("Expected " + typeof(T).Name + ".");
        }

        private static async Task<T> ExpectThrowsAsync<T>(Func<Task> action) where T : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (T ex)
            {
                return ex;
            }

            throw new InvalidOperationException("Expected " + typeof(T).Name + ".");
        }

        #endregion

        #region Private-Classes

        private sealed class CliResult
        {
            internal int ExitCode { get; set; } = 0;

            internal string Output { get; set; } = String.Empty;

            internal string Error { get; set; } = String.Empty;
        }

        private sealed class ScriptedTransport : IChatCompletionTransport
        {
            internal ConcurrentQueue<string> Responses { get; } = new ConcurrentQueue<string>();

            internal ConcurrentQueue<int> StatusCodes { get; } = new ConcurrentQueue<int>();

            internal List<ChatCompletionRequestDto> Requests { get; } = new List<ChatCompletionRequestDto>();

            internal bool HoldOpen { get; set; } = false;

            internal ConcurrentQueue<ChatTransportResult> Results { get; } = new ConcurrentQueue<ChatTransportResult>();

            internal Exception? Throw { get; set; } = null;

            internal bool Holding { get; private set; } = false;

            public async Task<ChatTransportResult> SendAsync(ChatCompletionRequestDto request, Func<byte[], int, Task> onBytes, CancellationToken token)
            {
                Requests.Add(JsonUtil.Deserialize<ChatCompletionRequestDto>(JsonUtil.Serialize(request))!);
                if (Throw != null)
                {
                    Exception thrown = Throw;
                    Throw = null;
                    throw thrown;
                }

                if (!Responses.TryDequeue(out string? body)) body = String.Empty;
                int status = StatusCodes.TryDequeue(out int code) ? code : 200;
                byte[] bytes = Encoding.UTF8.GetBytes(body);
                await onBytes(bytes, bytes.Length).ConfigureAwait(false);
                if (HoldOpen)
                {
                    HoldOpen = false;
                    Holding = true;
                    await Task.Delay(Timeout.Infinite, token).ConfigureAwait(false);
                }

                if (Results.TryDequeue(out ChatTransportResult? scripted)) return scripted;
                return new ChatTransportResult { Route = "local:test", Provider = "test", StatusCode = status, Completed = status < 400 };
            }
        }

        private sealed class FakeTools : IChatToolHost
        {
            internal List<string> Calls { get; } = new List<string>();

            public IReadOnlyList<ChatCompletionToolDto> Definitions { get; } = new List<ChatCompletionToolDto>
            {
                new ChatCompletionToolDto { Function = new ChatCompletionFunctionDto { Name = "read_it", Description = "Read." } },
                new ChatCompletionToolDto { Function = new ChatCompletionFunctionDto { Name = "change_it", Description = "Change." } }
            };

            public bool IsMutating(string name)
            {
                return !String.Equals(name, "read_it", StringComparison.Ordinal);
            }

            public Task<string> InvokeAsync(string name, string arguments, ChatToolContext context, CancellationToken token)
            {
                lock (Calls) Calls.Add(name + ":" + arguments);
                return Task.FromResult("{\"ok\":true}");
            }
        }

        #endregion
    }
}

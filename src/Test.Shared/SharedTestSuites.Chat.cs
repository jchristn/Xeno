namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Core;
    using Xeno.Core.Chat;
    using Xeno.Core.Database;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;
    using Xeno.Core.Settings;
    using Xeno.Server;

    /// <summary>
    /// Server chat: the shared chat store on the test database engine, and chat through the server's API and network
    /// relay to a connected client.
    /// </summary>
    public static partial class SharedTestSuites
    {
        /// <summary>
        /// Server chat suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor ServerChatSuite()
        {
            const string suite = "ServerChat";
            return new TestSuiteDescriptor(suite, "Server Chat", new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor(suite, "StoreScopesByTenantAndUser", "The server chat store keeps users and tenants apart, filters history, and links ratings to turns", executeAsync: async delegate { await RunServerChatStoreAsync().ConfigureAwait(false); }),
                new TestCaseDescriptor(suite, "ChatThroughTheRelay", "Server chat streams an answer from a connected client, records request history, and keeps feedback and analytics", executeAsync: async delegate { await RunServerChatRelayAsync().ConfigureAwait(false); })
            });
        }

        private static async Task RunServerChatStoreAsync()
        {
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-chat-store-" + Guid.NewGuid().ToString("N") + ".db");
            DatabaseDriverBase driver = await DatabaseDriverFactory.CreateAndInitializeAsync(TestDatabase.Settings(dbFile), null, CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (driver.Chat == null) throw new InvalidOperationException("The driver has no chat store.");
                IChatStore alice = driver.Chat.For("ten_a", "usr_alice");
                IChatStore bob = driver.Chat.For("ten_a", "usr_bob");
                IChatStore other = driver.Chat.For("ten_b", "usr_alice");
                IChatStore tenant = driver.Chat.For("ten_a", null);

                ChatConversation conversation = await alice.CreateConversationAsync(new ChatConversation { Title = "Alice 100% plan", Model = "llama3.2:latest", Surface = ChatSurfaceEnum.Server }, CancellationToken.None).ConfigureAwait(false);
                await bob.CreateConversationAsync(new ChatConversation { Title = "Bob", Model = "qwen3" }, CancellationToken.None).ConfigureAwait(false);
                ServerChatCheck(conversation.Id.StartsWith("chconv_", StringComparison.Ordinal), "Conversation id prefix is wrong.");
                ServerChatCheck((await alice.ListConversationsAsync(null, null, 10, CancellationToken.None).ConfigureAwait(false)).Single().Id == conversation.Id, "Alice sees someone else's conversations.");
                ServerChatCheck(await bob.GetConversationAsync(conversation.Id, true, CancellationToken.None).ConfigureAwait(false) == null, "Bob can read Alice's conversation.");
                ServerChatCheck(await other.GetConversationAsync(conversation.Id, true, CancellationToken.None).ConfigureAwait(false) == null, "Another tenant can read the conversation.");
                ServerChatCheck((await alice.ListConversationsAsync("100%", null, 10, CancellationToken.None).ConfigureAwait(false)).Count == 1 && (await alice.ListConversationsAsync("1000", null, 10, CancellationToken.None).ConfigureAwait(false)).Count == 0, "Search with % is wrong.");

                ChatMessage user = await alice.AddMessageAsync(new ChatMessage { ConversationId = conversation.Id, Role = ChatRoleEnum.User, Content = "hi", TurnId = "chturn_1" }, CancellationToken.None).ConfigureAwait(false);
                ChatMessage answer = await alice.AddMessageAsync(new ChatMessage { ConversationId = conversation.Id, Role = ChatRoleEnum.Assistant, Content = "hello there", Model = "llama3.2:latest", Route = "network", TurnId = "chturn_1", TotalMs = 50 }, CancellationToken.None).ConfigureAwait(false);
                ServerChatCheck(user.Sequence == 1 && answer.Sequence == 2, "Messages are not numbered.");
                DateTime now = DateTime.UtcNow;
                await alice.UpsertTurnAsync(new ChatTurn { Id = "chturn_1", ConversationId = conversation.Id, AssistantMessageId = answer.Id, Model = "llama3.2:latest", Route = "network", Provider = "client-1", Status = ChatTurnStatusEnum.Success, TotalMs = 50, Surface = ChatSurfaceEnum.Server, CreatedUtc = now.AddMinutes(-5) }, CancellationToken.None).ConfigureAwait(false);
                await alice.UpsertTurnAsync(new ChatTurn { Id = "chturn_1", ConversationId = conversation.Id, AssistantMessageId = answer.Id, Model = "llama3.2:latest", Route = "network", Provider = "client-1", Status = ChatTurnStatusEnum.Success, TotalMs = 60, CompletionTokens = 4, Surface = ChatSurfaceEnum.Server, CreatedUtc = now.AddMinutes(-5) }, CancellationToken.None).ConfigureAwait(false);
                await bob.UpsertTurnAsync(new ChatTurn { Id = "chturn_2", ConversationId = "chconv_x", Model = "qwen3", Route = "network", Status = ChatTurnStatusEnum.Error, Error = "boom", Surface = ChatSurfaceEnum.Server, CreatedUtc = now.AddMinutes(-3) }, CancellationToken.None).ConfigureAwait(false);

                ChatFeedback feedback = await alice.SetFeedbackAsync(answer.Id, new ChatFeedbackRequest { Rating = -1, Comment = "Too short" }, CancellationToken.None).ConfigureAwait(false);
                ChatFeedback again = await alice.SetFeedbackAsync(answer.Id, new ChatFeedbackRequest { Rating = 1, Comment = "Fine after all" }, CancellationToken.None).ConfigureAwait(false);
                ServerChatCheck(feedback.Id == again.Id && again.ConversationTitle == "Alice 100% plan" && again.Excerpt == "hello there", "Rating again did not replace the rating.");
                await ServerChatExpectAsync<ArgumentException>(() => alice.SetFeedbackAsync(user.Id, new ChatFeedbackRequest { Rating = 1 }, CancellationToken.None)).ConfigureAwait(false);
                await ServerChatExpectAsync<KeyNotFoundException>(() => bob.SetFeedbackAsync(answer.Id, new ChatFeedbackRequest { Rating = 1 }, CancellationToken.None)).ConfigureAwait(false);

                ChatPage<ChatTurn> mine = await alice.QueryTurnsAsync(new ChatHistoryQuery(), CancellationToken.None).ConfigureAwait(false);
                ServerChatCheck(mine.Total == 1 && mine.Items[0].TotalMs == 60 && mine.Items[0].FeedbackRating == 1 && mine.Items[0].ConversationTitle == "Alice 100% plan", "Alice's history is wrong.");
                ChatPage<ChatTurn> all = await tenant.QueryTurnsAsync(new ChatHistoryQuery { PageSize = 1, Page = 2 }, CancellationToken.None).ConfigureAwait(false);
                ServerChatCheck(all.Total == 2 && all.Items.Single().Id == "chturn_1", "Tenant-wide history paging is wrong.");
                ServerChatCheck((await tenant.QueryTurnsAsync(new ChatHistoryQuery { Status = ChatTurnStatusEnum.Error, Model = "QWEN3" }, CancellationToken.None).ConfigureAwait(false)).Items.Single().Id == "chturn_2", "History filters are wrong.");
                ServerChatCheck((await tenant.QueryTurnsAsync(new ChatHistoryQuery { Rating = 0 }, CancellationToken.None).ConfigureAwait(false)).Items.Single().Id == "chturn_2", "The unrated filter is wrong.");
                ServerChatCheck((await tenant.QueryFeedbackAsync(new ChatFeedbackQuery { HasComment = true, Search = "after" }, CancellationToken.None).ConfigureAwait(false)).Total == 1, "Feedback search is wrong.");
                ChatAnalytics analytics = await tenant.AnalyticsAsync(now.AddHours(-1), now.AddMinutes(1), 900, CancellationToken.None).ConfigureAwait(false);
                ServerChatCheck(analytics.Turns == 2 && analytics.Errors == 1 && analytics.ThumbsUp == 1, "Analytics are wrong.");
                await ServerChatExpectAsync<InvalidOperationException>(() => tenant.CreateConversationAsync(new ChatConversation { Model = "m" }, CancellationToken.None)).ConfigureAwait(false);

                ServerChatCheck(await alice.DeleteFeedbackAsync(answer.Id, CancellationToken.None).ConfigureAwait(false) && (await alice.GetTurnAsync("chturn_1", CancellationToken.None).ConfigureAwait(false))!.FeedbackRating == null, "Removing a rating did not clear the turn.");
                ServerChatCheck(!await bob.DeleteConversationAsync(conversation.Id, CancellationToken.None).ConfigureAwait(false), "Bob deleted Alice's conversation.");
                ServerChatCheck(await alice.DeleteConversationAsync(conversation.Id, CancellationToken.None).ConfigureAwait(false) && await alice.GetMessageAsync(answer.Id, CancellationToken.None).ConfigureAwait(false) == null, "Deleting the conversation left its messages.");
                await DatabaseSeeder.PurgeTenantEntitiesAsync(driver, "ten_a", false, CancellationToken.None).ConfigureAwait(false);
                ServerChatCheck((await tenant.QueryTurnsAsync(new ChatHistoryQuery(), CancellationToken.None).ConfigureAwait(false)).Total == 0, "A tenant purge left chat history.");
            }
            finally
            {
                driver.Dispose();
                await TestDatabase.CleanupAsync(dbFile).ConfigureAwait(false);
            }
        }

        private static async Task RunServerChatRelayAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-chat-relay-" + Guid.NewGuid().ToString("N") + ".db");
            XenoSettings settings = new XenoSettings
            {
                Rest = new RestSettings { Hostname = "127.0.0.1", Port = port, Ssl = false },
                Database = TestDatabase.Settings(dbFile)
            };
            string baseUrl = "http://127.0.0.1:" + port;
            using (CancellationTokenSource serverTokenSource = new CancellationTokenSource())
            using (CancellationTokenSource clientTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
            {
                XenoServer server = new XenoServer(settings);
                Task serverTask = server.StartAsync(serverTokenSource.Token);
                try
                {
                    await WaitForHealthAsync(port).ConfigureAwait(false);
                    using (HttpClient anonymous = new HttpClient())
                    using (HttpResponseMessage denied = await anonymous.GetAsync(baseUrl + "/v1.0/api/chat/conversations").ConfigureAwait(false))
                    {
                        ServerChatCheck((int)denied.StatusCode == 401, "Chat answered without sign-in: " + (int)denied.StatusCode);
                    }

                    using (ClientWebSocket socket = new ClientWebSocket())
                    using (HttpClient http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) })
                    {
                        ApplySeedCredentialHeaders(socket);
                        await socket.ConnectAsync(new Uri("ws://127.0.0.1:" + port + settings.WebSocket.Path), clientTokenSource.Token).ConfigureAwait(false);
                        Task clientTask = FakeClientLoopAsync(socket, clientTokenSource.Token);
                        await SendWebSocketJsonAsync(socket, new
                        {
                            type = "client.register",
                            clientId = "xcl_touchstone",
                            tenantId = Constants.DefaultTenantId,
                            name = "touchstone-client",
                            inventory = new
                            {
                                runners = new[]
                                {
                                    new { name = "touchstone-runner", apiType = "OpenAICompatible", endpoint = "http://127.0.0.1:1", models = new[] { new { name = "llama3.2:latest", supportsCompletions = true, supportsEmbeddings = false } } }
                                }
                            }
                        }, clientTokenSource.Token).ConfigureAwait(false);
                        await WaitForRegisteredClientAsync(port).ConfigureAwait(false);
                        await AuthenticateSeedAdminAsync(http, baseUrl).ConfigureAwait(false);

                        ChatModelList? models = JsonUtil.Deserialize<ChatModelList>(await http.GetStringAsync(baseUrl + "/v1.0/api/chat/models").ConfigureAwait(false));
                        ServerChatCheck(models != null && models.Entries.Any(item => item.Name == "llama3.2:latest" && item.OnNetwork), "The chat model list is wrong.");

                        ChatConversation conversation;
                        using (HttpResponseMessage created = await PostJsonAsync(http, baseUrl + "/v1.0/api/chat/conversations", "{\"model\":\"llama3.2:latest\"}").ConfigureAwait(false))
                        {
                            string body = await created.Content.ReadAsStringAsync().ConfigureAwait(false);
                            ServerChatCheck((int)created.StatusCode == 201, "Creating a conversation failed: " + body);
                            conversation = JsonUtil.Deserialize<ChatConversation>(body)!;
                            ServerChatCheck(conversation.Surface == ChatSurfaceEnum.Server, "The conversation is not marked Server.");
                        }

                        string stream;
                        using (HttpResponseMessage sent = await PostJsonAsync(http, baseUrl + "/v1.0/api/chat/conversations/" + conversation.Id + "/messages", "{\"content\":\"ping\"}").ConfigureAwait(false))
                        {
                            stream = await sent.Content.ReadAsStringAsync().ConfigureAwait(false);
                            ServerChatCheck(sent.IsSuccessStatusCode && (sent.Content.Headers.ContentType?.MediaType ?? String.Empty) == "text/event-stream", "Sending failed: " + stream);
                        }

                        StringBuilder buffer = new StringBuilder(stream + "\n\n");
                        List<ChatStreamEvent> events = ChatSse.Drain(buffer);
                        string answer = String.Concat(events.Where(item => item.Kind == ChatStreamEventKindEnum.Chunk).Select(item => item.Text));
                        ChatTurn? turn = events.LastOrDefault(item => item.Kind == ChatStreamEventKindEnum.Done)?.Turn;
                        ServerChatCheck(answer.StartsWith("stream-", StringComparison.Ordinal), "The answer did not stream: " + stream);
                        ServerChatCheck(turn != null && turn.Status == ChatTurnStatusEnum.Success && turn.Route == "network" && turn.Provider == "touchstone-client" && turn.Surface == ChatSurfaceEnum.Server, "The turn is wrong: " + stream);

                        ChatConversation read = JsonUtil.Deserialize<ChatConversation>(await http.GetStringAsync(baseUrl + "/v1.0/api/chat/conversations/" + conversation.Id).ConfigureAwait(false))!;
                        ChatMessage assistant = read.Messages.Last();
                        ServerChatCheck(read.Messages.Count == 2 && assistant.Content == answer && read.Title == "ping", "The conversation was not saved: " + read.Messages.Count);

                        string requests = await http.GetStringAsync(baseUrl + "/v1.0/api/request-history?pageSize=50").ConfigureAwait(false);
                        ServerChatCheck(requests.Contains("/v1/chat/completions", StringComparison.Ordinal), "The model call is not in request history.");

                        using (HttpRequestMessage rate = new HttpRequestMessage(HttpMethod.Put, baseUrl + "/v1.0/api/chat/messages/" + assistant.Id + "/feedback") { Content = new StringContent("{\"rating\":-1,\"comment\":\"meh\"}", Encoding.UTF8, "application/json") })
                        using (HttpResponseMessage rated = await http.SendAsync(rate).ConfigureAwait(false))
                        {
                            ServerChatCheck(rated.IsSuccessStatusCode, "Rating failed: " + await rated.Content.ReadAsStringAsync().ConfigureAwait(false));
                        }

                        ChatPage<ChatTurn>? history = JsonUtil.Deserialize<ChatPage<ChatTurn>>(await http.GetStringAsync(baseUrl + "/v1.0/api/chat/history?rating=-1").ConfigureAwait(false));
                        ServerChatCheck(history != null && history.Total == 1 && history.Items[0].Id == turn!.Id, "Chat history is wrong.");
                        ChatPage<ChatFeedback>? feedback = JsonUtil.Deserialize<ChatPage<ChatFeedback>>(await http.GetStringAsync(baseUrl + "/v1.0/api/chat/feedback?scope=tenant&hasComment=true").ConfigureAwait(false));
                        ServerChatCheck(feedback != null && feedback.Items.Single().Comment == "meh", "Tenant feedback review is wrong.");
                        ChatAnalytics? analytics = JsonUtil.Deserialize<ChatAnalytics>(await http.GetStringAsync(baseUrl + "/v1.0/api/chat/analytics").ConfigureAwait(false));
                        ServerChatCheck(analytics != null && analytics.Turns == 1 && analytics.Network == 1 && analytics.ThumbsDown == 1, "Analytics are wrong.");

                        using (HttpResponseMessage empty = await PostJsonAsync(http, baseUrl + "/v1.0/api/chat/conversations/" + conversation.Id + "/messages", "{\"content\":\"\"}").ConfigureAwait(false))
                        {
                            ServerChatCheck((int)empty.StatusCode == 400, "An empty message was not a 400.");
                        }

                        using (HttpResponseMessage missing = await http.GetAsync(baseUrl + "/v1.0/api/chat/conversations/chconv_missing").ConfigureAwait(false))
                        {
                            ServerChatCheck((int)missing.StatusCode == 404, "A missing conversation was not a 404.");
                        }

                        using (HttpResponseMessage deleted = await http.DeleteAsync(baseUrl + "/v1.0/api/chat/conversations/" + conversation.Id).ConfigureAwait(false))
                        {
                            ServerChatCheck(deleted.IsSuccessStatusCode && (await deleted.Content.ReadAsStringAsync().ConfigureAwait(false)).Contains("true", StringComparison.Ordinal), "Deleting failed.");
                        }

                        clientTokenSource.Cancel();
                        try
                        {
                            await clientTask.ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is OperationCanceledException || ex is WebSocketException)
                        {
                        }
                    }
                }
                finally
                {
                    await StopServerAsync(server, serverTask, serverTokenSource, dbFile).ConfigureAwait(false);
                }
            }
        }

        private static void ServerChatCheck(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static async Task ServerChatExpectAsync<T>(Func<Task> action) where T : Exception
        {
            try
            {
                await action().ConfigureAwait(false);
            }
            catch (T)
            {
                return;
            }

            throw new InvalidOperationException("Expected " + typeof(T).Name + ".");
        }
    }
}

namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
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
    using Xeno.Client.Mcp;
    using Xeno.Client.Services;
    using Xeno.Client.Tools;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Xeno's tools and MCP servers: the shared tool catalog, the in-app chat's tool host, the HTTP MCP server in the
    /// client process (loopback only, token required), and "xeno mcp" over stdio.
    /// </summary>
    public static class McpTestSuites
    {
        #region Public-Methods

        /// <summary>
        /// MCP suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor McpSuite()
        {
            const string suite = "Mcp";
            return new TestSuiteDescriptor(suite, "MCP and tools", new List<TestCaseDescriptor>
            {
                Case(suite, "CatalogDescribesTools", "Every tool is described, read-only tools are marked, and xeno_chat needs a gateway", () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (XenoWorkspace workspace = new XenoWorkspace())
                    {
                        XenoToolCatalog without = new XenoToolCatalog(workspace, NoStore, null);
                        XenoToolCatalog with = new XenoToolCatalog(workspace, NoStore, new FakeGateway());
                        Check(without.Find(XenoToolCatalog.ChatToolName) == null && with.Find(XenoToolCatalog.ChatToolName) != null, "xeno_chat should exist only with a gateway.");
                        string[] readOnly = { "xeno_status", "xeno_list_models", "xeno_resolve_route", "xeno_list_providers", "xeno_provider_health", "xeno_check_providers", "xeno_request_history_query", "xeno_chat_history_query", "xeno_feedback_query", "xeno_usage", "xeno_list_conversations", "xeno_get_conversation" };
                        string[] mutating = { "xeno_set_route_rule", "xeno_remove_route_rule", "xeno_start_client", "xeno_stop_client", "xeno_chat" };
                        Check(readOnly.All(name => with.Find(name)?.Mutating == false), "A read-only tool is missing or marked as changing things.");
                        Check(mutating.All(name => with.Find(name)?.Mutating == true), "A tool that changes things is missing or marked read-only.");
                        Check(with.Tools.Count == readOnly.Length + mutating.Length, "Unexpected tools: " + String.Join(",", with.Tools.Select(tool => tool.Name)));
                        Check(with.Tools.All(tool => tool.Description.Length > 20 && tool.Schema.Required.All(required => tool.Schema.Properties.ContainsKey(required))), "A tool lacks a description or requires an undeclared argument.");

                        XenoChatToolHost host = new XenoChatToolHost(with);
                        Check(host.Definitions.All(item => item.Function.Name != XenoToolCatalog.ChatToolName) && host.Definitions.Count == with.Tools.Count - 1, "The chat tool host offers xeno_chat.");
                        Check(!host.IsMutating("xeno_status") && host.IsMutating("xeno_stop_client") && host.IsMutating("no_such_tool"), "Approval flags are wrong.");
                        ExpectThrows<ArgumentException>(() => host.InvokeAsync(XenoToolCatalog.ChatToolName, "{}", new Xeno.Core.Chat.ChatToolContext(), CancellationToken.None).GetAwaiter().GetResult());
                    }

                    return Task.CompletedTask;
                }),
                Case(suite, "ToolsReadAndChangeSettings", "Tools report status and providers without credentials, and route rule tools save to client.json", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (XenoWorkspace workspace = new XenoWorkspace())
                    {
                        ClientSettings settings = new ClientSettings { Providers = new List<ClientRunnerSettings> { ListenerRoutingHarness.Provider("box", "Ollama", "http://127.0.0.1:1", "llama3.1:8b") } };
                        settings.Providers[0].Credential = "secret-credential-value";
                        settings.Save();
                        XenoToolCatalog catalog = new XenoToolCatalog(workspace, NoStore, null);
                        string status = await catalog.InvokeAsync("xeno_status", "{}", CancellationToken.None).ConfigureAwait(false);
                        Check(JsonUtil.Deserialize<ClientStatusSnapshot>(status) != null, "xeno_status did not return a status.");
                        string providers = await catalog.InvokeAsync("xeno_list_providers", "{}", CancellationToken.None).ConfigureAwait(false);
                        XenoToolProviderList? list = JsonUtil.Deserialize<XenoToolProviderList>(providers);
                        Check(list != null && list.Providers.Single().Name == "box" && list.Providers[0].HasCredential && list.Providers[0].Models.Single() == "llama3.1:8b", "xeno_list_providers is wrong: " + providers);
                        Check(!providers.Contains("secret-credential-value", StringComparison.Ordinal), "A credential leaked through xeno_list_providers.");

                        string route = await catalog.InvokeAsync("xeno_resolve_route", "{\"model\":\"llama3.1:8b\"}", CancellationToken.None).ConfigureAwait(false);
                        Check(route.Contains("local:box", StringComparison.Ordinal), "xeno_resolve_route is wrong: " + route);
                        await catalog.InvokeAsync("xeno_set_route_rule", "{\"model\":\"qwen3\",\"target\":\"network\"}", CancellationToken.None).ConfigureAwait(false);
                        Check(ClientSettings.Load().LocalInferenceServer.Routes.Single().Model == "qwen3", "xeno_set_route_rule did not save.");
                        string removed = await catalog.InvokeAsync("xeno_remove_route_rule", "{\"model\":\"qwen3\"}", CancellationToken.None).ConfigureAwait(false);
                        Check(JsonUtil.Deserialize<XenoToolMessage>(removed)!.Success && ClientSettings.Load().LocalInferenceServer.Routes.Count == 0, "xeno_remove_route_rule did not save.");

                        await ExpectThrowsAsync<ArgumentException>(() => catalog.InvokeAsync("xeno_resolve_route", "{}", CancellationToken.None)).ConfigureAwait(false);
                        await ExpectThrowsAsync<ArgumentException>(() => catalog.InvokeAsync("xeno_set_route_rule", "{\"model\":\"x\",\"target\":\"moon\"}", CancellationToken.None)).ConfigureAwait(false);
                        await ExpectThrowsAsync<ArgumentException>(() => catalog.InvokeAsync("nope", "{}", CancellationToken.None)).ConfigureAwait(false);
                        await ExpectThrowsAsync<ArgumentException>(() => catalog.InvokeAsync("xeno_status", "not json", CancellationToken.None)).ConfigureAwait(false);
                    }
                }),
                Case(suite, "ChatToolsReadChatHistory", "Chat tools list conversations, read one, and query history and feedback", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (XenoWorkspace workspace = new XenoWorkspace())
                    {
                        SqliteChatStore store = new SqliteChatStore(new ClientLocalDatabaseSettings { Filename = Path.Combine(home.Directory, "chat.db") });
                        await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
                        ChatConversation conversation = await store.CreateConversationAsync(new ChatConversation { Title = "Tools", Model = "m" }, CancellationToken.None).ConfigureAwait(false);
                        ChatMessage answer = await store.AddMessageAsync(new ChatMessage { ConversationId = conversation.Id, Role = ChatRoleEnum.Assistant, Content = "hi", Model = "m" }, CancellationToken.None).ConfigureAwait(false);
                        await store.SetFeedbackAsync(answer.Id, new ChatFeedbackRequest { Rating = 1, Comment = "nice" }, CancellationToken.None).ConfigureAwait(false);
                        await store.UpsertTurnAsync(new ChatTurn { ConversationId = conversation.Id, AssistantMessageId = answer.Id, Model = "m", Route = "network", Status = ChatTurnStatusEnum.Success, Surface = ChatSurfaceEnum.Mcp }, CancellationToken.None).ConfigureAwait(false);
                        XenoToolCatalog catalog = new XenoToolCatalog(workspace, token => Task.FromResult<IChatStore>(store), null);
                        Check((await catalog.InvokeAsync("xeno_list_conversations", "{\"search\":\"too\"}", CancellationToken.None).ConfigureAwait(false)).Contains(conversation.Id, StringComparison.Ordinal), "xeno_list_conversations did not find the conversation.");
                        Check((await catalog.InvokeAsync("xeno_get_conversation", "{\"conversationId\":\"" + conversation.Id + "\"}", CancellationToken.None).ConfigureAwait(false)).Contains("nice", StringComparison.Ordinal), "xeno_get_conversation lacks the feedback.");
                        ChatPage<ChatTurn>? turns = JsonUtil.Deserialize<ChatPage<ChatTurn>>(await catalog.InvokeAsync("xeno_chat_history_query", "{\"surface\":\"mcp\",\"rating\":1}", CancellationToken.None).ConfigureAwait(false));
                        Check(turns != null && turns.Total == 1, "xeno_chat_history_query did not filter.");
                        ChatPage<ChatFeedback>? feedback = JsonUtil.Deserialize<ChatPage<ChatFeedback>>(await catalog.InvokeAsync("xeno_feedback_query", "{\"hasComment\":true}", CancellationToken.None).ConfigureAwait(false));
                        Check(feedback != null && feedback.Items.Single().Comment == "nice", "xeno_feedback_query is wrong.");
                        await ExpectThrowsAsync<KeyNotFoundException>(() => catalog.InvokeAsync("xeno_get_conversation", "{\"conversationId\":\"cnv_missing\"}", CancellationToken.None)).ConfigureAwait(false);
                        await ExpectThrowsAsync<ArgumentException>(() => catalog.InvokeAsync("xeno_chat_history_query", "{\"status\":\"Exploded\"}", CancellationToken.None)).ConfigureAwait(false);
                    }
                }),
                Case(suite, "ChatModelUsesXenoTools", "The in-app chat model calls a read-only Xeno tool and answers from its result", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (XenoWorkspace workspace = new XenoWorkspace())
                    {
                        new ClientSettings { Providers = new List<ClientRunnerSettings> { ListenerRoutingHarness.Provider("box", "Ollama", "http://127.0.0.1:1", "llama3.1:8b") } }.Save();
                        SqliteChatStore store = new SqliteChatStore(new ClientLocalDatabaseSettings { Filename = Path.Combine(home.Directory, "chat.db") });
                        await store.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
                        XenoToolCatalog catalog = new XenoToolCatalog(workspace, token => Task.FromResult<IChatStore>(store), null);
                        ToolCallingTransport transport = new ToolCallingTransport("xeno_list_providers");
                        ChatEngine engine = new ChatEngine(store, transport, () => new ClientChatSettings(), new XenoChatToolHost(catalog));
                        ChatConversation conversation = await store.CreateConversationAsync(new ChatConversation { Model = "m", ToolsEnabled = true }, CancellationToken.None).ConfigureAwait(false);
                        ChatTurn turn = await engine.SendAsync(conversation.Id, new ChatSendRequest { Content = "Which providers do I have?" }, null, CancellationToken.None).ConfigureAwait(false);
                        Check(turn.Status == ChatTurnStatusEnum.Success && turn.ToolCallCount == 1, "The tool turn failed: " + turn.Error);
                        Check(transport.ToolResult != null && transport.ToolResult.Contains("box", StringComparison.Ordinal), "The model did not receive the tool result.");
                        Check(transport.OfferedTools.Contains("xeno_status") && !transport.OfferedTools.Contains(XenoToolCatalog.ChatToolName), "The offered tools are wrong.");
                    }
                }),
                Case(suite, "HttpServerNeedsLoopbackAndToken", "The HTTP MCP server listens on loopback only, requires the token, and serves the tools", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        ClientSettings settings = new ClientSettings();
                        settings.LocalDashboard.Database.Filename = Path.Combine(home.Directory, "local-dashboard.db");
                        settings.Mcp.Enabled = true;
                        settings.Mcp.Host = "0.0.0.0";
                        settings.Mcp.Port = FakeProviderServer.GetFreePort();
                        Check(!await XenoMcpHttpRuntime.StartAsync(settings, null, CancellationToken.None).ConfigureAwait(false), "A wildcard host was accepted.");
                        settings.Mcp.Host = "127.0.0.1";
                        settings.Mcp.Enabled = false;
                        Check(!await XenoMcpHttpRuntime.StartAsync(settings, null, CancellationToken.None).ConfigureAwait(false), "A disabled server started.");
                        settings.Mcp.Enabled = true;
                        Check(await XenoMcpHttpRuntime.StartAsync(settings, null, CancellationToken.None).ConfigureAwait(false), "The server did not start.");
                        try
                        {
                            string token = XenoMcpToken.GetOrCreate();
                            Check(token.StartsWith("xmcp_", StringComparison.Ordinal) && XenoMcpToken.GetOrCreate() == token, "The token is not stable.");
                            if (!OperatingSystem.IsWindows()) Check((File.GetUnixFileMode(XenoMcpToken.FilePath) & (UnixFileMode.GroupRead | UnixFileMode.OtherRead)) == 0, "The token file is readable by other users.");
                            string url = settings.Mcp.Url();
                            using (HttpClient http = new HttpClient())
                            {
                                using (HttpResponseMessage denied = await http.PostAsync(url, Rpc(1, "initialize", "{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1\"}}")).ConfigureAwait(false))
                                {
                                    Check((int)denied.StatusCode == 401, "A request without the token was not refused: " + (int)denied.StatusCode);
                                }

                                http.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
                                string sessionId;
                                using (HttpResponseMessage init = await http.PostAsync(url, Rpc(1, "initialize", "{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1\"}}")).ConfigureAwait(false))
                                {
                                    string body = await init.Content.ReadAsStringAsync().ConfigureAwait(false);
                                    Check(init.IsSuccessStatusCode && body.Contains("\"Xeno\"", StringComparison.Ordinal), "initialize failed: " + body);
                                    sessionId = init.Headers.TryGetValues("Mcp-Session-Id", out IEnumerable<string>? values) ? values.First() : String.Empty;
                                }

                                string tools = await PostAsync(http, url, sessionId, Rpc(2, "tools/list", "{}")).ConfigureAwait(false);
                                Check(tools.Contains("xeno_status", StringComparison.Ordinal) && tools.Contains("xeno_list_providers", StringComparison.Ordinal) && !tools.Contains("\"xeno_chat\"", StringComparison.Ordinal), "tools/list is wrong (xeno_chat needs the local dashboard): " + tools);
                                string called = await PostAsync(http, url, sessionId, Rpc(3, "tools/call", "{\"name\":\"xeno_resolve_route\",\"arguments\":{\"model\":\"llama3.1:8b\"}}")).ConfigureAwait(false);
                                Check(called.Contains("network", StringComparison.Ordinal) && !called.Contains("\"isError\":true", StringComparison.Ordinal), "tools/call failed: " + called);
                                string failed = await PostAsync(http, url, sessionId, Rpc(4, "tools/call", "{\"name\":\"xeno_resolve_route\",\"arguments\":{}}")).ConfigureAwait(false);
                                Check(failed.Contains("\"isError\":true", StringComparison.Ordinal) && failed.Contains("model", StringComparison.Ordinal), "A missing argument was not reported: " + failed);
                                string thrown = await PostAsync(http, url, sessionId, Rpc(5, "tools/call", "{\"name\":\"xeno_set_route_rule\",\"arguments\":{\"model\":\"x\",\"target\":\"Local\",\"provider\":\"nope\"}}")).ConfigureAwait(false);
                                Check(thrown.Contains("\"isError\":true", StringComparison.Ordinal) && thrown.Contains("nope", StringComparison.Ordinal), "A tool's own error was not reported: " + thrown);
                            }
                        }
                        finally
                        {
                            XenoMcpHttpRuntime.Stop();
                        }

                        Check(XenoMcpHttpRuntime.Url == null, "The server did not stop.");
                    }
                }),
                Case(suite, "StdioServesTools", "xeno mcp serves the tools over stdio", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        new ClientSettings().Save();
                        string dll = typeof(Xeno.Client.Program).Assembly.Location;
                        string? hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
                        ProcessStartInfo info = new ProcessStartInfo(!String.IsNullOrEmpty(hostPath) && File.Exists(hostPath) ? hostPath : "dotnet")
                        {
                            RedirectStandardInput = true,
                            RedirectStandardOutput = true,
                            RedirectStandardError = true,
                            UseShellExecute = false,
                            StandardOutputEncoding = new UTF8Encoding(false)
                        };
                        info.ArgumentList.Add(dll);
                        info.ArgumentList.Add("mcp");
                        info.Environment["XENO_CLIENT_HOME"] = home.Directory;
                        using (Process process = Process.Start(info) ?? throw new InvalidOperationException("Could not start xeno mcp."))
                        {
                            try
                            {
                                process.BeginErrorReadLine();
                                string init = await StdioCallAsync(process, 1, "initialize", "{\"protocolVersion\":\"2025-06-18\",\"capabilities\":{},\"clientInfo\":{\"name\":\"test\",\"version\":\"1\"}}").ConfigureAwait(false);
                                Check(init.Contains("\"Xeno\"", StringComparison.Ordinal), "initialize failed: " + init);
                                await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\"}").ConfigureAwait(false);
                                string tools = await StdioCallAsync(process, 2, "tools/list", "{}").ConfigureAwait(false);
                                Check(tools.Contains("\"xeno_chat\"", StringComparison.Ordinal) && tools.Contains("xeno_feedback_query", StringComparison.Ordinal), "tools/list is wrong: " + tools);
                                string status = await StdioCallAsync(process, 3, "tools/call", "{\"name\":\"xeno_status\",\"arguments\":{}}").ConfigureAwait(false);
                                Check(status.Contains("StatusText", StringComparison.Ordinal), "xeno_status failed: " + status);
                                string chat = await StdioCallAsync(process, 4, "tools/call", "{\"name\":\"xeno_chat\",\"arguments\":{\"model\":\"m\",\"message\":\"hi\"}}").ConfigureAwait(false);
                                Check(chat.Contains("\"isError\":true", StringComparison.Ordinal) && chat.Contains("client running", StringComparison.Ordinal), "xeno_chat without a running client did not explain itself: " + chat);
                            }
                            finally
                            {
                                process.StandardInput.Close();
                                if (!process.WaitForExit(5000)) process.Kill(true);
                            }
                        }
                    }
                })
            });
        }

        #endregion

        #region Private-Methods

        private static Task<IChatStore> NoStore(CancellationToken token)
        {
            throw new InvalidOperationException("No chat store in this test.");
        }

        private static StringContent Rpc(int id, string method, string parameters)
        {
            return new StringContent("{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"" + method + "\",\"params\":" + parameters + "}", Encoding.UTF8, "application/json");
        }

        private static async Task<string> PostAsync(HttpClient http, string url, string sessionId, StringContent content)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content })
            {
                if (!String.IsNullOrEmpty(sessionId)) request.Headers.Add("Mcp-Session-Id", sessionId);
                using (HttpResponseMessage response = await http.SendAsync(request).ConfigureAwait(false))
                {
                    string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Check(response.IsSuccessStatusCode, "MCP call failed with " + (int)response.StatusCode + ": " + body);
                    return body;
                }
            }
        }

        private static async Task<string> StdioCallAsync(Process process, int id, string method, string parameters)
        {
            await process.StandardInput.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"id\":" + id + ",\"method\":\"" + method + "\",\"params\":" + parameters + "}").ConfigureAwait(false);
            await process.StandardInput.FlushAsync().ConfigureAwait(false);
            using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
            {
                while (true)
                {
                    string? line = await process.StandardOutput.ReadLineAsync(timeout.Token).ConfigureAwait(false);
                    if (line == null) throw new InvalidOperationException("xeno mcp closed its output.");
                    if (line.Contains("\"id\":" + id + ",", StringComparison.Ordinal) || line.Contains("\"id\":" + id + "}", StringComparison.Ordinal)) return line;
                }
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

        private sealed class FakeGateway : IXenoChatGateway
        {
            public Task<XenoChatToolResult> AskAsync(XenoChatToolArguments arguments, CancellationToken token)
            {
                return Task.FromResult(new XenoChatToolResult { Answer = "ok" });
            }
        }

        private sealed class ToolCallingTransport : IChatCompletionTransport
        {
            private readonly string _Tool;
            private int _Calls = 0;

            internal ToolCallingTransport(string tool)
            {
                _Tool = tool;
            }

            internal string? ToolResult { get; private set; } = null;

            internal List<string> OfferedTools { get; } = new List<string>();

            public async Task<ChatTransportResult> SendAsync(ChatCompletionRequestDto request, Func<byte[], int, Task> onBytes, CancellationToken token)
            {
                _Calls++;
                string body;
                if (_Calls == 1)
                {
                    OfferedTools.AddRange((request.Tools ?? new List<ChatCompletionToolDto>()).Select(tool => tool.Function.Name));
                    body = "data: {\"choices\":[{\"delta\":{\"tool_calls\":[{\"index\":0,\"id\":\"t1\",\"function\":{\"name\":\"" + _Tool + "\",\"arguments\":\"{}\"}}]},\"finish_reason\":\"tool_calls\"}]}\n\n";
                }
                else
                {
                    ToolResult = request.Messages.LastOrDefault(item => item.Role == "tool")?.Content;
                    body = "data: {\"choices\":[{\"delta\":{\"content\":\"You have box.\"},\"finish_reason\":\"stop\"}]}\n\n";
                }

                byte[] bytes = Encoding.UTF8.GetBytes(body);
                await onBytes(bytes, bytes.Length).ConfigureAwait(false);
                return new ChatTransportResult { Route = "local:test", StatusCode = 200, Completed = true };
            }
        }

        #endregion
    }
}

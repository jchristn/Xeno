namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Voltaic.Core;
    using Voltaic.Mcp;
    using Xeno.Client;
    using Xeno.Client.Mcp;
    using Xeno.Client.Tools;
    using Xeno.Client.Tools.Mcp;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// Chat's client for external MCP servers: tools from configured servers are offered as mcp_&lt;server&gt;_&lt;tool&gt;
    /// with their schemas and read-only hints, calls go to the right server, and settings changes connect, reconnect,
    /// and disconnect servers. Talks to Xeno's own MCP server over HTTP and stdio, and to a small Voltaic test server.
    /// </summary>
    public static class McpClientTestSuites
    {
        #region Private-Members

        private static readonly Regex _ValidName = new Regex("^[A-Za-z0-9_-]{1,64}$", RegexOptions.CultureInvariant);

        private const string LongToolName = "a_tool_whose_name_is_far_too_long_to_fit_in_the_sixty_four_character_limit";

        #endregion

        #region Public-Methods

        /// <summary>
        /// MCP client suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor McpClientSuite()
        {
            const string suite = "McpClient";
            return new TestSuiteDescriptor(suite, "External MCP servers in chat", new List<TestCaseDescriptor>
            {
                Case(suite, "HttpToolsAreOfferedAndCalled", "Tools from HTTP servers are named, mapped, and called; failures stay per server", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (TestServer notes = TestServer.Start())
                    {
                        ClientSettings xeno = StartXenoServer(home);
                        try
                        {
                            ClientSettings settings = new ClientSettings();
                            settings.Chat.McpServers = new List<ChatMcpServerSettings>
                            {
                                XenoHttp(xeno),
                                Http("notes", notes.Url),
                                Http("my.srv", notes.Url),
                                Http("my_srv", notes.Url),
                                new ChatMcpServerSettings { Name = "down", Transport = "http", Url = "http://127.0.0.1:" + FakeProviderServer.GetFreePort() + "/mcp", TimeoutSeconds = 5 },
                                new ChatMcpServerSettings { Name = "missing", Transport = "stdio", Command = "xeno-no-such-command-" + Guid.NewGuid().ToString("N"), TimeoutSeconds = 5 },
                                new ChatMcpServerSettings { Name = "off", Transport = "http", Url = notes.Url, Enabled = false }
                            };

                            using (McpClientManager manager = new McpClientManager(() => settings))
                            {
                                Check(manager.Tools.Count == 0, "Tools were offered before a refresh.");
                                await manager.RefreshAsync(CancellationToken.None).ConfigureAwait(false);
                                IReadOnlyList<McpChatTool> tools = manager.Tools;
                                string names = String.Join(",", tools.Select(tool => tool.Name));

                                Check(tools.All(tool => _ValidName.IsMatch(tool.Name) && tool.Name.StartsWith("mcp_", StringComparison.Ordinal)), "A tool name is not valid: " + names);
                                Check(tools.Select(tool => tool.Name.ToLowerInvariant()).Distinct().Count() == tools.Count, "Tool names are not unique: " + names);

                                McpChatTool? route = tools.FirstOrDefault(tool => tool.Name == "mcp_xeno_xeno_resolve_route");
                                Check(route != null && route.Server == "xeno" && route.ToolName == "xeno_resolve_route", "Xeno's tools are missing: " + names);
                                Check(route!.Schema.Required.Contains("model") && route.Schema.Properties.ContainsKey("model") && route.Schema.Properties["model"].Type == "string", "The route tool's schema did not map.");
                                Check(tools.Where(tool => tool.Server == "xeno").All(tool => tool.Mutating), "An untrusted server's tool without a read-only hint was marked read-only.");

                                McpChatTool? read = tools.FirstOrDefault(tool => tool.Name == "mcp_notes_read_note");
                                Check(read != null && !read.Mutating && read.Description == "Read a note.", "The read-only hint did not map.");
                                Check(tools.Single(tool => tool.Name == "mcp_notes_fail").Mutating, "A tool without a read-only hint was marked read-only.");
                                ChatToolSchemaDto schema = read!.Schema;
                                Check(schema.Required.SequenceEqual(new[] { "path" }) && schema.Properties["path"].Description == "Note path", "Required or descriptions did not map.");
                                Check(schema.Properties["tags"].Type == "array" && schema.Properties["tags"].Items?.Type == "string", "Array items did not map.");
                                Check(schema.Properties["limit"].Type == "integer" && schema.Properties["mode"].Type == "string" && schema.Properties["mode"].Enum!.SequenceEqual(new[] { "a", "b" }), "anyOf, type arrays, or enum did not map.");
                                ChatToolPropertyDto filter = schema.Properties["filter"];
                                Check(filter.Type == "object" && filter.Properties!["since"].Type == "string" && filter.Required!.Single() == "since", "A nested object did not map.");

                                Check(tools.Any(tool => tool.Name == "mcp_my_srv_read_note") && tools.Any(tool => tool.Name == "mcp_my_srv_read_note_2"), "Colliding names were not told apart: " + names);
                                List<McpChatTool> longNames = tools.Where(tool => tool.ToolName == LongToolName).ToList();
                                Check(longNames.Count == 3 && longNames.All(tool => tool.Name.Length <= 64), "Long names were not shortened: " + String.Join(",", longNames.Select(tool => tool.Name)));

                                IReadOnlyList<McpServerStatus> servers = manager.Servers;
                                Check(servers.Count == 7, "Every configured server should have a state.");
                                Check(servers.Single(server => server.Name == "xeno").Connected && servers.Single(server => server.Name == "notes").ToolCount == 4, "Connected servers are wrong.");
                                McpServerStatus down = servers.Single(server => server.Name == "down");
                                Check(!down.Connected && !String.IsNullOrWhiteSpace(down.Error) && down.Transport == "http", "An unreachable server did not report an error.");
                                McpServerStatus missing = servers.Single(server => server.Name == "missing");
                                Check(!missing.Connected && !String.IsNullOrWhiteSpace(missing.Error) && missing.Transport == "stdio", "A command that does not exist did not report an error.");
                                McpServerStatus off = servers.Single(server => server.Name == "off");
                                Check(!off.Enabled && !off.Connected && off.Error == null && tools.All(tool => tool.Server != "off"), "A disabled server was connected.");

                                string resolved = await manager.InvokeAsync("mcp_xeno_xeno_resolve_route", "{\"model\":\"llama3.1:8b\"}", CancellationToken.None).ConfigureAwait(false);
                                Check(resolved.Contains("network", StringComparison.Ordinal), "The call did not reach Xeno's server: " + resolved);
                                string note = await manager.InvokeAsync("mcp_notes_read_note", "{\"path\":\"todo.md\"}", CancellationToken.None).ConfigureAwait(false);
                                Check(note == "note todo.md", "Text content was not returned: " + note);
                                string picture = await manager.InvokeAsync("mcp_notes_picture", "", CancellationToken.None).ConfigureAwait(false);
                                Check(picture.StartsWith("[image image/png", StringComparison.Ordinal) && picture.EndsWith("\ncaption", StringComparison.Ordinal), "Image content was not described: " + picture);

                                InvalidOperationException failed = await ExpectThrowsAsync<InvalidOperationException>(() => manager.InvokeAsync("mcp_notes_fail", "{}", CancellationToken.None)).ConfigureAwait(false);
                                Check(failed.Message.Contains("disk is full", StringComparison.Ordinal), "isError did not carry the tool's text: " + failed.Message);
                                await ExpectThrowsAsync<InvalidOperationException>(() => manager.InvokeAsync("mcp_xeno_xeno_resolve_route", "{}", CancellationToken.None)).ConfigureAwait(false);
                                await ExpectThrowsAsync<ArgumentException>(() => manager.InvokeAsync("mcp_notes_nope", "{}", CancellationToken.None)).ConfigureAwait(false);
                                await ExpectThrowsAsync<ArgumentException>(() => manager.InvokeAsync("mcp_off_read_note", "{}", CancellationToken.None)).ConfigureAwait(false);
                                await ExpectThrowsAsync<ArgumentException>(() => manager.InvokeAsync("mcp_notes_read_note", "not json", CancellationToken.None)).ConfigureAwait(false);
                            }
                        }
                        finally
                        {
                            XenoMcpHttpRuntime.Stop();
                        }
                    }
                }),
                Case(suite, "SettingsChangesApply", "Refreshing picks up disabled, removed, changed, and added servers", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (TestServer notes = TestServer.Start())
                    {
                        ClientSettings settings = new ClientSettings();
                        settings.Chat.McpServers = new List<ChatMcpServerSettings> { Http("notes", notes.Url), Http("more", notes.Url) };
                        using (McpClientManager manager = new McpClientManager(() => settings))
                        {
                            await manager.RefreshAsync(CancellationToken.None).ConfigureAwait(false);
                            Check(manager.Tools.Count(tool => tool.Server == "notes") == 4 && manager.Tools.Any(tool => tool.Server == "more"), "Both servers should be connected.");
                            Check(manager.Tools.Single(tool => tool.Name == "mcp_notes_fail").Mutating, "fail should need approval.");

                            settings.Chat.McpServers[0].Enabled = false;
                            await manager.RefreshAsync(CancellationToken.None).ConfigureAwait(false);
                            Check(manager.Tools.All(tool => tool.Server != "notes") && manager.Tools.Any(tool => tool.Server == "more"), "Disabling a server did not remove its tools.");
                            McpServerStatus disabled = manager.Servers.Single(server => server.Name == "notes");
                            Check(!disabled.Enabled && !disabled.Connected && disabled.ToolCount == 0, "The disabled server's state is wrong.");
                            await ExpectThrowsAsync<ArgumentException>(() => manager.InvokeAsync("mcp_notes_read_note", "{\"path\":\"x\"}", CancellationToken.None)).ConfigureAwait(false);

                            settings.Chat.McpServers[0].Enabled = true;
                            settings.Chat.McpServers[0].TrustReadOnlyTools = true;
                            await manager.RefreshAsync(CancellationToken.None).ConfigureAwait(false);
                            Check(manager.Tools.Where(tool => tool.Server == "notes").All(tool => !tool.Mutating) && manager.Tools.Count(tool => tool.Server == "notes") == 4, "A trusted server's tools should not need approval.");

                            settings.Chat.McpServers.RemoveAt(1);
                            settings.Chat.McpServers.Add(Http("extra", notes.Url));
                            await manager.RefreshAsync(CancellationToken.None).ConfigureAwait(false);
                            Check(manager.Servers.All(server => server.Name != "more") && manager.Tools.All(tool => tool.Server != "more"), "A removed server is still listed.");
                            Check(manager.Tools.Any(tool => tool.Name == "mcp_extra_read_note"), "An added server was not connected.");
                            string note = await manager.InvokeAsync("mcp_extra_read_note", "{\"path\":\"a\"}", CancellationToken.None).ConfigureAwait(false);
                            Check(note == "note a", "The added server's tool did not run: " + note);
                        }
                    }
                }),
                Case(suite, "HttpServerRestartReconnects", "A call after the server restarts reconnects instead of failing", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        int port = FakeProviderServer.GetFreePort();
                        ClientSettings settings = new ClientSettings();
                        settings.Chat.McpServers = new List<ChatMcpServerSettings> { Http("notes", "http://127.0.0.1:" + port + "/mcp") };
                        using (McpClientManager manager = new McpClientManager(() => settings))
                        {
                            using (TestServer first = TestServer.Start(port))
                            {
                                await manager.RefreshAsync(CancellationToken.None).ConfigureAwait(false);
                                Check(await manager.InvokeAsync("mcp_notes_read_note", "{\"path\":\"1\"}", CancellationToken.None).ConfigureAwait(false) == "note 1", "The first call failed.");
                            }

                            using (TestServer second = TestServer.Start(port))
                            {
                                string again = await manager.InvokeAsync("mcp_notes_read_note", "{\"path\":\"2\"}", CancellationToken.None).ConfigureAwait(false);
                                Check(again == "note 2", "The call after a restart failed: " + again);
                            }

                            manager.Dispose();
                            Check(manager.Tools.Count == 0, "Tools are still offered after dispose.");
                        }
                    }
                }),
                Case(suite, "StdioServerRuns", "xeno mcp runs as a stdio server with the configured environment and working directory", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        new ClientSettings().Save();
                        string dll = typeof(Xeno.Client.Program).Assembly.Location;
                        string? hostPath = Environment.GetEnvironmentVariable("DOTNET_HOST_PATH");
                        string dotnet = !String.IsNullOrEmpty(hostPath) && File.Exists(hostPath) ? hostPath : "dotnet";
                        string directory = Directory.CreateDirectory(Path.Combine(home.Directory, "work")).FullName;
                        string before = Directory.GetCurrentDirectory();
                        ClientSettings settings = new ClientSettings();
                        settings.Chat.McpServers = new List<ChatMcpServerSettings>
                        {
                            new ChatMcpServerSettings
                            {
                                Name = "local xeno",
                                Transport = "stdio",
                                Command = dotnet,
                                Arguments = new List<string> { dll, "mcp" },
                                Environment = new Dictionary<string, string> { ["XENO_CLIENT_HOME"] = home.Directory, ["XENO_MCP_CLIENT_TEST"] = "1" },
                                WorkingDirectory = directory,
                                TimeoutSeconds = 60
                            }
                        };

                        McpClientManager manager = new McpClientManager(() => settings);
                        try
                        {
                            await manager.RefreshAsync(CancellationToken.None).ConfigureAwait(false);
                            McpServerStatus status = manager.Servers.Single();
                            Check(status.Connected && status.Transport == "stdio", "xeno mcp did not connect: " + status.Error);
                            Check(manager.Tools.Any(tool => tool.Name == "mcp_local_xeno_xeno_chat"), "The server name was not sanitized: " + String.Join(",", manager.Tools.Select(tool => tool.Name)));
                            Check(Directory.GetCurrentDirectory() == before && Environment.GetEnvironmentVariable("XENO_MCP_CLIENT_TEST") == null, "Launching changed this process's directory or environment.");
                            string result = await manager.InvokeAsync("mcp_local_xeno_xeno_status", "{}", CancellationToken.None).ConfigureAwait(false);
                            Check(result.Contains("StatusText", StringComparison.Ordinal), "xeno_status over stdio failed: " + result);
                        }
                        finally
                        {
                            manager.Dispose();
                        }
                    }
                })
            });
        }

        #endregion

        #region Private-Methods

        private static ClientSettings StartXenoServer(ClientHomeScope home)
        {
            ClientSettings settings = new ClientSettings();
            settings.LocalDashboard.Database.Filename = Path.Combine(home.Directory, "local-dashboard.db");
            settings.Mcp.Enabled = true;
            settings.Mcp.Host = "127.0.0.1";
            settings.Mcp.Port = FakeProviderServer.GetFreePort();
            if (!XenoMcpHttpRuntime.StartAsync(settings, null, CancellationToken.None).GetAwaiter().GetResult()) throw new InvalidOperationException("Xeno's MCP server did not start.");
            return settings;
        }

        private static ChatMcpServerSettings XenoHttp(ClientSettings xeno)
        {
            ChatMcpServerSettings server = Http("xeno", "http://127.0.0.1:" + xeno.Mcp.Port + "/mcp");
            server.Headers["Authorization"] = "Bearer " + XenoMcpToken.GetOrCreate();
            return server;
        }

        private static ChatMcpServerSettings Http(string name, string url)
        {
            return new ChatMcpServerSettings { Name = name, Transport = "http", Url = url, TimeoutSeconds = 15 };
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

        // A Voltaic HTTP MCP server with a read-only tool and a rich schema, an image tool, a failing tool, and a
        // tool with a long name.
        private sealed class TestServer : IDisposable
        {
            private readonly McpHttpServer _Server;
            private readonly CancellationTokenSource _Cancellation;

            private TestServer(McpHttpServer server, CancellationTokenSource cancellation, int port)
            {
                _Server = server;
                _Cancellation = cancellation;
                Url = "http://127.0.0.1:" + port + "/mcp";
            }

            internal string Url { get; }

            internal static TestServer Start(int port = 0)
            {
                if (port == 0) port = FakeProviderServer.GetFreePort();
                McpHttpServer server = new McpHttpServer("127.0.0.1", port);
                server.ServerName = "Notes";
                server.IncludeToolExceptionMessages = true;
                Dictionary<string, object> properties = new Dictionary<string, object>
                {
                    ["path"] = new { type = "string", description = "Note path" },
                    ["tags"] = new { type = "array", items = new { type = "string" } },
                    ["limit"] = new { anyOf = new object[] { new { type = "integer" }, new { type = "null" } } },
                    ["mode"] = new { type = new[] { "string", "null" }, @enum = new[] { "a", "b" } },
                    ["filter"] = new { type = "object", properties = new Dictionary<string, object> { ["since"] = new { type = "string" } }, required = new[] { "since" } }
                };
                server.RegisterTool(
                    new ToolDefinition
                    {
                        Name = "read_note",
                        Description = "Read a note.",
                        InputSchema = new { type = "object", properties = properties, required = new[] { "path" } },
                        Annotations = new McpAnnotations { ReadOnlyHint = true }
                    },
                    (RpcParameters? parameters) => (object)McpToolCallResult.FromText("note " + PathOf(parameters)));
                server.RegisterTool("picture", "Draw a picture.", new { type = "object", properties = new Dictionary<string, object>() }, (RpcParameters? parameters) =>
                    (object)new McpToolCallResult
                    {
                        Content = new List<object>
                        {
                            new McpImageContent { Data = Convert.ToBase64String(new byte[] { 1, 2, 3, 4, 5, 6 }), MimeType = "image/png" },
                            new McpTextContent { Text = "caption" }
                        }
                    });
                server.RegisterTool("fail", "Always fails.", new { type = "object", properties = new Dictionary<string, object>() }, (RpcParameters? parameters) =>
                {
                    McpToolCallResult result = McpToolCallResult.FromText("disk is full");
                    result.IsError = true;
                    return result;
                });
                server.RegisterTool(LongToolName, "Long name.", new { type = "object", properties = new Dictionary<string, object>() }, (RpcParameters? parameters) => (object)McpToolCallResult.FromText("ok"));
                CancellationTokenSource cancellation = new CancellationTokenSource();
                Task listen = server.StartAsync(cancellation.Token);
                if (listen.IsCompleted) throw new InvalidOperationException("The test MCP server did not start: " + listen.Exception?.GetBaseException().Message);
                return new TestServer(server, cancellation, port);
            }

            public void Dispose()
            {
                _Cancellation.Cancel();
                try { _Server.Stop(); } catch (Exception) { }
                try { _Server.Dispose(); } catch (Exception) { }
                _Cancellation.Dispose();
            }

            private static string PathOf(RpcParameters? parameters)
            {
                NoteArguments? arguments = parameters != null && parameters.HasValue && !String.IsNullOrWhiteSpace(parameters.RawJson)
                    ? Xeno.Core.Serialization.JsonUtil.Deserialize<NoteArguments>(parameters.RawJson)
                    : null;
                return arguments?.Path ?? String.Empty;
            }
        }

        private sealed class NoteArguments
        {
            public string? Path { get; set; } = null;
        }

        #endregion
    }
}

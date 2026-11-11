namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client;
    using Xeno.Client.Services;
    using Xeno.Client.Tools;
    using Xeno.Client.Tools.Workspace;
    using Xeno.Client.Tools.Workspace.Planning;
    using Xeno.Client.Tools.Workspace.Processes;
    using Xeno.Core.Chat;

    /// <summary>
    /// The chat tool host with every tool: Xeno's own, the workspace tools ported from Mux, and their settings gates;
    /// the per-conversation working directory; and chat secrets in settings responses.
    /// </summary>
    public static class WorkspaceToolHostTestSuites
    {
        /// <summary>
        /// Tool host suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor ToolHostSuite()
        {
            const string suite = "WorkspaceToolHost";
            return new TestSuiteDescriptor(suite, "Chat tool host", new List<TestCaseDescriptor>
            {
                Case(suite, "HostOffersEveryMuxTool", "Chat offers Xeno's tools plus every Mux tool group, gated by settings", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (XenoWorkspace workspace = new XenoWorkspace())
                    using (BackgroundProcessRegistry processes = new BackgroundProcessRegistry())
                    {
                        ClientSettings settings = new ClientSettings();
                        XenoChatToolHost host = NewHost(workspace, processes, settings, home.Directory);
                        HashSet<string> names = new HashSet<string>(host.Definitions.Select(item => item.Function.Name), StringComparer.Ordinal);
                        string[] expected = { "xeno_status", "read_file", "write_file", "edit_file", "multi_edit", "delete_file", "file_metadata", "list_directory", "manage_directory", "glob", "grep", "run_process", "process_start", "process_output", "process_list", "process_stop", "web_retrieve", "plan_tasks", "update_task", "remember", "forget", "recall" };
                        string[] missing = expected.Where(name => !names.Contains(name)).ToArray();
                        Check(missing.Length == 0, "Missing tools: " + String.Join(", ", missing));
                        Check(!names.Contains("web_search") && !names.Contains(XenoToolCatalog.ChatToolName), "web_search needs a provider, and xeno_chat is never offered.");
                        Check(host.Definitions.Select(item => item.Function.Name).Distinct(StringComparer.Ordinal).Count() == host.Definitions.Count, "Tool names repeat.");
                        Check(host.Definitions.All(item => item.Function.Description.Length > 10 && item.Function.Parameters.Required.All(required => item.Function.Parameters.Properties.ContainsKey(required))), "A tool lacks a description or requires an undeclared argument.");
                        Check(!host.IsMutating("read_file") && host.IsMutating("write_file") && host.IsMutating("run_process") && !host.IsMutating("glob") && host.IsMutating("remember") && !host.IsMutating("recall"), "Approval flags are wrong.");

                        settings.Chat.WebSearch = new ChatWebSearchSettings { Provider = "Tavily", TavilyApiKey = "key" };
                        settings.Chat.TaskPlanningEnabled = false;
                        settings.Chat.MemoryEnabled = false;
                        names = new HashSet<string>(host.Definitions.Select(item => item.Function.Name), StringComparer.Ordinal);
                        Check(names.Contains("web_search") && !names.Contains("plan_tasks") && !names.Contains("update_task") && !names.Contains("remember") && !names.Contains("recall"), "Settings gates do not apply.");
                        await ExpectThrowsAsync<ArgumentException>(() => host.InvokeAsync("recall", "{}", new ChatToolContext { ConversationId = "c1", WorkingDirectory = home.Directory }, CancellationToken.None)).ConfigureAwait(false);
                        Check(host.Describe().Any(item => item.Name == "read_file" && item.Group == "Files") && host.Describe().Any(item => item.Name == "run_process" && item.Group == "Processes") && host.Describe().Any(item => item.Name == "xeno_status" && item.Group == "Xeno"), "Tool groups are wrong.");
                    }
                }),
                Case(suite, "ToolsRunInTheConversationFolder", "Workspace tools run in the conversation's folder, cannot leave it, and long output is cut off", async () =>
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (XenoWorkspace workspace = new XenoWorkspace())
                    using (BackgroundProcessRegistry processes = new BackgroundProcessRegistry())
                    {
                        string folder = Path.Combine(home.Directory, "project");
                        Directory.CreateDirectory(folder);
                        File.WriteAllText(Path.Combine(folder, "notes.txt"), "alpha\nbeta\n");
                        File.WriteAllText(Path.Combine(home.Directory, "secret.txt"), "outside");
                        ClientSettings settings = new ClientSettings();
                        XenoChatToolHost host = NewHost(workspace, processes, settings, home.Directory);
                        ChatToolContext context = new ChatToolContext { ConversationId = "c1", TurnId = "t1", WorkingDirectory = folder };
                        string read = await host.InvokeAsync("read_file", "{\"file_path\":\"notes.txt\"}", context, CancellationToken.None).ConfigureAwait(false);
                        Check(read.Contains("beta", StringComparison.Ordinal), "read_file did not read inside the folder: " + read);
                        await ExpectThrowsAsync<UnauthorizedAccessException>(() => host.InvokeAsync("read_file", "{\"file_path\":\"../secret.txt\"}", context, CancellationToken.None)).ConfigureAwait(false);
                        await ExpectThrowsAsync<UnauthorizedAccessException>(() => host.InvokeAsync("read_file", "{\"file_path\":\"" + Path.Combine(home.Directory, "secret.txt").Replace("\\", "\\\\") + "\"}", context, CancellationToken.None)).ConfigureAwait(false);
                        await ExpectThrowsAsync<ArgumentException>(() => host.InvokeAsync("read_file", "{\"file_path\":\"notes.txt\"}", new ChatToolContext { ConversationId = "c1", WorkingDirectory = Path.Combine(home.Directory, "missing") }, CancellationToken.None)).ConfigureAwait(false);

                        File.WriteAllText(Path.Combine(folder, "big.txt"), new string('x', 5000));
                        settings.Chat.MaxToolOutputCharacters = 1000;
                        string big = await host.InvokeAsync("read_file", "{\"file_path\":\"big.txt\"}", context, CancellationToken.None).ConfigureAwait(false);
                        Check(big.Length < 1300 && big.Contains("[Output truncated", StringComparison.Ordinal), "Long output was not cut off.");

                        Check(XenoChatToolHost.EffectiveWorkingDirectory(null, new ClientChatSettings()) == Path.GetFullPath(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)), "The default folder should be the home folder.");
                        Check(XenoChatToolHost.EffectiveWorkingDirectory(null, new ClientChatSettings { DefaultWorkingDirectory = folder }) == folder, "The default folder setting is ignored.");
                        Check(XenoChatToolHost.EffectiveWorkingDirectory(home.Directory, new ClientChatSettings { DefaultWorkingDirectory = folder }) == Path.GetFullPath(home.Directory), "A conversation's folder should win.");
                        string note = ChatEngine.ToolsNote(host.Definitions, folder);
                        Check(note.Contains(folder, StringComparison.Ordinal) && note.Contains("- read_file", StringComparison.Ordinal), "The model is not told its folder and tools.");
                    }
                }),
                Case(suite, "ChatSecretsStayHidden", "Settings responses hide web search keys and MCP headers, and saving the placeholder keeps them", () =>
                {
                    ClientChatSettings stored = new ClientChatSettings
                    {
                        WebSearch = new ChatWebSearchSettings { Provider = "Tavily", TavilyApiKey = "tvly-real" },
                        McpServers = new List<ChatMcpServerSettings>
                        {
                            new ChatMcpServerSettings { Name = "docs", Transport = "http", Url = "http://127.0.0.1:1/mcp", Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Authorization"] = "Bearer real" }, Environment = new Dictionary<string, string>(StringComparer.Ordinal) { ["TOKEN"] = "env-real" } }
                        }
                    };
                    ClientChatSettings redacted = ChatSettingsSecrets.Redact(stored);
                    Check(redacted.WebSearch.TavilyApiKey == ChatSettingsSecrets.Placeholder && redacted.McpServers[0].Headers["Authorization"] == ChatSettingsSecrets.Placeholder && redacted.McpServers[0].Environment["TOKEN"] == ChatSettingsSecrets.Placeholder, "Secrets were not hidden.");
                    Check(stored.WebSearch.TavilyApiKey == "tvly-real" && stored.McpServers[0].Headers["Authorization"] == "Bearer real", "Redacting changed the stored settings.");
                    redacted.MaxToolIterations = 5;
                    ChatSettingsSecrets.Restore(redacted, stored);
                    Check(redacted.WebSearch.TavilyApiKey == "tvly-real" && redacted.McpServers[0].Headers["Authorization"] == "Bearer real" && redacted.McpServers[0].Environment["TOKEN"] == "env-real" && redacted.MaxToolIterations == 5, "Saving the placeholder lost the stored secrets.");
                    ClientChatSettings changed = ChatSettingsSecrets.Redact(stored);
                    changed.WebSearch.TavilyApiKey = "tvly-new";
                    changed.McpServers[0].Headers["Authorization"] = "Bearer new";
                    ChatSettingsSecrets.Restore(changed, stored);
                    Check(changed.WebSearch.TavilyApiKey == "tvly-new" && changed.McpServers[0].Headers["Authorization"] == "Bearer new", "A new secret was not kept.");
                    Check(ProviderConfiguration.Redact(new ClientSettings { Chat = stored }).Chat.WebSearch.TavilyApiKey == ChatSettingsSecrets.Placeholder, "The settings response leaks the chat secrets or leaves chat out.");
                    return Task.CompletedTask;
                })
            });
        }

        private static XenoChatToolHost NewHost(XenoWorkspace workspace, BackgroundProcessRegistry processes, ClientSettings settings, string home)
        {
            XenoToolCatalog catalog = new XenoToolCatalog(workspace, token => Task.FromException<IChatStore>(new InvalidOperationException("No chat store in this test.")), null);
            IReadOnlyList<IWorkspaceTool> tools = WorkspaceTools.Create(processes, new TaskPlanStore(), Path.Combine(home, "memory"));
            return new XenoChatToolHost(catalog, () => settings, tools, null);
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

        private static async Task ExpectThrowsAsync<T>(Func<Task> action) where T : Exception
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

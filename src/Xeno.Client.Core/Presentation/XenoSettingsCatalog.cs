namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Services;

    /// <summary>
    /// The editable local settings, in display order, for the desktop app and terminal app settings editors. Applying
    /// values produces a <see cref="LocalClientSettingsUpdateRequest"/> that <see cref="LocalSettingsEditor"/> validates,
    /// the same request the local web dashboard sends.
    /// </summary>
    public static class XenoSettingsCatalog
    {
        #region Public-Members

        /// <summary>
        /// Section names in display order.
        /// </summary>
        public static IReadOnlyList<string> Sections
        {
            get { return Fields.Select(item => item.Section).Distinct(StringComparer.Ordinal).ToList(); }
        }

        /// <summary>
        /// Every editable setting.
        /// </summary>
        public static readonly IReadOnlyList<XenoSettingField> Fields = new List<XenoSettingField>
        {
            Toggle("dashboard.enabled", "Web dashboard", "Serve the local web dashboard", "The browser dashboard for this machine, served by the client while it runs.", r => r.Enabled, (r, v) => r.Enabled = v),
            Choice("dashboard.host", "Web dashboard", "Listen address", "127.0.0.1 keeps the dashboard on this machine; 0.0.0.0 exposes it to your network.", new List<string> { "127.0.0.1", "localhost", "::1", "[::1]", "0.0.0.0" }, r => r.Host, (r, v) => r.Host = v),
            Number("dashboard.port", "Web dashboard", "Port", "Default 9343.", 1, 65535, r => r.Port, (r, v) => r.Port = v),
            Toggle("dashboard.token", "Web dashboard", "Require the local access token", "Recommended. The token is injected into the page the client serves.", r => r.RequireLocalToken, (r, v) => r.RequireLocalToken = v),
            Toggle("dashboard.open", "Web dashboard", "Open the dashboard when the client starts", String.Empty, r => r.OpenBrowserOnStart, (r, v) => r.OpenBrowserOnStart = v),

            Toggle("inference.enabled", "Local inference server", "Serve OpenAI and Ollama APIs on this machine", "Local apps call this endpoint. Models served on this machine are answered here; everything else goes through the Xeno network.", r => Inference(r).Enabled, (r, v) => Inference(r).Enabled = v),
            Choice("inference.host", "Local inference server", "Listen address", "Loopback only.", new List<string> { "127.0.0.1", "localhost", "::1", "[::1]" }, r => Inference(r).Host, (r, v) => Inference(r).Host = v),
            Number("inference.port", "Local inference server", "Port", "Default 11435. Ollama keeps its own port, 11434.", 1, 65535, r => Inference(r).Port, (r, v) => Inference(r).Port = v),
            Toggle("inference.cors", "Local inference server", "Allow browser apps (CORS)", String.Empty, r => Inference(r).EnableCors, (r, v) => Inference(r).EnableCors = v),
            Number("inference.timeout", "Local inference server", "Request timeout (ms)", String.Empty, 1000, Int32.MaxValue, r => Inference(r).RequestTimeoutMs, (r, v) => Inference(r).RequestTimeoutMs = v),
            Number("inference.maxBody", "Local inference server", "Maximum request body (bytes)", String.Empty, 1024, Int32.MaxValue, r => Inference(r).MaxRequestBodyBytes, (r, v) => Inference(r).MaxRequestBodyBytes = v),
            Choice("inference.routingMode", "Local inference server", "Routing", "PreferLocal answers models served on this machine locally and sends the rest to the Xeno network. NetworkOnly sends everything to the network. LocalOnly never uses the network.", new List<string> { "PreferLocal", "NetworkOnly", "LocalOnly" }, r => Inference(r).RoutingMode.ToString(), (r, v) => Inference(r).RoutingMode = Enum.Parse<LocalInferenceRoutingModeEnum>(v, true), false),
            Toggle("inference.discoverLocalModels", "Local inference server", "Route every model a local provider reports", "Includes models you have not added to Xeno, such as a signed-in Ollama's cloud models.", r => Inference(r).DiscoverLocalModels, (r, v) => Inference(r).DiscoverLocalModels = v, false),
            Number("inference.healthInterval", "Local inference server", "Provider health check interval (ms)", "Default 15000.", 1000, 300000, r => Inference(r).HealthCheckIntervalMs, (r, v) => Inference(r).HealthCheckIntervalMs = v, false),
            Number("inference.requestRetention", "Local inference server", "Keep local endpoint requests for usage (days)", "Local endpoint requests are kept this long for the Usage page. Default 30.", 1, 365, r => Inference(r).RequestRetentionDays, (r, v) => Inference(r).RequestRetentionDays = v, false),
            Number("analytics.maxRecords", "Usage analytics", "Most records per chart query", "A Usage page query stops after this many requests and says the range was not read in full. Default 250000.", 10000, 5000000, r => Analytics(r).MaxRecordsPerQuery, (r, v) => Analytics(r).MaxRecordsPerQuery = v, false),
            Number("analytics.topN", "Usage analytics", "Groups shown by name", "Charts by model, provider, or route name this many and fold the rest into Other. Default 8.", 1, 20, r => Analytics(r).DefaultTopN, (r, v) => Analytics(r).DefaultTopN = v, false),
            Number("inference.localQueueTimeout", "Local inference server", "Wait for a free slot (ms)", "How long a local request waits when its provider is at its limit before it gets a 429. 0 rejects immediately. Default 30000.", 0, 300000, r => Inference(r).LocalQueueTimeoutMs, (r, v) => Inference(r).LocalQueueTimeoutMs = v, false),

            Number("concurrency.defaultMax", "Concurrency", "Default max concurrent requests per provider", "Used by providers that set no limit of their own. Network requests and local requests share it. Default 4.", 1, 256, r => r.DefaultMaxConcurrentRequests ?? 4, (r, v) => r.DefaultMaxConcurrentRequests = v, false),

            Number("history.maxEntries", "Request history", "Requests to keep", "Older requests are pruned. Default 1000.", 1, Int32.MaxValue, r => History(r).MaxEntries, (r, v) => History(r).MaxEntries = v, false),
            Toggle("history.bodies", "Request history", "Store request and response bodies", "Bodies can contain prompts and completions. Turn off to keep only metadata.", r => History(r).PersistBodies, (r, v) => History(r).PersistBodies = v, false),
            Number("history.maxBody", "Request history", "Maximum stored body size (bytes)", String.Empty, 0, Int32.MaxValue, r => History(r).MaxBodyBytes, (r, v) => History(r).MaxBodyBytes = v, false),

            Toggle("telemetry.enabled", "Telemetry", "Collect host telemetry", "CPU, memory, disk, and GPU readings stored on this machine.", r => Telemetry(r).Enabled, (r, v) => Telemetry(r).Enabled = v),
            Number("telemetry.interval", "Telemetry", "Host collection interval (ms)", String.Empty, 1000, Int32.MaxValue, r => Telemetry(r).CollectionIntervalMs, (r, v) => Telemetry(r).CollectionIntervalMs = v),
            Number("telemetry.retention", "Telemetry", "Retention (days)", String.Empty, 1, 3650, r => Telemetry(r).RetentionDays, (r, v) => Telemetry(r).RetentionDays = v),
            Toggle("telemetry.gpu", "Telemetry", "Collect GPU telemetry", String.Empty, r => Telemetry(r).Gpu.Enabled, (r, v) => Telemetry(r).Gpu.Enabled = v),
            Toggle("telemetry.providers", "Telemetry", "Collect provider telemetry", "Health, loaded models, and queue depth from local providers.", r => Telemetry(r).ProviderTelemetry.Enabled, (r, v) => Telemetry(r).ProviderTelemetry.Enabled = v),
            Number("telemetry.providerInterval", "Telemetry", "Provider collection interval (ms)", String.Empty, 1000, Int32.MaxValue, r => Telemetry(r).ProviderTelemetry.CollectionIntervalMs, (r, v) => Telemetry(r).ProviderTelemetry.CollectionIntervalMs = v),

            Text("chat.systemPrompt", "Chat", "System prompt for new chats", "Sent before every message in new conversations. Leave empty for Xeno's built-in prompt; each chat's System Prompt can still be changed or cleared.", r => Chat(r).DefaultSystemPrompt ?? String.Empty, (r, v) => Chat(r).DefaultSystemPrompt = String.IsNullOrWhiteSpace(v) ? null : v),
            Number("chat.retention", "Chat", "Keep chat history (days)", "Chat request history and tool calls older than this are removed. Conversations stay until you delete them. Default 30.", 1, 3650, r => Chat(r).HistoryRetentionDays, (r, v) => Chat(r).HistoryRetentionDays = v, false),
            Number("chat.maxToolIterations", "Chat", "Most model calls per message", "How many times the model may call tools and look at the results before it must answer. Default 8.", 1, 20, r => Chat(r).MaxToolIterations, (r, v) => Chat(r).MaxToolIterations = v, false),
            Number("chat.maxToolCalls", "Chat", "Most tool calls per message", "Default 16.", 1, 50, r => Chat(r).MaxToolCallsPerTurn, (r, v) => Chat(r).MaxToolCallsPerTurn = v, false),
            Number("chat.approvalTimeout", "Chat", "Wait for tool approval (seconds)", "Tools that change something ask first. No answer in this time denies the call. Default 120.", 10, 3600, r => Chat(r).ToolApprovalTimeoutSeconds, (r, v) => Chat(r).ToolApprovalTimeoutSeconds = v, false),
            Text("chat.workingDirectory", "Chat", "Working folder for new chats", "File and process tools work in a chat's folder and cannot reach outside it. Leave empty for your home folder.", r => Chat(r).DefaultWorkingDirectory ?? String.Empty, (r, v) => Chat(r).DefaultWorkingDirectory = String.IsNullOrWhiteSpace(v) ? null : v.Trim()),
            Number("chat.processTimeout", "Chat", "Command time limit (seconds)", "run_process stops a command that runs longer than this. Default 120.", 5, 3600, r => Chat(r).ProcessTimeoutSeconds, (r, v) => Chat(r).ProcessTimeoutSeconds = v, false),
            Number("chat.maxToolOutput", "Chat", "Most tool output sent to the model (characters)", "Longer tool results are cut off. Default 50000.", 1000, 1000000, r => Chat(r).MaxToolOutputCharacters, (r, v) => Chat(r).MaxToolOutputCharacters = v, false),
            Toggle("chat.taskPlanning", "Chat", "Task planning tools", "plan_tasks and update_task let the model keep a task list while it works.", r => Chat(r).TaskPlanningEnabled, (r, v) => Chat(r).TaskPlanningEnabled = v, false),
            Toggle("chat.memory", "Chat", "Memory tool", "Lets the model save and recall facts across conversations, in the client home's memory folder.", r => Chat(r).MemoryEnabled, (r, v) => Chat(r).MemoryEnabled = v, false),
            Toggle("chat.webPrivate", "Chat", "Let web_retrieve open private addresses", "Off by default: web_retrieve refuses loopback and private network addresses.", r => Chat(r).WebRetrieveAllowPrivateNetworks, (r, v) => Chat(r).WebRetrieveAllowPrivateNetworks = v, false),
            Choice("chat.webSearch", "Chat", "Web search provider", "web_search is offered when a provider and its API key are set.", new List<string> { "None", "Tavily", "You" }, r => Chat(r).WebSearch.Provider, (r, v) => Chat(r).WebSearch.Provider = v, false),
            Text("chat.tavilyKey", "Chat", "Tavily API key", "Used when the web search provider is Tavily.", r => Chat(r).WebSearch.TavilyApiKey ?? String.Empty, (r, v) => Chat(r).WebSearch.TavilyApiKey = String.IsNullOrWhiteSpace(v) ? null : v.Trim()),
            Text("chat.youKey", "Chat", "You.com API key", "Used when the web search provider is You.", r => Chat(r).WebSearch.YouApiKey ?? String.Empty, (r, v) => Chat(r).WebSearch.YouApiKey = String.IsNullOrWhiteSpace(v) ? null : v.Trim()),
            Number("chat.webSearchResults", "Chat", "Web search results", "Results per search. Default 5.", 1, 20, r => Chat(r).WebSearch.MaxResults, (r, v) => Chat(r).WebSearch.MaxResults = v, false),

            Toggle("mcp.enabled", "MCP server", "Serve Xeno's tools over MCP (HTTP)", "For Claude Code and other agents. \"xeno mcp\" (stdio) works without this.", r => Mcp(r).Enabled, (r, v) => Mcp(r).Enabled = v),
            Choice("mcp.host", "MCP server", "Listen address", "Loopback only.", new List<string> { "127.0.0.1", "localhost", "::1", "[::1]" }, r => Mcp(r).Host, (r, v) => Mcp(r).Host = v),
            Number("mcp.port", "MCP server", "Port", "Default 11436.", 1, 65535, r => Mcp(r).Port, (r, v) => Mcp(r).Port = v),
            Toggle("mcp.token", "MCP server", "Require the MCP token", "Recommended. Show it with \"xeno mcp token\".", r => Mcp(r).RequireToken, (r, v) => Mcp(r).RequireToken = v)
        };

        #endregion

        #region Public-Methods

        /// <summary>
        /// One-line description of a section, for settings editors.
        /// </summary>
        /// <param name="section">Section name from <see cref="Sections"/>.</param>
        /// <returns>Description, empty for an unknown section. Never null.</returns>
        public static string SectionDescription(string? section)
        {
            switch (section)
            {
                case "Web dashboard": return "The browser dashboard this machine's client serves while it runs.";
                case "Local inference server": return "The OpenAI- and Ollama-compatible endpoint apps on this machine call for local and Xeno network models.";
                case "Concurrency": return "How many requests each provider handles at once, shared by local apps and the Xeno network.";
                case "Request history": return "What the client keeps about the requests this machine handled.";
                case "Telemetry": return "Host and provider readings collected and stored on this machine.";
                case "MCP server": return "Lets agents such as Claude Code use Xeno's tools over HTTP on this machine.";
                case "Chat": return "Conversations with local and Xeno network models, and the Xeno tools the model may use.";
                default: return String.Empty;
            }
        }

        /// <summary>
        /// Read every field's current value.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <returns>Values by key.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        public static Dictionary<string, string> Read(ClientSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            LocalClientSettingsUpdateRequest request = LocalSettingsEditor.FromSettings(settings);
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (XenoSettingField field in Fields) values[field.Key] = field.Read(request);
            return values;
        }

        /// <summary>
        /// Validate values and build an update request from the current settings with the values applied.
        /// </summary>
        /// <param name="settings">Current client settings (loaded fresh; its nested objects are reused).</param>
        /// <param name="values">Values by key; missing keys keep their current values.</param>
        /// <returns>Update request.</returns>
        /// <exception cref="ArgumentNullException">Settings or values is null.</exception>
        /// <exception cref="ArgumentException">A value is not valid for its field.</exception>
        public static LocalClientSettingsUpdateRequest BuildRequest(ClientSettings settings, IReadOnlyDictionary<string, string> values)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(values);
            LocalClientSettingsUpdateRequest request = LocalSettingsEditor.FromSettings(settings);
            foreach (XenoSettingField field in Fields)
            {
                if (!values.TryGetValue(field.Key, out string? value)) continue;
                field.Write(request, Normalize(field, value ?? String.Empty));
            }

            return request;
        }

        /// <summary>
        /// Determine whether any changed value requires a client restart.
        /// </summary>
        /// <param name="before">Values before editing.</param>
        /// <param name="after">Values after editing.</param>
        /// <returns>True when a restart is required.</returns>
        public static bool RequiresRestart(IReadOnlyDictionary<string, string> before, IReadOnlyDictionary<string, string> after)
        {
            ArgumentNullException.ThrowIfNull(before);
            ArgumentNullException.ThrowIfNull(after);
            foreach (XenoSettingField field in Fields)
            {
                if (!field.RequiresRestart) continue;
                before.TryGetValue(field.Key, out string? left);
                after.TryGetValue(field.Key, out string? right);
                if (!String.Equals(left, right, StringComparison.Ordinal)) return true;
            }

            return false;
        }

        #endregion

        #region Private-Methods

        private static string Normalize(XenoSettingField field, string value)
        {
            string trimmed = value.Trim();
            switch (field.Kind)
            {
                case XenoSettingKindEnum.Toggle:
                    if (String.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase)) return "true";
                    if (String.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase)) return "false";
                    throw new ArgumentException(field.Label + " must be true or false.");
                case XenoSettingKindEnum.Number:
                    if (!Int32.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int number) || number < field.Minimum || number > field.Maximum)
                    {
                        string range = field.Maximum == Int32.MaxValue ? "at least " + field.Minimum : "from " + field.Minimum + " through " + field.Maximum;
                        throw new ArgumentException(field.Label + " must be a whole number " + range + ".");
                    }

                    return number.ToString(CultureInfo.InvariantCulture);
                case XenoSettingKindEnum.Choice:
                    string? choice = field.Choices.FirstOrDefault(item => String.Equals(item, trimmed, StringComparison.OrdinalIgnoreCase));
                    if (choice == null) throw new ArgumentException(field.Label + " must be one of: " + String.Join(", ", field.Choices) + ".");
                    return choice;
                default:
                    return trimmed;
            }
        }

        private static ClientLocalInferenceServerSettings Inference(LocalClientSettingsUpdateRequest request)
        {
            if (request.LocalInferenceServer == null) request.LocalInferenceServer = new ClientLocalInferenceServerSettings();
            return request.LocalInferenceServer;
        }

        private static ClientLocalHistorySettings History(LocalClientSettingsUpdateRequest request)
        {
            if (request.HistoryRetention == null) request.HistoryRetention = new ClientLocalHistorySettings();
            return request.HistoryRetention;
        }

        private static ClientTelemetrySettings Telemetry(LocalClientSettingsUpdateRequest request)
        {
            if (request.Telemetry == null) request.Telemetry = new ClientTelemetrySettings();
            return request.Telemetry;
        }

        private static ClientMcpSettings Mcp(LocalClientSettingsUpdateRequest request)
        {
            if (request.Mcp == null) request.Mcp = new ClientMcpSettings();
            return request.Mcp;
        }

        private static ClientAnalyticsSettings Analytics(LocalClientSettingsUpdateRequest request)
        {
            if (request.Analytics == null) request.Analytics = new ClientAnalyticsSettings();
            return request.Analytics;
        }

        private static ClientChatSettings Chat(LocalClientSettingsUpdateRequest request)
        {
            if (request.Chat == null) request.Chat = new ClientChatSettings();
            return request.Chat;
        }

        private static XenoSettingField Text(string key, string section, string label, string help, Func<LocalClientSettingsUpdateRequest, string> read, Action<LocalClientSettingsUpdateRequest, string> write, bool restart = false)
        {
            return new XenoSettingField
            {
                Key = key,
                Section = section,
                Label = label,
                Help = help,
                Kind = XenoSettingKindEnum.Text,
                RequiresRestart = restart,
                Read = request => read(request),
                Write = (request, value) => write(request, value)
            };
        }

        private static XenoSettingField Toggle(string key, string section, string label, string help, Func<LocalClientSettingsUpdateRequest, bool> read, Action<LocalClientSettingsUpdateRequest, bool> write, bool restart = true)
        {
            return new XenoSettingField
            {
                Key = key,
                Section = section,
                Label = label,
                Help = help,
                Kind = XenoSettingKindEnum.Toggle,
                RequiresRestart = restart,
                Read = request => read(request) ? "true" : "false",
                Write = (request, value) => write(request, value == "true")
            };
        }

        private static XenoSettingField Number(string key, string section, string label, string help, int minimum, int maximum, Func<LocalClientSettingsUpdateRequest, int> read, Action<LocalClientSettingsUpdateRequest, int> write, bool restart = true)
        {
            return new XenoSettingField
            {
                Key = key,
                Section = section,
                Label = label,
                Help = help,
                Kind = XenoSettingKindEnum.Number,
                Minimum = minimum,
                Maximum = maximum,
                RequiresRestart = restart,
                Read = request => read(request).ToString(CultureInfo.InvariantCulture),
                Write = (request, value) => write(request, Int32.Parse(value, NumberStyles.Integer, CultureInfo.InvariantCulture))
            };
        }

        private static XenoSettingField Choice(string key, string section, string label, string help, List<string> choices, Func<LocalClientSettingsUpdateRequest, string> read, Action<LocalClientSettingsUpdateRequest, string> write, bool restart = true)
        {
            return new XenoSettingField
            {
                Key = key,
                Section = section,
                Label = label,
                Help = help,
                Kind = XenoSettingKindEnum.Choice,
                Choices = choices,
                RequiresRestart = restart,
                Read = request => read(request),
                Write = (request, value) => write(request, value)
            };
        }

        #endregion
    }
}

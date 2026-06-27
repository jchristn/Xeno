namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Net.WebSockets;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client;
    using Xeno.Client.LocalDashboard;
    using Xeno.Client.LocalDashboard.Database;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Core;
    using Xeno.Core.Database;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;
    using Xeno.Core.Services;
    using Xeno.Core.Settings;
    using Xeno.Server;

    /// <summary>
    /// Shared Touchstone test suites for Xeno.
    /// </summary>
    public static class SharedTestSuites
    {
        /// <summary>
        /// All shared suites.
        /// </summary>
        public static IReadOnlyList<TestSuiteDescriptor> All
        {
            get
            {
                return new List<TestSuiteDescriptor>
                {
                    SettingsSuite(),
                    DatabaseProviderSuite(),
                    AuthenticationSuite(),
                    RoutingSuite(),
                    TenantIsolationSuite(),
                    ClientCliSuite(),
                    ManagementApiSuite(),
                    RelaySuite()
                };
            }
        }

        /// <summary>
        /// Settings test suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor SettingsSuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Settings",
                displayName: "Settings",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "DefaultSettingsContainRequiredSections",
                        displayName: "Default settings contain required sections",
                        executeAsync: delegate
                        {
                            XenoSettings settings = new XenoSettings();
                            if (settings.Rest == null) throw new InvalidOperationException("Rest settings missing.");
                            if (settings.Cors == null) throw new InvalidOperationException("CORS settings missing.");
                            if (settings.WebSocket == null) throw new InvalidOperationException("WebSocket settings missing.");
                            if (settings.Database == null) throw new InvalidOperationException("Database settings missing.");
                            if (settings.Logging == null) throw new InvalidOperationException("Logging settings missing.");
                            return Task.CompletedTask;
                        }),
                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "GeneratedIdsAreCompactAndSortable",
                        displayName: "Generated IDs are compact and k-sortable",
                        executeAsync: delegate
                        {
                            string[] prefixes = new[] { "aud_", "cred_", "mc_", "md_", "perm_", "req_", "rmap_", "role_", "sess_", "usr_", "xcl_", "xeno_" };
                            foreach (string prefix in prefixes)
                            {
                                string id = IdGenerator.NewId(prefix);
                                if (id.Length != IdGenerator.MaxIdLength) throw new InvalidOperationException("ID length mismatch: " + id);
                                if (!id.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("ID prefix mismatch: " + id);
                                string[] parts = id.Substring(prefix.Length).Split('_');
                                if (parts.Length != 2) throw new InvalidOperationException("ID did not include sortable and random parts: " + id);
                                if (String.IsNullOrWhiteSpace(parts[0])) throw new InvalidOperationException("ID sortable part missing: " + id);
                                if (String.IsNullOrWhiteSpace(parts[1])) throw new InvalidOperationException("ID random part missing: " + id);
                            }

                            List<string> ids = Enumerable.Range(0, 64).Select(_ => IdGenerator.NewId("req_")).ToList();
                            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count) throw new InvalidOperationException("Generated IDs were not unique.");
                            bool sawNonHexRandomCharacter = false;
                            string? previousSortable = null;
                            foreach (string id in ids)
                            {
                                string[] parts = id.Substring("req_".Length).Split('_');
                                if (parts.Length != 2) throw new InvalidOperationException("ID did not include sortable and random parts: " + id);
                                if (previousSortable != null && String.CompareOrdinal(previousSortable, parts[0]) > 0) throw new InvalidOperationException("Generated IDs were not k-sortable.");
                                previousSortable = parts[0];
                                if (parts[1].Any(character => !Uri.IsHexDigit(character))) sawNonHexRandomCharacter = true;
                            }

                            if (!sawNonHexRandomCharacter) throw new InvalidOperationException("Generated random portions looked hexadecimal-only.");
                            return Task.CompletedTask;
                        }),
                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "ClientTelemetrySettingsClampDefaults",
                        displayName: "Client telemetry settings clamp defaults",
                        executeAsync: delegate
                        {
                            ClientSettings settings = new ClientSettings();
                            if (settings.Telemetry == null) throw new InvalidOperationException("Telemetry settings missing.");
                            if (!settings.Telemetry.Enabled) throw new InvalidOperationException("Telemetry should be enabled by default.");
                            if (settings.Telemetry.CollectionIntervalMs != 15000) throw new InvalidOperationException("Unexpected telemetry interval.");
                            if (settings.Telemetry.RetentionDays != 30) throw new InvalidOperationException("Unexpected telemetry retention.");
                            if (settings.Telemetry.PruneIntervalMinutes != 60) throw new InvalidOperationException("Unexpected telemetry prune interval.");
                            if (!settings.Telemetry.Gpu.Enabled) throw new InvalidOperationException("GPU telemetry should be enabled by default.");
                            settings.Telemetry.CollectionIntervalMs = 1;
                            settings.Telemetry.RetentionDays = 0;
                            settings.Telemetry.PruneIntervalMinutes = 0;
                            settings.Telemetry.Gpu.CommandTimeoutMs = 1;
                            if (settings.Telemetry.CollectionIntervalMs != 5000) throw new InvalidOperationException("Telemetry interval was not clamped.");
                            if (settings.Telemetry.RetentionDays != 1) throw new InvalidOperationException("Telemetry retention minimum was not clamped.");
                            if (settings.Telemetry.PruneIntervalMinutes != 1) throw new InvalidOperationException("Telemetry prune interval minimum was not clamped.");
                            if (settings.Telemetry.Gpu.CommandTimeoutMs != 1000) throw new InvalidOperationException("GPU command timeout minimum was not clamped.");
                            settings.Telemetry.RetentionDays = 9999;
                            if (settings.Telemetry.RetentionDays != 365) throw new InvalidOperationException("Telemetry retention maximum was not clamped.");
                            string telemetryId = IdGenerator.NewId("tel_");
                            string gpuId = IdGenerator.NewId("tgd_");
                            if (telemetryId.Length != IdGenerator.MaxIdLength || !telemetryId.StartsWith("tel_", StringComparison.Ordinal)) throw new InvalidOperationException("Telemetry ID shape mismatch.");
                            if (gpuId.Length != IdGenerator.MaxIdLength || !gpuId.StartsWith("tgd_", StringComparison.Ordinal)) throw new InvalidOperationException("Telemetry GPU ID shape mismatch.");
                            return Task.CompletedTask;
                        })
                });
        }

        /// <summary>
        /// Client CLI test suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor ClientCliSuite()
        {
            return new TestSuiteDescriptor(
                "ClientCli",
                "Client CLI",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        "ClientCli",
                        "ClientResetRestoresFactoryDefaults",
                        "Client reset restores factory defaults",
                        executeAsync: async delegate
                        {
                            string clientHome = Path.Combine(Path.GetTempPath(), "xeno-client-reset-" + Guid.NewGuid().ToString("N"));
                            Directory.CreateDirectory(clientHome);
                            string settingsFile = Path.Combine(clientHome, "client.json");
                            string stateFile = Path.Combine(clientHome, "state.json");

                            try
                            {
                                File.WriteAllText(settingsFile, "{\"TenantId\":\"custom\",\"UseColors\":false,\"Providers\":[]}");
                                File.WriteAllText(stateFile, "{\"IsConnected\":true}");
                                ClientCommandResult cancelled = await RunXenoClientAsync(new[] { "reset" }, clientHome, "NO" + Environment.NewLine).ConfigureAwait(false);
                                if (cancelled.ExitCode != 0) throw new InvalidOperationException("Reset cancel failed: " + cancelled.StandardError + cancelled.StandardOutput);
                                if (!File.ReadAllText(settingsFile).Contains("\"custom\"", StringComparison.Ordinal)) throw new InvalidOperationException("Cancelled reset changed client settings.");
                                if (!File.Exists(stateFile)) throw new InvalidOperationException("Cancelled reset removed runtime state.");

                                ClientCommandResult confirmed = await RunXenoClientAsync(new[] { "reset" }, clientHome, "RESET" + Environment.NewLine).ConfigureAwait(false);
                                if (confirmed.ExitCode != 0) throw new InvalidOperationException("Confirmed reset failed: " + confirmed.StandardError + confirmed.StandardOutput);
                                AssertFactoryDefaultClientSettings(settingsFile, stateFile);

                                File.WriteAllText(settingsFile, "{broken json");
                                File.WriteAllText(stateFile, "{\"IsConnected\":true}");
                                ClientCommandResult forced = await RunXenoClientAsync(new[] { "--json", "reset", "--force" }, clientHome).ConfigureAwait(false);
                                if (forced.ExitCode != 0) throw new InvalidOperationException("Forced reset failed: " + forced.StandardError + forced.StandardOutput);
                                if (!forced.StandardOutput.Contains("\"reset\": true", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Forced reset JSON output did not report success.");
                                AssertFactoryDefaultClientSettings(settingsFile, stateFile);
                            }
                            finally
                            {
                                if (Directory.Exists(clientHome)) Directory.Delete(clientHome, true);
                            }
                        }),
                    new TestCaseDescriptor(
                        "ClientCli",
                        "ClientModelDeleteRemovesConfiguredModels",
                        "Client model delete removes configured models",
                        executeAsync: async delegate
                        {
                            string clientHome = Path.Combine(Path.GetTempPath(), "xeno-client-model-delete-" + Guid.NewGuid().ToString("N"));
                            Directory.CreateDirectory(clientHome);

                            try
                            {
                                ClientCommandResult initial = await RunXenoClientAsync(new[] { "--json", "model", "list" }, clientHome).ConfigureAwait(false);
                                if (initial.ExitCode != 0) throw new InvalidOperationException("Initial model list failed: " + initial.StandardError + initial.StandardOutput);
                                if (!initial.StandardOutput.Contains("llama3.2:latest", StringComparison.Ordinal)) throw new InvalidOperationException("Default model was not listed before delete.");
                                ClientCommandResult modelAliasList = await RunXenoClientAsync(new[] { "--json", "model", "ls" }, clientHome).ConfigureAwait(false);
                                if (modelAliasList.ExitCode != 0 || !modelAliasList.StandardOutput.Contains("llama3.2:latest", StringComparison.Ordinal)) throw new InvalidOperationException("Model ls alias failed: " + modelAliasList.StandardError + modelAliasList.StandardOutput);
                                ClientCommandResult providerAliasList = await RunXenoClientAsync(new[] { "provider", "ls" }, clientHome).ConfigureAwait(false);
                                if (providerAliasList.ExitCode != 0 || !providerAliasList.StandardOutput.Contains("local-ollama", StringComparison.Ordinal)) throw new InvalidOperationException("Provider ls alias failed: " + providerAliasList.StandardError + providerAliasList.StandardOutput);
                                ClientCommandResult configShow = await RunXenoClientAsync(new[] { "config", "show" }, clientHome).ConfigureAwait(false);
                                if (configShow.ExitCode != 0) throw new InvalidOperationException("Config show failed: " + configShow.StandardError + configShow.StandardOutput);
                                if (!configShow.StandardOutput.Contains("llama3.2:latest", StringComparison.Ordinal)) throw new InvalidOperationException("Config show did not include configured models.");
                                if (!configShow.StandardOutput.Contains("Completions:", StringComparison.Ordinal) || !configShow.StandardOutput.Contains("Embeddings:", StringComparison.Ordinal)) throw new InvalidOperationException("Config show did not include model capabilities.");
                                string clientSettingsFile = Path.Combine(clientHome, "client.json");
                                ClientSettings disabledDashboardSettings = DeserializeRequired<ClientSettings>(File.ReadAllText(clientSettingsFile));
                                disabledDashboardSettings.LocalDashboard.Enabled = false;
                                File.WriteAllText(clientSettingsFile, JsonUtil.Serialize(disabledDashboardSettings, true));
                                ClientCommandResult disabledStatus = await RunXenoClientAsync(new[] { "status" }, clientHome).ConfigureAwait(false);
                                if (disabledStatus.ExitCode != 0) throw new InvalidOperationException("Disabled local dashboard status failed: " + disabledStatus.StandardError + disabledStatus.StandardOutput);
                                if (!disabledStatus.StandardOutput.Contains("Local Dashboard: disabled", StringComparison.Ordinal)) throw new InvalidOperationException("Disabled local dashboard status was not compact: " + disabledStatus.StandardOutput);
                                disabledDashboardSettings.LocalDashboard.Enabled = true;
                                File.WriteAllText(clientSettingsFile, JsonUtil.Serialize(disabledDashboardSettings, true));
                                ClientCommandResult providerAdd = await RunXenoClientAsync(new[] { "provider", "add", "--name", "openai-compatible", "--type", "OpenAICompatible", "--endpoint", "http://127.0.0.1:11435" }, clientHome).ConfigureAwait(false);
                                if (providerAdd.ExitCode != 0) throw new InvalidOperationException("Provider add with API type failed: " + providerAdd.StandardError + providerAdd.StandardOutput);
                                ClientCommandResult startHelp = await RunXenoClientAsync(new[] { "start", "--help" }, clientHome).ConfigureAwait(false);
                                if (startHelp.ExitCode != 0 || !startHelp.StandardOutput.Contains("xeno start", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Start alias help failed: " + startHelp.StandardError + startHelp.StandardOutput);
                                ClientCommandResult disconnect = await RunXenoClientAsync(new[] { "disconnect" }, clientHome).ConfigureAwait(false);
                                if (disconnect.ExitCode != 0 || !disconnect.StandardOutput.Contains("not running", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Disconnect alias failed: " + disconnect.StandardError + disconnect.StandardOutput);

                                ClientCommandResult deleted = await RunXenoClientAsync(new[] { "model", "rm", "--name", "llama3.2:latest" }, clientHome).ConfigureAwait(false);
                                if (deleted.ExitCode != 0) throw new InvalidOperationException("Model delete failed: " + deleted.StandardError + deleted.StandardOutput);
                                if (!deleted.StandardOutput.Contains("Deleted model", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Model delete did not report success.");

                                ClientCommandResult emptyList = await RunXenoClientAsync(new[] { "--json", "model", "list" }, clientHome).ConfigureAwait(false);
                                if (emptyList.ExitCode != 0) throw new InvalidOperationException("Model list after delete failed: " + emptyList.StandardError + emptyList.StandardOutput);
                                List<TestCliModelRecord> modelList = DeserializeRequired<List<TestCliModelRecord>>(emptyList.StandardOutput);
                                if (modelList.Count != 0) throw new InvalidOperationException("Model delete did not remove the configured model.");

                                ClientCommandResult missing = await RunXenoClientAsync(new[] { "model", "delete", "--name", "llama3.2:latest" }, clientHome).ConfigureAwait(false);
                                if (missing.ExitCode == 0) throw new InvalidOperationException("Deleting a missing model should fail.");
                                if (!missing.StandardOutput.Contains("Model not found", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Missing model delete did not print a useful error.");

                                ClientCommandResult addedWithEndpoint = await RunXenoClientAsync(new[] { "model", "add", "--name", "all-minilm", "--provider", "ollama", "--endpoint", "http://view.homedns.org:11434", "--api-type", "Ollama", "--type", "embedding" }, clientHome).ConfigureAwait(false);
                                if (addedWithEndpoint.ExitCode == 0) throw new InvalidOperationException("Model add should not accept endpoint URL arguments.");
                                if (!addedWithEndpoint.StandardOutput.Contains("Unknown model add option: --endpoint", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Model add endpoint error was not useful: " + addedWithEndpoint.StandardError + addedWithEndpoint.StandardOutput);
                                ClientCommandResult addedEmbedding = await RunXenoClientAsync(new[] { "model", "add", "--provider", "local-ollama", "--name", "embed-test", "--type", "embedding" }, clientHome).ConfigureAwait(false);
                                if (addedEmbedding.ExitCode != 0) throw new InvalidOperationException("Embedding model add failed: " + addedEmbedding.StandardError + addedEmbedding.StandardOutput);
                                ClientCommandResult addedBoth = await RunXenoClientAsync(new[] { "model", "add", "--provider", "local-ollama", "--name", "both-test", "--completions", "true", "--embeddings", "true" }, clientHome).ConfigureAwait(false);
                                if (addedBoth.ExitCode != 0) throw new InvalidOperationException("Dual-capability model add failed: " + addedBoth.StandardError + addedBoth.StandardOutput);

                                ClientCommandResult capabilityList = await RunXenoClientAsync(new[] { "--json", "model", "ls" }, clientHome).ConfigureAwait(false);
                                if (capabilityList.ExitCode != 0) throw new InvalidOperationException("Capability model list failed: " + capabilityList.StandardError + capabilityList.StandardOutput);
                                List<TestCliModelRecord> capabilityModels = DeserializeRequired<List<TestCliModelRecord>>(capabilityList.StandardOutput);
                                TestCliModelRecord? embeddingModel = capabilityModels.FirstOrDefault(item => String.Equals(item.Name, "embed-test", StringComparison.Ordinal));
                                if (embeddingModel == null || embeddingModel.SupportsCompletions || !embeddingModel.SupportsEmbeddings) throw new InvalidOperationException("Embedding model capabilities were not persisted.");
                                TestCliModelRecord? bothModel = capabilityModels.FirstOrDefault(item => String.Equals(item.Name, "both-test", StringComparison.Ordinal));
                                if (bothModel == null || !bothModel.SupportsCompletions || !bothModel.SupportsEmbeddings) throw new InvalidOperationException("Dual-capability model capabilities were not persisted.");

                                ClientCommandResult scopedDelete = await RunXenoClientAsync(new[] { "model", "remote", "--provider", "local-ollama", "--name", "embed-test" }, clientHome).ConfigureAwait(false);
                                if (scopedDelete.ExitCode != 0) throw new InvalidOperationException("Provider-scoped model delete failed: " + scopedDelete.StandardError + scopedDelete.StandardOutput);
                                ClientCommandResult providerDelete = await RunXenoClientAsync(new[] { "provider", "rm", "--name", "local-ollama" }, clientHome).ConfigureAwait(false);
                                if (providerDelete.ExitCode != 0) throw new InvalidOperationException("Provider rm alias failed: " + providerDelete.StandardError + providerDelete.StandardOutput);
                            }
                            finally
                            {
                                if (Directory.Exists(clientHome)) Directory.Delete(clientHome, true);
                            }
                        }),
                    new TestCaseDescriptor(
                        "ClientCli",
                        "ClientStateRequiresRegistrationBeforeConnected",
                        "Client state requires registration before connected",
                        executeAsync: delegate
                        {
                            string clientHome = Path.Combine(Path.GetTempPath(), "xeno-client-state-" + Guid.NewGuid().ToString("N"));
                            string? previousHome = Environment.GetEnvironmentVariable("XENO_CLIENT_HOME");
                            Environment.SetEnvironmentVariable("XENO_CLIENT_HOME", clientHome);
                            Directory.CreateDirectory(clientHome);

                            try
                            {
                                ClientSettings settings = new ClientSettings
                                {
                                    ClientId = "xcl_state_touchstone",
                                    TenantId = Constants.DefaultTenantId,
                                    Name = "state-touchstone"
                                };
                                settings.Server.Url = "ws://127.0.0.1:9000/ws/v1/clients";
                                settings.Server.AccessKey = "default";
                                settings.Server.SecretKey = "default";

                                ClientConnectionState.MarkStarted(settings, false);
                                ClientConnectionState started = ClientConnectionState.Load();
                                if (started.IsConnected) throw new InvalidOperationException("Started state should not be connected.");
                                if (started.IsRegistered) throw new InvalidOperationException("Started state should not be registered.");
                                if (started.ProcessId != Environment.ProcessId) throw new InvalidOperationException("Started state did not record the current process.");

                                ClientConnectionState.MarkWebSocketConnected(settings);
                                ClientConnectionState socketConnected = ClientConnectionState.Load();
                                if (socketConnected.IsConnected) throw new InvalidOperationException("WebSocket connection should not be reported as connected before registration.");
                                if (socketConnected.IsRegistered) throw new InvalidOperationException("WebSocket connection should not be registered before acknowledgement.");
                                if (socketConnected.WebSocketConnectedUtc == null) throw new InvalidOperationException("WebSocket connection timestamp was not recorded.");

                                ClientConnectionState.MarkRegistered(settings);
                                ClientConnectionState registered = ClientConnectionState.Load();
                                if (!registered.IsConnected) throw new InvalidOperationException("Registered state should be connected.");
                                if (!registered.IsRegistered) throw new InvalidOperationException("Registered state should be registered.");
                                if (registered.ConnectedUtc == null) throw new InvalidOperationException("Connected timestamp was not recorded.");
                                if (registered.RegisteredUtc == null) throw new InvalidOperationException("Registered timestamp was not recorded.");

                                ClientConnectionState.MarkDisconnected(settings, "test complete");
                                ClientConnectionState disconnected = ClientConnectionState.Load();
                                if (disconnected.IsConnected) throw new InvalidOperationException("Disconnected state should not be connected.");
                                if (disconnected.IsRegistered) throw new InvalidOperationException("Disconnected state should not be registered.");
                                return Task.CompletedTask;
                            }
                            finally
                            {
                                Environment.SetEnvironmentVariable("XENO_CLIENT_HOME", previousHome);
                                if (Directory.Exists(clientHome)) Directory.Delete(clientHome, true);
                            }
                        }),
                    new TestCaseDescriptor(
                        "ClientCli",
                        "ClientLocalDashboardSettingsPersist",
                        "Client local dashboard settings persist and state reports URL",
                        executeAsync: delegate
                        {
                            string clientHome = Path.Combine(Path.GetTempPath(), "xeno-client-local-settings-" + Guid.NewGuid().ToString("N"));
                            string? previousHome = Environment.GetEnvironmentVariable("XENO_CLIENT_HOME");
                            Environment.SetEnvironmentVariable("XENO_CLIENT_HOME", clientHome);
                            Directory.CreateDirectory(clientHome);

                            try
                            {
                                ClientSettings settings = new ClientSettings
                                {
                                    ClientId = "xcl_local_settings",
                                    TenantId = Constants.DefaultTenantId,
                                    Name = "local-settings"
                                };
                                settings.LocalDashboard.Enabled = true;
                                settings.LocalDashboard.Host = "0.0.0.0";
                                settings.LocalDashboard.Port = 9343;
                                settings.LocalDashboard.Database.Filename = Path.Combine(clientHome, "local-dashboard.db");
                                settings.Save();

                                ClientSettings loaded = ClientSettings.Load();
                                if (!loaded.LocalDashboard.Enabled) throw new InvalidOperationException("Local dashboard enabled setting did not persist.");
                                if (!String.Equals(loaded.LocalDashboard.Host, "0.0.0.0", StringComparison.Ordinal)) throw new InvalidOperationException("Local dashboard host did not persist.");
                                if (!String.Equals(loaded.LocalDashboard.Database.ResolveFilename(), Path.Combine(clientHome, "local-dashboard.db"), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Local dashboard database filename did not resolve correctly.");

                                ClientConnectionState.MarkLocalDashboardListening(loaded, "http://127.0.0.1:9343");
                                ClientConnectionState state = ClientConnectionState.Load();
                                if (!state.LocalDashboardEnabled) throw new InvalidOperationException("Local dashboard state should be enabled.");
                                if (!state.LocalDashboardListening) throw new InvalidOperationException("Local dashboard state should be listening.");
                                if (!String.Equals(state.LocalDashboardUrl, "http://127.0.0.1:9343", StringComparison.Ordinal)) throw new InvalidOperationException("Local dashboard URL was not recorded.");
                                return Task.CompletedTask;
                            }
                            finally
                            {
                                Environment.SetEnvironmentVariable("XENO_CLIENT_HOME", previousHome);
                                if (Directory.Exists(clientHome)) Directory.Delete(clientHome, true);
                            }
                        }),
                    new TestCaseDescriptor(
                        "ClientCli",
                        "ClientLocalDashboardSqliteHistory",
                        "Client local dashboard SQLite history migrates, writes, summarizes, prunes, and redacts",
                        executeAsync: async delegate
                        {
                            string clientHome = Path.Combine(Path.GetTempPath(), "xeno-client-local-db-" + Guid.NewGuid().ToString("N"));
                            string filename = Path.Combine(clientHome, "history", "local-dashboard.db");
                            Directory.CreateDirectory(clientHome);

                            try
                            {
                                ClientLocalDatabaseSettings databaseSettings = new ClientLocalDatabaseSettings { Filename = filename };
                                SqliteLocalRequestHistoryMethods history = new SqliteLocalRequestHistoryMethods(databaseSettings);
                                await history.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
                                await history.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
                                if (!File.Exists(filename)) throw new InvalidOperationException("SQLite history database was not created.");

                                Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                                {
                                    ["Authorization"] = "Bearer secret",
                                    ["X-Test"] = "visible"
                                };
                                Dictionary<string, string> redacted = LocalSecretRedactor.RedactHeaders(headers);
                                if (!String.Equals(redacted["Authorization"], "redacted", StringComparison.Ordinal)) throw new InvalidOperationException("Authorization header was not redacted.");
                                if (!String.Equals(redacted["X-Test"], "visible", StringComparison.Ordinal)) throw new InvalidOperationException("Non-sensitive header should not be redacted.");
                                bool rejectedTable = false;
                                try
                                {
                                    Xeno.Client.LocalDashboard.Database.Sqlite.Sanitizer.Table("local_request_headers;DROP TABLE local_request_history");
                                }
                                catch (ArgumentException)
                                {
                                    rejectedTable = true;
                                }

                                if (!rejectedTable) throw new InvalidOperationException("SQLite local dashboard sanitizer did not reject a dynamic table name.");

                                ClientLocalHistorySettings retention = new ClientLocalHistorySettings { MaxEntries = 1, MaxBodyBytes = 8, PersistBodies = true };
                                LocalRequestHistoryEntry first = new LocalRequestHistoryEntry
                                {
                                    Id = "req_local_1",
                                    TenantId = Constants.DefaultTenantId,
                                    ClientId = "xcl_local",
                                    ReceivedUtc = DateTime.UtcNow.AddMinutes(-1),
                                    CompletedUtc = DateTime.UtcNow,
                                    Method = "POST",
                                    Path = "/v1/chat/completions?x='test'",
                                    RequestorIp = "127.0.0.1",
                                    ProviderName = "local-ollama",
                                    ApiType = "Ollama",
                                    ModelName = "llama3.2:latest",
                                    StatusCode = 200,
                                    Outcome = LocalRequestOutcomeEnum.Success,
                                    RequestHeaders = redacted,
                                    ResponseHeaders = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["Content-Type"] = "application/json" },
                                    RequestBody = "0123456789abcdef",
                                    ResponseBody = "{\"ok\":true}",
                                    Telemetry = new LocalRequestTelemetry { DurationMs = 12, StreamChunkCount = 0, StreamEventCount = 0 }
                                };
                                first.Timeline.Add(new LocalRequestTimelineEvent { Stage = LocalRequestTimelineStageEnum.Received, TimestampUtc = first.ReceivedUtc, Message = "received" });
                                await history.UpsertAsync(first, retention, CancellationToken.None).ConfigureAwait(false);

                                LocalRequestHistoryEntry? loaded = await history.GetAsync("req_local_1", CancellationToken.None).ConfigureAwait(false);
                                if (loaded == null) throw new InvalidOperationException("History entry was not loaded.");
                                if (!String.Equals(loaded.RequestHeaders["Authorization"], "redacted", StringComparison.Ordinal)) throw new InvalidOperationException("Redacted header was not persisted.");
                                if (loaded.RequestBody == null || loaded.RequestBody.Length > 8) throw new InvalidOperationException("Request body was not truncated according to retention settings.");

                                IReadOnlyList<LocalRequestHistorySummaryBucket> buckets = await history.SummarizeAsync(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddMinutes(1), 5, CancellationToken.None).ConfigureAwait(false);
                                if (!buckets.Any(item => item.Total > 0)) throw new InvalidOperationException("Summary did not include written history.");

                                LocalRequestHistoryEntry second = new LocalRequestHistoryEntry
                                {
                                    Id = "req_local_2",
                                    ReceivedUtc = DateTime.UtcNow,
                                    Method = "POST",
                                    Path = "/v1/embeddings",
                                    Outcome = LocalRequestOutcomeEnum.Failure,
                                    Error = "test"
                                };
                                await history.UpsertAsync(second, retention, CancellationToken.None).ConfigureAwait(false);
                                IReadOnlyList<LocalRequestHistoryEntry> entries = await history.ListAsync(10, CancellationToken.None).ConfigureAwait(false);
                                if (entries.Count != 1) throw new InvalidOperationException("History pruning by MaxEntries failed.");
                                if (!String.Equals(entries[0].Id, "req_local_2", StringComparison.Ordinal)) throw new InvalidOperationException("History pruning retained the wrong entry.");
                            }
                            finally
                            {
                                if (Directory.Exists(clientHome)) Directory.Delete(clientHome, true);
                            }
                        }),
                    new TestCaseDescriptor(
                        "ClientCli",
                        "ClientLocalDashboardApiRequiresTokenAndManagesProvidersAndModels",
                        "Client local dashboard API requires token and manages providers and models",
                        executeAsync: async delegate
                        {
                            string clientHome = Path.Combine(Path.GetTempPath(), "xeno-client-local-api-" + Guid.NewGuid().ToString("N"));
                            string? previousHome = Environment.GetEnvironmentVariable("XENO_CLIENT_HOME");
                            Environment.SetEnvironmentVariable("XENO_CLIENT_HOME", clientHome);
                            Directory.CreateDirectory(clientHome);

                            try
                            {
                                int port = GetFreePort();
                                ClientSettings settings = new ClientSettings
                                {
                                    ClientId = "xcl_local_api",
                                    TenantId = Constants.DefaultTenantId,
                                    Name = "local-api"
                                };
                                settings.LocalDashboard.Enabled = true;
                                settings.LocalDashboard.Host = "127.0.0.1";
                                settings.LocalDashboard.Port = port;
                                settings.LocalDashboard.RequireLocalToken = true;
                                settings.LocalDashboard.Database.Filename = Path.Combine(clientHome, "local-dashboard.db");
                                settings.Runners.Clear();
                                settings.Save();

                                LocalDashboardDatabaseDriverBase driver = LocalDashboardDatabaseDriverFactory.Create(settings.LocalDashboard.Database);
                                await driver.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
                                ILocalRequestHistoryMethods history = driver.RequestHistory;
                                IClientTelemetryHistoryMethods telemetryHistory = driver.TelemetryHistory;
                                ClientSettings invalidHostSettings = new ClientSettings();
                                invalidHostSettings.LocalDashboard.Host = "+";
                                invalidHostSettings.LocalDashboard.Port = port;
                                bool invalidHostRejected = false;
                                try
                                {
                                    using (LocalDashboardService invalidHostService = new LocalDashboardService(invalidHostSettings, history, telemetryHistory, null))
                                    {
                                    }
                                }
                                catch (ArgumentException)
                                {
                                    invalidHostRejected = true;
                                }

                                if (!invalidHostRejected) throw new InvalidOperationException("Wildcard local dashboard host was not rejected.");

                                using (driver)
                                using (LocalDashboardService service = new LocalDashboardService(settings, history, telemetryHistory, null))
                                using (HttpClient httpClient = new HttpClient())
                                {
                                    await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
                                    string baseUrl = "http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture);

                                    HttpResponseMessage unauthorized = await httpClient.GetAsync(baseUrl + "/api/v1/state").ConfigureAwait(false);
                                    if ((int)unauthorized.StatusCode != 401) throw new InvalidOperationException("Local dashboard API should require the local token.");

                                    string html = await httpClient.GetStringAsync(baseUrl + "/").ConfigureAwait(false);
                                    string scriptPath = ExtractHtmlAttribute(html, "src=\"", "\"");
                                    string stylesheetPath = ExtractHtmlAttributeEnding(html, "href=\"", "\"", ".css");
                                    if (!scriptPath.EndsWith(".js", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Local dashboard script path was not a JavaScript asset.");
                                    if (!stylesheetPath.EndsWith(".css", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Local dashboard stylesheet path was not a CSS asset.");
                                    HttpResponseMessage scriptResponse = await httpClient.GetAsync(baseUrl + scriptPath).ConfigureAwait(false);
                                    if (!scriptResponse.IsSuccessStatusCode) throw new InvalidOperationException("Local dashboard script asset did not load: " + scriptPath);
                                    if (!((scriptResponse.Content.Headers.ContentType?.ToString() ?? String.Empty).Contains("javascript", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Local dashboard script asset MIME type was not JavaScript.");
                                    HttpResponseMessage stylesheetResponse = await httpClient.GetAsync(baseUrl + stylesheetPath).ConfigureAwait(false);
                                    if (!stylesheetResponse.IsSuccessStatusCode) throw new InvalidOperationException("Local dashboard stylesheet asset did not load: " + stylesheetPath);
                                    if (!((stylesheetResponse.Content.Headers.ContentType?.ToString() ?? String.Empty).Contains("text/css", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Local dashboard stylesheet asset MIME type was not CSS.");
                                    string token = ExtractLocalDashboardToken(html);
                                    httpClient.DefaultRequestHeaders.Add("X-Xeno-Local-Token", token);

                                    HttpResponseMessage stateResponse = await httpClient.GetAsync(baseUrl + "/api/v1/state").ConfigureAwait(false);
                                    if (!stateResponse.IsSuccessStatusCode) throw new InvalidOperationException("Authorized local dashboard state request failed.");
                                    LocalDashboardDatabaseResponse database = DeserializeRequired<LocalDashboardDatabaseResponse>(await httpClient.GetStringAsync(baseUrl + "/api/v1/database").ConfigureAwait(false));
                                    if (!database.Exists) throw new InvalidOperationException("Local dashboard database status did not report an existing database.");
                                    if (!String.Equals(database.Status.ToString(), LocalDashboardDatabaseStatusEnum.Ready.ToString(), StringComparison.Ordinal)) throw new InvalidOperationException("Local dashboard database status was not ready.");

                                    string providerJson = JsonUtil.Serialize(new LocalProviderUpsertRequest
                                    {
                                        Name = "touchstone",
                                        ApiType = "Ollama",
                                        Endpoint = baseUrl,
                                        TimeoutMs = 300000
                                    });
                                    HttpResponseMessage providerCreate = await PostJsonAsync(httpClient, baseUrl + "/api/v1/providers", providerJson).ConfigureAwait(false);
                                    if (!providerCreate.IsSuccessStatusCode) throw new InvalidOperationException("Provider create failed: " + await providerCreate.Content.ReadAsStringAsync().ConfigureAwait(false));

                                    LocalProviderListResponse providers = DeserializeRequired<LocalProviderListResponse>(await httpClient.GetStringAsync(baseUrl + "/api/v1/providers").ConfigureAwait(false));
                                    if (!providers.Providers.Any(item => String.Equals(item.Name, "touchstone", StringComparison.Ordinal))) throw new InvalidOperationException("Created provider was not listed.");
                                    if (!ClientConnectionState.IsInventoryUpdatePending()) throw new InvalidOperationException("Provider create did not request inventory re-advertisement.");
                                    ILocalDashboardMetadataMethods metadataMethods = (ILocalDashboardMetadataMethods)history;
                                    string? snapshotUtc = await metadataMethods.GetMetadataAsync("last_configuration_snapshot_utc", CancellationToken.None).ConfigureAwait(false);
                                    if (String.IsNullOrWhiteSpace(snapshotUtc)) throw new InvalidOperationException("Provider create did not write local configuration snapshot metadata.");

                                    HttpResponseMessage providerTest = await PostJsonAsync(httpClient, baseUrl + "/api/v1/providers/touchstone/test", "{}").ConfigureAwait(false);
                                    if (!providerTest.IsSuccessStatusCode) throw new InvalidOperationException("Provider test failed: " + await providerTest.Content.ReadAsStringAsync().ConfigureAwait(false));
                                    LocalProviderTestResponse providerTestResponse = DeserializeRequired<LocalProviderTestResponse>(await providerTest.Content.ReadAsStringAsync().ConfigureAwait(false));
                                    if (!providerTestResponse.Success) throw new InvalidOperationException("Provider test did not report success.");

                                    string providerUpdateJson = JsonUtil.Serialize(new LocalProviderUpsertRequest
                                    {
                                        Name = "touchstone",
                                        ApiType = "OpenAICompatible",
                                        Endpoint = "http://127.0.0.1:11435",
                                        TimeoutMs = 120000
                                    });
                                    HttpResponseMessage providerUpdate = await PutJsonAsync(httpClient, baseUrl + "/api/v1/providers/touchstone", providerUpdateJson).ConfigureAwait(false);
                                    if (!providerUpdate.IsSuccessStatusCode) throw new InvalidOperationException("Provider update failed: " + await providerUpdate.Content.ReadAsStringAsync().ConfigureAwait(false));
                                    providers = DeserializeRequired<LocalProviderListResponse>(await httpClient.GetStringAsync(baseUrl + "/api/v1/providers").ConfigureAwait(false));
                                    ClientRunnerSettings? updatedProvider = providers.Providers.FirstOrDefault(item => String.Equals(item.Name, "touchstone", StringComparison.Ordinal));
                                    if (updatedProvider == null || !String.Equals(updatedProvider.ApiType, "OpenAICompatible", StringComparison.Ordinal)) throw new InvalidOperationException("Provider update was not persisted.");

                                    string modelJson = JsonUtil.Serialize(new LocalModelUpsertRequest
                                    {
                                        Provider = "touchstone",
                                        Name = "touchstone-model",
                                        SupportsCompletions = false,
                                        SupportsEmbeddings = true
                                    });
                                    HttpResponseMessage modelCreate = await PostJsonAsync(httpClient, baseUrl + "/api/v1/models", modelJson).ConfigureAwait(false);
                                    if (!modelCreate.IsSuccessStatusCode) throw new InvalidOperationException("Model create failed: " + await modelCreate.Content.ReadAsStringAsync().ConfigureAwait(false));

                                    LocalModelListResponse models = DeserializeRequired<LocalModelListResponse>(await httpClient.GetStringAsync(baseUrl + "/api/v1/models").ConfigureAwait(false));
                                    LocalModelEntry? model = models.Models.FirstOrDefault(item => String.Equals(item.Name, "touchstone-model", StringComparison.Ordinal));
                                    if (model == null) throw new InvalidOperationException("Created model was not listed.");
                                    if (model.SupportsCompletions || !model.SupportsEmbeddings) throw new InvalidOperationException("Created model capability flags were not persisted.");

                                    string modelUpdateJson = JsonUtil.Serialize(new LocalModelUpsertRequest
                                    {
                                        Provider = "touchstone",
                                        Name = "touchstone-model",
                                        SupportsCompletions = true,
                                        SupportsEmbeddings = true
                                    });
                                    HttpResponseMessage modelUpdate = await PutJsonAsync(httpClient, baseUrl + "/api/v1/models/touchstone/touchstone-model", modelUpdateJson).ConfigureAwait(false);
                                    if (!modelUpdate.IsSuccessStatusCode) throw new InvalidOperationException("Model update failed: " + await modelUpdate.Content.ReadAsStringAsync().ConfigureAwait(false));
                                    models = DeserializeRequired<LocalModelListResponse>(await httpClient.GetStringAsync(baseUrl + "/api/v1/models").ConfigureAwait(false));
                                    model = models.Models.FirstOrDefault(item => String.Equals(item.Name, "touchstone-model", StringComparison.Ordinal));
                                    if (model == null || !model.SupportsCompletions || !model.SupportsEmbeddings) throw new InvalidOperationException("Model update capability flags were not persisted.");

                                    HttpResponseMessage modelDelete = await httpClient.DeleteAsync(baseUrl + "/api/v1/models/touchstone/touchstone-model").ConfigureAwait(false);
                                    if (!modelDelete.IsSuccessStatusCode) throw new InvalidOperationException("Model delete failed.");
                                    LocalDashboardMutationResponse modelDeleteResponse = DeserializeRequired<LocalDashboardMutationResponse>(await modelDelete.Content.ReadAsStringAsync().ConfigureAwait(false));
                                    if (!modelDeleteResponse.RemovedOne) throw new InvalidOperationException("Model delete did not report removal.");

                                    HttpResponseMessage providerDelete = await httpClient.DeleteAsync(baseUrl + "/api/v1/providers/touchstone").ConfigureAwait(false);
                                    if (!providerDelete.IsSuccessStatusCode) throw new InvalidOperationException("Provider delete failed.");
                                    LocalDashboardMutationResponse providerDeleteResponse = DeserializeRequired<LocalDashboardMutationResponse>(await providerDelete.Content.ReadAsStringAsync().ConfigureAwait(false));
                                    if (!providerDeleteResponse.RemovedOne) throw new InvalidOperationException("Provider delete did not report removal.");
                                }
                            }
                            finally
                            {
                                Environment.SetEnvironmentVariable("XENO_CLIENT_HOME", previousHome);
                                if (Directory.Exists(clientHome)) Directory.Delete(clientHome, true);
                            }
                        }),
                    new TestCaseDescriptor(
                        "ClientCli",
                        "ClientLocalDashboardFailureDoesNotBlockRegistration",
                        "Client local dashboard startup failure does not block registration",
                        executeAsync: async delegate
                        {
                            int port = GetFreePort();
                            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-client-dashboard-fail-" + Guid.NewGuid().ToString("N") + ".db");
                            string clientHome = Path.Combine(Path.GetTempPath(), "xeno-client-dashboard-fail-" + Guid.NewGuid().ToString("N"));
                            XenoSettings settings = new XenoSettings
                            {
                                Rest = new RestSettings
                                {
                                    Hostname = "127.0.0.1",
                                    Port = port,
                                    Ssl = false
                                },
                                Database = new DatabaseSettings
                                {
                                    Type = DatabaseTypeEnum.Sqlite,
                                    Filename = dbFile
                                }
                            };

                            using (CancellationTokenSource serverTokenSource = new CancellationTokenSource())
                            {
                                XenoServer server = new XenoServer(settings);
                                Task serverTask = server.StartAsync(serverTokenSource.Token);
                                try
                                {
                                    await WaitForHealthAsync(port).ConfigureAwait(false);
                                    Directory.CreateDirectory(clientHome);
                                    ClientSettings clientSettings = new ClientSettings
                                    {
                                        TenantId = Constants.DefaultTenantId,
                                        Name = "dashboard-fail-touchstone"
                                    };
                                    clientSettings.Server.Url = "ws://127.0.0.1:" + port + settings.WebSocket.Path;
                                    clientSettings.Server.RestUrl = "http://127.0.0.1:" + port;
                                    clientSettings.Server.AccessKey = "default";
                                    clientSettings.Server.SecretKey = "default";
                                    clientSettings.LocalDashboard.Enabled = true;
                                    clientSettings.LocalDashboard.Host = "127.0.0.1";
                                    clientSettings.LocalDashboard.Port = port;
                                    clientSettings.LocalDashboard.Database.Filename = Path.Combine(clientHome, "local-dashboard.db");
                                    File.WriteAllText(Path.Combine(clientHome, "client.json"), JsonUtil.Serialize(clientSettings, true));

                                    ClientCommandResult connect = await RunXenoClientAsync(new[] { "--json", "connect", "--timeout", "15000" }, clientHome).ConfigureAwait(false);
                                    if (connect.ExitCode != 0) throw new InvalidOperationException("Background connect failed despite dashboard startup failure: " + connect.StandardError + connect.StandardOutput);
                                    if (!connect.StandardOutput.Contains("\"connected\": true", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Client did not register when local dashboard failed: " + connect.StandardOutput);
                                    if (!connect.StandardOutput.Contains("\"listening\": false", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Local dashboard failure was not reported: " + connect.StandardOutput);

                                    ClientConnectionState state = DeserializeRequired<ClientConnectionState>(File.ReadAllText(Path.Combine(clientHome, "state.json")));
                                    if (!state.IsRegistered) throw new InvalidOperationException("Registered state was not recorded after dashboard failure.");
                                    if (state.LocalDashboardListening) throw new InvalidOperationException("Dashboard should not be listening after port conflict.");
                                    if (String.IsNullOrWhiteSpace(state.LocalDashboardLastError)) throw new InvalidOperationException("Dashboard failure reason was not recorded.");
                                }
                                finally
                                {
                                    await RunXenoClientAsync(new[] { "stop", "--force" }, clientHome).ConfigureAwait(false);
                                    await StopServerAsync(server, serverTask, serverTokenSource, dbFile).ConfigureAwait(false);
                                    if (Directory.Exists(clientHome)) Directory.Delete(clientHome, true);
                                }
                            }
                        }),
                    new TestCaseDescriptor(
                        "ClientCli",
                        "ClientConnectStartsBackgroundWorkerAndLogs",
                        "Client connect starts background worker and logs",
                        executeAsync: async delegate
                        {
                            await RunClientBackgroundConnectSmokeAsync().ConfigureAwait(false);
                        })
                });
        }

        /// <summary>
        /// Authentication and authorization suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor AuthenticationSuite()
        {
            return new TestSuiteDescriptor(
                "Authentication",
                "Authentication and Authorization",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        "Authentication",
                        "SessionIssueValidateRevoke",
                        "Session tokens issue, validate, and revoke",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            AuthenticationService auth = new AuthenticationService();
                            User user = platform.Users[0];
                            AuthTokenResult token = auth.IssueToken(platform, new AuthSettings(), user.TenantId, PrincipalTypeEnum.User, user.Id, null, AuthSchemeEnum.Credential);
                            AuthSession? session = auth.ValidateToken(platform, user.TenantId, token.Token);
                            if (session == null) throw new InvalidOperationException("Token did not validate.");
                            if (!auth.RevokeSession(platform, session.Id)) throw new InvalidOperationException("Session was not revoked.");
                            if (auth.ValidateToken(platform, user.TenantId, token.Token) != null) throw new InvalidOperationException("Revoked token still validates.");
                        }),
                    new TestCaseDescriptor(
                        "Authentication",
                        "CredentialValidation",
                        "Direct credentials validate with constant-time secret comparison",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            AuthenticationService auth = new AuthenticationService();
                            Credential credential = platform.Credentials[0];
                            if (auth.ValidateCredential(platform, credential.TenantId, credential.AccessKey, credential.SecretKey) == null) throw new InvalidOperationException("Credential did not validate.");
                            if (auth.ValidateCredential(platform, credential.TenantId, credential.AccessKey, "wrong") != null) throw new InvalidOperationException("Invalid credential validated.");
                        }),
                    new TestCaseDescriptor(
                        "Authentication",
                        "UserPasswordValidation",
                        "Seeded user password validates only for login bootstrap",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            AuthenticationService auth = new AuthenticationService();
                            User? user = auth.ValidateUserPassword(platform, Constants.DefaultTenantId, "admin@xeno", "password");
                            if (user == null) throw new InvalidOperationException("Seeded user password did not validate.");
                            if (auth.ValidateUserPassword(platform, Constants.DefaultTenantId, "admin@xeno", "wrong") != null) throw new InvalidOperationException("Invalid password validated.");
                        }),
                    new TestCaseDescriptor(
                        "Authentication",
                        "EffectivePermissionResolution",
                        "Tenant administrator effective permissions include admin",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            AuthenticationService auth = new AuthenticationService();
                            RequestContext context = new RequestContext
                            {
                                TenantId = platform.Users[0].TenantId,
                                UserId = platform.Users[0].Id,
                                PrincipalType = PrincipalTypeEnum.User,
                                IsAuthenticated = true
                            };
                            if (!auth.IsAllowed(platform, context, "Admin", OperationTypeEnum.Admin)) throw new InvalidOperationException("Tenant admin did not resolve admin permission.");
                        }),
                    new TestCaseDescriptor(
                        "Authentication",
                        "AuditPersistsDenial",
                        "Authorization denials create audit records",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            AuthenticationService auth = new AuthenticationService();
                            RequestContext context = new RequestContext { TenantId = platform.GetDefaultTenant().Id, IsAuthenticated = false };
                            auth.AppendAudit(platform, context, "Admin", OperationTypeEnum.Admin, false, "PermissionDenied", 403);
                            if (platform.AuditRecords.Count != 1) throw new InvalidOperationException("Audit record was not persisted.");
                            if (platform.AuditRecords[0].ReasonCode != "PermissionDenied") throw new InvalidOperationException("Audit reason mismatch.");
                        })
                });
        }

        /// <summary>
        /// Database provider suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor DatabaseProviderSuite()
        {
            List<TestCaseDescriptor> cases = new List<TestCaseDescriptor>();
            foreach (DatabaseTypeEnum provider in Enum.GetValues<DatabaseTypeEnum>())
            {
                cases.Add(new TestCaseDescriptor(
                    suiteId: "Database",
                    caseId: "Initialize" + provider,
                    displayName: "Initialize " + provider,
                    executeAsync: async delegate
                    {
                        DatabaseDriverBase driver = await DatabaseDriverFactory.CreateAndInitializeAsync(new DatabaseSettings { Type = provider }).ConfigureAwait(false);
                        if (driver.DatabaseType != provider) throw new InvalidOperationException("Provider mismatch.");
                        if (driver.Platform.Tenants.Count < 1) throw new InvalidOperationException("Seed tenant missing.");
                    }));
            }

            cases.Add(new TestCaseDescriptor(
                suiteId: "Database",
                caseId: "SqlitePersistsPlatformState",
                displayName: "SQLite persists platform state",
                executeAsync: async delegate
                {
                    string dbFile = Path.Combine(Path.GetTempPath(), "xeno-persist-" + Guid.NewGuid().ToString("N") + ".db");
                    try
                    {
                        DatabaseSettings settings = new DatabaseSettings
                        {
                            Type = DatabaseTypeEnum.Sqlite,
                            Filename = dbFile
                        };

                        DatabaseDriverBase first = await DatabaseDriverFactory.CreateAndInitializeAsync(settings).ConfigureAwait(false);
                        first.Platform.GetDefaultTenant().Name = "Persisted Tenant";
                        await first.SaveAsync().ConfigureAwait(false);
                        first.Dispose();

                        DatabaseDriverBase second = await DatabaseDriverFactory.CreateAndInitializeAsync(settings).ConfigureAwait(false);
                        if (second.Platform.GetDefaultTenant().Name != "Persisted Tenant") throw new InvalidOperationException("SQLite state did not persist.");
                        second.Dispose();
                    }
                    finally
                    {
                        await DeleteFileWithRetryAsync(dbFile).ConfigureAwait(false);
                    }
                }));

            return new TestSuiteDescriptor("Database", "Database Providers", cases);
        }

        /// <summary>
        /// Routing suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor RoutingSuite()
        {
            return new TestSuiteDescriptor(
                "Routing",
                "Routing",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        "Routing",
                        "CompletionRoutes",
                        "Completion requests route to a connected client",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            RoutingPolicy? defaultPolicy = platform.RoutingPolicies.FirstOrDefault(item => item.TenantId == Constants.DefaultTenantId && item.IsDefault);
                            if (defaultPolicy == null) throw new InvalidOperationException("Default routing policy was not seeded.");
                            if (defaultPolicy.LoadBalancingMode != LoadBalancingModeEnum.LeastRecentlyUsed) throw new InvalidOperationException("Default routing policy did not use least-recently-used balancing.");
                            platform.Clients[0].Connected = true;
                            RoutingService routing = new RoutingService(platform);
                            RoutingDecision decision = await routing.RouteAsync(new RequestContext { TenantId = Constants.DefaultTenantId, IsAuthenticated = true }, "llama3.2:latest", false).ConfigureAwait(false);
                            if (!decision.Success) throw new InvalidOperationException(decision.Message);
                            if (String.IsNullOrWhiteSpace(decision.SelectedClientId)) throw new InvalidOperationException("Client not selected.");
                            if (String.IsNullOrWhiteSpace(decision.RoutingPolicyId)) throw new InvalidOperationException("Routing policy was not recorded.");
                        }),
                    new TestCaseDescriptor(
                        "Routing",
                        "EmbeddingRoutes",
                        "Embedding requests route to an embedding-capable model",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            platform.Clients[0].Connected = true;
                            RoutingService routing = new RoutingService(platform);
                            RoutingDecision decision = await routing.RouteAsync(new RequestContext { TenantId = Constants.DefaultTenantId, IsAuthenticated = true }, "text-embedding-3-small", true).ConfigureAwait(false);
                            if (!decision.Success) throw new InvalidOperationException(decision.Message);
                            if (decision.EffectiveModel != "text-embedding-3-small") throw new InvalidOperationException("Effective model mismatch.");
                        }),
                    new TestCaseDescriptor(
                        "Routing",
                        "UnknownModelDoesNotRoute",
                        "Unknown model requests fail before client selection",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            platform.Clients[0].Connected = true;
                            RoutingService routing = new RoutingService(platform);
                            RoutingDecision decision = await routing.RouteAsync(new RequestContext { TenantId = Constants.DefaultTenantId, IsAuthenticated = true }, "missing-model", false).ConfigureAwait(false);
                            if (decision.Success) throw new InvalidOperationException("Unknown model unexpectedly routed.");
                            if (decision.OutcomeCode != "ModelNotFound") throw new InvalidOperationException("Unexpected outcome " + decision.OutcomeCode + ".");
                        }),
                    new TestCaseDescriptor(
                        "Routing",
                        "DisconnectedClientDoesNotRoute",
                        "Disconnected client is excluded from routing",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            platform.Clients[0].Connected = false;
                            RoutingService routing = new RoutingService(platform);
                            RoutingDecision decision = await routing.RouteAsync(new RequestContext { TenantId = Constants.DefaultTenantId, UserId = platform.Users[0].Id, PrincipalType = PrincipalTypeEnum.User, IsAuthenticated = true }, "llama3.2:latest", false).ConfigureAwait(false);
                            if (decision.Success) throw new InvalidOperationException("Disconnected client unexpectedly routed.");
                            if (decision.OutcomeCode != "NoEligibleClients") throw new InvalidOperationException("Unexpected disconnected-client outcome.");
                        }),
                    new TestCaseDescriptor(
                        "Routing",
                        "AdaptiveSelectsLowerLoad",
                        "Adaptive routing selects the lower-load client",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            platform.Clients[0].Connected = true;
                            platform.Clients[0].InFlightRequests = 3;
                            XenoClientRecord client = new XenoClientRecord
                            {
                                TenantId = Constants.DefaultTenantId,
                                Name = "lower-load",
                                Connected = true,
                                InFlightRequests = 0
                            };
                            client.Models.Add(new XenoClientModel { Name = "llama3.2:latest", SupportsCompletions = true });
                            platform.Clients.Add(client);
                            SetDefaultRoutingPolicyMode(platform, LoadBalancingModeEnum.Adaptive);
                            RoutingService routing = new RoutingService(platform, new RoutingSettings { LoadBalancingMode = LoadBalancingModeEnum.Adaptive });
                            RoutingDecision decision = await routing.RouteAsync(new RequestContext { TenantId = Constants.DefaultTenantId, IsAuthenticated = true }, "llama3.2:latest", false).ConfigureAwait(false);
                            if (!decision.Success) throw new InvalidOperationException(decision.Message);
                            if (decision.SelectedClientId != client.Id) throw new InvalidOperationException("Adaptive lower-load client was not selected.");
                            if (decision.LoadBalancingMode != LoadBalancingModeEnum.Adaptive) throw new InvalidOperationException("Adaptive mode was not recorded.");
                        }),
                    new TestCaseDescriptor(
                        "Routing",
                        "RoundRobinRotatesAcrossEligibleClients",
                        "Round-robin routing rotates across eligible clients",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = await BuildRoutingPlatformWithTwoClientsAsync().ConfigureAwait(false);
                            SetDefaultRoutingPolicyMode(platform, LoadBalancingModeEnum.RoundRobin);
                            RoutingService routing = new RoutingService(platform, new RoutingSettings { LoadBalancingMode = LoadBalancingModeEnum.RoundRobin });
                            RoutingDecision first = await routing.RouteAsync(new RequestContext { TenantId = Constants.DefaultTenantId, IsAuthenticated = true }, "llama3.2:latest", false).ConfigureAwait(false);
                            RoutingDecision second = await routing.RouteAsync(new RequestContext { TenantId = Constants.DefaultTenantId, IsAuthenticated = true }, "llama3.2:latest", false).ConfigureAwait(false);
                            if (!first.Success || !second.Success) throw new InvalidOperationException("Round-robin routing failed.");
                            if (first.SelectedClientId == second.SelectedClientId) throw new InvalidOperationException("Round-robin did not rotate across eligible clients.");
                            if (first.LoadBalancingMode != LoadBalancingModeEnum.RoundRobin) throw new InvalidOperationException("Round-robin mode was not recorded.");
                        }),
                    new TestCaseDescriptor(
                        "Routing",
                        "FirstAvailableSelectsDeterministicFirstClient",
                        "First-available routing selects deterministic first client",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = await BuildRoutingPlatformWithTwoClientsAsync().ConfigureAwait(false);
                            SetDefaultRoutingPolicyMode(platform, LoadBalancingModeEnum.FirstAvailable);
                            RoutingService routing = new RoutingService(platform, new RoutingSettings { LoadBalancingMode = LoadBalancingModeEnum.FirstAvailable });
                            RoutingDecision decision = await routing.RouteAsync(new RequestContext { TenantId = Constants.DefaultTenantId, IsAuthenticated = true }, "llama3.2:latest", false).ConfigureAwait(false);
                            if (!decision.Success) throw new InvalidOperationException(decision.Message);
                            if (decision.SelectedClientName != "alpha-client") throw new InvalidOperationException("First-available did not select the deterministic first client.");
                        }),
                    new TestCaseDescriptor(
                        "Routing",
                        "LeastRecentlyUsedRotatesByRouteScope",
                        "Least-recently-used routing rotates by route scope",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = await BuildRoutingPlatformWithTwoClientsAsync().ConfigureAwait(false);
                            SetDefaultRoutingPolicyMode(platform, LoadBalancingModeEnum.LeastRecentlyUsed);
                            RoutingService routing = new RoutingService(platform, new RoutingSettings { LoadBalancingMode = LoadBalancingModeEnum.LeastRecentlyUsed });
                            RoutingDecision first = await routing.RouteAsync(new RequestContext { TenantId = Constants.DefaultTenantId, IsAuthenticated = true }, "llama3.2:latest", false).ConfigureAwait(false);
                            RoutingDecision second = await routing.RouteAsync(new RequestContext { TenantId = Constants.DefaultTenantId, IsAuthenticated = true }, "llama3.2:latest", false).ConfigureAwait(false);
                            if (!first.Success || !second.Success) throw new InvalidOperationException("Least-recently-used routing failed.");
                            if (first.SelectedClientId == second.SelectedClientId) throw new InvalidOperationException("Least-recently-used did not rotate by route scope.");
                        }),
                    new TestCaseDescriptor(
                        "Routing",
                        "RandomSelectsEligibleClientAndRecordsMode",
                        "Random routing selects an eligible client and records mode",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = await BuildRoutingPlatformWithTwoClientsAsync().ConfigureAwait(false);
                            SetDefaultRoutingPolicyMode(platform, LoadBalancingModeEnum.Random);
                            RoutingService routing = new RoutingService(platform, new RoutingSettings { LoadBalancingMode = LoadBalancingModeEnum.Random });
                            RoutingDecision decision = await routing.RouteAsync(new RequestContext { TenantId = Constants.DefaultTenantId, IsAuthenticated = true }, "llama3.2:latest", false).ConfigureAwait(false);
                            if (!decision.Success) throw new InvalidOperationException(decision.Message);
                            if (decision.LoadBalancingMode != LoadBalancingModeEnum.Random) throw new InvalidOperationException("Random mode was not recorded.");
                            if (!decision.CandidateClientIds.Contains(decision.SelectedClientId ?? String.Empty)) throw new InvalidOperationException("Random selected a non-candidate client.");
                        }),
                    new TestCaseDescriptor(
                        "Routing",
                        "ExactClientPolicyWinsOverWildcard",
                        "Exact client routing policy wins over wildcard default",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = await BuildRoutingPlatformWithTwoClientsAsync().ConfigureAwait(false);
                            RoutingPolicy policy = new RoutingPolicy
                            {
                                TenantId = Constants.DefaultTenantId,
                                Name = "Pin beta",
                                ModelName = "llama3.2:latest",
                                ClientName = "beta-client",
                                LoadBalancingMode = LoadBalancingModeEnum.FirstAvailable,
                                Active = true
                            };
                            platform.RoutingPolicies.Add(policy);
                            RoutingService routing = new RoutingService(platform);
                            RoutingDecision decision = await routing.RouteAsync(new RequestContext { TenantId = Constants.DefaultTenantId, IsAuthenticated = true }, "llama3.2:latest", false).ConfigureAwait(false);
                            if (!decision.Success) throw new InvalidOperationException(decision.Message);
                            if (decision.SelectedClientName != "beta-client") throw new InvalidOperationException("Exact client policy did not select beta-client.");
                            if (decision.RoutingPolicyId != policy.Id) throw new InvalidOperationException("Exact client policy was not recorded.");
                            if (decision.CandidateCount != 1) throw new InvalidOperationException("Exact client policy did not narrow candidate clients.");
                        }),
                    new TestCaseDescriptor(
                        "Routing",
                        "RecordHistoryPersistsRoutingPolicyMetadata",
                        "Request history persists routing policy metadata",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = await BuildRoutingPlatformWithTwoClientsAsync().ConfigureAwait(false);
                            RoutingService routing = new RoutingService(platform);
                            RequestContext context = new RequestContext { TenantId = Constants.DefaultTenantId, IsAuthenticated = true };
                            RoutingDecision decision = await routing.RouteAsync(context, "llama3.2:latest", false).ConfigureAwait(false);
                            if (!decision.Success) throw new InvalidOperationException(decision.Message);
                            RequestHistoryEntry entry = routing.RecordHistory(context, "/v1/chat/completions", "POST", decision);
                            if (entry.RoutingDecision == null) throw new InvalidOperationException("Request history missing routing decision.");
                            if (String.IsNullOrWhiteSpace(entry.RoutingDecision.RoutingPolicyId)) throw new InvalidOperationException("Request history missing routing policy ID.");
                            if (entry.RoutingDecision.RoutingPolicyRequestTimeoutMs != 300000) throw new InvalidOperationException("Request history missing routing policy timeout.");
                        }),
                    new TestCaseDescriptor(
                        "Routing",
                        "StaleHeartbeatExcluded",
                        "Stale client heartbeat excludes client from routing",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            platform.Clients[0].Connected = true;
                            platform.Clients[0].LastHeartbeatUtc = DateTime.UtcNow.AddMinutes(-5);
                            RoutingService routing = new RoutingService(platform);
                            RoutingDecision decision = await routing.RouteAsync(new RequestContext { TenantId = Constants.DefaultTenantId, IsAuthenticated = true }, "llama3.2:latest", false).ConfigureAwait(false);
                            if (decision.Success) throw new InvalidOperationException("Stale heartbeat client unexpectedly routed.");
                            if (decision.OutcomeCode != "NoEligibleClients") throw new InvalidOperationException("Unexpected stale heartbeat outcome.");
                        })
                });
        }

        /// <summary>
        /// Tenant isolation suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor TenantIsolationSuite()
        {
            return new TestSuiteDescriptor(
                "TenantIsolation",
                "Tenant Isolation",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        "TenantIsolation",
                        "CrossTenantRouteDenied",
                        "Cross-tenant routing does not leak clients",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            RoutingService routing = new RoutingService(platform);
                            RoutingDecision decision = await routing.RouteAsync(new RequestContext { TenantId = "ten_other", IsAuthenticated = true }, "llama3.2:latest", false).ConfigureAwait(false);
                            if (decision.Success) throw new InvalidOperationException("Cross-tenant route unexpectedly succeeded.");
                        }),
                    new TestCaseDescriptor(
                        "TenantIsolation",
                        "TenantScopedEnumeration",
                        "Tenant scoped model enumeration is isolated",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            int defaultCount = platform.ModelDefinitions.Count(item => item.TenantId == Constants.DefaultTenantId);
                            int otherCount = platform.ModelDefinitions.Count(item => item.TenantId == "ten_other");
                            if (defaultCount < 1) throw new InvalidOperationException("Default tenant models missing.");
                            if (otherCount != 0) throw new InvalidOperationException("Unexpected cross-tenant models.");
                        })
                });
        }

        /// <summary>
        /// WebSocket relay suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor RelaySuite()
        {
            return new TestSuiteDescriptor(
                "Relay",
                "WebSocket Relay",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        "Relay",
                        "OpenAiCompletionRelaysThroughClient",
                        "OpenAI-compatible completions relay through a connected Xeno client",
                        executeAsync: delegate { return RunRelaySmokeAsync(); }),
                    new TestCaseDescriptor(
                        "Relay",
                        "OpenAiStreamingCompletionRelaysThroughClient",
                        "Streaming OpenAI-compatible completions relay through WebSocket chunks and persist telemetry",
                        executeAsync: delegate { return RunRelayStreamingSmokeAsync(); }),
                    new TestCaseDescriptor(
                        "Relay",
                        "WebSocketRequiresCredential",
                        "WebSocket client connections require client credential authentication",
                        executeAsync: delegate { return RunWebSocketGateSmokeAsync(); }),
                    new TestCaseDescriptor(
                        "Relay",
                        "LoginCredentialConnects",
                        "Password login issues a client credential that can connect to WebSocket",
                        executeAsync: delegate { return RunLoginCredentialConnectSmokeAsync(); })
                });
        }

        /// <summary>
        /// Management API suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor ManagementApiSuite()
        {
            return new TestSuiteDescriptor(
                "ManagementApi",
                "Management API",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        "ManagementApi",
                        "ManagementRoutesSmoke",
                        "Management routes expose settings, clients, analytics, backup, and metrics",
                        executeAsync: delegate { return RunManagementSmokeAsync(); }),
                    new TestCaseDescriptor(
                        "ManagementApi",
                        "DashboardLoginSmoke",
                        "Dashboard login discovers tenants and issues an authenticated bearer session",
                        executeAsync: delegate { return RunDashboardLoginSmokeAsync(); }),
                    new TestCaseDescriptor(
                        "ManagementApi",
                        "ManagementDeleteRoutesSmoke",
                        "Management delete routes remove records and update OpenAPI",
                        executeAsync: delegate { return RunManagementDeleteSmokeAsync(); }),
                    new TestCaseDescriptor(
                        "ManagementApi",
                        "ManagementUpdateRoutesSmoke",
                        "Management update routes persist editable administration records",
                        executeAsync: delegate { return RunManagementUpdateSmokeAsync(); }),
                    new TestCaseDescriptor(
                        "ManagementApi",
                        "PascalCaseClientInventoryModelsAreRegistered",
                        "PascalCase client inventory registers advertised models",
                        executeAsync: delegate { return RunPascalCaseClientInventorySmokeAsync(); })
                });
        }

        private static async Task RunManagementSmokeAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-management-" + Guid.NewGuid().ToString("N") + ".db");
            string settingsFile = Path.Combine(Path.GetTempPath(), "xeno-management-" + Guid.NewGuid().ToString("N") + ".json");
            XenoSettings settings = new XenoSettings
            {
                Rest = new RestSettings
                {
                    Hostname = "127.0.0.1",
                    Port = port,
                    Ssl = false
                },
                Database = new DatabaseSettings
                {
                    Type = DatabaseTypeEnum.Sqlite,
                    Filename = dbFile
                }
            };

            using (CancellationTokenSource serverTokenSource = new CancellationTokenSource())
            {
                XenoServer server = new XenoServer(settings, settingsFile);
                Task serverTask = server.StartAsync(serverTokenSource.Token);

                try
                {
                    await WaitForHealthAsync(port).ConfigureAwait(false);
                    using (HttpClient httpClient = new HttpClient())
                    {
                        string settingsBody = await httpClient.GetStringAsync("http://127.0.0.1:" + port + "/v1.0/api/settings").ConfigureAwait(false);
                        if (!settingsBody.Contains("settingsFile", StringComparison.Ordinal)) throw new InvalidOperationException("Settings response missing settingsFile.");

                        using (HttpResponseMessage saveSettingsResponse = await PostJsonAsync(httpClient, "http://127.0.0.1:" + port + "/v1.0/api/settings", JsonUtil.Serialize(settings)).ConfigureAwait(false))
                        {
                            string saveSettingsBody = await saveSettingsResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!saveSettingsResponse.IsSuccessStatusCode || !File.Exists(settingsFile)) throw new InvalidOperationException("Settings save failed: " + saveSettingsBody);
                        }

                        string backup = await httpClient.GetStringAsync("http://127.0.0.1:" + port + "/v1.0/api/backup").ConfigureAwait(false);
                        if (!backup.Contains("llama3.2:latest", StringComparison.Ordinal)) throw new InvalidOperationException("Backup did not include advertised model.");

                        string clients = await httpClient.GetStringAsync("http://127.0.0.1:" + port + "/v1.0/api/clients").ConfigureAwait(false);
                        if (!clients.Contains("llama3.2:latest", StringComparison.Ordinal)) throw new InvalidOperationException("Clients response missing advertised model.");

                        string routingPolicies = await httpClient.GetStringAsync("http://127.0.0.1:" + port + "/v1.0/api/routing-policies").ConfigureAwait(false);
                        if (!routingPolicies.Contains("Default Routing Policy", StringComparison.Ordinal)) throw new InvalidOperationException("Routing policies response missing default policy.");

                        string analytics = await httpClient.GetStringAsync("http://127.0.0.1:" + port + "/v1.0/api/analytics/overview").ConfigureAwait(false);
                        if (!analytics.Contains("failureRate", StringComparison.Ordinal)) throw new InvalidOperationException("Analytics overview missing failureRate.");

                        using (HttpResponseMessage completionResponse = await PostJsonAsync(httpClient, "http://127.0.0.1:" + port + "/v1/chat/completions", "{\"model\":\"missing-model\",\"messages\":[{\"role\":\"user\",\"content\":\"hello\"}]}").ConfigureAwait(false))
                        {
                            string completionBody = await completionResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if ((int)completionResponse.StatusCode < 400 || !completionBody.Contains("ModelNotFound", StringComparison.Ordinal)) throw new InvalidOperationException("Expected missing model request to be recorded as a failed request: " + completionBody);
                        }

                        string summary = await httpClient.GetStringAsync("http://127.0.0.1:" + port + "/v1.0/api/request-history/summary").ConfigureAwait(false);
                        if (!summary.Contains("\"count\"", StringComparison.Ordinal) || !summary.Contains("\"errors\"", StringComparison.Ordinal) || !summary.Contains("\"success\"", StringComparison.Ordinal) || !summary.Contains("\"failed\"", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Request history summary missing chart bucket fields: " + summary);
                        }

                        string requestHistory = await httpClient.GetStringAsync("http://127.0.0.1:" + port + "/v1.0/api/request-history").ConfigureAwait(false);
                        if (!requestHistory.Contains("/v1/chat/completions", StringComparison.Ordinal) || !requestHistory.Contains("ModelNotFound", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Request history list missing recorded OpenAI request: " + requestHistory);
                        }

                        string metrics = await httpClient.GetStringAsync("http://127.0.0.1:" + port + "/metrics").ConfigureAwait(false);
                        if (!metrics.Contains("xeno_connected_clients", StringComparison.Ordinal)) throw new InvalidOperationException("Metrics output missing connected clients.");
                    }
                }
                finally
                {
                    serverTokenSource.Cancel();
                    server.Stop();
                    try
                    {
                        await serverTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    server.Dispose();
                    await DeleteFileWithRetryAsync(dbFile).ConfigureAwait(false);
                    await DeleteFileWithRetryAsync(settingsFile).ConfigureAwait(false);
                }
            }
        }

        private static async Task RunDashboardLoginSmokeAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-dashboard-login-" + Guid.NewGuid().ToString("N") + ".db");
            string settingsFile = Path.Combine(Path.GetTempPath(), "xeno-dashboard-login-" + Guid.NewGuid().ToString("N") + ".json");
            XenoSettings settings = new XenoSettings
            {
                Rest = new RestSettings
                {
                    Hostname = "127.0.0.1",
                    Port = port,
                    RequireAuthentication = true,
                    Ssl = false
                },
                Database = new DatabaseSettings
                {
                    Type = DatabaseTypeEnum.Sqlite,
                    Filename = dbFile
                }
            };

            using (CancellationTokenSource serverTokenSource = new CancellationTokenSource())
            {
                XenoServer server = new XenoServer(settings, settingsFile);
                Task serverTask = server.StartAsync(serverTokenSource.Token);

                try
                {
                    await WaitForHealthAsync(port).ConfigureAwait(false);
                    string baseUrl = "http://127.0.0.1:" + port;
                    using (HttpClient httpClient = new HttpClient())
                    {
                        using (HttpResponseMessage tenantResponse = await httpClient.GetAsync(baseUrl + "/v1.0/api/auth/tenants?email=admin%40xeno").ConfigureAwait(false))
                        {
                            string tenantJson = await tenantResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!tenantResponse.IsSuccessStatusCode) throw new InvalidOperationException("Tenant lookup failed: " + tenantJson);
                            AuthTenantLookupResponse lookup = DeserializeRequired<AuthTenantLookupResponse>(tenantJson);
                            if (lookup.Tenants.Count != 1) throw new InvalidOperationException("Tenant lookup did not return exactly one tenant.");
                            if (lookup.Tenants[0].Id != Constants.DefaultTenantId) throw new InvalidOperationException("Tenant lookup returned the wrong tenant.");
                        }

                        using (HttpResponseMessage missingTenantResponse = await httpClient.GetAsync(baseUrl + "/v1.0/api/auth/tenants?email=missing%40xeno").ConfigureAwait(false))
                        {
                            string missingTenantJson = await missingTenantResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!missingTenantResponse.IsSuccessStatusCode) throw new InvalidOperationException("Missing-user tenant lookup failed: " + missingTenantJson);
                            AuthTenantLookupResponse lookup = DeserializeRequired<AuthTenantLookupResponse>(missingTenantJson);
                            if (lookup.Tenants.Count != 0) throw new InvalidOperationException("Unknown email should not return tenants.");
                        }

                        using (HttpResponseMessage unauthenticatedSettings = await httpClient.GetAsync(baseUrl + "/v1.0/api/settings").ConfigureAwait(false))
                        {
                            if ((int)unauthenticatedSettings.StatusCode != 401) throw new InvalidOperationException("Authenticated route did not reject anonymous dashboard request.");
                        }

                        string loginBody = JsonUtil.Serialize(new AuthLoginRequest
                        {
                            TenantId = Constants.DefaultTenantId,
                            Email = "admin@xeno",
                            Password = "password"
                        });
                        using (HttpResponseMessage loginResponse = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/auth/dashboard/login", loginBody).ConfigureAwait(false))
                        {
                            string loginJson = await loginResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!loginResponse.IsSuccessStatusCode) throw new InvalidOperationException("Dashboard login failed: " + loginJson);
                            AuthDashboardLoginResponse login = DeserializeRequired<AuthDashboardLoginResponse>(loginJson);
                            if (String.IsNullOrWhiteSpace(login.Token)) throw new InvalidOperationException("Dashboard login did not return a token.");
                            if (login.TenantId != Constants.DefaultTenantId) throw new InvalidOperationException("Dashboard login returned the wrong tenant.");

                            using (HttpRequestMessage settingsRequest = new HttpRequestMessage(HttpMethod.Get, baseUrl + "/v1.0/api/settings"))
                            {
                                settingsRequest.Headers.Add("Authorization", "Bearer " + login.Token);
                                settingsRequest.Headers.Add("x-token", login.Token);
                                settingsRequest.Headers.Add("x-tenant-guid", login.TenantId);
                                using (HttpResponseMessage settingsResponse = await httpClient.SendAsync(settingsRequest).ConfigureAwait(false))
                                {
                                    string settingsJson = await settingsResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                                    if (!settingsResponse.IsSuccessStatusCode) throw new InvalidOperationException("Bearer-authenticated settings request failed: " + settingsJson);
                                    if (!settingsJson.Contains("settingsFile", StringComparison.Ordinal)) throw new InvalidOperationException("Authenticated settings response missing settingsFile.");
                                }
                            }
                        }
                    }
                }
                finally
                {
                    serverTokenSource.Cancel();
                    server.Stop();
                    try
                    {
                        await serverTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    server.Dispose();
                    await DeleteFileWithRetryAsync(dbFile).ConfigureAwait(false);
                    await DeleteFileWithRetryAsync(settingsFile).ConfigureAwait(false);
                }
            }
        }

        private static async Task RunManagementDeleteSmokeAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-management-delete-" + Guid.NewGuid().ToString("N") + ".db");
            XenoSettings settings = new XenoSettings
            {
                Rest = new RestSettings
                {
                    Hostname = "127.0.0.1",
                    Port = port,
                    Ssl = false
                },
                Database = new DatabaseSettings
                {
                    Type = DatabaseTypeEnum.Sqlite,
                    Filename = dbFile
                }
            };

            using (CancellationTokenSource serverTokenSource = new CancellationTokenSource())
            {
                XenoServer server = new XenoServer(settings);
                Task serverTask = server.StartAsync(serverTokenSource.Token);

                try
                {
                    await WaitForHealthAsync(port).ConfigureAwait(false);
                    using (HttpClient httpClient = new HttpClient())
                    {
                        string baseUrl = "http://127.0.0.1:" + port;
                        string openApi = await httpClient.GetStringAsync(baseUrl + "/openapi.json").ConfigureAwait(false);
                        if (!openApi.Contains("client-model", StringComparison.OrdinalIgnoreCase) && !openApi.Contains("clientmodels", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("OpenAPI missing client model delete route.");
                        if (!openApi.Contains("request-history", StringComparison.OrdinalIgnoreCase) && !openApi.Contains("requesthistory", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("OpenAPI missing request history delete route.");
                        foreach (OpenApiSecuritySchemeExpectation scheme in new List<OpenApiSecuritySchemeExpectation>
                        {
                            new OpenApiSecuritySchemeExpectation { Scheme = "xTokenAuth", Header = "x-token" },
                            new OpenApiSecuritySchemeExpectation { Scheme = "accessKeyAuth", Header = "x-access-key" },
                            new OpenApiSecuritySchemeExpectation { Scheme = "secretKeyAuth", Header = "x-secret-key" },
                            new OpenApiSecuritySchemeExpectation { Scheme = "tenantHeader", Header = "x-tenant-guid" },
                            new OpenApiSecuritySchemeExpectation { Scheme = "tenantIdHeader", Header = "x-tenant-id" }
                        })
                        {
                            if (!OpenApiHasSecurityScheme(openApi, scheme.Scheme, scheme.Header)) throw new InvalidOperationException("OpenAPI missing security scheme " + scheme.Scheme + ".");
                        }

                        foreach (OpenApiRouteExpectation route in new List<OpenApiRouteExpectation>
                        {
                            new OpenApiRouteExpectation { Route = "/metrics", Method = "get" },
                            new OpenApiRouteExpectation { Route = "/openapi.json", Method = "get" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/auth/token/details", Method = "get" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/auth/token/refresh", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/auth/logout", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/routing-policies", Method = "get" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/routing-policies/{id}", Method = "put" }
                        })
                        {
                            if (!OpenApiHasMethod(openApi, route.Route, route.Method)) throw new InvalidOperationException("OpenAPI missing " + route.Method.ToUpperInvariant() + " " + route.Route + ".");
                        }

                        foreach (OpenApiParameterExpectation parameter in new List<OpenApiParameterExpectation>
                        {
                            new OpenApiParameterExpectation { Route = "/v1.0/api/users/{id}", Method = "put", Name = "id", Location = "path" },
                            new OpenApiParameterExpectation { Route = "/v1.0/api/request-history/summary", Method = "get", Name = "fromUtc", Location = "query" },
                            new OpenApiParameterExpectation { Route = "/v1.0/api/request-history/summary", Method = "get", Name = "toUtc", Location = "query" },
                            new OpenApiParameterExpectation { Route = "/v1.0/api/request-history/summary", Method = "get", Name = "bucketMinutes", Location = "query" }
                        })
                        {
                            if (!OpenApiHasParameter(openApi, parameter.Route, parameter.Method, parameter.Name, parameter.Location)) throw new InvalidOperationException("OpenAPI missing " + parameter.Location + " parameter " + parameter.Name + " on " + parameter.Method.ToUpperInvariant() + " " + parameter.Route + ".");
                        }

                        foreach (OpenApiRouteExpectation route in new List<OpenApiRouteExpectation>
                        {
                            new OpenApiRouteExpectation { Route = "/v1.0/api/auth/login", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/auth/token", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/users", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/users/{id}", Method = "put" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/routing-policies", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/routing-policies/{id}", Method = "put" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-models", Method = "delete" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/request-history/bulk", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/settings", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1/chat/completions", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1/completions", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1/embeddings", Method = "post" }
                        })
                        {
                            if (!OpenApiHasRequestBody(openApi, route.Route, route.Method)) throw new InvalidOperationException("OpenAPI missing editable request body on " + route.Method.ToUpperInvariant() + " " + route.Route + ".");
                        }

                        string swagger = await httpClient.GetStringAsync(baseUrl + "/swagger").ConfigureAwait(false);
                        if (!swagger.Contains("xeno-auth-panel", StringComparison.Ordinal) || !swagger.Contains("xeno.swagger.auth", StringComparison.Ordinal) || !swagger.Contains("requestInterceptor", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Swagger UI does not include Xeno authentication loading support.");
                        }

                        using (HttpResponseMessage completionResponse = await PostJsonAsync(httpClient, baseUrl + "/v1/chat/completions", "{\"model\":\"llama3.2:latest\",\"messages\":[{\"role\":\"user\",\"content\":\"ping\"}]}").ConfigureAwait(false))
                        {
                            _ = await completionResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        }
                        string historyJson = await httpClient.GetStringAsync(baseUrl + "/v1.0/api/request-history").ConfigureAwait(false);
                        List<RequestHistoryEntry> historyRecords = DeserializeRequired<List<RequestHistoryEntry>>(historyJson);
                        RequestHistoryEntry? history = historyRecords.FirstOrDefault();
                        string historyId = history?.Id ?? String.Empty;
                        if (String.IsNullOrWhiteSpace(historyId)) throw new InvalidOperationException("No request history entry was created.");
                        using (HttpResponseMessage deleteHistory = await DeleteJsonAsync(httpClient, baseUrl + "/v1.0/api/request-history/" + Uri.EscapeDataString(historyId)).ConfigureAwait(false))
                        {
                            if (!deleteHistory.IsSuccessStatusCode) throw new InvalidOperationException("Request history delete failed: " + (int)deleteHistory.StatusCode);
                        }

                        using (HttpResponseMessage credentialResponse = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/credentials", "{}").ConfigureAwait(false))
                        {
                            string credentialJson = await credentialResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!credentialResponse.IsSuccessStatusCode) throw new InvalidOperationException("Credential create failed: " + credentialJson);
                            TestIdRecord credential = DeserializeRequired<TestIdRecord>(credentialJson);
                            string credentialId = credential.Id ?? String.Empty;
                            if (String.IsNullOrWhiteSpace(credentialId)) throw new InvalidOperationException("Created credential did not include an ID.");
                            using (HttpResponseMessage deleteCredential = await DeleteJsonAsync(httpClient, baseUrl + "/v1.0/api/credentials/" + Uri.EscapeDataString(credentialId)).ConfigureAwait(false))
                            {
                                if (!deleteCredential.IsSuccessStatusCode) throw new InvalidOperationException("Credential delete failed: " + (int)deleteCredential.StatusCode);
                            }
                        }

                        string clientsJson = await httpClient.GetStringAsync(baseUrl + "/v1.0/api/clients").ConfigureAwait(false);
                        List<XenoClientRecord> clients = DeserializeRequired<List<XenoClientRecord>>(clientsJson);
                        XenoClientRecord? client = clients.FirstOrDefault();
                        string clientId = client?.Id ?? String.Empty;
                        if (String.IsNullOrWhiteSpace(clientId)) throw new InvalidOperationException("Seed client not found.");

                        XenoClientModel? model = client?.Models.FirstOrDefault();
                        string modelName = model?.Name ?? String.Empty;
                        string source = model?.Source ?? String.Empty;
                        if (String.IsNullOrWhiteSpace(modelName)) throw new InvalidOperationException("Seed client model not found.");

                        string deleteModelBody = JsonUtil.Serialize(new { clientId, name = modelName, source });
                        using (HttpResponseMessage deleteModel = await DeleteJsonAsync(httpClient, baseUrl + "/v1.0/api/client-models", deleteModelBody).ConfigureAwait(false))
                        {
                            string deleteModelJson = await deleteModel.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!deleteModel.IsSuccessStatusCode || !deleteModelJson.Contains("\"removed\": 1", StringComparison.Ordinal)) throw new InvalidOperationException("Client model delete failed: " + deleteModelJson);
                        }

                        using (HttpResponseMessage deleteClient = await DeleteJsonAsync(httpClient, baseUrl + "/v1.0/api/clients/" + Uri.EscapeDataString(clientId)).ConfigureAwait(false))
                        {
                            if (!deleteClient.IsSuccessStatusCode) throw new InvalidOperationException("Client delete failed: " + (int)deleteClient.StatusCode);
                        }

                        string clientsAfterDelete = await httpClient.GetStringAsync(baseUrl + "/v1.0/api/clients").ConfigureAwait(false);
                        if (clientsAfterDelete.Contains(clientId, StringComparison.Ordinal)) throw new InvalidOperationException("Deleted client was still returned.");
                    }
                }
                finally
                {
                    await StopServerAsync(server, serverTask, serverTokenSource, dbFile).ConfigureAwait(false);
                }
            }
        }

        private static async Task<XenoPlatform> BuildRoutingPlatformWithTwoClientsAsync()
        {
            XenoPlatform platform = new XenoPlatform();
            await platform.InitializeAsync().ConfigureAwait(false);
            platform.Clients.Clear();

            XenoClientRecord first = new XenoClientRecord
            {
                TenantId = Constants.DefaultTenantId,
                Name = "alpha-client",
                Connected = true,
                Weight = 1,
                MaxParallelRequests = 4,
                InFlightRequests = 0
            };
            first.Models.Add(new XenoClientModel { Name = "llama3.2:latest", SupportsCompletions = true });
            platform.Clients.Add(first);

            XenoClientRecord second = new XenoClientRecord
            {
                TenantId = Constants.DefaultTenantId,
                Name = "beta-client",
                Connected = true,
                Weight = 1,
                MaxParallelRequests = 4,
                InFlightRequests = 0
            };
            second.Models.Add(new XenoClientModel { Name = "llama3.2:latest", SupportsCompletions = true });
            platform.Clients.Add(second);
            return platform;
        }

        private static void SetDefaultRoutingPolicyMode(XenoPlatform platform, LoadBalancingModeEnum mode)
        {
            RoutingPolicy? policy = platform.RoutingPolicies.FirstOrDefault(item => item.TenantId == Constants.DefaultTenantId && item.IsDefault);
            if (policy == null) throw new InvalidOperationException("Default routing policy was not seeded.");
            policy.LoadBalancingMode = mode;
            policy.LastUpdateUtc = DateTime.UtcNow;
        }

        private static async Task RunManagementUpdateSmokeAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-management-update-" + Guid.NewGuid().ToString("N") + ".db");
            XenoSettings settings = new XenoSettings
            {
                Rest = new RestSettings
                {
                    Hostname = "127.0.0.1",
                    Port = port,
                    Ssl = false
                },
                Database = new DatabaseSettings
                {
                    Type = DatabaseTypeEnum.Sqlite,
                    Filename = dbFile
                }
            };

            using (CancellationTokenSource serverTokenSource = new CancellationTokenSource())
            {
                XenoServer server = new XenoServer(settings);
                Task serverTask = server.StartAsync(serverTokenSource.Token);

                try
                {
                    await WaitForHealthAsync(port).ConfigureAwait(false);
                    using (HttpClient httpClient = new HttpClient())
                    {
                        string baseUrl = "http://127.0.0.1:" + port;
                        string openApi = await httpClient.GetStringAsync(baseUrl + "/openapi.json").ConfigureAwait(false);
                        foreach (string route in new[] { "/v1.0/api/tenants/{id}", "/v1.0/api/users/{id}", "/v1.0/api/credentials/{id}", "/v1.0/api/roles/{id}", "/v1.0/api/permissions/{id}", "/v1.0/api/routing-policies/{id}" })
                        {
                            if (!OpenApiHasMethod(openApi, route, "put")) throw new InvalidOperationException("OpenAPI missing PUT " + route + ".");
                        }

                        foreach (string route in new[] { "/v1.0/api/tenants", "/v1.0/api/users", "/v1.0/api/credentials", "/v1.0/api/roles", "/v1.0/api/permissions", "/v1.0/api/routing-policies" })
                        {
                            if (!OpenApiHasMethod(openApi, route, "post")) throw new InvalidOperationException("OpenAPI missing POST " + route + ".");
                        }

                        using (HttpResponseMessage createTenantResponse = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/tenants", JsonUtil.Serialize(new { name = "Touchstone Created Tenant", active = true })).ConfigureAwait(false))
                        {
                            string createTenantJson = await createTenantResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!createTenantResponse.IsSuccessStatusCode || !createTenantJson.Contains("Touchstone Created Tenant", StringComparison.Ordinal)) throw new InvalidOperationException("Tenant create failed: " + createTenantJson);
                        }

                        using (HttpResponseMessage createUserResponse = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/users", JsonUtil.Serialize(new
                        {
                            tenantId = Constants.DefaultTenantId,
                            username = "touchstone-create@xeno",
                            email = "touchstone-create@xeno",
                            isAdmin = false,
                            isTenantAdmin = false,
                            active = true,
                            password = "create-password"
                        })).ConfigureAwait(false))
                        {
                            string createUserJson = await createUserResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!createUserResponse.IsSuccessStatusCode || !createUserJson.Contains("touchstone-create@xeno", StringComparison.Ordinal)) throw new InvalidOperationException("User create failed: " + createUserJson);
                            if (createUserJson.Contains("PasswordHash", StringComparison.OrdinalIgnoreCase) || createUserJson.Contains("PasswordSalt", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("User create response exposed password material.");
                        }

                        using (HttpResponseMessage createCredentialResponse = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/credentials", JsonUtil.Serialize(new
                        {
                            accessKey = "touchstone-create-access",
                            secretKey = "touchstone-create-secret",
                            active = true
                        })).ConfigureAwait(false))
                        {
                            string createCredentialJson = await createCredentialResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!createCredentialResponse.IsSuccessStatusCode || !createCredentialJson.Contains("touchstone-create-secret", StringComparison.Ordinal)) throw new InvalidOperationException("Credential create failed or did not return one-time secret: " + createCredentialJson);
                        }

                        using (HttpResponseMessage createPermissionResponse = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/permissions", JsonUtil.Serialize(new
                        {
                            resourceType = "TouchstoneCreate",
                            operationType = "Read",
                            permit = true,
                            active = true
                        })).ConfigureAwait(false))
                        {
                            string createPermissionJson = await createPermissionResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!createPermissionResponse.IsSuccessStatusCode || !createPermissionJson.Contains("TouchstoneCreate", StringComparison.Ordinal)) throw new InvalidOperationException("Permission create failed: " + createPermissionJson);
                        }

                        using (HttpResponseMessage createRoleResponse = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/roles", JsonUtil.Serialize(new
                        {
                            name = "Touchstone Created Role",
                            permissionIds = Array.Empty<string>(),
                            active = true
                        })).ConfigureAwait(false))
                        {
                            string createRoleJson = await createRoleResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!createRoleResponse.IsSuccessStatusCode || !createRoleJson.Contains("Touchstone Created Role", StringComparison.Ordinal)) throw new InvalidOperationException("Role create failed: " + createRoleJson);
                        }

                        string routingPolicyId = String.Empty;
                        using (HttpResponseMessage createRoutingPolicyResponse = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/routing-policies", JsonUtil.Serialize(new
                        {
                            name = "Touchstone Routing Policy",
                            modelName = "llama3.2:latest",
                            clientName = "local-client",
                            requestTimeoutMs = 250000,
                            loadBalancingMode = "FirstAvailable",
                            active = true
                        })).ConfigureAwait(false))
                        {
                            string createRoutingPolicyJson = await createRoutingPolicyResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!createRoutingPolicyResponse.IsSuccessStatusCode || !createRoutingPolicyJson.Contains("Touchstone Routing Policy", StringComparison.Ordinal)) throw new InvalidOperationException("Routing policy create failed: " + createRoutingPolicyJson);
                            routingPolicyId = FirstRecordId("[" + createRoutingPolicyJson + "]");
                        }

                        using (HttpResponseMessage updateRoutingPolicyResponse = await PutJsonAsync(httpClient, baseUrl + "/v1.0/api/routing-policies/" + Uri.EscapeDataString(routingPolicyId), JsonUtil.Serialize(new
                        {
                            name = "Touchstone Updated Routing Policy",
                            loadBalancingMode = "Random",
                            requestTimeoutMs = 260000,
                            active = true
                        })).ConfigureAwait(false))
                        {
                            string updateRoutingPolicyJson = await updateRoutingPolicyResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!updateRoutingPolicyResponse.IsSuccessStatusCode || !updateRoutingPolicyJson.Contains("Touchstone Updated Routing Policy", StringComparison.Ordinal) || !updateRoutingPolicyJson.Contains("Random", StringComparison.Ordinal)) throw new InvalidOperationException("Routing policy update failed: " + updateRoutingPolicyJson);
                        }

                        using (HttpResponseMessage deleteRoutingPolicyResponse = await httpClient.DeleteAsync(baseUrl + "/v1.0/api/routing-policies/" + Uri.EscapeDataString(routingPolicyId)).ConfigureAwait(false))
                        {
                            if (!deleteRoutingPolicyResponse.IsSuccessStatusCode) throw new InvalidOperationException("Routing policy delete failed: " + (int)deleteRoutingPolicyResponse.StatusCode);
                        }

                        string tenantId = await FirstRecordIdAsync(httpClient, baseUrl + "/v1.0/api/tenants").ConfigureAwait(false);
                        using (HttpResponseMessage tenantResponse = await PutJsonAsync(httpClient, baseUrl + "/v1.0/api/tenants/" + Uri.EscapeDataString(tenantId), JsonUtil.Serialize(new { name = "Touchstone Tenant", active = true })).ConfigureAwait(false))
                        {
                            string tenantJson = await tenantResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!tenantResponse.IsSuccessStatusCode || !tenantJson.Contains("Touchstone Tenant", StringComparison.Ordinal)) throw new InvalidOperationException("Tenant update failed: " + tenantJson);
                        }

                        string usersJson = await httpClient.GetStringAsync(baseUrl + "/v1.0/api/users").ConfigureAwait(false);
                        if (usersJson.Contains("PasswordHash", StringComparison.OrdinalIgnoreCase) || usersJson.Contains("PasswordSalt", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Users response exposed password material.");
                        string userId = FirstRecordId(usersJson);
                        using (HttpResponseMessage userResponse = await PutJsonAsync(httpClient, baseUrl + "/v1.0/api/users/" + Uri.EscapeDataString(userId), JsonUtil.Serialize(new
                        {
                            username = "touchstone-admin@xeno",
                            email = "touchstone-admin@xeno",
                            isAdmin = true,
                            isTenantAdmin = true,
                            active = true,
                            password = "new-password"
                        })).ConfigureAwait(false))
                        {
                            string userJson = await userResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!userResponse.IsSuccessStatusCode || !userJson.Contains("touchstone-admin@xeno", StringComparison.Ordinal)) throw new InvalidOperationException("User update failed: " + userJson);
                            if (userJson.Contains("PasswordHash", StringComparison.OrdinalIgnoreCase) || userJson.Contains("PasswordSalt", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("User update response exposed password material.");
                        }

                        using (HttpResponseMessage loginResponse = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/auth/login", JsonUtil.Serialize(new
                        {
                            username = "touchstone-admin@xeno",
                            password = "new-password",
                            tenantId = Constants.DefaultTenantId
                        })).ConfigureAwait(false))
                        {
                            string loginJson = await loginResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!loginResponse.IsSuccessStatusCode || !loginJson.Contains("secretKey", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Updated user password did not validate: " + loginJson);
                        }

                        string credentialId = await FirstRecordIdAsync(httpClient, baseUrl + "/v1.0/api/credentials").ConfigureAwait(false);
                        using (HttpResponseMessage credentialResponse = await PutJsonAsync(httpClient, baseUrl + "/v1.0/api/credentials/" + Uri.EscapeDataString(credentialId), JsonUtil.Serialize(new
                        {
                            userId,
                            accessKey = "touchstone-access",
                            secretKey = "touchstone-secret",
                            active = true
                        })).ConfigureAwait(false))
                        {
                            string credentialJson = await credentialResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!credentialResponse.IsSuccessStatusCode || !credentialJson.Contains("touchstone-access", StringComparison.Ordinal)) throw new InvalidOperationException("Credential update failed: " + credentialJson);
                            if (credentialJson.Contains("touchstone-secret", StringComparison.Ordinal)) throw new InvalidOperationException("Credential update response exposed raw secret key.");
                        }

                        using (HttpResponseMessage tokenResponse = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/auth/token", "{\"accessKey\":\"touchstone-access\",\"secretKey\":\"touchstone-secret\"}").ConfigureAwait(false))
                        {
                            string tokenJson = await tokenResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!tokenResponse.IsSuccessStatusCode || !tokenJson.Contains("token", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Updated credential did not validate: " + tokenJson);
                        }

                        string roleId = await FirstRecordIdAsync(httpClient, baseUrl + "/v1.0/api/roles").ConfigureAwait(false);
                        string permissionId = await FirstRecordIdAsync(httpClient, baseUrl + "/v1.0/api/permissions").ConfigureAwait(false);
                        using (HttpResponseMessage roleResponse = await PutJsonAsync(httpClient, baseUrl + "/v1.0/api/roles/" + Uri.EscapeDataString(roleId), JsonUtil.Serialize(new
                        {
                            name = "Touchstone Role",
                            active = true,
                            permissionIds = new[] { permissionId }
                        })).ConfigureAwait(false))
                        {
                            string roleJson = await roleResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!roleResponse.IsSuccessStatusCode || !roleJson.Contains("Touchstone Role", StringComparison.Ordinal)) throw new InvalidOperationException("Role update failed: " + roleJson);
                            if (!roleJson.Contains(permissionId, StringComparison.Ordinal)) throw new InvalidOperationException("Role update response did not include assigned permission.");
                        }

                        using (HttpResponseMessage permissionResponse = await PutJsonAsync(httpClient, baseUrl + "/v1.0/api/permissions/" + Uri.EscapeDataString(permissionId), JsonUtil.Serialize(new
                        {
                            resourceType = "Touchstone",
                            operationType = "Write",
                            permit = false,
                            active = true
                        })).ConfigureAwait(false))
                        {
                            string permissionJson = await permissionResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!permissionResponse.IsSuccessStatusCode || !permissionJson.Contains("Touchstone", StringComparison.Ordinal) || !permissionJson.Contains("Write", StringComparison.Ordinal)) throw new InvalidOperationException("Permission update failed: " + permissionJson);
                        }
                    }
                }
                finally
                {
                    await StopServerAsync(server, serverTask, serverTokenSource, dbFile).ConfigureAwait(false);
                }
            }
        }

        private static async Task RunPascalCaseClientInventorySmokeAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-pascal-inventory-" + Guid.NewGuid().ToString("N") + ".db");
            XenoSettings settings = new XenoSettings
            {
                Rest = new RestSettings
                {
                    Hostname = "127.0.0.1",
                    Port = port,
                    Ssl = false
                },
                Database = new DatabaseSettings
                {
                    Type = DatabaseTypeEnum.Sqlite,
                    Filename = dbFile
                }
            };

            using (CancellationTokenSource serverTokenSource = new CancellationTokenSource())
            using (CancellationTokenSource clientTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                XenoServer server = new XenoServer(settings);
                Task serverTask = server.StartAsync(serverTokenSource.Token);

                try
                {
                    await WaitForHealthAsync(port).ConfigureAwait(false);
                    using (ClientWebSocket socket = new ClientWebSocket())
                    {
                        ApplySeedCredentialHeaders(socket);
                        await socket.ConnectAsync(new Uri("ws://127.0.0.1:" + port + settings.WebSocket.Path), clientTokenSource.Token).ConfigureAwait(false);
                        string? connected = await ReceiveWebSocketTextAsync(socket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(connected) || !connected.Contains("server.connected", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Authenticated WebSocket did not connect: " + connected);

                        await SendWebSocketJsonAsync(socket, new
                        {
                            type = "client.register",
                            clientId = "xcl_pascal_inventory",
                            tenantId = Constants.DefaultTenantId,
                            name = "pascal-inventory-client",
                            inventory = new
                            {
                                Providers = new[]
                                {
                                    new
                                    {
                                        Name = "pascal-provider",
                                        ApiType = "Ollama",
                                        Models = new[]
                                        {
                                            new { Name = "touchstone-pascal-model", SupportsCompletions = true, SupportsEmbeddings = true }
                                        }
                                    }
                                }
                            }
                        }, clientTokenSource.Token).ConfigureAwait(false);

                        string? registered = await ReceiveWebSocketTextAsync(socket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(registered) || !registered.Contains("server.registered", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Client did not register: " + registered);

                        await SendWebSocketJsonAsync(socket, new
                        {
                            type = "client.inventory.update",
                            clientId = "xcl_pascal_inventory",
                            tenantId = Constants.DefaultTenantId,
                            sentUtc = DateTime.UtcNow,
                            inventory = new
                            {
                                providers = new[]
                                {
                                    new
                                    {
                                        name = "updated-provider",
                                        apiType = "OpenAICompatible",
                                        models = new[]
                                        {
                                            new { name = "touchstone-updated-model", supportsCompletions = true, supportsEmbeddings = false }
                                        }
                                    }
                                }
                            }
                        }, clientTokenSource.Token).ConfigureAwait(false);

                        string? updated = await ReceiveWebSocketTextAsync(socket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(updated) || !updated.Contains("server.inventory.updated", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Client inventory update was not acknowledged: " + updated);
                    }

                    using (HttpClient httpClient = new HttpClient())
                    {
                        string clients = await httpClient.GetStringAsync("http://127.0.0.1:" + port + "/v1.0/api/clients").ConfigureAwait(false);
                        if (!clients.Contains("touchstone-updated-model", StringComparison.Ordinal)) throw new InvalidOperationException("Updated client models did not include inventory update model: " + clients);

                        string models = await httpClient.GetStringAsync("http://127.0.0.1:" + port + "/v1/models").ConfigureAwait(false);
                        if (!models.Contains("touchstone-updated-model", StringComparison.Ordinal)) throw new InvalidOperationException("OpenAI model list did not include inventory update model: " + models);
                    }
                }
                finally
                {
                    await StopServerAsync(server, serverTask, serverTokenSource, dbFile).ConfigureAwait(false);
                }
            }
        }

        private static async Task RunClientBackgroundConnectSmokeAsync()
        {
            int port = GetFreePort();
            int dashboardPort = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-client-background-" + Guid.NewGuid().ToString("N") + ".db");
            string clientHome = Path.Combine(Path.GetTempPath(), "xeno-client-background-" + Guid.NewGuid().ToString("N"));
            XenoSettings settings = new XenoSettings
            {
                Rest = new RestSettings
                {
                    Hostname = "127.0.0.1",
                    Port = port,
                    Ssl = false
                },
                Database = new DatabaseSettings
                {
                    Type = DatabaseTypeEnum.Sqlite,
                    Filename = dbFile
                }
            };

            using (CancellationTokenSource serverTokenSource = new CancellationTokenSource())
            {
                XenoServer server = new XenoServer(settings);
                Task serverTask = server.StartAsync(serverTokenSource.Token);

                try
                {
                    await WaitForHealthAsync(port).ConfigureAwait(false);
                    Directory.CreateDirectory(clientHome);
                    ClientSettings clientSettings = new ClientSettings
                    {
                        TenantId = Constants.DefaultTenantId,
                        Name = "background-touchstone"
                    };
                    clientSettings.Server.Url = "ws://127.0.0.1:" + port + settings.WebSocket.Path;
                    clientSettings.Server.RestUrl = "http://127.0.0.1:" + port;
                    clientSettings.Server.AccessKey = "default";
                    clientSettings.Server.SecretKey = "default";
                    clientSettings.LocalDashboard.Enabled = true;
                    clientSettings.LocalDashboard.Host = "127.0.0.1";
                    clientSettings.LocalDashboard.Port = dashboardPort;
                    clientSettings.LocalDashboard.Database.Filename = Path.Combine(clientHome, "local-dashboard.db");
                    File.WriteAllText(Path.Combine(clientHome, "client.json"), JsonUtil.Serialize(clientSettings, true));

                    ClientCommandResult connect = await RunXenoClientAsync(new[] { "start", "--timeout", "15000" }, clientHome).ConfigureAwait(false);
                    if (connect.ExitCode != 0) throw new InvalidOperationException("Background connect failed: " + connect.StandardError + connect.StandardOutput);
                    if (!connect.StandardOutput.Contains("Xeno client connected in the background", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Background start did not report connected: " + connect.StandardOutput);
                    if (!connect.StandardOutput.Contains("Local dashboard : http://127.0.0.1:" + dashboardPort.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Background start did not include local dashboard URL: " + connect.StandardOutput);

                    string stateFile = Path.Combine(clientHome, "state.json");
                    ClientConnectionState state = DeserializeRequired<ClientConnectionState>(File.ReadAllText(stateFile));
                    if (!state.IsConnected) throw new InvalidOperationException("Background state is not connected.");
                    if (!state.IsRegistered) throw new InvalidOperationException("Background state is not registered.");
                    if (!state.IsBackground) throw new InvalidOperationException("Background state did not record background mode.");
                    if (state.ProcessId == null || !IsProcessRunningForTest(state.ProcessId.Value)) throw new InvalidOperationException("Background worker process is not running.");
                    if (state.WebSocketConnectedUtc == null) throw new InvalidOperationException("Background state did not record WebSocket connection time.");
                    if (state.RegisteredUtc == null) throw new InvalidOperationException("Background state did not record registration time.");
                    if (!state.LocalDashboardListening) throw new InvalidOperationException("Background local dashboard was not listening.");
                    if (!String.Equals(state.LocalDashboardUrl, "http://127.0.0.1:" + dashboardPort.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Background local dashboard URL mismatch.");

                    await WaitForRegisteredClientAsync(port, "background-touchstone").ConfigureAwait(false);

                    ClientCommandResult secondConnect = await RunXenoClientAsync(new[] { "--json", "connect", "--timeout", "5000" }, clientHome).ConfigureAwait(false);
                    if (secondConnect.ExitCode != 0) throw new InvalidOperationException("Second background connect failed: " + secondConnect.StandardError + secondConnect.StandardOutput);
                    if (!secondConnect.StandardOutput.Contains("\"alreadyRunning\": true", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Second connect did not detect existing worker: " + secondConnect.StandardOutput);
                    if (!secondConnect.StandardOutput.Contains("\"listening\": true", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Second connect did not report local dashboard listening: " + secondConnect.StandardOutput);
                    if (!secondConnect.StandardOutput.Contains("http://127.0.0.1:" + dashboardPort.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Second connect did not include local dashboard URL: " + secondConnect.StandardOutput);

                    ClientCommandResult status = await RunXenoClientAsync(new[] { "--json", "status" }, clientHome).ConfigureAwait(false);
                    if (status.ExitCode != 0) throw new InvalidOperationException("Background status failed: " + status.StandardError + status.StandardOutput);
                    if (!status.StandardOutput.Contains("\"registered\": true", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Status did not report registered state: " + status.StandardOutput);
                    if (!status.StandardOutput.Contains("\"localDashboard\"", StringComparison.OrdinalIgnoreCase) || !status.StandardOutput.Contains("\"listening\": true", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Status did not report local dashboard state: " + status.StandardOutput);

                    ClientCommandResult configShow = await RunXenoClientAsync(new[] { "config", "show" }, clientHome).ConfigureAwait(false);
                    if (configShow.ExitCode != 0) throw new InvalidOperationException("Config show failed: " + configShow.StandardError + configShow.StandardOutput);
                    if (!configShow.StandardOutput.Contains("Local Dashboard", StringComparison.Ordinal) || !configShow.StandardOutput.Contains("http://127.0.0.1:" + dashboardPort.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)) throw new InvalidOperationException("Config show did not include local dashboard settings: " + configShow.StandardOutput);

                    ClientCommandResult logs = await RunXenoClientAsync(new[] { "logs", "--tail", "20" }, clientHome).ConfigureAwait(false);
                    if (logs.ExitCode != 0) throw new InvalidOperationException("Logs command failed: " + logs.StandardError + logs.StandardOutput);
                    if (!logs.StandardOutput.Contains("Attempting connection to server at ws://127.0.0.1:" + port, StringComparison.Ordinal)) throw new InvalidOperationException("Logs did not include connection attempt output: " + logs.StandardOutput);
                    if (!logs.StandardOutput.Contains("Client registered with Xeno server.", StringComparison.Ordinal)) throw new InvalidOperationException("Logs did not include registration output: " + logs.StandardOutput);
                    if (!logs.StandardOutput.Contains("Local dashboard listening at http://127.0.0.1:" + dashboardPort.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)) throw new InvalidOperationException("Logs did not include local dashboard startup output: " + logs.StandardOutput);

                    ClientCommandResult stop = await RunXenoClientAsync(new[] { "--json", "stop" }, clientHome).ConfigureAwait(false);
                    if (stop.ExitCode != 0) throw new InvalidOperationException("Stop command failed: " + stop.StandardError + stop.StandardOutput);
                    if (!stop.StandardOutput.Contains("\"stopped\": true", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Stop did not report success: " + stop.StandardOutput);
                    if (state.ProcessId != null && IsProcessRunningForTest(state.ProcessId.Value)) throw new InvalidOperationException("Background worker still running after stop.");
                }
                finally
                {
                    await RunXenoClientAsync(new[] { "stop", "--force" }, clientHome).ConfigureAwait(false);
                    await StopServerAsync(server, serverTask, serverTokenSource, dbFile).ConfigureAwait(false);
                    if (Directory.Exists(clientHome)) Directory.Delete(clientHome, true);
                }
            }
        }

        private static async Task RunRelaySmokeAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-" + Guid.NewGuid().ToString("N") + ".db");
            XenoSettings settings = new XenoSettings
            {
                Rest = new RestSettings
                {
                    Hostname = "127.0.0.1",
                    Port = port,
                    Ssl = false
                },
                Database = new DatabaseSettings
                {
                    Type = DatabaseTypeEnum.Sqlite,
                    Filename = dbFile
                }
            };
            settings.WebSocket.RequestTimeoutMs = 10000;

            using (CancellationTokenSource serverTokenSource = new CancellationTokenSource())
            using (CancellationTokenSource clientTokenSource = new CancellationTokenSource())
            {
                XenoServer server = new XenoServer(settings);
                Task serverTask = server.StartAsync(serverTokenSource.Token);

                try
                {
                    await WaitForHealthAsync(port).ConfigureAwait(false);

                    using (ClientWebSocket socket = new ClientWebSocket())
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
                                    new
                                    {
                                        name = "touchstone-runner",
                                        apiType = "OpenAICompatible",
                                        endpoint = "http://127.0.0.1:1",
                                        models = new[]
                                        {
                                            new { name = "llama3.2:latest", supportsCompletions = true, supportsEmbeddings = false }
                                        }
                                    }
                                }
                            }
                        }, clientTokenSource.Token).ConfigureAwait(false);

                        await WaitForRegisteredClientAsync(port).ConfigureAwait(false);

                        using (HttpClient httpClient = new HttpClient())
                        using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:" + port + "/v1/chat/completions"))
                        {
                            request.Content = new StringContent(
                                "{\"model\":\"llama3.2:latest\",\"messages\":[{\"role\":\"user\",\"content\":\"ping\"}]}",
                                Encoding.UTF8,
                                "application/json");

                            using (HttpResponseMessage response = await httpClient.SendAsync(request).ConfigureAwait(false))
                            {
                                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Relay returned " + (int)response.StatusCode + ": " + body);
                                if (!body.Contains("relay-ok", StringComparison.Ordinal)) throw new InvalidOperationException("Relay body mismatch: " + body);
                                if (HeaderValue(response, "X-Client-ID") != "xcl_touchstone") throw new InvalidOperationException("Relay response did not include selected client header.");
                                if (HeaderValue(response, "X-Xeno-Routing-Outcome") != "Completed") throw new InvalidOperationException("Relay response did not include completed routing outcome.");
                                if (HeaderValue(response, "X-Xeno-Model") != "llama3.2:latest") throw new InvalidOperationException("Relay response did not include effective model header.");
                                if (HeaderValue(response, "X-Xeno-Selected-API-Type") != "OpenAICompatible") throw new InvalidOperationException("Relay response did not include selected API type header.");
                                if (HeaderValue(response, "X-Xeno-Selected-Provider") != "OpenAICompatible") throw new InvalidOperationException("Relay response did not include backward-compatible selected provider header.");
                                if (HeaderValue(response, "X-Xeno-Selected-Source") != "touchstone-runner") throw new InvalidOperationException("Relay response did not include selected source header.");
                                if (HeaderValue(response, "X-Xeno-Routing-Candidate-Count") != "1") throw new InvalidOperationException("Relay response did not include candidate count header.");
                                if (HeaderValue(response, "X-Xeno-Load-Balancing-Mode") != "LeastRecentlyUsed") throw new InvalidOperationException("Relay response did not include load-balancing mode header.");
                                if (String.IsNullOrWhiteSpace(HeaderValue(response, "X-Xeno-Routing-Policy-ID"))) throw new InvalidOperationException("Relay response did not include routing policy ID header.");
                                if (HeaderValue(response, "X-Xeno-Routing-Policy-Match") != "*/*") throw new InvalidOperationException("Relay response did not include routing policy match header.");
                                if (String.IsNullOrWhiteSpace(HeaderValue(response, "X-Xeno-Selection-Reason"))) throw new InvalidOperationException("Relay response did not include selection reason header.");
                                if (String.IsNullOrWhiteSpace(HeaderValue(response, "X-Xeno-Request-History-ID"))) throw new InvalidOperationException("Relay response did not include request history trace header.");
                                if (HeaderLong(response, "X-Xeno-Client-Roundtrip-Ms") < 0) throw new InvalidOperationException("Relay response included invalid client roundtrip header.");
                                if (HeaderLong(response, "X-Xeno-Request-Duration-Ms") < 0) throw new InvalidOperationException("Relay response included invalid request duration header.");
                                string timeline = HeaderValue(response, "X-Xeno-Routing-Timeline");
                                if (!timeline.Contains("ClientSelection", StringComparison.Ordinal) || !timeline.Contains("ClientResponse", StringComparison.Ordinal)) throw new InvalidOperationException("Relay response did not include routing timeline header.");
                            }
                        }

                        clientTokenSource.Cancel();
                        try
                        {
                            await clientTask.ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                    }
                }
                finally
                {
                    serverTokenSource.Cancel();
                    server.Stop();
                    try
                    {
                        await serverTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                    }

                    server.Dispose();
                    await DeleteFileWithRetryAsync(dbFile).ConfigureAwait(false);
                }
            }
        }

        private static async Task RunWebSocketGateSmokeAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-ws-gate-" + Guid.NewGuid().ToString("N") + ".db");
            XenoSettings settings = new XenoSettings
            {
                Rest = new RestSettings
                {
                    Hostname = "127.0.0.1",
                    Port = port,
                    Ssl = false
                },
                Database = new DatabaseSettings
                {
                    Type = DatabaseTypeEnum.Sqlite,
                    Filename = dbFile
                }
            };

            using (CancellationTokenSource serverTokenSource = new CancellationTokenSource())
            using (CancellationTokenSource clientTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
            {
                XenoServer server = new XenoServer(settings);
                Task serverTask = server.StartAsync(serverTokenSource.Token);

                try
                {
                    await WaitForHealthAsync(port).ConfigureAwait(false);
                    using (ClientWebSocket socket = new ClientWebSocket())
                    {
                        await socket.ConnectAsync(new Uri("ws://127.0.0.1:" + port + settings.WebSocket.Path), clientTokenSource.Token).ConfigureAwait(false);
                        string? message = await ReceiveWebSocketTextAsync(socket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(message) || !message.Contains("server.unauthorized", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException("Unauthenticated WebSocket was not rejected: " + message);
                        }
                    }

                    using (ClientWebSocket bearerSocket = new ClientWebSocket())
                    {
                        bearerSocket.Options.SetRequestHeader("x-tenant-guid", Constants.DefaultTenantId);
                        bearerSocket.Options.SetRequestHeader("Authorization", "Bearer not-accepted-for-clients");
                        await bearerSocket.ConnectAsync(new Uri("ws://127.0.0.1:" + port + settings.WebSocket.Path), clientTokenSource.Token).ConfigureAwait(false);
                        string? message = await ReceiveWebSocketTextAsync(bearerSocket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(message) || !message.Contains("server.unauthorized", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException("Bearer-authenticated WebSocket was not rejected: " + message);
                        }
                    }

                }
                finally
                {
                    await StopServerAsync(server, serverTask, serverTokenSource, dbFile).ConfigureAwait(false);
                }
            }
        }

        private static async Task RunRelayStreamingSmokeAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-relay-stream-" + Guid.NewGuid().ToString("N") + ".db");
            XenoSettings settings = new XenoSettings
            {
                Rest = new RestSettings
                {
                    Hostname = "127.0.0.1",
                    Port = port,
                    Ssl = false
                },
                Database = new DatabaseSettings
                {
                    Type = DatabaseTypeEnum.Sqlite,
                    Filename = dbFile
                }
            };

            using (CancellationTokenSource serverTokenSource = new CancellationTokenSource())
            using (CancellationTokenSource clientTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                XenoServer server = new XenoServer(settings);
                Task serverTask = server.StartAsync(serverTokenSource.Token);

                try
                {
                    await WaitForHealthAsync(port).ConfigureAwait(false);

                    using (ClientWebSocket socket = new ClientWebSocket())
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
                                    new
                                    {
                                        name = "touchstone-runner",
                                        apiType = "OpenAICompatible",
                                        endpoint = "http://127.0.0.1:1",
                                        models = new[]
                                        {
                                            new { name = "llama3.2:latest", supportsCompletions = true, supportsEmbeddings = false }
                                        }
                                    }
                                }
                            }
                        }, clientTokenSource.Token).ConfigureAwait(false);

                        await WaitForRegisteredClientAsync(port).ConfigureAwait(false);

                        using (HttpClient httpClient = new HttpClient())
                        using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, "http://127.0.0.1:" + port + "/v1/chat/completions"))
                        {
                            request.Content = new StringContent(
                                "{\"model\":\"llama3.2:latest\",\"stream\":true,\"messages\":[{\"role\":\"user\",\"content\":\"ping\"}]}",
                                Encoding.UTF8,
                                "application/json");

                            using (HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false))
                            {
                                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Streaming relay returned " + (int)response.StatusCode + ": " + body);
                                if (!body.Contains("stream-", StringComparison.Ordinal) || !body.Contains("\"ok\"", StringComparison.Ordinal)) throw new InvalidOperationException("Streaming relay body mismatch: " + body);
                                if (!((response.Content.Headers.ContentType?.ToString() ?? String.Empty).Contains("text/event-stream", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Streaming relay did not preserve event-stream content type.");
                                if (HeaderValue(response, "X-Client-ID") != "xcl_touchstone") throw new InvalidOperationException("Streaming relay response did not include selected client header.");
                                if (HeaderValue(response, "X-Xeno-Routing-Outcome") != "Streaming") throw new InvalidOperationException("Streaming relay initial routing outcome header mismatch.");

                                string historyId = HeaderValue(response, "X-Xeno-Request-History-ID");
                                if (String.IsNullOrWhiteSpace(historyId)) throw new InvalidOperationException("Streaming relay response did not include request history trace header.");

                                using (HttpResponseMessage historyResponse = await httpClient.GetAsync("http://127.0.0.1:" + port + "/api/v1/requesthistory/" + Uri.EscapeDataString(historyId)).ConfigureAwait(false))
                                {
                                    string historyJson = await historyResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                                    if (!historyResponse.IsSuccessStatusCode) throw new InvalidOperationException("Streaming request history lookup failed: " + historyJson);
                                    RequestHistoryEntry root = DeserializeRequired<RequestHistoryEntry>(historyJson);
                                    if (!(root.RequestBody ?? String.Empty).Contains("\"stream\":true", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Streaming request body was not captured.");
                                    string responseBody = root.ResponseBody ?? String.Empty;
                                    if (!responseBody.Contains("stream-", StringComparison.OrdinalIgnoreCase) || !responseBody.Contains("\"ok\"", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Streaming response body was not captured.");
                                    if (root.RequestHeaders.Count == 0) throw new InvalidOperationException("Streaming request headers were not captured.");
                                    if (root.ResponseHeaders.Count == 0) throw new InvalidOperationException("Streaming response headers were not captured.");
                                    if (root.Telemetry == null) throw new InvalidOperationException("Streaming telemetry was not captured.");
                                    if (root.Telemetry.TotalTokens != 7) throw new InvalidOperationException("Streaming total tokens were not persisted.");
                                    if (root.Telemetry.StreamChunkCount != 2) throw new InvalidOperationException("Streaming chunk count was not persisted.");
                                    if (root.Timeline.Count < 3) throw new InvalidOperationException("Streaming timeline was not captured.");
                                }
                            }
                        }

                        clientTokenSource.Cancel();
                        try
                        {
                            await clientTask.ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
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

        private static async Task RunLoginCredentialConnectSmokeAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-login-ws-" + Guid.NewGuid().ToString("N") + ".db");
            XenoSettings settings = new XenoSettings
            {
                Rest = new RestSettings
                {
                    Hostname = "127.0.0.1",
                    Port = port,
                    Ssl = false
                },
                Database = new DatabaseSettings
                {
                    Type = DatabaseTypeEnum.Sqlite,
                    Filename = dbFile
                }
            };

            using (CancellationTokenSource serverTokenSource = new CancellationTokenSource())
            using (CancellationTokenSource clientTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(15)))
            {
                XenoServer server = new XenoServer(settings);
                Task serverTask = server.StartAsync(serverTokenSource.Token);

                try
                {
                    await WaitForHealthAsync(port).ConfigureAwait(false);
                    string loginBody = "{\"username\":\"admin@xeno\",\"password\":\"password\",\"tenantId\":\"" + Constants.DefaultTenantId + "\"}";
                    using (HttpClient httpClient = new HttpClient())
                    using (HttpResponseMessage loginResponse = await PostJsonAsync(httpClient, "http://127.0.0.1:" + port + "/v1.0/api/auth/login", loginBody).ConfigureAwait(false))
                    {
                        string responseBody = await loginResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (!loginResponse.IsSuccessStatusCode) throw new InvalidOperationException("Login failed: " + responseBody);

                        TestLoginCredentialResponse login = DeserializeRequired<TestLoginCredentialResponse>(responseBody);
                        using (ClientWebSocket socket = new ClientWebSocket())
                        {
                            string accessKey = login.AccessKey ?? String.Empty;
                            string secretKey = login.SecretKey ?? String.Empty;
                            if (String.IsNullOrWhiteSpace(accessKey) || String.IsNullOrWhiteSpace(secretKey)) throw new InvalidOperationException("Login did not return a credential.");

                            ApplyCredentialHeaders(socket, accessKey, secretKey);
                            await socket.ConnectAsync(new Uri("ws://127.0.0.1:" + port + settings.WebSocket.Path), clientTokenSource.Token).ConfigureAwait(false);
                            string? connected = await ReceiveWebSocketTextAsync(socket, clientTokenSource.Token).ConfigureAwait(false);
                            if (String.IsNullOrWhiteSpace(connected) || !connected.Contains("server.connected", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Authenticated WebSocket did not connect: " + connected);

                            await SendWebSocketJsonAsync(socket, new
                            {
                                type = "client.register",
                                clientId = "xcl_login_touchstone",
                                tenantId = Constants.DefaultTenantId,
                                name = "login-touchstone-client",
                                inventory = new
                                {
                                    runners = Array.Empty<object>()
                                }
                            }, clientTokenSource.Token).ConfigureAwait(false);
                            string? registered = await ReceiveWebSocketTextAsync(socket, clientTokenSource.Token).ConfigureAwait(false);
                            if (String.IsNullOrWhiteSpace(registered) || !registered.Contains("server.registered", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Authenticated client did not register: " + registered);
                        }
                    }
                }
                finally
                {
                    await StopServerAsync(server, serverTask, serverTokenSource, dbFile).ConfigureAwait(false);
                }
            }
        }

        private static async Task FakeClientLoopAsync(ClientWebSocket socket, CancellationToken token)
        {
            while (!token.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                string? message = await ReceiveWebSocketTextAsync(socket, token).ConfigureAwait(false);
                if (String.IsNullOrWhiteSpace(message)) continue;
                if (!message.Contains("\"server.request\"", StringComparison.OrdinalIgnoreCase)) continue;

                ServerRequestMessage? serverRequest = JsonUtil.Deserialize<ServerRequestMessage>(message);
                if (serverRequest == null) throw new InvalidOperationException("Server request could not be deserialized.");
                string requestId = String.IsNullOrWhiteSpace(serverRequest.RequestId) ? Guid.NewGuid().ToString("N") : serverRequest.RequestId;
                string requestorIp = serverRequest.RequestorIp ?? String.Empty;
                if (String.IsNullOrWhiteSpace(requestorIp)) throw new InvalidOperationException("Server request did not include requestorIp.");
                Dictionary<string, string> headers = serverRequest.Headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string forwardedFor = headers.TryGetValue("X-Forwarded-For", out string? forwardedForValue) ? forwardedForValue : String.Empty;
                if (!forwardedFor.Contains(requestorIp, StringComparison.Ordinal)) throw new InvalidOperationException("X-Forwarded-For did not include requestor IP.");
                string realIp = headers.TryGetValue("X-Real-IP", out string? realIpValue) ? realIpValue : String.Empty;
                string xenoRequestorIp = headers.TryGetValue("X-Xeno-Requestor-IP", out string? requestorIpValue) ? requestorIpValue : String.Empty;
                string xenoRequestId = headers.TryGetValue("X-Xeno-Request-ID", out string? requestIdValue) ? requestIdValue : String.Empty;
                if (realIp != requestorIp) throw new InvalidOperationException("X-Real-IP did not match requestor IP.");
                if (xenoRequestorIp != requestorIp) throw new InvalidOperationException("X-Xeno-Requestor-IP did not match requestor IP.");
                if (xenoRequestId != requestId) throw new InvalidOperationException("Forwarded request ID did not match server request ID.");
                if (serverRequest.Stream)
                    {
                        string first = "data: {\"choices\":[{\"delta\":{\"content\":\"stream-\"}}]}\n\n";
                        string second = "data: {\"choices\":[{\"delta\":{\"content\":\"ok\"}}],\"usage\":{\"prompt_tokens\":3,\"completion_tokens\":4,\"total_tokens\":7}}\n\ndata: [DONE]\n\n";
                        string fullBody = first + second;
                        await SendWebSocketJsonAsync(socket, new
                        {
                            type = "client.response.start",
                            requestId,
                            clientId = "xcl_touchstone",
                            tenantId = Constants.DefaultTenantId,
                            success = true,
                            statusCode = 200,
                            contentType = "text/event-stream",
                            headers = new Dictionary<string, string>
                            {
                                ["Content-Type"] = "text/event-stream",
                                ["x-request-id"] = "provider_touchstone"
                            },
                            telemetry = new
                            {
                                requestToHeadersMs = 4,
                                transferMode = "stream"
                            }
                        }, token).ConfigureAwait(false);
                        await SendWebSocketJsonAsync(socket, new
                        {
                            type = "client.response.chunk",
                            requestId,
                            clientId = "xcl_touchstone",
                            tenantId = Constants.DefaultTenantId,
                            sequence = 1,
                            encoding = "base64",
                            byteCount = Encoding.UTF8.GetByteCount(first),
                            contentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(first))
                        }, token).ConfigureAwait(false);
                        await Task.Delay(25, token).ConfigureAwait(false);
                        await SendWebSocketJsonAsync(socket, new
                        {
                            type = "client.response.chunk",
                            requestId,
                            clientId = "xcl_touchstone",
                            tenantId = Constants.DefaultTenantId,
                            sequence = 2,
                            encoding = "base64",
                            byteCount = Encoding.UTF8.GetByteCount(second),
                            contentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(second))
                        }, token).ConfigureAwait(false);
                        await SendWebSocketJsonAsync(socket, new
                        {
                            type = "client.response.complete",
                            requestId,
                            clientId = "xcl_touchstone",
                            tenantId = Constants.DefaultTenantId,
                            success = true,
                            statusCode = 200,
                            contentType = "text/event-stream",
                            headers = new Dictionary<string, string>
                            {
                                ["Content-Type"] = "text/event-stream",
                                ["x-request-id"] = "provider_touchstone"
                            },
                            telemetry = new
                            {
                                totalTimeMs = 42,
                                upstreamTotalMs = 42,
                                requestToHeadersMs = 4,
                                timeToFirstTokenMs = 8,
                                totalStreamingTimeMs = 34,
                                promptTokens = 3,
                                completionTokens = 4,
                                totalTokens = 7,
                                streamChunkCount = 2,
                                streamEventCount = 3,
                                requestBytes = 82,
                                responseBytes = Encoding.UTF8.GetByteCount(fullBody),
                                transferMode = "stream",
                                providerRequestId = "provider_touchstone"
                            },
                            body = fullBody
                        }, token).ConfigureAwait(false);
                        continue;
                    }

                    string completion = "{\"id\":\"chatcmpl_touchstone\",\"object\":\"chat.completion\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"relay-ok\"},\"finish_reason\":\"stop\"}]}";
                    await SendWebSocketJsonAsync(socket, new
                    {
                        type = "client.response",
                        requestId,
                        clientId = "xcl_touchstone",
                        tenantId = Constants.DefaultTenantId,
                        success = true,
                        statusCode = 200,
                        contentType = "application/json",
                        body = completion
                    }, token).ConfigureAwait(false);
            }
        }

        private static async Task WaitForHealthAsync(int port)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                for (int i = 0; i < 50; i++)
                {
                    try
                    {
                        using (HttpResponseMessage response = await httpClient.GetAsync("http://127.0.0.1:" + port + "/health").ConfigureAwait(false))
                        {
                            if (response.IsSuccessStatusCode) return;
                        }
                    }
                    catch (HttpRequestException)
                    {
                    }

                    await Task.Delay(100).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException("Server health endpoint did not become ready.");
        }

        private static async Task WaitForRegisteredClientAsync(int port)
        {
            await WaitForRegisteredClientAsync(port, "xcl_touchstone").ConfigureAwait(false);
        }

        private static async Task WaitForRegisteredClientAsync(int port, string expectedText)
        {
            using (HttpClient httpClient = new HttpClient())
            {
                for (int i = 0; i < 50; i++)
                {
                    using (HttpResponseMessage response = await httpClient.GetAsync("http://127.0.0.1:" + port + "/v1.0/api/clients").ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (response.IsSuccessStatusCode && body.Contains(expectedText, StringComparison.Ordinal)) return;
                    }

                    await Task.Delay(100).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException("Client did not register: " + expectedText);
        }

        private static bool IsProcessRunningForTest(int processId)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    return !process.HasExited;
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static async Task SendWebSocketJsonAsync(ClientWebSocket socket, object value, CancellationToken token)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(JsonUtil.Serialize(value));
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
        }

        private static void ApplySeedCredentialHeaders(ClientWebSocket socket)
        {
            ApplyCredentialHeaders(socket, "default", "default");
        }

        private static void ApplyCredentialHeaders(ClientWebSocket socket, string accessKey, string secretKey)
        {
            socket.Options.SetRequestHeader("x-tenant-guid", Constants.DefaultTenantId);
            socket.Options.SetRequestHeader("x-access-key", accessKey);
            socket.Options.SetRequestHeader("x-secret-key", secretKey);
        }

        private static async Task<HttpResponseMessage> PostJsonAsync(HttpClient httpClient, string url, string json)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                return await httpClient.SendAsync(request).ConfigureAwait(false);
            }
        }

        private static async Task<HttpResponseMessage> PutJsonAsync(HttpClient httpClient, string url, string json)
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Put, url))
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                return await httpClient.SendAsync(request).ConfigureAwait(false);
            }
        }

        private static async Task<HttpResponseMessage> DeleteJsonAsync(HttpClient httpClient, string url, string json = "{}")
        {
            using (HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Delete, url))
            {
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                return await httpClient.SendAsync(request).ConfigureAwait(false);
            }
        }

        private static string HeaderValue(HttpResponseMessage response, string name)
        {
            if (!response.Headers.TryGetValues(name, out IEnumerable<string>? values)) return String.Empty;
            return values.FirstOrDefault() ?? String.Empty;
        }

        private static long HeaderLong(HttpResponseMessage response, string name)
        {
            string value = HeaderValue(response, name);
            return Int64.TryParse(value, out long parsed) ? parsed : -1;
        }

        private static async Task<string> FirstRecordIdAsync(HttpClient httpClient, string url)
        {
            string json = await httpClient.GetStringAsync(url).ConfigureAwait(false);
            return FirstRecordId(json);
        }

        private static string FirstRecordId(string json)
        {
            List<TestIdRecord> records = DeserializeRequired<List<TestIdRecord>>(json);
            TestIdRecord? record = records.FirstOrDefault();
            string id = record?.Id ?? String.Empty;
            if (String.IsNullOrWhiteSpace(id)) throw new InvalidOperationException("Response did not include a record ID.");
            return id;
        }

        private static string ExtractLocalDashboardToken(string html)
        {
            string marker = "window.__XENO_LOCAL_TOKEN__='";
            int start = html.IndexOf(marker, StringComparison.Ordinal);
            if (start < 0) throw new InvalidOperationException("Local dashboard token bootstrap marker was not found.");
            start += marker.Length;
            int end = html.IndexOf("'", start, StringComparison.Ordinal);
            if (end <= start) throw new InvalidOperationException("Local dashboard token bootstrap value was not found.");
            return html.Substring(start, end - start);
        }

        private static string ExtractHtmlAttribute(string html, string prefix, string suffix)
        {
            int start = html.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (start < 0) throw new InvalidOperationException("Expected HTML attribute prefix was not found: " + prefix);
            start += prefix.Length;
            int end = html.IndexOf(suffix, start, StringComparison.OrdinalIgnoreCase);
            if (end < start) throw new InvalidOperationException("Expected HTML attribute suffix was not found: " + suffix);
            return html.Substring(start, end - start);
        }

        private static string ExtractHtmlAttributeEnding(string html, string prefix, string suffix, string ending)
        {
            int searchStart = 0;
            while (searchStart < html.Length)
            {
                int start = html.IndexOf(prefix, searchStart, StringComparison.OrdinalIgnoreCase);
                if (start < 0) break;
                start += prefix.Length;
                int end = html.IndexOf(suffix, start, StringComparison.OrdinalIgnoreCase);
                if (end < start) break;
                string value = html.Substring(start, end - start);
                if (value.EndsWith(ending, StringComparison.OrdinalIgnoreCase)) return value;
                searchStart = end + suffix.Length;
            }

            throw new InvalidOperationException("Expected HTML attribute ending was not found: " + ending);
        }

        private static bool OpenApiHasMethod(string json, string path, string method)
        {
            TestOpenApiDocument document = DeserializeRequired<TestOpenApiDocument>(json);
            if (!document.Paths.TryGetValue(path, out Dictionary<string, TestOpenApiOperation>? pathItem)) return false;
            return pathItem.ContainsKey(method);
        }

        private static bool OpenApiHasSecurityScheme(string json, string schemeName, string headerName)
        {
            TestOpenApiDocument document = DeserializeRequired<TestOpenApiDocument>(json);
            if (!document.Components.SecuritySchemes.TryGetValue(schemeName, out TestOpenApiSecurityScheme? scheme)) return false;
            string actualHeader = scheme.Name ?? String.Empty;
            return String.Equals(actualHeader, headerName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool OpenApiHasParameter(string json, string path, string method, string name, string location)
        {
            TestOpenApiOperation? operation = GetOpenApiOperation(json, path, method);
            if (operation == null) return false;
            foreach (TestOpenApiParameter parameter in operation.Parameters)
            {
                string actualName = parameter.Name ?? String.Empty;
                string actualLocation = parameter.In ?? String.Empty;
                if (String.Equals(actualName, name, StringComparison.OrdinalIgnoreCase) && String.Equals(actualLocation, location, StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }

        private static bool OpenApiHasRequestBody(string json, string path, string method)
        {
            TestOpenApiOperation? operation = GetOpenApiOperation(json, path, method);
            if (operation?.RequestBody == null) return false;
            if (!operation.RequestBody.Content.TryGetValue("application/json", out TestOpenApiMediaType? mediaType)) return false;
            return mediaType.Schema != null;
        }

        private static TestOpenApiOperation? GetOpenApiOperation(string json, string path, string method)
        {
            TestOpenApiDocument document = DeserializeRequired<TestOpenApiDocument>(json);
            if (!document.Paths.TryGetValue(path, out Dictionary<string, TestOpenApiOperation>? pathItem)) return null;
            if (!pathItem.TryGetValue(method, out TestOpenApiOperation? operation)) return null;
            return operation;
        }

        private static async Task<string?> ReceiveWebSocketTextAsync(ClientWebSocket socket, CancellationToken token)
        {
            byte[] buffer = new byte[8192];
            using (MemoryStream stream = new MemoryStream())
            {
                while (true)
                {
                    WebSocketReceiveResult result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close) return null;
                    stream.Write(buffer, 0, result.Count);
                    if (result.EndOfMessage) break;
                }

                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static async Task StopServerAsync(XenoServer server, Task serverTask, CancellationTokenSource serverTokenSource, string dbFile)
        {
            serverTokenSource.Cancel();
            server.Stop();
            try
            {
                await serverTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }

            server.Dispose();
            await DeleteFileWithRetryAsync(dbFile).ConfigureAwait(false);
        }

        private static void AssertFactoryDefaultClientSettings(string settingsFile, string stateFile)
        {
            if (!File.Exists(settingsFile)) throw new InvalidOperationException("Reset did not recreate client settings.");
            if (File.Exists(stateFile)) throw new InvalidOperationException("Reset did not remove runtime state.");

            ClientSettings settings = DeserializeRequired<ClientSettings>(File.ReadAllText(settingsFile));
            if (!String.Equals(settings.TenantId, Constants.DefaultTenantId, StringComparison.Ordinal)) throw new InvalidOperationException("Reset did not restore the default tenant.");
            if (settings.Providers.Count == 0)
            {
                throw new InvalidOperationException("Reset did not restore the default provider endpoint.");
            }
        }

        private static async Task<ClientCommandResult> RunXenoClientAsync(string[] args, string clientHome, string? standardInput = null)
        {
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardError = true,
                RedirectStandardInput = standardInput != null,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(typeof(Xeno.Client.Program).Assembly.Location);
            foreach (string arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            startInfo.Environment["XENO_CLIENT_HOME"] = clientHome;
            startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";

            using (Process process = new Process { StartInfo = startInfo })
            {
                if (!process.Start()) throw new InvalidOperationException("Unable to start Xeno client process.");

                if (standardInput != null)
                {
                    await process.StandardInput.WriteAsync(standardInput).ConfigureAwait(false);
                    process.StandardInput.Close();
                }

                string standardOutput = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                string standardError = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);
                await process.WaitForExitAsync().ConfigureAwait(false);
                return new ClientCommandResult
                {
                    ExitCode = process.ExitCode,
                    StandardOutput = standardOutput,
                    StandardError = standardError
                };
            }
        }

        private static int GetFreePort()
        {
            TcpListener listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
            try
            {
                listener.Start();
                return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        private static async Task DeleteFileWithRetryAsync(string filename)
        {
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    if (File.Exists(filename)) File.Delete(filename);
                    return;
                }
                catch (IOException)
                {
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }
        }

        private static T DeserializeRequired<T>(string json) where T : class
        {
            T? value = JsonUtil.Deserialize<T>(json);
            if (value == null) throw new InvalidOperationException("JSON payload could not be deserialized as " + typeof(T).Name + ".");
            return value;
        }
    }
}

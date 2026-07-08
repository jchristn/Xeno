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
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Touchstone.Core;
    using Xeno.Client;
    using Xeno.Client.LocalDashboard;
    using Xeno.Client.LocalDashboard.Database;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Telemetry.Models;
    using Xeno.Client.Telemetry.Services;
    using Xeno.Core;
    using Xeno.Core.Database;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;
    using Xeno.Core.Services;
    using Xeno.Core.Settings;
    using Xeno.Server;
    using Xeno.Server.Services;
    using Xeno.Tray;

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
                    TelemetrySuite(),
                    RoutingSuite(),
                    TenantIsolationSuite(),
                    ClientCliSuite(),
                    TraySuite(),
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
                        caseId: "DefaultCorsHeadersExcludeLegacySecretKey",
                        displayName: "Default CORS headers exclude legacy secret-key authentication",
                        executeAsync: delegate
                        {
                            XenoSettings settings = new XenoSettings();
                            if (!settings.Cors.AllowedHeaders.Any(item => String.Equals(item, "x-access-key", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Default CORS headers do not allow x-access-key.");
                            if (!settings.Cors.AllowedHeaders.Any(item => String.Equals(item, "x-token", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Default CORS headers do not allow x-token.");
                            if (settings.Cors.AllowedHeaders.Any(item => String.Equals(item, "x-secret-key", StringComparison.OrdinalIgnoreCase))) throw new InvalidOperationException("Default CORS headers still allow x-secret-key.");
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
                        }),
                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "ClientProviderTelemetrySettingsClampDefaults",
                        displayName: "Client provider telemetry settings clamp defaults",
                        executeAsync: delegate
                        {
                            ClientSettings settings = new ClientSettings();
                            if (!settings.Telemetry.ProviderTelemetry.Enabled) throw new InvalidOperationException("Provider telemetry should be enabled by default.");
                            if (settings.Telemetry.ProviderTelemetry.CollectionIntervalMs != 15000) throw new InvalidOperationException("Unexpected provider telemetry interval.");
                            if (settings.Telemetry.ProviderTelemetry.RequestTimeoutMs != 5000) throw new InvalidOperationException("Unexpected provider telemetry timeout.");
                            if (settings.Telemetry.ProviderTelemetry.ConcurrentProviderLimit != 4) throw new InvalidOperationException("Unexpected provider telemetry concurrency.");
                            settings.Telemetry.ProviderTelemetry.CollectionIntervalMs = 1;
                            settings.Telemetry.ProviderTelemetry.RequestTimeoutMs = 1;
                            settings.Telemetry.ProviderTelemetry.ConcurrentProviderLimit = 0;
                            settings.Telemetry.ProviderTelemetry.RawMetricsMaxBytes = 99999999;
                            if (settings.Telemetry.ProviderTelemetry.CollectionIntervalMs != 5000) throw new InvalidOperationException("Provider telemetry interval was not clamped.");
                            if (settings.Telemetry.ProviderTelemetry.RequestTimeoutMs != 500) throw new InvalidOperationException("Provider telemetry timeout was not clamped.");
                            if (settings.Telemetry.ProviderTelemetry.ConcurrentProviderLimit != 1) throw new InvalidOperationException("Provider telemetry concurrency was not clamped.");
                            if (settings.Telemetry.ProviderTelemetry.RawMetricsMaxBytes != 1048576) throw new InvalidOperationException("Provider telemetry raw metrics max was not clamped.");
                            string providerTelemetryId = IdGenerator.NewId("ptel_");
                            if (providerTelemetryId.Length != IdGenerator.MaxIdLength || !providerTelemetryId.StartsWith("ptel_", StringComparison.Ordinal)) throw new InvalidOperationException("Provider telemetry ID shape mismatch.");
                            return Task.CompletedTask;
                        }),
                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "ProviderTelemetryPrometheusParserParsesLabels",
                        displayName: "Provider telemetry Prometheus parser parses labels",
                        executeAsync: delegate
                        {
                            PrometheusMetricsParser parser = new PrometheusMetricsParser();
                            PrometheusMetricDocument document = parser.Parse("vllm:num_requests_running{model_name=\"llama\",engine=\"0\"} 2\nvllm:gpu_cache_usage_perc 42.5", CancellationToken.None);
                            if (document.Samples.Count != 2) throw new InvalidOperationException("Prometheus parser sample count mismatch.");
                            PrometheusMetricSample sample = document.Samples[0];
                            if (sample.Name != "vllm:num_requests_running") throw new InvalidOperationException("Prometheus parser metric name mismatch.");
                            if (sample.Value != 2) throw new InvalidOperationException("Prometheus parser metric value mismatch.");
                            if (!sample.Labels.Any(item => item.Name == "model_name" && item.Value == "llama")) throw new InvalidOperationException("Prometheus parser label mismatch.");
                            return Task.CompletedTask;
                        }),
                    new TestCaseDescriptor(
                        suiteId: "Settings",
                        caseId: "ProviderTelemetrySqlitePersistsAndRollsUp",
                        displayName: "Provider telemetry SQLite persists and rolls up",
                        executeAsync: async delegate
                        {
                            string directory = Path.Combine(Path.GetTempPath(), "xeno-provider-telemetry-" + Guid.NewGuid().ToString("N"));
                            Directory.CreateDirectory(directory);
                            ClientLocalDatabaseSettings databaseSettings = new ClientLocalDatabaseSettings { Filename = Path.Combine(directory, "local.db") };
                            SqliteClientProviderTelemetryHistoryMethods history = new SqliteClientProviderTelemetryHistoryMethods(databaseSettings);
                            await history.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
                            ClientProviderTelemetrySnapshot snapshot = new ClientProviderTelemetrySnapshot
                            {
                                Metadata = new ClientProviderTelemetryMetadata
                                {
                                    Id = IdGenerator.NewId("ptel_"),
                                    ClientId = "xcl_test",
                                    MachineName = "test",
                                    CollectedUtc = DateTime.UtcNow,
                                    ProviderCount = 1
                                }
                            };
                            snapshot.Providers.Add(new ClientProviderTelemetryProviderStatus
                            {
                                Endpoint = new ClientProviderTelemetryEndpoint
                                {
                                    ProviderName = "ollama",
                                    ApiType = "Ollama",
                                    Kind = ClientProviderTelemetryKindEnum.Ollama,
                                    EndpointDisplay = "http://127.0.0.1:11434",
                                    EndpointHash = "hash"
                                },
                                Status = ClientProviderTelemetryStatusEnum.Available,
                                HealthStatus = ClientProviderTelemetryHealthStatusEnum.Healthy,
                                AvailableModelCount = 1,
                                LoadedModelCount = 1,
                                ActiveRequests = 2,
                                QueuedRequests = 1
                            });
                            await history.CreateAsync(snapshot, new ClientTelemetrySettings(), CancellationToken.None).ConfigureAwait(false);
                            ClientProviderTelemetrySampleDetail? latest = await history.ReadLatestAsync("ollama", CancellationToken.None).ConfigureAwait(false);
                            if (latest == null) throw new InvalidOperationException("Latest provider telemetry was not persisted.");
                            if (latest.Sample.ProviderName != "ollama") throw new InvalidOperationException("Latest provider telemetry provider mismatch.");
                            ClientProviderTelemetryRollupRequest rollupRequest = new ClientProviderTelemetryRollupRequest { FromUtc = DateTime.UtcNow.AddHours(-1), ToUtc = DateTime.UtcNow.AddHours(1), BucketMinutes = 60, Metric = ClientProviderTelemetryMetricEnum.ActiveRequests };
                            IReadOnlyList<ClientProviderTelemetryRollupBucket> buckets = await history.RollupAsync(rollupRequest, CancellationToken.None).ConfigureAwait(false);
                            if (!buckets.Any(item => item.Count > 0 && item.Average == 2)) throw new InvalidOperationException("Provider telemetry rollup did not include persisted value.");
                            Directory.Delete(directory, true);
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
                        "ClientProviderUsageTelemetryPreparedAndParsed",
                        "Client provider usage telemetry is requested and parsed",
                        executeAsync: delegate
                        {
                            MethodInfo? prepare = typeof(Xeno.Client.Program).GetMethod("PrepareUpstreamRequestBody", BindingFlags.NonPublic | BindingFlags.Static);
                            if (prepare == null) throw new InvalidOperationException("PrepareUpstreamRequestBody was not found.");

                            object? preparedBodyObject = prepare.Invoke(null, new object[] { "OpenAICompatible", "/v1/chat/completions", "{\"model\":\"llama3.2:latest\",\"stream\":true,\"messages\":[]}", true });
                            string preparedBody = preparedBodyObject?.ToString() ?? String.Empty;
                            if (!preparedBody.Contains("\"stream_options\"", StringComparison.Ordinal)) throw new InvalidOperationException("Prepared streaming request did not include stream_options: " + preparedBody);
                            if (!preparedBody.Contains("\"include_usage\":true", StringComparison.Ordinal)) throw new InvalidOperationException("Prepared streaming request did not request usage telemetry: " + preparedBody);

                            object? overriddenBodyObject = prepare.Invoke(null, new object[] { "Ollama", "/v1/chat/completions", "{\"model\":\"llama3.2:latest\",\"stream\":true,\"stream_options\":{\"include_usage\":false}}", true });
                            string overriddenBody = overriddenBodyObject?.ToString() ?? String.Empty;
                            if (!overriddenBody.Contains("\"include_usage\":true", StringComparison.Ordinal)) throw new InvalidOperationException("Prepared Ollama-compatible streaming request did not force usage telemetry: " + overriddenBody);

                            object? embeddingBodyObject = prepare.Invoke(null, new object[] { "OpenAI", "/v1/embeddings", "{\"model\":\"embed\"}", true });
                            string embeddingBody = embeddingBodyObject?.ToString() ?? String.Empty;
                            if (embeddingBody.Contains("stream_options", StringComparison.Ordinal)) throw new InvalidOperationException("Prepared embedding request should not include stream_options: " + embeddingBody);

                            MethodInfo? buildTelemetry = typeof(Xeno.Client.Program).GetMethod("BuildTelemetry", BindingFlags.NonPublic | BindingFlags.Static);
                            if (buildTelemetry == null) throw new InvalidOperationException("BuildTelemetry was not found.");

                            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                            object? telemetryObject = buildTelemetry.Invoke(null, new object?[]
                            {
                                "{}",
                                "{\"model\":\"llama3.2:latest\",\"done\":true,\"prompt_eval_count\":5,\"eval_count\":9}",
                                headers,
                                12.0,
                                null,
                                null,
                                0,
                                0,
                                false
                            });

                            RequestHistoryTelemetry? telemetry = telemetryObject as RequestHistoryTelemetry;
                            if (telemetry == null) throw new InvalidOperationException("BuildTelemetry did not return request telemetry.");
                            if (telemetry.PromptTokens != 5) throw new InvalidOperationException("Ollama prompt token telemetry was not parsed.");
                            if (telemetry.CompletionTokens != 9) throw new InvalidOperationException("Ollama completion token telemetry was not parsed.");
                            if (telemetry.TotalTokens != 14) throw new InvalidOperationException("Ollama total token telemetry was not computed.");

                            return Task.CompletedTask;
                        }),
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
                                    ["Cookie"] = "session=secret",
                                    ["X-Api-Key"] = "provider-secret",
                                    ["x-access-key"] = "tenant-access-key",
                                    ["X-Provider-Credential"] = "provider-credential",
                                    ["X-Test"] = "visible"
                                };
                                Dictionary<string, string> redacted = LocalSecretRedactor.RedactHeaders(headers);
                                if (!String.Equals(redacted["Authorization"], "redacted", StringComparison.Ordinal)) throw new InvalidOperationException("Authorization header was not redacted.");
                                if (!String.Equals(redacted["Cookie"], "redacted", StringComparison.Ordinal)) throw new InvalidOperationException("Cookie header was not redacted.");
                                if (!String.Equals(redacted["X-Api-Key"], "redacted", StringComparison.Ordinal)) throw new InvalidOperationException("API key header was not redacted.");
                                if (!String.Equals(redacted["x-access-key"], "redacted", StringComparison.Ordinal)) throw new InvalidOperationException("Access key header was not redacted.");
                                if (!String.Equals(redacted["X-Provider-Credential"], "redacted", StringComparison.Ordinal)) throw new InvalidOperationException("Credential header was not redacted.");
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
                                    RequestorTenantId = Constants.DefaultTenantId,
                                    RequestorPrincipalType = "User",
                                    RequestorPrincipalId = "usr_local_requestor",
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
                                if (!String.Equals(loaded.RequestorTenantId, Constants.DefaultTenantId, StringComparison.Ordinal)) throw new InvalidOperationException("Requestor tenant ID was not persisted.");
                                if (!String.Equals(loaded.RequestorPrincipalType, "User", StringComparison.Ordinal)) throw new InvalidOperationException("Requestor principal type was not persisted.");
                                if (!String.Equals(loaded.RequestorPrincipalId, "usr_local_requestor", StringComparison.Ordinal)) throw new InvalidOperationException("Requestor principal ID was not persisted.");
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
                                IClientProviderTelemetryHistoryMethods providerTelemetryHistory = driver.ProviderTelemetryHistory;
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
                                using (LocalDashboardService service = new LocalDashboardService(settings, history, telemetryHistory, null, providerTelemetryHistory, null))
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

                                    ClientProviderTelemetrySnapshot providerTelemetrySnapshot = new ClientProviderTelemetrySnapshot
                                    {
                                        Metadata = new ClientProviderTelemetryMetadata
                                        {
                                            Id = IdGenerator.NewId("ptel_"),
                                            ClientId = settings.ClientId,
                                            MachineName = settings.Name,
                                            CollectedUtc = DateTime.UtcNow,
                                            ProviderCount = 1
                                        }
                                    };
                                    providerTelemetrySnapshot.Providers.Add(new ClientProviderTelemetryProviderStatus
                                    {
                                        Endpoint = new ClientProviderTelemetryEndpoint
                                        {
                                            ProviderName = "touchstone-provider-telemetry",
                                            ApiType = "Ollama",
                                            Kind = ClientProviderTelemetryKindEnum.Ollama,
                                            EndpointDisplay = "http://127.0.0.1:11434",
                                            EndpointHash = "touchstonehash"
                                        },
                                        Status = ClientProviderTelemetryStatusEnum.Available,
                                        HealthStatus = ClientProviderTelemetryHealthStatusEnum.Healthy,
                                        Version = "test",
                                        AvailableModelCount = 1,
                                        LoadedModelCount = 1,
                                        ActiveRequests = 3,
                                        QueuedRequests = 1
                                    });
                                    await providerTelemetryHistory.CreateAsync(providerTelemetrySnapshot, settings.Telemetry, CancellationToken.None).ConfigureAwait(false);

                                    HttpResponseMessage providerTelemetryStatusResponse = await httpClient.GetAsync(baseUrl + "/api/v1/telemetry/providers/status").ConfigureAwait(false);
                                    if (!providerTelemetryStatusResponse.IsSuccessStatusCode) throw new InvalidOperationException("Provider telemetry status route failed.");
                                    ClientTelemetryPersistenceStatus providerTelemetryStatus = DeserializeRequired<ClientTelemetryPersistenceStatus>(await providerTelemetryStatusResponse.Content.ReadAsStringAsync().ConfigureAwait(false));
                                    if (providerTelemetryStatus.Samples < 1) throw new InvalidOperationException("Provider telemetry status did not report samples.");

                                    ClientProviderTelemetryLatestResponse providerTelemetryLatest = DeserializeRequired<ClientProviderTelemetryLatestResponse>(await httpClient.GetStringAsync(baseUrl + "/api/v1/telemetry/providers/latest").ConfigureAwait(false));
                                    if (providerTelemetryLatest.Detail == null || !String.Equals(providerTelemetryLatest.Detail.Sample.ProviderName, "touchstone-provider-telemetry", StringComparison.Ordinal)) throw new InvalidOperationException("Provider telemetry latest route did not return the persisted sample.");

                                    HttpResponseMessage providerTelemetrySearchResponse = await PostJsonAsync(httpClient, baseUrl + "/api/v1/telemetry/providers/history/search", JsonUtil.Serialize(new ClientProviderTelemetrySearchRequest { ProviderName = "touchstone-provider-telemetry", Metric = ClientProviderTelemetryMetricEnum.ActiveRequests })).ConfigureAwait(false);
                                    if (!providerTelemetrySearchResponse.IsSuccessStatusCode) throw new InvalidOperationException("Provider telemetry search route failed.");
                                    ClientProviderTelemetrySearchResult providerTelemetrySearch = DeserializeRequired<ClientProviderTelemetrySearchResult>(await providerTelemetrySearchResponse.Content.ReadAsStringAsync().ConfigureAwait(false));
                                    if (providerTelemetrySearch.Samples.Count < 1) throw new InvalidOperationException("Provider telemetry search route did not return samples.");

                                    HttpResponseMessage providerTelemetryRollupResponse = await PostJsonAsync(httpClient, baseUrl + "/api/v1/telemetry/providers/history/rollups", JsonUtil.Serialize(new ClientProviderTelemetryRollupRequest { Metric = ClientProviderTelemetryMetricEnum.ActiveRequests, BucketMinutes = 60 })).ConfigureAwait(false);
                                    if (!providerTelemetryRollupResponse.IsSuccessStatusCode) throw new InvalidOperationException("Provider telemetry rollup route failed.");

                                    ClientProviderTelemetrySampleDetail providerTelemetryDetail = DeserializeRequired<ClientProviderTelemetrySampleDetail>(await httpClient.GetStringAsync(baseUrl + "/api/v1/telemetry/providers/history/" + providerTelemetryLatest.Detail.Sample.Id).ConfigureAwait(false));
                                    if (providerTelemetryDetail.Sample.ActiveRequests != 3) throw new InvalidOperationException("Provider telemetry detail route returned the wrong metric value.");

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
        /// Tray companion suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor TraySuite()
        {
            return new TestSuiteDescriptor(
                suiteId: "Tray",
                displayName: "Tray",
                cases: new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        suiteId: "Tray",
                        caseId: "TrayCommandBuilderUsesJsonStartAndStop",
                        displayName: "Tray command builder uses JSON start and stop commands",
                        executeAsync: delegate
                        {
                            IReadOnlyList<string> start = XenoTrayCommandBuilder.BuildStartArguments();
                            IReadOnlyList<string> stop = XenoTrayCommandBuilder.BuildStopArguments();
                            if (start.Count != 2 || start[0] != "--json" || start[1] != "start") throw new InvalidOperationException("Start command arguments were not stable.");
                            if (stop.Count != 2 || stop[0] != "--json" || stop[1] != "stop") throw new InvalidOperationException("Stop command arguments were not stable.");
                            return Task.CompletedTask;
                        }),
                    new TestCaseDescriptor(
                        suiteId: "Tray",
                        caseId: "ConnectedTrayStatusDisablesStartAndEnablesOperations",
                        displayName: "Connected tray status disables start and enables operations",
                        executeAsync: delegate
                        {
                            XenoTrayStatus status = new XenoTrayStatus
                            {
                                Running = true,
                                Connected = true,
                                Registered = true,
                                LocalDashboardListening = true,
                                LocalDashboardUrl = "http://127.0.0.1:9343"
                            };
                            XenoTrayMenuState menu = XenoTrayStatusFormatter.BuildMenuState(status);
                            if (menu.StatusHeader != "Status: Connected") throw new InvalidOperationException("Connected status header mismatch.");
                            if (menu.StartEnabled) throw new InvalidOperationException("Start should be disabled for a running client.");
                            if (!menu.OpenDashboardEnabled) throw new InvalidOperationException("Dashboard should be enabled when listening.");
                            if (!menu.ExitClientEnabled) throw new InvalidOperationException("Exit client should be enabled when running.");
                            return Task.CompletedTask;
                        }),
                    new TestCaseDescriptor(
                        suiteId: "Tray",
                        caseId: "StoppedTrayStatusEnablesStart",
                        displayName: "Stopped tray status enables start",
                        executeAsync: delegate
                        {
                            XenoTrayStatus status = new XenoTrayStatus();
                            XenoTrayMenuState menu = XenoTrayStatusFormatter.BuildMenuState(status);
                            if (menu.StatusHeader != "Status: Stopped") throw new InvalidOperationException("Stopped status header mismatch.");
                            if (!menu.StartEnabled) throw new InvalidOperationException("Start should be enabled for a stopped client.");
                            if (menu.OpenDashboardEnabled) throw new InvalidOperationException("Dashboard should be disabled when not listening.");
                            if (menu.ExitClientEnabled) throw new InvalidOperationException("Exit client should be disabled when stopped.");
                            return Task.CompletedTask;
                        }),
                    new TestCaseDescriptor(
                        suiteId: "Tray",
                        caseId: "TrayStatisticsTextIncludesLocalRequestCounts",
                        displayName: "Tray statistics text includes local request counts",
                        executeAsync: delegate
                        {
                            XenoTrayStatus status = new XenoTrayStatus
                            {
                                Name = "THINKPAD",
                                ClientId = "xcl_test",
                                WebSocketUrl = "ws://localhost:9000/ws/v1/clients",
                                Running = true,
                                Connected = true,
                                Registered = true,
                                ProcessId = 1234
                            };
                            XenoTrayStatistics statistics = new XenoTrayStatistics
                            {
                                Requests = 12,
                                Failures = 1,
                                StreamingRequests = 3,
                                AverageLatencyMs = 42.5
                            };
                            string text = XenoTrayStatusFormatter.BuildStatisticsText(status, statistics);
                            if (!text.Contains("Status: Connected", StringComparison.Ordinal)) throw new InvalidOperationException("Statistics text did not include status.");
                            if (!text.Contains("Requests: 12", StringComparison.Ordinal)) throw new InvalidOperationException("Statistics text did not include requests.");
                            if (!text.Contains("Failures: 1", StringComparison.Ordinal)) throw new InvalidOperationException("Statistics text did not include failures.");
                            if (!text.Contains("Average latency: 42.5 ms", StringComparison.Ordinal)) throw new InvalidOperationException("Statistics text did not include latency.");
                            return Task.CompletedTask;
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
                            AuthSession? inferredSession = auth.ValidateToken(platform, token.Token);
                            if (inferredSession == null || inferredSession.TenantId != user.TenantId) throw new InvalidOperationException("Token tenant was not inferred.");
                            AuthSession? session = auth.ValidateToken(platform, user.TenantId, token.Token);
                            if (session == null) throw new InvalidOperationException("Token did not validate.");
                            if (auth.ValidateToken(platform, "ten_wrong", token.Token) != null) throw new InvalidOperationException("Token validated for the wrong tenant.");
                            if (!auth.RevokeSession(platform, session.Id)) throw new InvalidOperationException("Session was not revoked.");
                            if (auth.ValidateToken(platform, user.TenantId, token.Token) != null) throw new InvalidOperationException("Revoked token still validates.");
                        }),
                    new TestCaseDescriptor(
                        "Authentication",
                        "CredentialValidation",
                        "Direct credentials validate by tenant-bound access key",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            AuthenticationService auth = new AuthenticationService();
                            Credential credential = platform.Credentials[0];
                            Credential? inferredCredential = auth.ValidateCredential(platform, credential.AccessKey);
                            if (inferredCredential == null || inferredCredential.TenantId != credential.TenantId) throw new InvalidOperationException("Credential tenant was not inferred.");
                            if (auth.ValidateCredential(platform, credential.TenantId, credential.AccessKey) == null) throw new InvalidOperationException("Credential did not validate.");
                            if (auth.ValidateCredential(platform, "ten_wrong", credential.AccessKey) != null) throw new InvalidOperationException("Credential validated for the wrong tenant.");
                            if (auth.ValidateCredential(platform, credential.TenantId, "wrong") != null) throw new InvalidOperationException("Invalid credential validated.");
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
                        }),
                    new TestCaseDescriptor(
                        "Authentication",
                        "AuthorizationKernelContracts",
                        "Authorization kernel enforces system admin, tenant admin, RBAC permit, implicit deny, and explicit deny",
                        executeAsync: async delegate
                        {
                            await RunAuthorizationKernelContractsAsync().ConfigureAwait(false);
                        })
                });
        }

        private static async Task RunAuthorizationKernelContractsAsync()
        {
            XenoPlatform platform = new XenoPlatform();
            await platform.InitializeAsync().ConfigureAwait(false);
            AuthorizationService authorization = new AuthorizationService();
            Tenant defaultTenant = platform.GetDefaultTenant();
            Tenant otherTenant = new Tenant { Id = "ten_security_other", Name = "Security Other", Active = true };
            platform.Tenants.Add(otherTenant);
            platform.EnsureTenantDefaults(otherTenant.Id);

            User systemAdmin = platform.Users.First(item => item.TenantId == defaultTenant.Id && item.IsAdmin);
            User tenantAdmin = new User
            {
                TenantId = defaultTenant.Id,
                Username = "tenant-admin-security",
                Email = "tenant-admin-security@xeno",
                IsAdmin = false,
                IsTenantAdmin = true,
                Active = true
            };
            platform.Users.Add(tenantAdmin);
            User rbacUser = new User
            {
                TenantId = defaultTenant.Id,
                Username = "rbac-security",
                Email = "rbac-security@xeno",
                IsAdmin = false,
                IsTenantAdmin = false,
                Active = true
            };
            platform.Users.Add(rbacUser);

            Permission readUsers = new Permission { TenantId = defaultTenant.Id, ResourceType = "Users", OperationType = OperationTypeEnum.Read, Permit = true };
            Permission denyUsers = new Permission { TenantId = defaultTenant.Id, ResourceType = "Users", OperationType = OperationTypeEnum.Read, Permit = false };
            platform.Permissions.Add(readUsers);
            platform.Permissions.Add(denyUsers);
            Role reader = new Role { TenantId = defaultTenant.Id, Name = "Security Reader", PermissionIds = new List<string> { readUsers.Id } };
            Role deniedReader = new Role { TenantId = defaultTenant.Id, Name = "Security Denied Reader", PermissionIds = new List<string> { readUsers.Id, denyUsers.Id } };
            platform.Roles.Add(reader);
            platform.Roles.Add(deniedReader);
            platform.RoleAssignments.Add(new RoleAssignment { TenantId = defaultTenant.Id, RoleId = reader.Id, PrincipalType = PrincipalTypeEnum.User, PrincipalId = rbacUser.Id });

            RequestContext systemContext = new RequestContext { TenantId = defaultTenant.Id, UserId = systemAdmin.Id, PrincipalType = PrincipalTypeEnum.User, IsAuthenticated = true, AuthScheme = AuthSchemeEnum.Bearer };
            AssertAllowed(authorization.Authorize(platform, new AuthorizationRequest { Context = systemContext, TargetTenantId = otherTenant.Id, ResourceType = "Users", OperationType = OperationTypeEnum.Delete }), "System admin should access other tenant.");

            RequestContext tenantAdminContext = new RequestContext { TenantId = defaultTenant.Id, UserId = tenantAdmin.Id, PrincipalType = PrincipalTypeEnum.User, IsAuthenticated = true, AuthScheme = AuthSchemeEnum.Bearer };
            AssertAllowed(authorization.Authorize(platform, new AuthorizationRequest { Context = tenantAdminContext, TargetTenantId = defaultTenant.Id, ResourceType = "Users", OperationType = OperationTypeEnum.Delete }), "Tenant admin should access own tenant.");
            AssertDenied(authorization.Authorize(platform, new AuthorizationRequest { Context = tenantAdminContext, TargetTenantId = otherTenant.Id, ResourceType = "Users", OperationType = OperationTypeEnum.Read }), "TenantMismatch");

            RequestContext rbacContext = new RequestContext { TenantId = defaultTenant.Id, UserId = rbacUser.Id, PrincipalType = PrincipalTypeEnum.User, IsAuthenticated = true, AuthScheme = AuthSchemeEnum.Bearer };
            AssertAllowed(authorization.Authorize(platform, new AuthorizationRequest { Context = rbacContext, TargetTenantId = defaultTenant.Id, ResourceType = "Users", OperationType = OperationTypeEnum.Read }), "RBAC read should be allowed.");
            AssertDenied(authorization.Authorize(platform, new AuthorizationRequest { Context = rbacContext, TargetTenantId = defaultTenant.Id, ResourceType = "Users", OperationType = OperationTypeEnum.Write }), "PermissionDenied");

            platform.RoleAssignments.Clear();
            platform.RoleAssignments.Add(new RoleAssignment { TenantId = defaultTenant.Id, RoleId = deniedReader.Id, PrincipalType = PrincipalTypeEnum.User, PrincipalId = rbacUser.Id });
            AssertDenied(authorization.Authorize(platform, new AuthorizationRequest { Context = rbacContext, TargetTenantId = defaultTenant.Id, ResourceType = "Users", OperationType = OperationTypeEnum.Read }), "PermissionDeniedExplicit");

            Credential adminOwnedCredential = new Credential { TenantId = defaultTenant.Id, UserId = systemAdmin.Id, AccessKey = "admin-owned", SecretKey = "secret", Active = true };
            platform.Credentials.Add(adminOwnedCredential);
            RequestContext credentialContext = new RequestContext { TenantId = defaultTenant.Id, UserId = systemAdmin.Id, CredentialId = adminOwnedCredential.Id, PrincipalType = PrincipalTypeEnum.Credential, IsAuthenticated = true, AuthScheme = AuthSchemeEnum.Credential };
            AssertDenied(authorization.Authorize(platform, new AuthorizationRequest { Context = credentialContext, TargetTenantId = otherTenant.Id, ResourceType = "Users", OperationType = OperationTypeEnum.Read }), "TenantMismatch");
        }

        private static void AssertAllowed(AuthorizationDecision decision, string message)
        {
            if (!decision.Allowed) throw new InvalidOperationException(message + " Decision: " + decision.ReasonCode + " " + decision.Message);
        }

        private static void AssertDenied(AuthorizationDecision decision, string expectedReasonCode)
        {
            if (decision.Allowed) throw new InvalidOperationException("Authorization unexpectedly succeeded.");
            if (!String.Equals(decision.ReasonCode, expectedReasonCode, StringComparison.Ordinal)) throw new InvalidOperationException("Expected denial reason " + expectedReasonCode + " but got " + decision.ReasonCode + ".");
        }

        /// <summary>
        /// Server-requested telemetry test suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor TelemetrySuite()
        {
            return new TestSuiteDescriptor(
                "Telemetry",
                "Server Telemetry",
                new List<TestCaseDescriptor>
                {
                    new TestCaseDescriptor(
                        "Telemetry",
                        "TelemetryCollectionSettingsDefaultsAndClamps",
                        "Telemetry collection settings expose defaults and clamps",
                        executeAsync: delegate
                        {
                            TelemetryCollectionSettings settings = new TelemetryCollectionSettings();
                            if (!settings.Enabled) throw new InvalidOperationException("Telemetry collection should be enabled by default.");
                            if (settings.CollectionIntervalSeconds != 60) throw new InvalidOperationException("Unexpected telemetry collection interval default.");
                            if (settings.RequestTimeoutMs != 5000) throw new InvalidOperationException("Unexpected telemetry request timeout default.");
                            if (settings.RetentionDays != 30) throw new InvalidOperationException("Unexpected telemetry retention default.");
                            if (settings.PruneIntervalMinutes != 60) throw new InvalidOperationException("Unexpected telemetry prune interval default.");
                            if (!settings.CollectHostTelemetry) throw new InvalidOperationException("Host telemetry collection should default to enabled.");
                            if (!settings.CollectProviderTelemetry) throw new InvalidOperationException("Provider telemetry collection should default to enabled.");
                            if (!settings.PersistRawSnapshots) throw new InvalidOperationException("Raw snapshot persistence should default to enabled.");

                            settings.CollectionIntervalSeconds = 1;
                            settings.RequestTimeoutMs = 1;
                            settings.RetentionDays = 0;
                            settings.PruneIntervalMinutes = 0;
                            settings.MaxRawSnapshotBytes = -1;
                            settings.MaxSamplesPerClientResponse = 0;
                            settings.MaxProviderMetricRowsPerResponse = -1;
                            settings.MaxConcurrentClientRequests = 0;
                            if (settings.CollectionIntervalSeconds != 10) throw new InvalidOperationException("Telemetry collection interval minimum clamp failed.");
                            if (settings.RequestTimeoutMs != 1000) throw new InvalidOperationException("Telemetry request timeout minimum clamp failed.");
                            if (settings.RetentionDays != 1) throw new InvalidOperationException("Telemetry retention minimum clamp failed.");
                            if (settings.PruneIntervalMinutes != 1) throw new InvalidOperationException("Telemetry prune interval minimum clamp failed.");
                            if (settings.MaxRawSnapshotBytes != 0) throw new InvalidOperationException("Telemetry raw snapshot size minimum clamp failed.");
                            if (settings.MaxSamplesPerClientResponse != 1) throw new InvalidOperationException("Telemetry max samples minimum clamp failed.");
                            if (settings.MaxProviderMetricRowsPerResponse != 0) throw new InvalidOperationException("Telemetry provider metric rows minimum clamp failed.");
                            if (settings.MaxConcurrentClientRequests != 1) throw new InvalidOperationException("Telemetry concurrency minimum clamp failed.");

                            settings.CollectionIntervalSeconds = 999;
                            settings.RequestTimeoutMs = 999999;
                            settings.RetentionDays = 99999;
                            settings.PruneIntervalMinutes = 99999;
                            settings.MaxRawSnapshotBytes = 99999999;
                            settings.MaxSamplesPerClientResponse = 99999;
                            settings.MaxProviderMetricRowsPerResponse = 999999;
                            settings.MaxConcurrentClientRequests = 999;
                            if (settings.CollectionIntervalSeconds != 300) throw new InvalidOperationException("Telemetry collection interval maximum clamp failed.");
                            if (settings.RequestTimeoutMs != 60000) throw new InvalidOperationException("Telemetry request timeout maximum clamp failed.");
                            if (settings.RetentionDays != 3650) throw new InvalidOperationException("Telemetry retention maximum clamp failed.");
                            if (settings.PruneIntervalMinutes != 1440) throw new InvalidOperationException("Telemetry prune interval maximum clamp failed.");
                            if (settings.MaxRawSnapshotBytes != 1048576) throw new InvalidOperationException("Telemetry raw snapshot size maximum clamp failed.");
                            if (settings.MaxSamplesPerClientResponse != 250) throw new InvalidOperationException("Telemetry max samples maximum clamp failed.");
                            if (settings.MaxProviderMetricRowsPerResponse != 10000) throw new InvalidOperationException("Telemetry provider metric rows maximum clamp failed.");
                            if (settings.MaxConcurrentClientRequests != 64) throw new InvalidOperationException("Telemetry concurrency maximum clamp failed.");
                            return Task.CompletedTask;
                        }),
                    new TestCaseDescriptor(
                        "Telemetry",
                        "TelemetryIdPrefixes",
                        "Telemetry ID helpers emit expected prefixes",
                        executeAsync: delegate
                        {
                            AssertIdPrefix(IdGenerator.NewClientTelemetrySampleId(), "ctel_");
                            AssertIdPrefix(IdGenerator.NewClientTelemetryGpuSampleId(), "ctgpu_");
                            AssertIdPrefix(IdGenerator.NewClientProviderTelemetrySampleId(), "cptel_");
                            AssertIdPrefix(IdGenerator.NewClientProviderTelemetryModelSampleId(), "cptmdl_");
                            AssertIdPrefix(IdGenerator.NewClientProviderTelemetryMetricSampleId(), "cptmet_");
                            AssertIdPrefix(IdGenerator.NewTelemetryCollectionRequestId(), "telreq_");
                            return Task.CompletedTask;
                        }),
                    new TestCaseDescriptor(
                        "Telemetry",
                        "TelemetryWireContractsSerialize",
                        "Telemetry WebSocket request and snapshot contracts serialize",
                        executeAsync: delegate
                        {
                            Xeno.Core.Models.Telemetry.ServerTelemetryCollectionRequestMessage request = new Xeno.Core.Models.Telemetry.ServerTelemetryCollectionRequestMessage
                            {
                                RequestId = "telreq_test",
                                TenantId = Constants.DefaultTenantId,
                                ClientId = "xcl_test",
                                CollectHostTelemetry = true,
                                CollectProviderTelemetry = false,
                                IncludeRawSnapshot = false,
                                MaxRawSnapshotBytes = 1024,
                                MaxProviderMetricRows = 10,
                                MaxSamples = 1
                            };
                            string requestJson = JsonUtil.Serialize(request);
                            Xeno.Core.Models.Telemetry.ServerTelemetryCollectionRequestMessage parsedRequest = DeserializeRequired<Xeno.Core.Models.Telemetry.ServerTelemetryCollectionRequestMessage>(requestJson);
                            if (parsedRequest.Type != "server.telemetry.request") throw new InvalidOperationException("Telemetry request type did not serialize.");
                            if (parsedRequest.RequestId != request.RequestId) throw new InvalidOperationException("Telemetry request ID did not round-trip.");
                            if (parsedRequest.CollectProviderTelemetry) throw new InvalidOperationException("Telemetry request provider flag did not round-trip.");

                            Xeno.Core.Models.Telemetry.ClientTelemetrySnapshotMessage snapshot = new Xeno.Core.Models.Telemetry.ClientTelemetrySnapshotMessage
                            {
                                RequestId = request.RequestId,
                                TenantId = Constants.DefaultTenantId,
                                ClientId = "xcl_test",
                                Snapshot = new Xeno.Core.Models.Telemetry.ClientTelemetrySnapshot
                                {
                                    Metadata = new Xeno.Core.Models.Telemetry.ClientTelemetryMetadata
                                    {
                                        Id = "ctel_test",
                                        ClientId = "xcl_test",
                                        MachineName = "test-machine",
                                        CollectedUtc = DateTime.UtcNow,
                                        CollectionElapsedMs = 12
                                    }
                                }
                            };
                            string snapshotJson = JsonUtil.Serialize(snapshot);
                            Xeno.Core.Models.Telemetry.ClientTelemetrySnapshotMessage parsedSnapshot = DeserializeRequired<Xeno.Core.Models.Telemetry.ClientTelemetrySnapshotMessage>(snapshotJson);
                            if (parsedSnapshot.Type != "client.telemetry.snapshot") throw new InvalidOperationException("Telemetry snapshot type did not serialize.");
                            if (parsedSnapshot.Snapshot.Metadata.MachineName != "test-machine") throw new InvalidOperationException("Telemetry snapshot metadata did not round-trip.");

                            ServerRequestCancellationMessage cancellation = new ServerRequestCancellationMessage
                            {
                                Type = "server.request.cancel",
                                RequestId = "req_cancel",
                                Reason = "CallerDisconnected",
                                RequestorIp = "127.0.0.1",
                                Method = "POST",
                                RelativePath = "/v1/chat/completions",
                                Source = "local-provider"
                            };
                            string cancellationJson = JsonUtil.Serialize(cancellation);
                            ServerRequestCancellationMessage parsedCancellation = DeserializeRequired<ServerRequestCancellationMessage>(cancellationJson);
                            if (parsedCancellation.Type != "server.request.cancel") throw new InvalidOperationException("Request cancellation type did not serialize.");
                            if (parsedCancellation.Reason != "CallerDisconnected") throw new InvalidOperationException("Request cancellation reason did not round-trip.");
                            if (parsedCancellation.Source != "local-provider") throw new InvalidOperationException("Request cancellation source did not round-trip.");
                            return Task.CompletedTask;
                        }),
                    new TestCaseDescriptor(
                        "Telemetry",
                        "ClientTelemetryLatestCacheStoresLatestSamples",
                        "Client telemetry latest cache stores and filters latest samples",
                        executeAsync: delegate
                        {
                            using (ClientTelemetryLatestCache cache = new ClientTelemetryLatestCache())
                            {
                                Xeno.Core.Models.Telemetry.ServerClientTelemetrySampleRecord host = new Xeno.Core.Models.Telemetry.ServerClientTelemetrySampleRecord
                                {
                                    Id = "ctel_test",
                                    TenantId = Constants.DefaultTenantId,
                                    ClientId = "xcl_test",
                                    ClientName = "client",
                                    MachineName = "machine"
                                };
                                cache.SetHost(host);
                                if (cache.GetHost(Constants.DefaultTenantId, null).Count != 1) throw new InvalidOperationException("Host cache did not return tenant sample.");
                                if (cache.GetHost(Constants.DefaultTenantId, "missing").Count != 0) throw new InvalidOperationException("Host cache ignored client filter.");

                                Xeno.Core.Models.Telemetry.ServerProviderTelemetrySampleRecord provider = new Xeno.Core.Models.Telemetry.ServerProviderTelemetrySampleRecord
                                {
                                    Id = "cptel_test",
                                    TenantId = Constants.DefaultTenantId,
                                    ClientId = "xcl_test",
                                    ProviderName = "ollama"
                                };
                                cache.SetProvider(provider);
                                if (cache.GetProviders(Constants.DefaultTenantId, "xcl_test").Count != 1) throw new InvalidOperationException("Provider cache did not return client sample.");
                                cache.ClearTenant(Constants.DefaultTenantId);
                                if (cache.GetHost(Constants.DefaultTenantId, null).Count != 0) throw new InvalidOperationException("Host cache did not clear tenant samples.");
                                if (cache.GetProviders(Constants.DefaultTenantId, null).Count != 0) throw new InvalidOperationException("Provider cache did not clear tenant samples.");
                            }

                            return Task.CompletedTask;
                        }),
                    new TestCaseDescriptor(
                        "Telemetry",
                        "AcceptedTelemetrySnapshotPersistsForDashboard",
                        "Accepted telemetry WebSocket snapshots are persisted for dashboard latest and rollup APIs",
                        executeAsync: delegate { return RunAcceptedTelemetrySnapshotPersistsForDashboardAsync(); }),
                    new TestCaseDescriptor(
                        "Telemetry",
                        "TelemetryOpenApiIncludesRoutes",
                        "OpenAPI includes telemetry routes and request bodies",
                        executeAsync: delegate { return RunTelemetryOpenApiSmokeAsync(); }),
                    new TestCaseDescriptor(
                        "Telemetry",
                        "RoutingPolicyValidationRejectsInvalidTelemetry",
                        "Routing policy validation rejects invalid telemetry policy fields",
                        executeAsync: delegate { return RunRoutingPolicyValidationSmokeAsync(); })
                });
        }

        private static async Task RunAcceptedTelemetrySnapshotPersistsForDashboardAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-dashboard-telemetry-" + Guid.NewGuid().ToString("N") + ".db");
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
                },
                TelemetryCollection = new TelemetryCollectionSettings
                {
                    Enabled = false
                }
            };

            using (CancellationTokenSource serverTokenSource = new CancellationTokenSource())
            using (CancellationTokenSource clientTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
            {
                XenoServer server = new XenoServer(settings);
                Task serverTask = server.StartAsync(serverTokenSource.Token);
                try
                {
                    await WaitForHealthAsync(port).ConfigureAwait(false);
                    DateTime collectedUtc = DateTime.UtcNow;
                    using (ClientWebSocket socket = new ClientWebSocket())
                    {
                        ApplySeedCredentialHeaders(socket);
                        await socket.ConnectAsync(new Uri("ws://127.0.0.1:" + port + settings.WebSocket.Path), clientTokenSource.Token).ConfigureAwait(false);
                        string? connected = await ReceiveWebSocketTextAsync(socket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(connected) || !connected.Contains("server.connected", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Telemetry test WebSocket did not connect: " + connected);

                        await SendWebSocketJsonAsync(socket, new
                        {
                            type = "client.register",
                            clientId = "xcl_dashboard_telemetry",
                            tenantId = Constants.DefaultTenantId,
                            name = "dashboard-telemetry-client",
                            inventory = new
                            {
                                runners = Array.Empty<object>()
                            }
                        }, clientTokenSource.Token).ConfigureAwait(false);
                        string? registered = await ReceiveWebSocketTextAsync(socket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(registered) || !registered.Contains("server.registered", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Telemetry test client did not register: " + registered);

                        Xeno.Core.Models.Telemetry.ClientTelemetrySnapshotMessage snapshot = new Xeno.Core.Models.Telemetry.ClientTelemetrySnapshotMessage
                        {
                            RequestId = "telreq_dashboard_telemetry",
                            TenantId = Constants.DefaultTenantId,
                            ClientId = "xcl_dashboard_telemetry",
                            Snapshot = new Xeno.Core.Models.Telemetry.ClientTelemetrySnapshot
                            {
                                Metadata = new Xeno.Core.Models.Telemetry.ClientTelemetryMetadata
                                {
                                    ClientId = "xcl_dashboard_telemetry",
                                    MachineName = "dashboard-telemetry-machine",
                                    CollectedUtc = collectedUtc,
                                    CollectionElapsedMs = 7
                                },
                                Cpu = new Xeno.Core.Models.Telemetry.ClientCpuTelemetry
                                {
                                    UtilizationPercent = 42
                                },
                                Memory = new Xeno.Core.Models.Telemetry.ClientMemoryTelemetry
                                {
                                    UtilizationPercent = 55,
                                    TotalBytes = 1000,
                                    UsedBytes = 550,
                                    AvailableBytes = 450
                                },
                                Gpu = new Xeno.Core.Models.Telemetry.ClientGpuTelemetry
                                {
                                    AverageUtilizationPercent = 12
                                }
                            }
                        };

                        await SendWebSocketJsonAsync(socket, snapshot, clientTokenSource.Token).ConfigureAwait(false);
                        string? ack = await ReceiveWebSocketTextAsync(socket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(ack) || !ack.Contains("server.telemetry.ack", StringComparison.OrdinalIgnoreCase) || !ack.Contains("\"accepted\":true", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Telemetry snapshot was not accepted: " + ack);
                    }

                    using (HttpClient httpClient = new HttpClient())
                    {
                        string baseUrl = "http://127.0.0.1:" + port;
                        await AuthenticateSeedAdminAsync(httpClient, baseUrl).ConfigureAwait(false);
                        string latestJson = await httpClient.GetStringAsync(baseUrl + "/v1.0/api/client-telemetry/latest?clientId=xcl_dashboard_telemetry").ConfigureAwait(false);
                        Xeno.Core.Models.Telemetry.ServerClientTelemetryLatestResponse latest = DeserializeRequired<Xeno.Core.Models.Telemetry.ServerClientTelemetryLatestResponse>(latestJson);
                        Xeno.Core.Models.Telemetry.ServerClientTelemetrySampleRecord latestHost = latest.HostTelemetry.FirstOrDefault() ?? throw new InvalidOperationException("Latest host telemetry did not include the accepted WebSocket sample: " + latestJson);
                        if (latestHost.CpuUtilizationPercent != 42) throw new InvalidOperationException("Latest host telemetry returned the wrong CPU value.");

                        Xeno.Core.Models.Telemetry.ServerClientTelemetryRollupRequest rollupRequest = new Xeno.Core.Models.Telemetry.ServerClientTelemetryRollupRequest
                        {
                            TenantId = Constants.DefaultTenantId,
                            ClientId = "xcl_dashboard_telemetry",
                            Metric = Xeno.Core.Models.Telemetry.ClientTelemetryMetricEnum.CpuUtilizationPercent,
                            FromUtc = collectedUtc.AddMinutes(-5),
                            ToUtc = collectedUtc.AddMinutes(5),
                            BucketMinutes = 5
                        };
                        using (HttpResponseMessage rollupResponse = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/client-telemetry/history/rollups", JsonUtil.Serialize(rollupRequest)).ConfigureAwait(false))
                        {
                            string rollupJson = await rollupResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!rollupResponse.IsSuccessStatusCode) throw new InvalidOperationException("Telemetry rollup request failed: " + rollupJson);
                            IReadOnlyList<Xeno.Core.Models.Telemetry.ServerClientTelemetryRollupBucket>? buckets = JsonUtil.Deserialize<List<Xeno.Core.Models.Telemetry.ServerClientTelemetryRollupBucket>>(rollupJson);
                            if (buckets == null || !buckets.Any(item => item.Count > 0 && item.Average == 42)) throw new InvalidOperationException("Telemetry rollup did not include the accepted WebSocket sample: " + rollupJson);
                        }
                    }
                }
                finally
                {
                    await StopServerAsync(server, serverTask, serverTokenSource, dbFile).ConfigureAwait(false);
                }
            }
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

                        await RunPlatformStateRoundTripAsync(settings, "sqlite").ConfigureAwait(false);
                    }
                    finally
                    {
                        await DeleteFileWithRetryAsync(dbFile).ConfigureAwait(false);
                    }
                }));

            cases.Add(new TestCaseDescriptor(
                suiteId: "Database",
                caseId: "PostgresqlPersistsPlatformState",
                displayName: "PostgreSQL persists complete platform state when a test connection is provided",
                executeAsync: async delegate
                {
                    string? connectionString = Environment.GetEnvironmentVariable("XENO_TEST_POSTGRESQL_CONNECTION_STRING");
                    if (String.IsNullOrWhiteSpace(connectionString)) return;
                    await RunPlatformStateRoundTripAsync(new DatabaseSettings
                    {
                        Type = DatabaseTypeEnum.Postgresql,
                        ConnectionString = connectionString
                    }, "postgresql").ConfigureAwait(false);
                }));

            cases.Add(new TestCaseDescriptor(
                suiteId: "Database",
                caseId: "MysqlPersistsPlatformState",
                displayName: "MySQL persists complete platform state when a test connection is provided",
                executeAsync: async delegate
                {
                    string? connectionString = Environment.GetEnvironmentVariable("XENO_TEST_MYSQL_CONNECTION_STRING");
                    if (String.IsNullOrWhiteSpace(connectionString)) return;
                    await RunPlatformStateRoundTripAsync(new DatabaseSettings
                    {
                        Type = DatabaseTypeEnum.Mysql,
                        ConnectionString = connectionString
                    }, "mysql").ConfigureAwait(false);
                }));

            cases.Add(new TestCaseDescriptor(
                suiteId: "Database",
                caseId: "SqlServerPersistsPlatformState",
                displayName: "SQL Server persists complete platform state when a test connection is provided",
                executeAsync: async delegate
                {
                    string? connectionString = Environment.GetEnvironmentVariable("XENO_TEST_SQLSERVER_CONNECTION_STRING");
                    if (String.IsNullOrWhiteSpace(connectionString)) return;
                    await RunPlatformStateRoundTripAsync(new DatabaseSettings
                    {
                        Type = DatabaseTypeEnum.SqlServer,
                        ConnectionString = connectionString
                    }, "sqlserver").ConfigureAwait(false);
                }));

            cases.Add(new TestCaseDescriptor(
                suiteId: "Database",
                caseId: "SqliteModelTokenLeaderboardAggregatesRequestHistory",
                displayName: "SQLite model token leaderboard aggregates request history",
                executeAsync: async delegate
                {
                    string dbFile = Path.Combine(Path.GetTempPath(), "xeno-model-leaderboard-" + Guid.NewGuid().ToString("N") + ".db");
                    try
                    {
                        await RunModelTokenLeaderboardRoundTripAsync(new DatabaseSettings
                        {
                            Type = DatabaseTypeEnum.Sqlite,
                            Filename = dbFile
                        }, "sqlite").ConfigureAwait(false);
                    }
                    finally
                    {
                        await DeleteFileWithRetryAsync(dbFile).ConfigureAwait(false);
                    }
                }));

            cases.Add(new TestCaseDescriptor(
                suiteId: "Database",
                caseId: "PostgresqlModelTokenLeaderboardAggregatesRequestHistory",
                displayName: "PostgreSQL model token leaderboard aggregates request history when a test connection is provided",
                executeAsync: async delegate
                {
                    string? connectionString = Environment.GetEnvironmentVariable("XENO_TEST_POSTGRESQL_CONNECTION_STRING");
                    if (String.IsNullOrWhiteSpace(connectionString)) return;
                    await RunModelTokenLeaderboardRoundTripAsync(new DatabaseSettings
                    {
                        Type = DatabaseTypeEnum.Postgresql,
                        ConnectionString = connectionString
                    }, "postgresql").ConfigureAwait(false);
                }));

            cases.Add(new TestCaseDescriptor(
                suiteId: "Database",
                caseId: "MysqlModelTokenLeaderboardAggregatesRequestHistory",
                displayName: "MySQL model token leaderboard aggregates request history when a test connection is provided",
                executeAsync: async delegate
                {
                    string? connectionString = Environment.GetEnvironmentVariable("XENO_TEST_MYSQL_CONNECTION_STRING");
                    if (String.IsNullOrWhiteSpace(connectionString)) return;
                    await RunModelTokenLeaderboardRoundTripAsync(new DatabaseSettings
                    {
                        Type = DatabaseTypeEnum.Mysql,
                        ConnectionString = connectionString
                    }, "mysql").ConfigureAwait(false);
                }));

            cases.Add(new TestCaseDescriptor(
                suiteId: "Database",
                caseId: "SqlServerModelTokenLeaderboardAggregatesRequestHistory",
                displayName: "SQL Server model token leaderboard aggregates request history when a test connection is provided",
                executeAsync: async delegate
                {
                    string? connectionString = Environment.GetEnvironmentVariable("XENO_TEST_SQLSERVER_CONNECTION_STRING");
                    if (String.IsNullOrWhiteSpace(connectionString)) return;
                    await RunModelTokenLeaderboardRoundTripAsync(new DatabaseSettings
                    {
                        Type = DatabaseTypeEnum.SqlServer,
                        ConnectionString = connectionString
                    }, "sqlserver").ConfigureAwait(false);
                }));

            cases.Add(new TestCaseDescriptor(
                suiteId: "Database",
                caseId: "SqliteTelemetryCollectionAttemptsRoundTrip",
                displayName: "SQLite telemetry collection attempts preserve success fields",
                executeAsync: async delegate
                {
                    string dbFile = Path.Combine(Path.GetTempPath(), "xeno-telemetry-attempts-" + Guid.NewGuid().ToString("N") + ".db");
                    try
                    {
                        DatabaseSettings settings = new DatabaseSettings
                        {
                            Type = DatabaseTypeEnum.Sqlite,
                            Filename = dbFile
                        };

                        using (DatabaseDriverBase driver = await DatabaseDriverFactory.CreateAndInitializeAsync(settings).ConfigureAwait(false))
                        {
                            if (driver.ClientTelemetryCollectionAttempts == null) throw new InvalidOperationException("Telemetry collection attempts driver was not initialized.");
                            Xeno.Core.Models.Telemetry.ServerClientTelemetryCollectionAttemptRecord attempt = new Xeno.Core.Models.Telemetry.ServerClientTelemetryCollectionAttemptRecord
                            {
                                TenantId = Constants.DefaultTenantId,
                                ClientId = "xcl_attempt",
                                ClientName = "attempt-client",
                                HostRequested = true,
                                ProviderRequested = true,
                                Status = "Started"
                            };

                            await driver.ClientTelemetryCollectionAttempts.CreateAttemptAsync(attempt, CancellationToken.None).ConfigureAwait(false);
                            attempt.CompletedUtc = DateTime.UtcNow;
                            attempt.ElapsedMs = 123;
                            attempt.HostSucceeded = true;
                            attempt.ProviderSucceeded = true;
                            attempt.Status = "Completed";
                            attempt.HostSampleId = "ctel_attempt";
                            attempt.ProviderSampleCount = 2;
                            await driver.ClientTelemetryCollectionAttempts.CompleteAttemptAsync(attempt, CancellationToken.None).ConfigureAwait(false);

                            IReadOnlyList<Xeno.Core.Models.Telemetry.ServerClientTelemetryCollectionAttemptRecord> attempts = await driver.ClientTelemetryCollectionAttempts.SearchAttemptsAsync(Constants.DefaultTenantId, "xcl_attempt", 10, CancellationToken.None).ConfigureAwait(false);
                            Xeno.Core.Models.Telemetry.ServerClientTelemetryCollectionAttemptRecord stored = attempts.FirstOrDefault() ?? throw new InvalidOperationException("Telemetry collection attempt was not returned.");
                            if (!stored.HostRequested) throw new InvalidOperationException("Host requested flag did not round-trip.");
                            if (!stored.ProviderRequested) throw new InvalidOperationException("Provider requested flag did not round-trip.");
                            if (!stored.HostSucceeded) throw new InvalidOperationException("Host succeeded flag did not round-trip.");
                            if (!stored.ProviderSucceeded) throw new InvalidOperationException("Provider succeeded flag did not round-trip.");
                            if (stored.HostSampleId != "ctel_attempt") throw new InvalidOperationException("Host sample id did not round-trip.");
                            if (stored.ProviderSampleCount != 2) throw new InvalidOperationException("Provider sample count did not round-trip.");
                        }
                    }
                    finally
                    {
                        await DeleteFileWithRetryAsync(dbFile).ConfigureAwait(false);
                    }
                }));

            cases.Add(new TestCaseDescriptor(
                suiteId: "Database",
                caseId: "PlatformTenantCascadeRemovesScopedState",
                displayName: "Platform tenant cascade removes all tenant-scoped state",
                executeAsync: async delegate
                {
                    const string tenantId = "ten_cascade";
                    const string keepTenantId = "ten_keep";
                    XenoPlatform platform = new XenoPlatform();
                    await platform.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
                    platform.Tenants.Add(new Tenant { Id = tenantId, Name = "Cascade" });
                    platform.Tenants.Add(new Tenant { Id = keepTenantId, Name = "Keep" });
                    platform.Users.Add(new User { Id = "usr_cascade", TenantId = tenantId, Email = "cascade@xeno" });
                    platform.Users.Add(new User { Id = "usr_keep", TenantId = keepTenantId, Email = "keep@xeno" });
                    platform.Credentials.Add(new Credential { Id = "cred_cascade", TenantId = tenantId, UserId = "usr_cascade", AccessKey = "cascade" });
                    platform.AuthSessions.Add(new AuthSession { Id = "sess_cascade", TenantId = tenantId, UserId = "usr_cascade" });
                    platform.Roles.Add(new Role { Id = "role_cascade", TenantId = tenantId, Name = "CascadeRole" });
                    platform.Permissions.Add(new Permission { Id = "perm_cascade", TenantId = tenantId, ResourceType = "Cascade", OperationType = OperationTypeEnum.Read });
                    platform.RoleAssignments.Add(new RoleAssignment { Id = "rmap_cascade", TenantId = tenantId, RoleId = "role_cascade", PrincipalType = PrincipalTypeEnum.User, PrincipalId = "usr_cascade" });
                    platform.AuditRecords.Add(new AuditRecord { Id = "aud_cascade", TenantId = tenantId, ResourceType = "Cascade" });
                    platform.Clients.Add(new XenoClientRecord { Id = "xcl_cascade", TenantId = tenantId, Name = "cascade-client" });
                    platform.ModelDefinitions.Add(new ModelDefinition { Id = "md_cascade", TenantId = tenantId, Name = "cascade-model" });
                    platform.ModelConfigurations.Add(new ModelConfiguration { Id = "mc_cascade", TenantId = tenantId, Name = "cascade-config" });
                    platform.RoutingPolicies.Add(new RoutingPolicy { Id = "rpol_cascade", TenantId = tenantId, Name = "cascade-routing", ModelName = "*", ClientName = "*" });
                    platform.RequestHistory.Add(new RequestHistoryEntry { Id = "req_cascade", TenantId = tenantId, RequestorTenantId = tenantId });

                    int removed = platform.Tenants.RemoveAll(item => item.Id == tenantId);
                    removed += platform.RemoveTenantScopedRecords(tenantId);
                    if (removed < 13) throw new InvalidOperationException("Tenant cascade removed too few records.");
                    if (platform.Tenants.Any(item => item.Id == tenantId)) throw new InvalidOperationException("Tenant record remained after cascade.");
                    if (platform.ToState().Users.Any(item => item.TenantId == tenantId)) throw new InvalidOperationException("Tenant users remained after cascade.");
                    if (platform.ToState().Credentials.Any(item => item.TenantId == tenantId)) throw new InvalidOperationException("Tenant credentials remained after cascade.");
                    if (platform.ToState().AuthSessions.Any(item => item.TenantId == tenantId)) throw new InvalidOperationException("Tenant sessions remained after cascade.");
                    if (platform.ToState().Roles.Any(item => item.TenantId == tenantId)) throw new InvalidOperationException("Tenant roles remained after cascade.");
                    if (platform.ToState().Permissions.Any(item => item.TenantId == tenantId)) throw new InvalidOperationException("Tenant permissions remained after cascade.");
                    if (platform.ToState().RoleAssignments.Any(item => item.TenantId == tenantId)) throw new InvalidOperationException("Tenant role assignments remained after cascade.");
                    if (platform.ToState().AuditRecords.Any(item => item.TenantId == tenantId)) throw new InvalidOperationException("Tenant audit records remained after cascade.");
                    if (platform.ToState().Clients.Any(item => item.TenantId == tenantId)) throw new InvalidOperationException("Tenant clients remained after cascade.");
                    if (platform.ToState().ModelDefinitions.Any(item => item.TenantId == tenantId)) throw new InvalidOperationException("Tenant model definitions remained after cascade.");
                    if (platform.ToState().ModelConfigurations.Any(item => item.TenantId == tenantId)) throw new InvalidOperationException("Tenant model configurations remained after cascade.");
                    if (platform.ToState().RoutingPolicies.Any(item => item.TenantId == tenantId)) throw new InvalidOperationException("Tenant routing policies remained after cascade.");
                    if (platform.ToState().RequestHistory.Any(item => item.TenantId == tenantId)) throw new InvalidOperationException("Tenant request history remained after cascade.");
                    if (!platform.Users.Any(item => item.TenantId == keepTenantId)) throw new InvalidOperationException("Cascade removed another tenant's user.");
                }));

            cases.Add(new TestCaseDescriptor(
                suiteId: "Database",
                caseId: "SqliteTenantPurgeRemovesTelemetryRemnants",
                displayName: "SQLite tenant purge removes telemetry remnants",
                executeAsync: async delegate
                {
                    const string tenantId = "ten_purge";
                    const string keepTenantId = "ten_keep";
                    string dbFile = Path.Combine(Path.GetTempPath(), "xeno-tenant-purge-" + Guid.NewGuid().ToString("N") + ".db");
                    try
                    {
                        DatabaseSettings settings = new DatabaseSettings
                        {
                            Type = DatabaseTypeEnum.Sqlite,
                            Filename = dbFile
                        };

                        using (DatabaseDriverBase driver = await DatabaseDriverFactory.CreateAndInitializeAsync(settings).ConfigureAwait(false))
                        {
                            if (driver.ClientTelemetry == null) throw new InvalidOperationException("Client telemetry driver was not initialized.");
                            if (driver.ClientProviderTelemetry == null) throw new InvalidOperationException("Client provider telemetry driver was not initialized.");
                            if (driver.ClientTelemetryCollectionAttempts == null) throw new InvalidOperationException("Telemetry collection attempts driver was not initialized.");

                            Xeno.Core.Models.Telemetry.ClientTelemetrySnapshot hostSnapshot = new Xeno.Core.Models.Telemetry.ClientTelemetrySnapshot
                            {
                                Metadata = new Xeno.Core.Models.Telemetry.ClientTelemetryMetadata
                                {
                                    ClientId = "xcl_purge",
                                    MachineName = "purge-client",
                                    CollectedUtc = DateTime.UtcNow
                                },
                                Gpu = new Xeno.Core.Models.Telemetry.ClientGpuTelemetry
                                {
                                    AverageUtilizationPercent = 10
                                }
                            };
                            hostSnapshot.Gpu.Devices.Add(new Xeno.Core.Models.Telemetry.ClientGpuDeviceTelemetry { Uuid = "gpu-purge", Name = "Purge GPU", UtilizationPercent = 10 });
                            await driver.ClientTelemetry.CreateHostSampleAsync(tenantId, "xcl_purge", "purge-client", "telreq_purge", hostSnapshot, null, CancellationToken.None).ConfigureAwait(false);
                            await driver.ClientTelemetry.CreateHostSampleAsync(keepTenantId, "xcl_keep", "keep-client", "telreq_keep", hostSnapshot, null, CancellationToken.None).ConfigureAwait(false);

                            Xeno.Core.Models.Telemetry.ClientProviderTelemetrySnapshot providerSnapshot = new Xeno.Core.Models.Telemetry.ClientProviderTelemetrySnapshot();
                            providerSnapshot.Providers.Add(new Xeno.Core.Models.Telemetry.ClientProviderTelemetryProviderStatus
                            {
                                Endpoint = new Xeno.Core.Models.Telemetry.ClientProviderTelemetryEndpoint
                                {
                                    ProviderName = "purge-provider",
                                    ApiType = "Ollama",
                                    Kind = Xeno.Core.Models.Telemetry.ClientProviderTelemetryKindEnum.Ollama,
                                    EndpointDisplay = "http://127.0.0.1:11434",
                                    EndpointHash = "purgehash"
                                },
                                Status = Xeno.Core.Models.Telemetry.ClientProviderTelemetryStatusEnum.Available,
                                HealthStatus = Xeno.Core.Models.Telemetry.ClientProviderTelemetryHealthStatusEnum.Healthy,
                                CollectedUtc = DateTime.UtcNow
                            });
                            IReadOnlyList<Xeno.Core.Models.Telemetry.ServerProviderTelemetrySampleRecord> providerRecords = await driver.ClientProviderTelemetry.CreateProviderSnapshotAsync(tenantId, "xcl_purge", "purge-client", "telreq_purge", providerSnapshot, false, CancellationToken.None).ConfigureAwait(false);
                            string providerSampleId = providerRecords.First().Id;
                            await driver.ClientTelemetryCollectionAttempts.CreateAttemptAsync(new Xeno.Core.Models.Telemetry.ServerClientTelemetryCollectionAttemptRecord { TenantId = tenantId, ClientId = "xcl_purge", ClientName = "purge-client", Status = "Started" }, CancellationToken.None).ConfigureAwait(false);

                            await InsertTelemetryRemnantRowsAsync(dbFile, tenantId, providerSampleId).ConfigureAwait(false);
                            await driver.PurgeTenantDataAsync(tenantId, CancellationToken.None).ConfigureAwait(false);
                        }

                        string[] tenantTables = new[]
                        {
                            "client_telemetry_samples",
                            "client_telemetry_gpu_samples",
                            "client_provider_telemetry_samples",
                            "client_provider_telemetry_models",
                            "client_provider_telemetry_metrics",
                            "client_telemetry_collection_attempts"
                        };
                        foreach (string tableName in tenantTables)
                        {
                            int count = await CountRowsForTenantAsync(dbFile, tableName, tenantId).ConfigureAwait(false);
                            if (count != 0) throw new InvalidOperationException("Tenant purge left rows in " + tableName + ".");
                        }

                        int keepCount = await CountRowsForTenantAsync(dbFile, "client_telemetry_samples", keepTenantId).ConfigureAwait(false);
                        if (keepCount != 1) throw new InvalidOperationException("Tenant purge removed another tenant's telemetry.");
                    }
                    finally
                    {
                        await DeleteFileWithRetryAsync(dbFile).ConfigureAwait(false);
                    }
                }));

            cases.Add(new TestCaseDescriptor(
                suiteId: "Database",
                caseId: "SqliteServerTelemetryRollupsReturnPersistedSamples",
                displayName: "SQLite server telemetry rollups return persisted host and provider samples",
                executeAsync: async delegate
                {
                    string dbFile = Path.Combine(Path.GetTempPath(), "xeno-server-telemetry-rollups-" + Guid.NewGuid().ToString("N") + ".db");
                    try
                    {
                        DatabaseSettings settings = new DatabaseSettings
                        {
                            Type = DatabaseTypeEnum.Sqlite,
                            Filename = dbFile
                        };

                        using (DatabaseDriverBase driver = await DatabaseDriverFactory.CreateAndInitializeAsync(settings).ConfigureAwait(false))
                        {
                            if (driver.ClientTelemetry == null) throw new InvalidOperationException("Client telemetry driver was not initialized.");
                            if (driver.ClientProviderTelemetry == null) throw new InvalidOperationException("Client provider telemetry driver was not initialized.");

                            DateTime sampleUtc = DateTime.UtcNow.AddMinutes(-5);
                            Xeno.Core.Models.Telemetry.ClientTelemetrySnapshot hostSnapshot = new Xeno.Core.Models.Telemetry.ClientTelemetrySnapshot
                            {
                                Metadata = new Xeno.Core.Models.Telemetry.ClientTelemetryMetadata
                                {
                                    ClientId = "xcl_rollup",
                                    MachineName = "rollup-client",
                                    CollectedUtc = sampleUtc,
                                    CollectionElapsedMs = 12
                                },
                                Cpu = new Xeno.Core.Models.Telemetry.ClientCpuTelemetry
                                {
                                    UtilizationPercent = 42
                                },
                                Memory = new Xeno.Core.Models.Telemetry.ClientMemoryTelemetry
                                {
                                    TotalBytes = 1000,
                                    UsedBytes = 550,
                                    AvailableBytes = 450,
                                    UtilizationPercent = 55
                                },
                                Disk = new Xeno.Core.Models.Telemetry.ClientDiskTelemetry
                                {
                                    TotalBytes = 1000,
                                    UsedBytes = 660,
                                    AvailableBytes = 340,
                                    UtilizationPercent = 66
                                },
                                Gpu = new Xeno.Core.Models.Telemetry.ClientGpuTelemetry
                                {
                                    AverageUtilizationPercent = 12
                                }
                            };

                            await driver.ClientTelemetry.CreateHostSampleAsync(Constants.DefaultTenantId, "xcl_rollup", "rollup-client", "telreq_rollup", hostSnapshot, null, CancellationToken.None).ConfigureAwait(false);

                            Xeno.Core.Models.Telemetry.ClientProviderTelemetrySnapshot providerSnapshot = new Xeno.Core.Models.Telemetry.ClientProviderTelemetrySnapshot
                            {
                                Metadata = new Xeno.Core.Models.Telemetry.ClientProviderTelemetryMetadata
                                {
                                    ClientId = "xcl_rollup",
                                    MachineName = "rollup-client",
                                    CollectedUtc = sampleUtc,
                                    ProviderCount = 1
                                }
                            };
                            providerSnapshot.Providers.Add(new Xeno.Core.Models.Telemetry.ClientProviderTelemetryProviderStatus
                            {
                                Endpoint = new Xeno.Core.Models.Telemetry.ClientProviderTelemetryEndpoint
                                {
                                    ProviderName = "rollup-provider",
                                    ApiType = "Ollama",
                                    Kind = Xeno.Core.Models.Telemetry.ClientProviderTelemetryKindEnum.Ollama,
                                    EndpointDisplay = "http://127.0.0.1:11434",
                                    EndpointHash = "rolluphash"
                                },
                                Status = Xeno.Core.Models.Telemetry.ClientProviderTelemetryStatusEnum.Available,
                                HealthStatus = Xeno.Core.Models.Telemetry.ClientProviderTelemetryHealthStatusEnum.Healthy,
                                CollectedUtc = sampleUtc,
                                AvailableModelCount = 7,
                                LoadedModelCount = 2,
                                ActiveRequests = 3,
                                QueuedRequests = 1,
                                LatencyP95Ms = 123
                            });

                            await driver.ClientProviderTelemetry.CreateProviderSnapshotAsync(Constants.DefaultTenantId, "xcl_rollup", "rollup-client", "telreq_rollup", providerSnapshot, false, CancellationToken.None).ConfigureAwait(false);

                            DateTime fromUtc = sampleUtc.AddHours(-1);
                            DateTime toUtc = sampleUtc.AddHours(1);
                            IReadOnlyList<Xeno.Core.Models.Telemetry.ServerClientTelemetryRollupBucket> hostBuckets = await driver.ClientTelemetry.RollupHostAsync(new Xeno.Core.Models.Telemetry.ServerClientTelemetryRollupRequest
                            {
                                TenantId = Constants.DefaultTenantId,
                                ClientId = "xcl_rollup",
                                FromUtc = fromUtc,
                                ToUtc = toUtc,
                                BucketMinutes = 5,
                                Metric = Xeno.Core.Models.Telemetry.ClientTelemetryMetricEnum.CpuUtilizationPercent
                            }, CancellationToken.None).ConfigureAwait(false);
                            if (!hostBuckets.Any(item => item.Count > 0 && item.Average == 42)) throw new InvalidOperationException("Host telemetry rollup did not include persisted CPU value.");

                            IReadOnlyList<Xeno.Core.Models.Telemetry.ServerProviderTelemetryRollupBucket> providerBuckets = await driver.ClientProviderTelemetry.RollupProviderAsync(new Xeno.Core.Models.Telemetry.ServerProviderTelemetryRollupRequest
                            {
                                TenantId = Constants.DefaultTenantId,
                                ClientId = "xcl_rollup",
                                ProviderName = "rollup-provider",
                                FromUtc = fromUtc,
                                ToUtc = toUtc,
                                BucketMinutes = 5,
                                Metric = Xeno.Core.Models.Telemetry.ClientProviderTelemetryMetricEnum.ActiveRequests
                            }, CancellationToken.None).ConfigureAwait(false);
                            if (!providerBuckets.Any(item => item.Count > 0 && item.Average == 3)) throw new InvalidOperationException("Provider telemetry rollup did not include persisted active request value.");

                            IReadOnlyList<Xeno.Core.Models.Telemetry.ServerProviderTelemetryRollupBucket> modelBuckets = await driver.ClientProviderTelemetry.RollupProviderAsync(new Xeno.Core.Models.Telemetry.ServerProviderTelemetryRollupRequest
                            {
                                TenantId = Constants.DefaultTenantId,
                                ClientId = "xcl_rollup",
                                ProviderName = "rollup-provider",
                                FromUtc = fromUtc,
                                ToUtc = toUtc,
                                BucketMinutes = 5,
                                Metric = Xeno.Core.Models.Telemetry.ClientProviderTelemetryMetricEnum.AvailableModelCount
                            }, CancellationToken.None).ConfigureAwait(false);
                            if (!modelBuckets.Any(item => item.Count > 0 && item.Average == 7)) throw new InvalidOperationException("Provider telemetry rollup did not include persisted available model count.");
                        }
                    }
                    finally
                    {
                        await DeleteFileWithRetryAsync(dbFile).ConfigureAwait(false);
                    }
                }));

            cases.Add(new TestCaseDescriptor(
                suiteId: "Database",
                caseId: "ServerTelemetryImplementationsCoverConfiguredProviders",
                displayName: "Server telemetry persistence implementations cover every database provider",
                executeAsync: delegate
                {
                    Type[] telemetryTypes = new[]
                    {
                        typeof(Xeno.Core.Database.Sqlite.SqliteTelemetryMethods),
                        typeof(Xeno.Core.Database.Postgresql.PostgresqlTelemetryMethods),
                        typeof(Xeno.Core.Database.Mysql.MysqlTelemetryMethods),
                        typeof(Xeno.Core.Database.SqlServer.SqlServerTelemetryMethods)
                    };

                    foreach (Type telemetryType in telemetryTypes)
                    {
                        if (!typeof(Xeno.Core.Database.Interfaces.IClientTelemetryMethods).IsAssignableFrom(telemetryType)) throw new InvalidOperationException(telemetryType.Name + " does not implement host telemetry persistence.");
                        if (!typeof(Xeno.Core.Database.Interfaces.IClientProviderTelemetryMethods).IsAssignableFrom(telemetryType)) throw new InvalidOperationException(telemetryType.Name + " does not implement provider telemetry persistence.");
                        if (!typeof(Xeno.Core.Database.Interfaces.IClientTelemetryCollectionAttemptMethods).IsAssignableFrom(telemetryType)) throw new InvalidOperationException(telemetryType.Name + " does not implement telemetry collection attempts.");
                    }

                    Type[] driverTypes = new[]
                    {
                        typeof(Xeno.Core.Database.Sqlite.SqliteDatabaseDriver),
                        typeof(Xeno.Core.Database.Postgresql.PostgresqlDatabaseDriver),
                        typeof(Xeno.Core.Database.Mysql.MysqlDatabaseDriver),
                        typeof(Xeno.Core.Database.SqlServer.SqlServerDatabaseDriver)
                    };

                    foreach (Type driverType in driverTypes)
                    {
                        MethodInfo? purgeMethod = driverType.GetMethod("PurgeTenantDataAsync", new[] { typeof(string), typeof(CancellationToken) });
                        if (purgeMethod == null || purgeMethod.DeclaringType == typeof(DatabaseDriverBase)) throw new InvalidOperationException(driverType.Name + " does not override tenant-scoped telemetry purge.");
                    }

                    return Task.CompletedTask;
                }));

            cases.Add(new TestCaseDescriptor(
                suiteId: "Database",
                caseId: "PostgresqlServerTelemetryRoundTrip",
                displayName: "PostgreSQL server telemetry persists and rolls up when a test connection is provided",
                executeAsync: async delegate
                {
                    string? connectionString = Environment.GetEnvironmentVariable("XENO_TEST_POSTGRESQL_CONNECTION_STRING");
                    if (String.IsNullOrWhiteSpace(connectionString)) return;
                    await RunServerTelemetryRoundTripAsync(new DatabaseSettings
                    {
                        Type = DatabaseTypeEnum.Postgresql,
                        ConnectionString = connectionString
                    }, "postgresql").ConfigureAwait(false);
                }));

            cases.Add(new TestCaseDescriptor(
                suiteId: "Database",
                caseId: "MysqlServerTelemetryRoundTrip",
                displayName: "MySQL server telemetry persists and rolls up when a test connection is provided",
                executeAsync: async delegate
                {
                    string? connectionString = Environment.GetEnvironmentVariable("XENO_TEST_MYSQL_CONNECTION_STRING");
                    if (String.IsNullOrWhiteSpace(connectionString)) return;
                    await RunServerTelemetryRoundTripAsync(new DatabaseSettings
                    {
                        Type = DatabaseTypeEnum.Mysql,
                        ConnectionString = connectionString
                    }, "mysql").ConfigureAwait(false);
                }));

            cases.Add(new TestCaseDescriptor(
                suiteId: "Database",
                caseId: "SqlServerServerTelemetryRoundTrip",
                displayName: "SQL Server telemetry persists and rolls up when a test connection is provided",
                executeAsync: async delegate
                {
                    string? connectionString = Environment.GetEnvironmentVariable("XENO_TEST_SQLSERVER_CONNECTION_STRING");
                    if (String.IsNullOrWhiteSpace(connectionString)) return;
                    await RunServerTelemetryRoundTripAsync(new DatabaseSettings
                    {
                        Type = DatabaseTypeEnum.SqlServer,
                        ConnectionString = connectionString
                    }, "sqlserver").ConfigureAwait(false);
                }));

            return new TestSuiteDescriptor("Database", "Database Providers", cases);
        }

        private static async Task RunPlatformStateRoundTripAsync(DatabaseSettings settings, string suffix)
        {
            string marker = suffix + "_" + Guid.NewGuid().ToString("N");
            string tenantId = "ten_state_" + marker;
            string userId = "usr_state_" + marker;
            string credentialId = "cred_state_" + marker;
            string sessionId = "sess_state_" + marker;
            string permissionId = "perm_state_" + marker;
            string roleId = "role_state_" + marker;
            string roleAssignmentId = "rmap_state_" + marker;
            string auditId = "aud_state_" + marker;
            string clientId = "xcl_state_" + marker;
            string modelDefinitionId = "md_state_" + marker;
            string modelConfigurationId = "mc_state_" + marker;
            string routingPolicyId = "rpol_state_" + marker;
            string requestHistoryId = "req_state_" + marker;
            using (DatabaseDriverBase first = await DatabaseDriverFactory.CreateAndInitializeAsync(settings).ConfigureAwait(false))
            {
                AddPlatformStateRoundTripRecords(first.Platform, tenantId, userId, credentialId, sessionId, permissionId, roleId, roleAssignmentId, auditId, clientId, modelDefinitionId, modelConfigurationId, routingPolicyId, requestHistoryId, marker);
                await first.SaveAsync(CancellationToken.None).ConfigureAwait(false);
            }

            using (DatabaseDriverBase second = await DatabaseDriverFactory.CreateAndInitializeAsync(settings).ConfigureAwait(false))
            {
                AssertPlatformStateRoundTrip(second.Platform, tenantId, userId, credentialId, sessionId, permissionId, roleId, roleAssignmentId, auditId, clientId, modelDefinitionId, modelConfigurationId, routingPolicyId, requestHistoryId, marker, settings.Type);
                int removed = second.Platform.Tenants.RemoveAll(item => item.Id == tenantId);
                removed += second.Platform.RemoveTenantScopedRecords(tenantId);
                if (removed < 13) throw new InvalidOperationException(settings.Type + " platform cleanup removed too few records.");
                await second.SaveAsync(CancellationToken.None).ConfigureAwait(false);
            }

            using (DatabaseDriverBase third = await DatabaseDriverFactory.CreateAndInitializeAsync(settings).ConfigureAwait(false))
            {
                if (third.Platform.Tenants.Any(item => item.Id == tenantId)) throw new InvalidOperationException(settings.Type + " platform cleanup did not persist.");
                if (third.Platform.ToState().Users.Any(item => item.TenantId == tenantId)) throw new InvalidOperationException(settings.Type + " platform cleanup left tenant users.");
                if (third.Platform.ToState().Clients.Any(item => item.TenantId == tenantId)) throw new InvalidOperationException(settings.Type + " platform cleanup left tenant clients.");
            }
        }

        private static async Task RunModelTokenLeaderboardRoundTripAsync(DatabaseSettings settings, string suffix)
        {
            string marker = suffix + "_" + Guid.NewGuid().ToString("N");
            string tenantId = "ten_leaderboard_" + marker;
            string otherTenantId = "ten_leaderboard_other_" + marker;
            DateTime nowUtc = DateTime.UtcNow;
            using (DatabaseDriverBase first = await DatabaseDriverFactory.CreateAndInitializeAsync(settings).ConfigureAwait(false))
            {
                first.Platform.Tenants.Add(new Tenant { Id = tenantId, Name = "Leaderboard " + marker });
                first.Platform.Tenants.Add(new Tenant { Id = otherTenantId, Name = "Other Leaderboard " + marker });
                first.Platform.RequestHistory.Add(CreateModelLeaderboardHistory("req_leader_a1_" + marker, tenantId, "leader-alpha", nowUtc.AddMinutes(-20), 40, 60, 100));
                first.Platform.RequestHistory.Add(CreateModelLeaderboardHistory("req_leader_a2_" + marker, tenantId, "leader-alpha", nowUtc.AddMinutes(-10), 10, 20, 0));
                first.Platform.RequestHistory.Add(CreateModelLeaderboardHistory("req_leader_b1_" + marker, tenantId, "leader-beta", nowUtc.AddMinutes(-5), 25, 35, 60));
                first.Platform.RequestHistory.Add(CreateModelLeaderboardHistory("req_leader_old_" + marker, tenantId, "leader-older", nowUtc.AddDays(-5), 1, 1, 2));
                first.Platform.RequestHistory.Add(CreateModelLeaderboardHistory("req_leader_other_" + marker, otherTenantId, "leader-alpha", nowUtc.AddMinutes(-5), 500, 500, 1000));
                first.Platform.RequestHistory.Add(CreateModelLeaderboardHistory("req_leader_blank_" + marker, tenantId, null, nowUtc.AddMinutes(-5), 100, 100, 200));
                await first.SaveAsync(CancellationToken.None).ConfigureAwait(false);
            }

            using (DatabaseDriverBase second = await DatabaseDriverFactory.CreateAndInitializeAsync(settings).ConfigureAwait(false))
            {
                ModelTokenLeaderboardResponse globalPage = await second.GetModelTokenLeaderboardAsync(new ModelTokenLeaderboardRequest
                {
                    FromUtc = nowUtc.AddHours(-1),
                    ToUtc = nowUtc.AddHours(1),
                    Limit = 1,
                    Offset = 0
                }, CancellationToken.None).ConfigureAwait(false);

                if (globalPage.Total != 2) throw new InvalidOperationException(settings.Type + " global leaderboard total should report two matching model records before pagination.");
                KeyValuePair<string, ModelTokenUsage> globalModel = globalPage.Models.FirstOrDefault();
                if (!String.Equals(globalModel.Key, "leader-alpha", StringComparison.Ordinal)) throw new InvalidOperationException(settings.Type + " global leaderboard did not rank the highest-token model first.");
                if (globalModel.Value.TotalTokens != 1130 || globalModel.Value.PromptTokens != 550 || globalModel.Value.OutputTokens != 580) throw new InvalidOperationException(settings.Type + " global leaderboard did not aggregate token totals across tenants.");

                ModelTokenLeaderboardResponse firstPage = await second.GetModelTokenLeaderboardAsync(new ModelTokenLeaderboardRequest
                {
                    TenantId = tenantId,
                    FromUtc = nowUtc.AddHours(-1),
                    ToUtc = nowUtc.AddHours(1),
                    Limit = 1,
                    Offset = 0
                }, CancellationToken.None).ConfigureAwait(false);

                if (firstPage.Total != 2) throw new InvalidOperationException(settings.Type + " leaderboard total should report two matching model records before pagination.");
                if (firstPage.Limit != 1) throw new InvalidOperationException(settings.Type + " leaderboard did not report the applied limit.");
                if (firstPage.Offset != 0) throw new InvalidOperationException(settings.Type + " leaderboard did not report the applied offset.");
                if (firstPage.Models.Count != 1) throw new InvalidOperationException(settings.Type + " leaderboard first page returned the wrong model count.");
                KeyValuePair<string, ModelTokenUsage> firstModel = firstPage.Models.FirstOrDefault();
                if (!String.Equals(firstModel.Key, "leader-alpha", StringComparison.Ordinal)) throw new InvalidOperationException(settings.Type + " leaderboard did not rank the highest-token model first.");
                if (firstModel.Value.TotalTokens != 130 || firstModel.Value.PromptTokens != 50 || firstModel.Value.OutputTokens != 80) throw new InvalidOperationException(settings.Type + " leaderboard did not aggregate alpha token totals.");

                ModelTokenLeaderboardResponse secondPage = await second.GetModelTokenLeaderboardAsync(new ModelTokenLeaderboardRequest
                {
                    TenantId = tenantId,
                    FromUtc = nowUtc.AddHours(-1),
                    ToUtc = nowUtc.AddHours(1),
                    Limit = 1,
                    Offset = 1
                }, CancellationToken.None).ConfigureAwait(false);

                if (secondPage.Total != 2) throw new InvalidOperationException(settings.Type + " leaderboard second page did not preserve total.");
                if (secondPage.Offset != 1) throw new InvalidOperationException(settings.Type + " leaderboard second page did not report the applied offset.");
                KeyValuePair<string, ModelTokenUsage> secondModel = secondPage.Models.FirstOrDefault();
                if (!String.Equals(secondModel.Key, "leader-beta", StringComparison.Ordinal)) throw new InvalidOperationException(settings.Type + " leaderboard second page returned the wrong model.");
                if (secondModel.Value.TotalTokens != 60 || secondModel.Value.PromptTokens != 25 || secondModel.Value.OutputTokens != 35) throw new InvalidOperationException(settings.Type + " leaderboard did not aggregate beta token totals.");

                ModelTokenLeaderboardResponse emptyPage = await second.GetModelTokenLeaderboardAsync(new ModelTokenLeaderboardRequest
                {
                    TenantId = otherTenantId,
                    FromUtc = nowUtc.AddHours(-1),
                    ToUtc = nowUtc.AddHours(1),
                    Limit = 10,
                    Offset = 1
                }, CancellationToken.None).ConfigureAwait(false);

                if (emptyPage.Total != 1 || emptyPage.Models.Count != 0) throw new InvalidOperationException(settings.Type + " leaderboard pagination total should include skipped matching records.");

                second.Platform.Tenants.RemoveAll(item => item.Id == tenantId || item.Id == otherTenantId);
                second.Platform.RequestHistory.RemoveAll(item => item.Id.Contains(marker, StringComparison.Ordinal));
                await second.SaveAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }

        private static RequestHistoryEntry CreateModelLeaderboardHistory(string id, string tenantId, string? model, DateTime createdUtc, int promptTokens, int outputTokens, int totalTokens)
        {
            RequestHistoryEntry entry = new RequestHistoryEntry
            {
                Id = id,
                TenantId = tenantId,
                RequestorTenantId = tenantId,
                Path = "/v1/chat/completions",
                Method = "POST",
                Model = model,
                StatusCode = 200,
                CreatedUtc = createdUtc,
                CompletedUtc = createdUtc.AddMilliseconds(25)
            };
            entry.Telemetry.PromptTokens = promptTokens;
            entry.Telemetry.CompletionTokens = outputTokens;
            entry.Telemetry.TotalTokens = totalTokens;
            return entry;
        }

        private static void AddPlatformStateRoundTripRecords(XenoPlatform platform, string tenantId, string userId, string credentialId, string sessionId, string permissionId, string roleId, string roleAssignmentId, string auditId, string clientId, string modelDefinitionId, string modelConfigurationId, string routingPolicyId, string requestHistoryId, string marker)
        {
            platform.Tenants.Add(new Tenant { Id = tenantId, Name = "Tenant " + marker });
            platform.Users.Add(new User { Id = userId, TenantId = tenantId, Username = "user_" + marker, Email = marker + "@xeno.test", IsAdmin = false, IsTenantAdmin = true, Active = true });
            platform.Credentials.Add(new Credential { Id = credentialId, TenantId = tenantId, UserId = userId, AccessKey = "access_" + marker, SecretKey = "secret_" + marker, Active = true });
            platform.AuthSessions.Add(new AuthSession { Id = sessionId, TenantId = tenantId, UserId = userId, CredentialId = credentialId, TokenHash = "token_" + marker, AuthScheme = AuthSchemeEnum.Credential, Active = true });
            platform.Permissions.Add(new Permission { Id = permissionId, TenantId = tenantId, ResourceType = "StateRoundTrip", OperationType = OperationTypeEnum.Write, Permit = true, BuiltIn = false, Active = true });
            platform.Roles.Add(new Role { Id = roleId, TenantId = tenantId, Name = "StateRole_" + marker, PermissionIds = new List<string> { permissionId }, Active = true });
            platform.RoleAssignments.Add(new RoleAssignment { Id = roleAssignmentId, TenantId = tenantId, RoleId = roleId, PrincipalType = PrincipalTypeEnum.User, PrincipalId = userId, Active = true });
            platform.AuditRecords.Add(new AuditRecord { Id = auditId, TenantId = tenantId, TargetTenantId = tenantId, TargetResourceId = roleId, RequestId = "audit_request_" + marker, PrincipalType = PrincipalTypeEnum.User, PrincipalId = userId, AuthScheme = AuthSchemeEnum.Credential, ResourceType = "StateRoundTrip", OperationType = OperationTypeEnum.Write, Allowed = true, ReasonCode = "RoundTrip", StatusCode = 202 });

            XenoClientRecord client = new XenoClientRecord
            {
                Id = clientId,
                TenantId = tenantId,
                Name = "client_" + marker,
                OwnerUserId = userId,
                OwnerCredentialId = credentialId,
                OwnerPrincipalType = PrincipalTypeEnum.User,
                Connected = true,
                ServiceState = ClientServiceStateEnum.Normal,
                MaxParallelRequests = 9,
                Weight = 77
            };
            client.Models.Add(new XenoClientModel { Name = "model_" + marker, Provider = ApiProviderTypeEnum.Ollama, Source = "source_" + marker, SupportsCompletions = true, SupportsEmbeddings = true });
            platform.Clients.Add(client);

            platform.ModelDefinitions.Add(new ModelDefinition { Id = modelDefinitionId, TenantId = tenantId, Name = "model_" + marker, SupportsCompletions = true, SupportsEmbeddings = true, Labels = new List<string> { "state", marker } });
            ModelConfiguration modelConfiguration = new ModelConfiguration { Id = modelConfigurationId, TenantId = tenantId, Name = "config_" + marker, Model = "model_" + marker };
            modelConfiguration.PinnedCompletionsProperties["temperature"] = 0.25;
            modelConfiguration.PinnedCompletionsProperties["marker"] = marker;
            platform.ModelConfigurations.Add(modelConfiguration);

            RoutingPolicy routingPolicy = new RoutingPolicy
            {
                Id = routingPolicyId,
                TenantId = tenantId,
                Name = "policy_" + marker,
                ModelName = "model_" + marker,
                ClientName = "client_" + marker,
                RequestTimeoutMs = 123456,
                LoadBalancingMode = LoadBalancingModeEnum.Random,
                MaxTelemetryAgeMs = 45000,
                TelemetryFallbackMode = RoutingPolicyFallbackModeEnum.FailClosed,
                Active = true
            };
            routingPolicy.TelemetryFilters.Add(new RoutingPolicyCondition { Metric = "telemetry.cpu.utilizationPercent", Operator = RoutingPolicyOperatorEnum.LessThanOrEqual, ValueType = RoutingPolicyMetricValueTypeEnum.Number, Value = "75" });
            routingPolicy.TelemetryRanking.Add(new RoutingPolicyRankingRule { Metric = "provider.queuedRequests", Direction = RoutingPolicyRankingDirectionEnum.Ascending, Weight = 2 });
            platform.RoutingPolicies.Add(routingPolicy);

            RequestHistoryEntry request = new RequestHistoryEntry
            {
                Id = requestHistoryId,
                TenantId = tenantId,
                RequestorTenantId = tenantId,
                Path = "/v1/chat/completions",
                Method = "POST",
                Model = "model_" + marker,
                SelectedClientId = clientId,
                StatusCode = 201,
                DurationMs = 321,
                SourceIp = "127.0.0.1",
                UserId = userId,
                CredentialId = credentialId,
                SessionId = sessionId,
                PrincipalType = PrincipalTypeEnum.User,
                RequestorPrincipalType = PrincipalTypeEnum.User,
                RequestorPrincipalId = userId,
                AuthScheme = AuthSchemeEnum.Credential,
                RequestBody = "{\"marker\":\"" + marker + "\"}",
                ResponseBody = "{\"ok\":true}",
                RequestBodyLength = 20,
                ResponseBodyLength = 11
            };
            request.RequestHeaders["x-marker"] = marker;
            request.ResponseHeaders["x-result"] = "ok";
            request.Telemetry.TotalTimeMs = 321;
            request.Telemetry.ProviderRequestId = "provider_" + marker;
            request.Telemetry.Metadata["marker"] = marker;
            request.Timeline.Add(new RequestHistoryTimelineEvent { Stage = RequestHistoryTimelineStageEnum.ClientSelection, Message = "selected", ElapsedMs = 5, Success = true });
            platform.RequestHistory.Add(request);
        }

        private static void AssertPlatformStateRoundTrip(XenoPlatform platform, string tenantId, string userId, string credentialId, string sessionId, string permissionId, string roleId, string roleAssignmentId, string auditId, string clientId, string modelDefinitionId, string modelConfigurationId, string routingPolicyId, string requestHistoryId, string marker, DatabaseTypeEnum provider)
        {
            if (!platform.Tenants.Any(item => item.Id == tenantId && item.Name == "Tenant " + marker)) throw new InvalidOperationException(provider + " tenant did not round-trip.");
            if (!platform.Users.Any(item => item.Id == userId && item.TenantId == tenantId && item.Email == marker + "@xeno.test")) throw new InvalidOperationException(provider + " user did not round-trip.");
            if (!platform.Credentials.Any(item => item.Id == credentialId && item.UserId == userId && item.AccessKey == "access_" + marker)) throw new InvalidOperationException(provider + " credential did not round-trip.");
            if (!platform.AuthSessions.Any(item => item.Id == sessionId && item.UserId == userId && item.CredentialId == credentialId)) throw new InvalidOperationException(provider + " auth session did not round-trip.");
            if (!platform.Permissions.Any(item => item.Id == permissionId && item.ResourceType == "StateRoundTrip" && item.OperationType == OperationTypeEnum.Write)) throw new InvalidOperationException(provider + " permission did not round-trip.");
            if (!platform.Roles.Any(item => item.Id == roleId && item.PermissionIds.Contains(permissionId))) throw new InvalidOperationException(provider + " role did not round-trip.");
            if (!platform.RoleAssignments.Any(item => item.Id == roleAssignmentId && item.RoleId == roleId && item.PrincipalId == userId)) throw new InvalidOperationException(provider + " role assignment did not round-trip.");
            if (!platform.AuditRecords.Any(item => item.Id == auditId && item.RequestId == "audit_request_" + marker && item.StatusCode == 202)) throw new InvalidOperationException(provider + " audit record did not round-trip.");

            XenoClientRecord client = platform.Clients.FirstOrDefault(item => item.Id == clientId) ?? throw new InvalidOperationException(provider + " client did not round-trip.");
            if (client.OwnerUserId != userId || client.Weight != 77 || !client.Models.Any(item => item.Name == "model_" + marker && item.SupportsEmbeddings)) throw new InvalidOperationException(provider + " client details did not round-trip.");
            ModelDefinition modelDefinition = platform.ModelDefinitions.FirstOrDefault(item => item.Id == modelDefinitionId) ?? throw new InvalidOperationException(provider + " model definition did not round-trip.");
            if (!modelDefinition.Labels.Contains(marker)) throw new InvalidOperationException(provider + " model definition labels did not round-trip.");
            ModelConfiguration modelConfiguration = platform.ModelConfigurations.FirstOrDefault(item => item.Id == modelConfigurationId) ?? throw new InvalidOperationException(provider + " model configuration did not round-trip.");
            if (!modelConfiguration.PinnedCompletionsProperties.ContainsKey("marker")) throw new InvalidOperationException(provider + " model configuration pinned properties did not round-trip.");
            RoutingPolicy routingPolicy = platform.RoutingPolicies.FirstOrDefault(item => item.Id == routingPolicyId) ?? throw new InvalidOperationException(provider + " routing policy did not round-trip.");
            if (routingPolicy.TelemetryFilters.Count != 1 || routingPolicy.TelemetryRanking.Count != 1 || routingPolicy.MaxTelemetryAgeMs != 45000) throw new InvalidOperationException(provider + " routing policy telemetry settings did not round-trip.");
            RequestHistoryEntry request = platform.RequestHistory.FirstOrDefault(item => item.Id == requestHistoryId) ?? throw new InvalidOperationException(provider + " request history did not round-trip.");
            if (request.Telemetry.ProviderRequestId != "provider_" + marker || !request.Telemetry.Metadata.ContainsKey("marker") || request.Timeline.Count != 1) throw new InvalidOperationException(provider + " request history detail did not round-trip.");
        }

        private static async Task RunServerTelemetryRoundTripAsync(DatabaseSettings settings, string suffix)
        {
            string tenantId = "ten_telemetry_" + suffix + "_" + Guid.NewGuid().ToString("N");
            string clientId = "xcl_telemetry_" + suffix;
            DateTime sampleUtc = DateTime.UtcNow.AddMinutes(-5);
            using (DatabaseDriverBase driver = await DatabaseDriverFactory.CreateAndInitializeAsync(settings).ConfigureAwait(false))
            {
                if (driver.ClientTelemetry == null) throw new InvalidOperationException(settings.Type + " client telemetry driver was not initialized.");
                if (driver.ClientProviderTelemetry == null) throw new InvalidOperationException(settings.Type + " client provider telemetry driver was not initialized.");
                if (driver.ClientTelemetryCollectionAttempts == null) throw new InvalidOperationException(settings.Type + " telemetry collection attempts driver was not initialized.");

                Xeno.Core.Models.Telemetry.ClientTelemetrySnapshot hostSnapshot = new Xeno.Core.Models.Telemetry.ClientTelemetrySnapshot
                {
                    Metadata = new Xeno.Core.Models.Telemetry.ClientTelemetryMetadata
                    {
                        ClientId = clientId,
                        MachineName = suffix + "-client",
                        CollectedUtc = sampleUtc,
                        CollectionElapsedMs = 11
                    },
                    Cpu = new Xeno.Core.Models.Telemetry.ClientCpuTelemetry
                    {
                        UtilizationPercent = 37
                    },
                    Memory = new Xeno.Core.Models.Telemetry.ClientMemoryTelemetry
                    {
                        TotalBytes = 1000,
                        UsedBytes = 410,
                        AvailableBytes = 590,
                        UtilizationPercent = 41
                    },
                    Disk = new Xeno.Core.Models.Telemetry.ClientDiskTelemetry
                    {
                        TotalBytes = 1000,
                        UsedBytes = 520,
                        AvailableBytes = 480,
                        UtilizationPercent = 52
                    },
                    Gpu = new Xeno.Core.Models.Telemetry.ClientGpuTelemetry
                    {
                        AverageUtilizationPercent = 13
                    }
                };

                await driver.ClientTelemetry.CreateHostSampleAsync(tenantId, clientId, suffix + "-client", "telreq_" + suffix, hostSnapshot, null, CancellationToken.None).ConfigureAwait(false);

                Xeno.Core.Models.Telemetry.ClientProviderTelemetrySnapshot providerSnapshot = new Xeno.Core.Models.Telemetry.ClientProviderTelemetrySnapshot
                {
                    Metadata = new Xeno.Core.Models.Telemetry.ClientProviderTelemetryMetadata
                    {
                        ClientId = clientId,
                        MachineName = suffix + "-client",
                        CollectedUtc = sampleUtc,
                        ProviderCount = 1
                    }
                };
                providerSnapshot.Providers.Add(new Xeno.Core.Models.Telemetry.ClientProviderTelemetryProviderStatus
                {
                    Endpoint = new Xeno.Core.Models.Telemetry.ClientProviderTelemetryEndpoint
                    {
                        ProviderName = suffix + "-provider",
                        ApiType = "Ollama",
                        Kind = Xeno.Core.Models.Telemetry.ClientProviderTelemetryKindEnum.Ollama,
                        EndpointDisplay = "http://127.0.0.1:11434",
                        EndpointHash = suffix + "hash"
                    },
                    Status = Xeno.Core.Models.Telemetry.ClientProviderTelemetryStatusEnum.Available,
                    HealthStatus = Xeno.Core.Models.Telemetry.ClientProviderTelemetryHealthStatusEnum.Healthy,
                    CollectedUtc = sampleUtc,
                    AvailableModelCount = 5,
                    LoadedModelCount = 2,
                    ActiveRequests = 4,
                    QueuedRequests = 1,
                    LatencyP95Ms = 95
                });

                await driver.ClientProviderTelemetry.CreateProviderSnapshotAsync(tenantId, clientId, suffix + "-client", "telreq_" + suffix, providerSnapshot, false, CancellationToken.None).ConfigureAwait(false);

                Xeno.Core.Models.Telemetry.ServerClientTelemetryCollectionAttemptRecord attempt = new Xeno.Core.Models.Telemetry.ServerClientTelemetryCollectionAttemptRecord
                {
                    TenantId = tenantId,
                    ClientId = clientId,
                    ClientName = suffix + "-client",
                    HostRequested = true,
                    ProviderRequested = true,
                    Status = "Started"
                };
                await driver.ClientTelemetryCollectionAttempts.CreateAttemptAsync(attempt, CancellationToken.None).ConfigureAwait(false);
                attempt.CompletedUtc = DateTime.UtcNow;
                attempt.ElapsedMs = 123;
                attempt.HostSucceeded = true;
                attempt.ProviderSucceeded = true;
                attempt.Status = "Completed";
                attempt.ProviderSampleCount = 1;
                await driver.ClientTelemetryCollectionAttempts.CompleteAttemptAsync(attempt, CancellationToken.None).ConfigureAwait(false);

                DateTime fromUtc = sampleUtc.AddHours(-1);
                DateTime toUtc = sampleUtc.AddHours(1);
                IReadOnlyList<Xeno.Core.Models.Telemetry.ServerClientTelemetryRollupBucket> hostBuckets = await driver.ClientTelemetry.RollupHostAsync(new Xeno.Core.Models.Telemetry.ServerClientTelemetryRollupRequest
                {
                    TenantId = tenantId,
                    ClientId = clientId,
                    FromUtc = fromUtc,
                    ToUtc = toUtc,
                    BucketMinutes = 5,
                    Metric = Xeno.Core.Models.Telemetry.ClientTelemetryMetricEnum.CpuUtilizationPercent
                }, CancellationToken.None).ConfigureAwait(false);
                if (!hostBuckets.Any(item => item.Count > 0 && Math.Abs((item.Average ?? 0) - 37) < 0.001)) throw new InvalidOperationException(settings.Type + " host telemetry rollup did not include persisted CPU value.");

                IReadOnlyList<Xeno.Core.Models.Telemetry.ServerProviderTelemetryRollupBucket> providerBuckets = await driver.ClientProviderTelemetry.RollupProviderAsync(new Xeno.Core.Models.Telemetry.ServerProviderTelemetryRollupRequest
                {
                    TenantId = tenantId,
                    ClientId = clientId,
                    ProviderName = suffix + "-provider",
                    FromUtc = fromUtc,
                    ToUtc = toUtc,
                    BucketMinutes = 5,
                    Metric = Xeno.Core.Models.Telemetry.ClientProviderTelemetryMetricEnum.ActiveRequests
                }, CancellationToken.None).ConfigureAwait(false);
                if (!providerBuckets.Any(item => item.Count > 0 && Math.Abs((item.Average ?? 0) - 4) < 0.001)) throw new InvalidOperationException(settings.Type + " provider telemetry rollup did not include persisted active request value.");

                IReadOnlyList<Xeno.Core.Models.Telemetry.ServerClientTelemetryCollectionAttemptRecord> attempts = await driver.ClientTelemetryCollectionAttempts.SearchAttemptsAsync(tenantId, clientId, 10, CancellationToken.None).ConfigureAwait(false);
                Xeno.Core.Models.Telemetry.ServerClientTelemetryCollectionAttemptRecord storedAttempt = attempts.FirstOrDefault() ?? throw new InvalidOperationException(settings.Type + " telemetry collection attempt was not returned.");
                if (!storedAttempt.HostSucceeded || !storedAttempt.ProviderSucceeded) throw new InvalidOperationException(settings.Type + " telemetry attempt success flags did not round-trip.");

                await driver.PurgeTenantDataAsync(tenantId, CancellationToken.None).ConfigureAwait(false);
                Xeno.Core.Models.Telemetry.ServerClientTelemetrySearchResult remaining = await driver.ClientTelemetry.SearchHostAsync(new Xeno.Core.Models.Telemetry.ServerClientTelemetrySearchRequest
                {
                    TenantId = tenantId,
                    ClientId = clientId,
                    Limit = 10
                }, CancellationToken.None).ConfigureAwait(false);
                if (remaining.Items.Count != 0) throw new InvalidOperationException(settings.Type + " telemetry tenant purge left host samples.");
            }
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
                            Credential credential = platform.Credentials[0];
                            RequestContext context = new RequestContext
                            {
                                TenantId = Constants.DefaultTenantId,
                                UserId = credential.UserId,
                                CredentialId = credential.Id,
                                PrincipalType = PrincipalTypeEnum.Credential,
                                AuthScheme = AuthSchemeEnum.Credential,
                                IsAuthenticated = true
                            };
                            RoutingDecision decision = await routing.RouteAsync(context, "llama3.2:latest", false).ConfigureAwait(false);
                            if (!decision.Success) throw new InvalidOperationException(decision.Message);
                            RequestHistoryEntry entry = routing.RecordHistory(context, "/v1/chat/completions", "POST", decision);
                            if (!String.Equals(entry.RequestorTenantId, Constants.DefaultTenantId, StringComparison.Ordinal)) throw new InvalidOperationException("Request history missing requestor tenant ID.");
                            if (entry.RequestorPrincipalType != PrincipalTypeEnum.Credential) throw new InvalidOperationException("Request history missing credential requestor principal type.");
                            if (!String.Equals(entry.RequestorPrincipalId, credential.Id, StringComparison.Ordinal)) throw new InvalidOperationException("Request history missing credential requestor principal ID.");
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
                        }),
                    new TestCaseDescriptor(
                        "TenantIsolation",
                        "TenantScopedResourceMatrix",
                        "Tenant scoped resources do not leak across clients, credentials, roles, permissions, audit, or history",
                        executeAsync: async delegate
                        {
                            XenoPlatform platform = new XenoPlatform();
                            await platform.InitializeAsync().ConfigureAwait(false);
                            Tenant otherTenant = new Tenant { Id = "ten_security_matrix", Name = "Security Matrix", Active = true };
                            platform.Tenants.Add(otherTenant);
                            platform.EnsureTenantDefaults(otherTenant.Id);
                            platform.Clients.Add(new XenoClientRecord { TenantId = otherTenant.Id, Name = "other-tenant-client", Connected = true });
                            platform.Credentials.Add(new Credential { TenantId = otherTenant.Id, UserId = "usr_other", AccessKey = "other-access", Active = true });
                            platform.RequestHistory.Add(new RequestHistoryEntry { TenantId = otherTenant.Id, Path = "/v1/chat/completions", Method = "POST", RequestorTenantId = otherTenant.Id });
                            platform.AuditRecords.Add(new AuditRecord { TenantId = otherTenant.Id, ResourceType = "Users", OperationType = OperationTypeEnum.Read, Allowed = false, StatusCode = 403 });

                            if (platform.Clients.Any(item => item.TenantId == Constants.DefaultTenantId && String.Equals(item.Name, "other-tenant-client", StringComparison.Ordinal))) throw new InvalidOperationException("Client inventory leaked across tenants.");
                            if (platform.Credentials.Any(item => item.TenantId == Constants.DefaultTenantId && String.Equals(item.AccessKey, "other-access", StringComparison.Ordinal))) throw new InvalidOperationException("Credential leaked across tenants.");
                            if (platform.RequestHistory.Any(item => item.TenantId == Constants.DefaultTenantId && String.Equals(item.RequestorTenantId, otherTenant.Id, StringComparison.Ordinal))) throw new InvalidOperationException("Request history leaked across tenants.");
                            if (platform.AuditRecords.Any(item => item.TenantId == Constants.DefaultTenantId && String.Equals(item.ResourceType, "Users", StringComparison.Ordinal) && item.StatusCode == 403)) throw new InvalidOperationException("Audit record leaked across tenants.");
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
                        "ModelTokenLeaderboardPublicSmoke",
                        "Model token leaderboard returns ranked public token totals with total count",
                        executeAsync: delegate { return RunModelTokenLeaderboardPublicSmokeAsync(); }),
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
                        "SecurityManagementContracts",
                        "Security-sensitive management and OpenAI routes enforce admin flags, tenant fences, RBAC, and authentication",
                        executeAsync: delegate { return RunSecurityManagementContractsAsync(); }),
                    new TestCaseDescriptor(
                        "ManagementApi",
                        "ManagementAuthorizationMatrixContracts",
                        "Protected management and inference routes enforce anonymous, unprivileged, and administrator access decisions",
                        executeAsync: delegate { return RunManagementAuthorizationMatrixContractsAsync(); }),
                    new TestCaseDescriptor(
                        "ManagementApi",
                        "CredentialLifecycleSecurityContracts",
                        "Credential lifecycle rejects secret-key exchange, tenant mismatch, disabled credentials, and stale sessions",
                        executeAsync: delegate { return RunCredentialLifecycleSecurityContractsAsync(); }),
                    new TestCaseDescriptor(
                        "ManagementApi",
                        "AuthRateLimitContracts",
                        "Authentication login endpoints enforce source IP rate limits and return 429",
                        executeAsync: delegate { return RunAuthRateLimitContractsAsync(); }),
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
                        string baseUrl = "http://127.0.0.1:" + port;
                        await AuthenticateSeedAdminAsync(httpClient, baseUrl).ConfigureAwait(false);
                        string settingsBody = await httpClient.GetStringAsync(baseUrl + "/v1.0/api/settings").ConfigureAwait(false);
                        if (!settingsBody.Contains("settingsFile", StringComparison.Ordinal)) throw new InvalidOperationException("Settings response missing settingsFile.");

                        using (HttpResponseMessage saveSettingsResponse = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/settings", JsonUtil.Serialize(settings)).ConfigureAwait(false))
                        {
                            string saveSettingsBody = await saveSettingsResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!saveSettingsResponse.IsSuccessStatusCode || !File.Exists(settingsFile)) throw new InvalidOperationException("Settings save failed: " + saveSettingsBody);
                        }

                        string backup = await httpClient.GetStringAsync(baseUrl + "/v1.0/api/backup").ConfigureAwait(false);
                        if (!backup.Contains("llama3.2:latest", StringComparison.Ordinal)) throw new InvalidOperationException("Backup did not include advertised model.");

                        string clients = await httpClient.GetStringAsync(baseUrl + "/v1.0/api/clients").ConfigureAwait(false);
                        if (!clients.Contains("llama3.2:latest", StringComparison.Ordinal)) throw new InvalidOperationException("Clients response missing advertised model.");

                        string routingPolicies = await httpClient.GetStringAsync(baseUrl + "/v1.0/api/routing-policies").ConfigureAwait(false);
                        if (!routingPolicies.Contains("Default Routing Policy", StringComparison.Ordinal)) throw new InvalidOperationException("Routing policies response missing default policy.");

                        string analytics = await httpClient.GetStringAsync(baseUrl + "/v1.0/api/analytics/overview").ConfigureAwait(false);
                        if (!analytics.Contains("failureRate", StringComparison.Ordinal)) throw new InvalidOperationException("Analytics overview missing failureRate.");

                        using (HttpResponseMessage completionResponse = await PostJsonAsync(httpClient, baseUrl + "/v1/chat/completions", "{\"model\":\"missing-model\",\"messages\":[{\"role\":\"user\",\"content\":\"hello\"}]}").ConfigureAwait(false))
                        {
                            string completionBody = await completionResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if ((int)completionResponse.StatusCode < 400 || !completionBody.Contains("ModelNotFound", StringComparison.Ordinal)) throw new InvalidOperationException("Expected missing model request to be recorded as a failed request: " + completionBody);
                        }

                        string summary = await httpClient.GetStringAsync(baseUrl + "/v1.0/api/request-history/summary").ConfigureAwait(false);
                        if (!summary.Contains("\"count\"", StringComparison.Ordinal) || !summary.Contains("\"errors\"", StringComparison.Ordinal) || !summary.Contains("\"success\"", StringComparison.Ordinal) || !summary.Contains("\"failed\"", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Request history summary missing chart bucket fields: " + summary);
                        }

                        string requestHistory = await httpClient.GetStringAsync(baseUrl + "/v1.0/api/request-history").ConfigureAwait(false);
                        if (!requestHistory.Contains("\"total\"", StringComparison.Ordinal) || !requestHistory.Contains("\"items\"", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Request history list missing paged result fields: " + requestHistory);
                        }

                        if (!requestHistory.Contains("/v1/chat/completions", StringComparison.Ordinal) || !requestHistory.Contains("ModelNotFound", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Request history list missing recorded OpenAI request: " + requestHistory);
                        }

                        string metrics = await httpClient.GetStringAsync(baseUrl + "/metrics").ConfigureAwait(false);
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

        private static async Task RunSecurityManagementContractsAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-security-contracts-" + Guid.NewGuid().ToString("N") + ".db");
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
                    string baseUrl = "http://127.0.0.1:" + port;

                    using (HttpClient anonymousClient = new HttpClient())
                    using (HttpResponseMessage anonymousModels = await anonymousClient.GetAsync(baseUrl + "/v1/models").ConfigureAwait(false))
                    {
                        if ((int)anonymousModels.StatusCode != 401) throw new InvalidOperationException("Anonymous OpenAI model list should return 401.");
                    }

                    using (HttpClient systemClient = new HttpClient())
                    {
                        await AuthenticateSeedAdminAsync(systemClient, baseUrl).ConfigureAwait(false);

                        using (HttpResponseMessage grantableResponse = await systemClient.GetAsync(baseUrl + "/v1.0/api/permissions/grantable").ConfigureAwait(false))
                        {
                            string grantableJson = await grantableResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!grantableResponse.IsSuccessStatusCode) throw new InvalidOperationException("System admin could not list grantable permissions: " + grantableJson);
                            if (!grantableJson.Contains("Inference", StringComparison.Ordinal) || !grantableJson.Contains("Execute", StringComparison.Ordinal)) throw new InvalidOperationException("Grantable permissions missing inference execute permission: " + grantableJson);
                        }

                        string permissionsJson = await systemClient.GetStringAsync(baseUrl + "/v1.0/api/permissions").ConfigureAwait(false);
                        List<Permission> permissions = DeserializeRequired<List<Permission>>(permissionsJson);
                        Permission? builtInPermission = permissions.FirstOrDefault(item => item.BuiltIn);
                        if (builtInPermission == null) throw new InvalidOperationException("Seeded platform did not expose a built-in permission.");
                        using (HttpResponseMessage updateBuiltInPermission = await PutJsonAsync(systemClient, baseUrl + "/v1.0/api/permissions/" + Uri.EscapeDataString(builtInPermission.Id), JsonUtil.Serialize(new
                        {
                            active = false
                        })).ConfigureAwait(false))
                        {
                            if ((int)updateBuiltInPermission.StatusCode != 403) throw new InvalidOperationException("Built-in permission update should return 403: " + (int)updateBuiltInPermission.StatusCode);
                        }

                        using (HttpResponseMessage deleteBuiltInPermission = await systemClient.DeleteAsync(baseUrl + "/v1.0/api/permissions/" + Uri.EscapeDataString(builtInPermission.Id)).ConfigureAwait(false))
                        {
                            if ((int)deleteBuiltInPermission.StatusCode != 403) throw new InvalidOperationException("Built-in permission delete should return 403: " + (int)deleteBuiltInPermission.StatusCode);
                        }

                        string seededAdminId = await FirstRecordIdAsync(systemClient, baseUrl + "/v1.0/api/users").ConfigureAwait(false);
                        using (HttpResponseMessage lastAdminSelfDemotion = await PutJsonAsync(systemClient, baseUrl + "/v1.0/api/users/" + Uri.EscapeDataString(seededAdminId), JsonUtil.Serialize(new
                        {
                            isAdmin = false
                        })).ConfigureAwait(false))
                        {
                            string lastAdminSelfDemotionJson = await lastAdminSelfDemotion.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if ((int)lastAdminSelfDemotion.StatusCode != 403) throw new InvalidOperationException("Last system admin should not self-demote: " + (int)lastAdminSelfDemotion.StatusCode + " " + lastAdminSelfDemotionJson);
                            if (!lastAdminSelfDemotionJson.Contains("LastSystemAdminProtected", StringComparison.Ordinal)) throw new InvalidOperationException("Last system admin self-demotion did not return the expected reason: " + lastAdminSelfDemotionJson);
                        }

                        await CreateUserViaApiAsync(systemClient, baseUrl, Constants.DefaultTenantId, "backup-system-admin@security.test", "password", true, false).ConfigureAwait(false);
                        using (HttpResponseMessage allowedSelfDemotion = await PutJsonAsync(systemClient, baseUrl + "/v1.0/api/users/" + Uri.EscapeDataString(seededAdminId), JsonUtil.Serialize(new
                        {
                            isAdmin = false
                        })).ConfigureAwait(false))
                        {
                            string allowedSelfDemotionJson = await allowedSelfDemotion.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!allowedSelfDemotion.IsSuccessStatusCode) throw new InvalidOperationException("System admin self-demotion should be allowed when another system admin exists: " + allowedSelfDemotionJson);
                        }

                        await AuthenticateUserAsync(systemClient, baseUrl, Constants.DefaultTenantId, "backup-system-admin@security.test", "password").ConfigureAwait(false);

                        string tenantBId;
                        using (HttpResponseMessage createTenantB = await PostJsonAsync(systemClient, baseUrl + "/v1.0/api/tenants", JsonUtil.Serialize(new { name = "Security Tenant B", active = true })).ConfigureAwait(false))
                        {
                            string tenantBJson = await createTenantB.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!createTenantB.IsSuccessStatusCode) throw new InvalidOperationException("System admin could not create tenant B: " + tenantBJson);
                            tenantBId = FirstRecordId("[" + tenantBJson + "]");
                        }

                        using (HttpResponseMessage systemTenants = await systemClient.GetAsync(baseUrl + "/v1.0/api/tenants").ConfigureAwait(false))
                        {
                            string systemTenantsJson = await systemTenants.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!systemTenants.IsSuccessStatusCode) throw new InvalidOperationException("System admin could not list tenants: " + systemTenantsJson);
                            List<Tenant> visibleTenants = DeserializeRequired<List<Tenant>>(systemTenantsJson);
                            if (visibleTenants.Count != 2) throw new InvalidOperationException("System admin should see all tenants.");
                            if (!visibleTenants.Any(item => item.Id == Constants.DefaultTenantId)) throw new InvalidOperationException("System admin tenant list omitted the default tenant.");
                            if (!visibleTenants.Any(item => item.Id == tenantBId)) throw new InvalidOperationException("System admin tenant list omitted tenant B.");
                        }

                        string tenantAdminAId = await CreateUserViaApiAsync(systemClient, baseUrl, Constants.DefaultTenantId, "tenant-admin-a@security.test", "password", false, true).ConfigureAwait(false);
                        string noAccessUserId = await CreateUserViaApiAsync(systemClient, baseUrl, Constants.DefaultTenantId, "no-access@security.test", "password", false, false).ConfigureAwait(false);
                        string rbacUserId = await CreateUserViaApiAsync(systemClient, baseUrl, Constants.DefaultTenantId, "rbac-writer@security.test", "password", false, false).ConfigureAwait(false);
                        await CreateUserViaApiAsync(systemClient, baseUrl, tenantBId, "tenant-b-user@security.test", "password", false, false).ConfigureAwait(false);

                        using (HttpResponseMessage systemModels = await systemClient.GetAsync(baseUrl + "/v1/models").ConfigureAwait(false))
                        {
                            string systemModelsJson = await systemModels.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!systemModels.IsSuccessStatusCode) throw new InvalidOperationException("System admin OpenAI model list failed: " + systemModelsJson);
                        }

                        using (HttpClient tenantAdminClient = new HttpClient())
                        {
                            await AuthenticateUserAsync(tenantAdminClient, baseUrl, Constants.DefaultTenantId, "tenant-admin-a@security.test", "password").ConfigureAwait(false);

                            using (HttpResponseMessage tenantAdminTenants = await tenantAdminClient.GetAsync(baseUrl + "/v1.0/api/tenants").ConfigureAwait(false))
                            {
                                string tenantAdminTenantsJson = await tenantAdminTenants.Content.ReadAsStringAsync().ConfigureAwait(false);
                                if (!tenantAdminTenants.IsSuccessStatusCode) throw new InvalidOperationException("Tenant admin could not list visible tenants: " + tenantAdminTenantsJson);
                                List<Tenant> visibleTenants = DeserializeRequired<List<Tenant>>(tenantAdminTenantsJson);
                                if (visibleTenants.Count != 1) throw new InvalidOperationException("Tenant admin should see only one tenant.");
                                if (visibleTenants[0].Id != Constants.DefaultTenantId) throw new InvalidOperationException("Tenant admin should only see their own tenant.");
                            }

                            using (HttpResponseMessage createForeignUser = await PostJsonAsync(tenantAdminClient, baseUrl + "/v1.0/api/users", JsonUtil.Serialize(new
                            {
                                tenantId = tenantBId,
                                username = "foreign-create@security.test",
                                email = "foreign-create@security.test",
                                password = "password",
                                active = true
                            })).ConfigureAwait(false))
                            {
                                string createForeignUserJson = await createForeignUser.Content.ReadAsStringAsync().ConfigureAwait(false);
                                if ((int)createForeignUser.StatusCode != 403) throw new InvalidOperationException("Tenant admin should not create users in another tenant: " + (int)createForeignUser.StatusCode + " " + createForeignUserJson);
                            }

                            using (HttpResponseMessage createTenantAdmin = await PostJsonAsync(tenantAdminClient, baseUrl + "/v1.0/api/users", JsonUtil.Serialize(new
                            {
                                tenantId = Constants.DefaultTenantId,
                                username = "tenant-admin-created@security.test",
                                email = "tenant-admin-created@security.test",
                                password = "password",
                                isTenantAdmin = true,
                                active = true
                            })).ConfigureAwait(false))
                            {
                                string createTenantAdminJson = await createTenantAdmin.Content.ReadAsStringAsync().ConfigureAwait(false);
                                if (!createTenantAdmin.IsSuccessStatusCode) throw new InvalidOperationException("Tenant admin should grant tenant admin in own tenant: " + createTenantAdminJson);
                            }

                            using (HttpResponseMessage createSystemAdmin = await PostJsonAsync(tenantAdminClient, baseUrl + "/v1.0/api/users", JsonUtil.Serialize(new
                            {
                                tenantId = Constants.DefaultTenantId,
                                username = "system-admin-denied@security.test",
                                email = "system-admin-denied@security.test",
                                password = "password",
                                isAdmin = true,
                                active = true
                            })).ConfigureAwait(false))
                            {
                                if ((int)createSystemAdmin.StatusCode != 403) throw new InvalidOperationException("Tenant admin should not grant system admin: " + (int)createSystemAdmin.StatusCode);
                            }

                            tenantAdminClient.DefaultRequestHeaders.Remove("x-tenant-guid");
                            tenantAdminClient.DefaultRequestHeaders.Add("x-tenant-guid", tenantBId);
                            using (HttpResponseMessage tenantMismatchModels = await tenantAdminClient.GetAsync(baseUrl + "/v1/models").ConfigureAwait(false))
                            {
                                if ((int)tenantMismatchModels.StatusCode != 401) throw new InvalidOperationException("Tenant-mismatched bearer token should fail authentication.");
                            }
                        }

                        string usersWritePermissionId = await CreatePermissionViaApiAsync(systemClient, baseUrl, "Users", "Write").ConfigureAwait(false);
                        string usersWriteRoleId = await CreateRoleViaApiAsync(systemClient, baseUrl, "Security Users Writer", usersWritePermissionId).ConfigureAwait(false);
                        await AssignRoleViaApiAsync(systemClient, baseUrl, usersWriteRoleId, PrincipalTypeEnum.User, rbacUserId).ConfigureAwait(false);

                        using (HttpClient rbacClient = new HttpClient())
                        {
                            await AuthenticateUserAsync(rbacClient, baseUrl, Constants.DefaultTenantId, "rbac-writer@security.test", "password").ConfigureAwait(false);
                            using (HttpResponseMessage rbacAdminFlag = await PutJsonAsync(rbacClient, baseUrl + "/v1.0/api/users/" + Uri.EscapeDataString(noAccessUserId), JsonUtil.Serialize(new
                            {
                                isTenantAdmin = true
                            })).ConfigureAwait(false))
                            {
                                if ((int)rbacAdminFlag.StatusCode != 403) throw new InvalidOperationException("RBAC user should not change tenant admin flag: " + (int)rbacAdminFlag.StatusCode);
                            }
                        }

                        using (HttpClient noAccessClient = new HttpClient())
                        {
                            await AuthenticateUserAsync(noAccessClient, baseUrl, Constants.DefaultTenantId, "no-access@security.test", "password").ConfigureAwait(false);
                            using (HttpResponseMessage noAccessModels = await noAccessClient.GetAsync(baseUrl + "/v1/models").ConfigureAwait(false))
                            {
                                if ((int)noAccessModels.StatusCode != 403) throw new InvalidOperationException("User without inference permission should get 403 from OpenAI model list.");
                            }
                        }
                    }
                }
                finally
                {
                    await StopServerAsync(server, serverTask, serverTokenSource, dbFile).ConfigureAwait(false);
                }
            }
        }

        private static async Task RunManagementAuthorizationMatrixContractsAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-management-auth-matrix-" + Guid.NewGuid().ToString("N") + ".db");
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
                    string baseUrl = "http://127.0.0.1:" + port;
                    using (HttpClient adminClient = new HttpClient())
                    using (HttpClient anonymousClient = new HttpClient())
                    using (HttpClient noAccessClient = new HttpClient())
                    {
                        await AuthenticateSeedAdminAsync(adminClient, baseUrl).ConfigureAwait(false);
                        await CreateUserViaApiAsync(adminClient, baseUrl, Constants.DefaultTenantId, "matrix-no-access@security.test", "password", false, false).ConfigureAwait(false);
                        await AuthenticateUserAsync(noAccessClient, baseUrl, Constants.DefaultTenantId, "matrix-no-access@security.test", "password").ConfigureAwait(false);

                        await AssertProtectedGetRouteAsync(anonymousClient, noAccessClient, adminClient, baseUrl + "/v1.0/api/settings", "settings").ConfigureAwait(false);
                        await AssertProtectedGetRouteAsync(anonymousClient, noAccessClient, adminClient, baseUrl + "/v1.0/api/backup", "backup").ConfigureAwait(false);
                        await AssertProtectedGetRouteAsync(anonymousClient, noAccessClient, adminClient, baseUrl + "/v1.0/api/users", "users").ConfigureAwait(false);
                        await AssertProtectedGetRouteAsync(anonymousClient, noAccessClient, adminClient, baseUrl + "/v1.0/api/credentials", "credentials").ConfigureAwait(false);
                        await AssertProtectedGetRouteAsync(anonymousClient, noAccessClient, adminClient, baseUrl + "/v1.0/api/roles", "roles").ConfigureAwait(false);
                        await AssertProtectedGetRouteAsync(anonymousClient, noAccessClient, adminClient, baseUrl + "/v1.0/api/permissions", "permissions").ConfigureAwait(false);
                        await AssertProtectedGetRouteAsync(anonymousClient, noAccessClient, adminClient, baseUrl + "/v1.0/api/role-assignments", "role assignments").ConfigureAwait(false);
                        await AssertProtectedGetRouteAsync(anonymousClient, noAccessClient, adminClient, baseUrl + "/v1.0/api/audit", "audit").ConfigureAwait(false);
                        await AssertProtectedGetRouteAsync(anonymousClient, noAccessClient, adminClient, baseUrl + "/v1.0/api/clients", "clients").ConfigureAwait(false);
                        await AssertProtectedGetRouteAsync(anonymousClient, noAccessClient, adminClient, baseUrl + "/v1.0/api/request-history", "request history").ConfigureAwait(false);
                        await AssertProtectedGetRouteAsync(anonymousClient, noAccessClient, adminClient, baseUrl + "/v1.0/api/request-history/summary", "request history summary").ConfigureAwait(false);
                        await AssertProtectedGetRouteAsync(anonymousClient, noAccessClient, adminClient, baseUrl + "/v1.0/api/request-history/analytics/overview", "request analytics").ConfigureAwait(false);
                        await AssertProtectedGetRouteAsync(anonymousClient, noAccessClient, adminClient, baseUrl + "/v1.0/api/client-telemetry/status", "client telemetry status").ConfigureAwait(false);
                        await AssertProtectedGetRouteAsync(anonymousClient, noAccessClient, adminClient, baseUrl + "/v1/models", "OpenAI model list").ConfigureAwait(false);
                        await AssertProtectedGetRouteAsync(anonymousClient, noAccessClient, adminClient, baseUrl + "/api/tags", "Ollama model list").ConfigureAwait(false);
                    }
                }
                finally
                {
                    await StopServerAsync(server, serverTask, serverTokenSource, dbFile).ConfigureAwait(false);
                }
            }
        }

        private static async Task RunCredentialLifecycleSecurityContractsAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-credential-lifecycle-" + Guid.NewGuid().ToString("N") + ".db");
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
                    string baseUrl = "http://127.0.0.1:" + port;

                    using (HttpClient adminClient = new HttpClient())
                    {
                        await AuthenticateSeedAdminAsync(adminClient, baseUrl).ConfigureAwait(false);
                        string tenantBId;
                        using (HttpResponseMessage createTenantB = await PostJsonAsync(adminClient, baseUrl + "/v1.0/api/tenants", JsonUtil.Serialize(new { name = "Credential Tenant B", active = true })).ConfigureAwait(false))
                        {
                            string tenantBJson = await createTenantB.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!createTenantB.IsSuccessStatusCode) throw new InvalidOperationException("System admin could not create credential tenant B: " + tenantBJson);
                            tenantBId = FirstRecordId("[" + tenantBJson + "]");
                        }

                        string credentialId;
                        using (HttpResponseMessage createCredential = await PostJsonAsync(adminClient, baseUrl + "/v1.0/api/credentials", JsonUtil.Serialize(new
                        {
                            accessKey = "lifecycle-access",
                            active = true
                        })).ConfigureAwait(false))
                        {
                            string createCredentialJson = await createCredential.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!createCredential.IsSuccessStatusCode) throw new InvalidOperationException("Credential create failed: " + createCredentialJson);
                            if (createCredentialJson.Contains("secretKey", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Credential create exposed secret-key material.");
                            credentialId = FirstRecordId("[" + createCredentialJson + "]");
                        }

                        using (HttpResponseMessage secretExchange = await PostJsonAsync(adminClient, baseUrl + "/v1.0/api/auth/token", "{\"accessKey\":\"lifecycle-access\",\"secretKey\":\"should-not-be-sent\"}").ConfigureAwait(false))
                        {
                            if ((int)secretExchange.StatusCode != 401) throw new InvalidOperationException("Credential token exchange accepted secret-key material: " + (int)secretExchange.StatusCode);
                        }

                        using (HttpClient mismatchClient = new HttpClient())
                        {
                            mismatchClient.DefaultRequestHeaders.Add("x-tenant-guid", tenantBId);
                            using (HttpResponseMessage mismatchToken = await PostJsonAsync(mismatchClient, baseUrl + "/v1.0/api/auth/token", "{\"accessKey\":\"lifecycle-access\"}").ConfigureAwait(false))
                            {
                                if ((int)mismatchToken.StatusCode != 401) throw new InvalidOperationException("Credential token exchange accepted the wrong tenant: " + (int)mismatchToken.StatusCode);
                            }
                        }

                        AuthTokenResult issuedToken;
                        using (HttpResponseMessage tokenResponse = await PostJsonAsync(adminClient, baseUrl + "/v1.0/api/auth/token", "{\"accessKey\":\"lifecycle-access\"}").ConfigureAwait(false))
                        {
                            string tokenJson = await tokenResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!tokenResponse.IsSuccessStatusCode) throw new InvalidOperationException("Credential token exchange failed: " + tokenJson);
                            issuedToken = DeserializeRequired<AuthTokenResult>(tokenJson);
                            if (String.IsNullOrWhiteSpace(issuedToken.Token)) throw new InvalidOperationException("Credential token exchange did not return a bearer token.");
                        }

                        using (HttpResponseMessage disableCredential = await PutJsonAsync(adminClient, baseUrl + "/v1.0/api/credentials/" + Uri.EscapeDataString(credentialId), JsonUtil.Serialize(new
                        {
                            active = false
                        })).ConfigureAwait(false))
                        {
                            string disableJson = await disableCredential.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!disableCredential.IsSuccessStatusCode) throw new InvalidOperationException("Credential disable failed: " + disableJson);
                        }

                        using (HttpResponseMessage disabledToken = await PostJsonAsync(adminClient, baseUrl + "/v1.0/api/auth/token", "{\"accessKey\":\"lifecycle-access\"}").ConfigureAwait(false))
                        {
                            if ((int)disabledToken.StatusCode != 401) throw new InvalidOperationException("Disabled credential was accepted for token exchange: " + (int)disabledToken.StatusCode);
                        }

                        using (HttpClient staleSessionClient = new HttpClient())
                        {
                            ApplyBearerHeaders(staleSessionClient, issuedToken.TenantId, issuedToken.Token);
                            using (HttpResponseMessage staleDetails = await staleSessionClient.GetAsync(baseUrl + "/v1.0/api/auth/token/details").ConfigureAwait(false))
                            {
                                if ((int)staleDetails.StatusCode != 401) throw new InvalidOperationException("Bearer session backed by disabled credential still validated: " + (int)staleDetails.StatusCode);
                            }
                        }

                        using (HttpResponseMessage deleteCredential = await adminClient.DeleteAsync(baseUrl + "/v1.0/api/credentials/" + Uri.EscapeDataString(credentialId)).ConfigureAwait(false))
                        {
                            if (!deleteCredential.IsSuccessStatusCode) throw new InvalidOperationException("Credential delete failed: " + (int)deleteCredential.StatusCode);
                        }

                        using (HttpResponseMessage deletedToken = await PostJsonAsync(adminClient, baseUrl + "/v1.0/api/auth/token", "{\"accessKey\":\"lifecycle-access\"}").ConfigureAwait(false))
                        {
                            if ((int)deletedToken.StatusCode != 401) throw new InvalidOperationException("Deleted credential was accepted for token exchange: " + (int)deletedToken.StatusCode);
                        }
                    }
                }
                finally
                {
                    await StopServerAsync(server, serverTask, serverTokenSource, dbFile).ConfigureAwait(false);
                }
            }
        }

        private static async Task RunAuthRateLimitContractsAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-auth-rate-limit-" + Guid.NewGuid().ToString("N") + ".db");
            XenoSettings settings = new XenoSettings
            {
                Rest = new RestSettings
                {
                    Hostname = "127.0.0.1",
                    Port = port,
                    Ssl = false
                },
                Auth = new AuthSettings
                {
                    MaxLoginRequestsPerMinute = 1
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
                    string baseUrl = "http://127.0.0.1:" + port;
                    using (HttpClient httpClient = new HttpClient())
                    {
                        string loginBody = JsonUtil.Serialize(new AuthLoginRequest
                        {
                            TenantId = Constants.DefaultTenantId,
                            Email = "admin@xeno",
                            Password = "wrong-password"
                        });
                        using (HttpResponseMessage firstLogin = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/auth/login", loginBody).ConfigureAwait(false))
                        {
                            if ((int)firstLogin.StatusCode == 429) throw new InvalidOperationException("First login attempt should not be rate limited.");
                        }

                        using (HttpResponseMessage secondLogin = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/auth/login", loginBody).ConfigureAwait(false))
                        {
                            string secondLoginJson = await secondLogin.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if ((int)secondLogin.StatusCode != 429) throw new InvalidOperationException("Second login attempt should return 429: " + (int)secondLogin.StatusCode + " " + secondLoginJson);
                            if (!secondLogin.Headers.TryGetValues("Retry-After", out IEnumerable<string>? retryAfterValues) || !retryAfterValues.Contains("60")) throw new InvalidOperationException("Rate limited login response missing Retry-After: " + secondLoginJson);
                            if (!secondLoginJson.Contains("RateLimited", StringComparison.Ordinal)) throw new InvalidOperationException("Rate limited login response missing reason code: " + secondLoginJson);
                        }
                    }
                }
                finally
                {
                    await StopServerAsync(server, serverTask, serverTokenSource, dbFile).ConfigureAwait(false);
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
                            if (lookup.Email != "admin@xeno") throw new InvalidOperationException("Tenant lookup returned an encoded email value.");
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
                                using (HttpResponseMessage settingsResponse = await httpClient.SendAsync(settingsRequest).ConfigureAwait(false))
                                {
                                    string settingsJson = await settingsResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                                    if (!settingsResponse.IsSuccessStatusCode) throw new InvalidOperationException("Bearer-authenticated settings request failed: " + settingsJson);
                                    if (!settingsJson.Contains("settingsFile", StringComparison.Ordinal)) throw new InvalidOperationException("Authenticated settings response missing settingsFile.");
                                }
                            }
                        }

                        AuthDashboardLoginResponse signup;
                        using (HttpResponseMessage signupResponse = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/onboarding/signup", JsonUtil.Serialize(new AuthOnboardingRequest
                        {
                            TenantName = "Dashboard Signup Tenant",
                            Email = "tenant-admin@signup.test",
                            Username = "tenant-admin@signup.test",
                            Password = "password"
                        })).ConfigureAwait(false))
                        {
                            string signupJson = await signupResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!signupResponse.IsSuccessStatusCode) throw new InvalidOperationException("Onboarding signup failed: " + signupJson);
                            signup = DeserializeRequired<AuthDashboardLoginResponse>(signupJson);
                            if (String.IsNullOrWhiteSpace(signup.TenantId)) throw new InvalidOperationException("Onboarding signup did not return a tenant ID.");
                        }

                        await AuthenticateSeedAdminAsync(httpClient, baseUrl).ConfigureAwait(false);
                        using (HttpResponseMessage usersResponse = await httpClient.GetAsync(baseUrl + "/v1.0/api/users").ConfigureAwait(false))
                        {
                            string usersJson = await usersResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!usersResponse.IsSuccessStatusCode) throw new InvalidOperationException("System admin could not inspect users after onboarding: " + usersJson);
                            List<User> users = DeserializeRequired<List<User>>(usersJson);
                            bool copiedSeedAdmin = users.Any(item => item.TenantId == signup.TenantId && String.Equals(item.Email, "admin@xeno", StringComparison.OrdinalIgnoreCase));
                            if (copiedSeedAdmin) throw new InvalidOperationException("Onboarding should not create admin@xeno in the new tenant.");
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

        private static async Task RunModelTokenLeaderboardPublicSmokeAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-model-leaderboard-public-" + Guid.NewGuid().ToString("N") + ".db");
            DateTime nowUtc = DateTime.UtcNow;
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

            using (DatabaseDriverBase driver = await DatabaseDriverFactory.CreateAndInitializeAsync(settings.Database).ConfigureAwait(false))
            {
                const string otherTenantId = "ten_public_leaderboard_other";
                driver.Platform.Tenants.Add(new Tenant { Id = otherTenantId, Name = "Public Leaderboard Other" });
                driver.Platform.RequestHistory.Add(CreateModelLeaderboardHistory("req_public_alpha_1", Constants.DefaultTenantId, "public-alpha", nowUtc.AddMinutes(-20), 40, 60, 100));
                driver.Platform.RequestHistory.Add(CreateModelLeaderboardHistory("req_public_alpha_2", Constants.DefaultTenantId, "public-alpha", nowUtc.AddMinutes(-10), 10, 20, 30));
                driver.Platform.RequestHistory.Add(CreateModelLeaderboardHistory("req_public_beta_1", Constants.DefaultTenantId, "public-beta", nowUtc.AddMinutes(-5), 25, 35, 60));
                driver.Platform.RequestHistory.Add(CreateModelLeaderboardHistory("req_public_alpha_other", otherTenantId, "public-alpha", nowUtc.AddMinutes(-5), 500, 500, 1000));
                driver.Platform.RequestHistory.Add(CreateModelLeaderboardHistory("req_public_old", Constants.DefaultTenantId, "public-old", nowUtc.AddDays(-3), 1, 1, 2));
                await driver.SaveAsync(CancellationToken.None).ConfigureAwait(false);
            }

            using (CancellationTokenSource serverTokenSource = new CancellationTokenSource())
            {
                XenoServer server = new XenoServer(settings);
                Task serverTask = server.StartAsync(serverTokenSource.Token);

                try
                {
                    await WaitForHealthAsync(port).ConfigureAwait(false);
                    string baseUrl = "http://127.0.0.1:" + port;
                    string fromUtc = Uri.EscapeDataString(nowUtc.AddHours(-1).ToString("O"));
                    string toUtc = Uri.EscapeDataString(nowUtc.AddHours(1).ToString("O"));
                    using (HttpClient anonymousClient = new HttpClient())
                    {
                        using (HttpResponseMessage settingsResponse = await anonymousClient.GetAsync(baseUrl + "/v1.0/api/settings").ConfigureAwait(false))
                        {
                            if ((int)settingsResponse.StatusCode != 401) throw new InvalidOperationException("Authenticated settings route should still reject anonymous requests.");
                        }

                        using (HttpResponseMessage leaderboardResponse = await anonymousClient.GetAsync(baseUrl + "/v1.0/api/request-history/model-leaderboard?fromUtc=" + fromUtc + "&toUtc=" + toUtc + "&limit=1&offset=0").ConfigureAwait(false))
                        {
                            string leaderboardJson = await leaderboardResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!leaderboardResponse.IsSuccessStatusCode) throw new InvalidOperationException("Anonymous leaderboard GET failed: " + leaderboardJson);
                            ModelTokenLeaderboardResponse leaderboard = DeserializeRequired<ModelTokenLeaderboardResponse>(leaderboardJson);
                            if (leaderboard.Total != 2) throw new InvalidOperationException("Anonymous leaderboard GET did not include total matching model records.");
                            if (leaderboard.Models.Count != 1 || !leaderboard.Models.ContainsKey("public-alpha")) throw new InvalidOperationException("Anonymous leaderboard GET did not return the top-ranked model.");
                            ModelTokenUsage alpha = leaderboard.Models["public-alpha"];
                            if (alpha.TotalTokens != 1130 || alpha.PromptTokens != 550 || alpha.OutputTokens != 580) throw new InvalidOperationException("Anonymous leaderboard GET did not aggregate global token totals across tenants.");
                        }

                        using (HttpResponseMessage leaderboardDefaultsResponse = await PostJsonAsync(anonymousClient, baseUrl + "/v1.0/api/request-history/model-leaderboard", "{}").ConfigureAwait(false))
                        {
                            string leaderboardJson = await leaderboardDefaultsResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!leaderboardDefaultsResponse.IsSuccessStatusCode) throw new InvalidOperationException("Anonymous leaderboard POST with omitted pagination failed: " + leaderboardJson);
                            ModelTokenLeaderboardResponse leaderboard = DeserializeRequired<ModelTokenLeaderboardResponse>(leaderboardJson);
                            if (leaderboard.Limit != 100 || leaderboard.Offset != 0) throw new InvalidOperationException("Anonymous leaderboard POST did not apply omitted pagination defaults.");
                        }

                        using (HttpResponseMessage leaderboardNullResponse = await PostJsonAsync(anonymousClient, baseUrl + "/v1.0/api/request-history/model-leaderboard", "{\"limit\":null,\"offset\":null}").ConfigureAwait(false))
                        {
                            string leaderboardJson = await leaderboardNullResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!leaderboardNullResponse.IsSuccessStatusCode) throw new InvalidOperationException("Anonymous leaderboard POST with null pagination failed: " + leaderboardJson);
                            ModelTokenLeaderboardResponse leaderboard = DeserializeRequired<ModelTokenLeaderboardResponse>(leaderboardJson);
                            if (leaderboard.Limit != 100 || leaderboard.Offset != 0) throw new InvalidOperationException("Anonymous leaderboard POST did not treat null pagination as defaults.");
                        }

                        ModelTokenLeaderboardRequest request = new ModelTokenLeaderboardRequest
                        {
                            TenantId = Constants.DefaultTenantId,
                            FromUtc = nowUtc.AddHours(-1),
                            ToUtc = nowUtc.AddHours(1),
                            Limit = 1,
                            Offset = 1
                        };
                        using (HttpResponseMessage leaderboardResponse = await PostJsonAsync(anonymousClient, baseUrl + "/v1.0/api/request-history/model-leaderboard", JsonUtil.Serialize(request)).ConfigureAwait(false))
                        {
                            string leaderboardJson = await leaderboardResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!leaderboardResponse.IsSuccessStatusCode) throw new InvalidOperationException("Anonymous leaderboard POST failed: " + leaderboardJson);
                            ModelTokenLeaderboardResponse leaderboard = DeserializeRequired<ModelTokenLeaderboardResponse>(leaderboardJson);
                            if (leaderboard.Total != 2) throw new InvalidOperationException("Anonymous leaderboard POST did not preserve total across pagination.");
                            if (leaderboard.Offset != 1) throw new InvalidOperationException("Anonymous leaderboard POST did not report the applied offset.");
                            if (leaderboard.Models.Count != 1 || !leaderboard.Models.ContainsKey("public-beta")) throw new InvalidOperationException("Anonymous leaderboard POST did not return the second-ranked model.");
                        }
                    }
                }
                finally
                {
                    await StopServerAsync(server, serverTask, serverTokenSource, dbFile).ConfigureAwait(false);
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
                        await AuthenticateSeedAdminAsync(httpClient, baseUrl).ConfigureAwait(false);
                        string openApi = await httpClient.GetStringAsync(baseUrl + "/openapi.json").ConfigureAwait(false);
                        if (!openApi.Contains("client-model", StringComparison.OrdinalIgnoreCase) && !openApi.Contains("clientmodels", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("OpenAPI missing client model delete route.");
                        if (!openApi.Contains("request-history", StringComparison.OrdinalIgnoreCase) && !openApi.Contains("requesthistory", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("OpenAPI missing request history delete route.");
                        foreach (OpenApiSecuritySchemeExpectation scheme in new List<OpenApiSecuritySchemeExpectation>
                        {
                            new OpenApiSecuritySchemeExpectation { Scheme = "xTokenAuth", Header = "x-token" },
                            new OpenApiSecuritySchemeExpectation { Scheme = "accessKeyAuth", Header = "x-access-key" },
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
                            new OpenApiRouteExpectation { Route = "/v1.0/api/routing-policies/{id}", Method = "put" },
                            new OpenApiRouteExpectation { Route = "/api/tags", Method = "get" }
                        })
                        {
                            if (!OpenApiHasMethod(openApi, route.Route, route.Method)) throw new InvalidOperationException("OpenAPI missing " + route.Method.ToUpperInvariant() + " " + route.Route + ".");
                        }

                        foreach (OpenApiParameterExpectation parameter in new List<OpenApiParameterExpectation>
                        {
                            new OpenApiParameterExpectation { Route = "/v1.0/api/users/{id}", Method = "put", Name = "id", Location = "path" },
                            new OpenApiParameterExpectation { Route = "/v1.0/api/request-history", Method = "get", Name = "page", Location = "query" },
                            new OpenApiParameterExpectation { Route = "/v1.0/api/request-history", Method = "get", Name = "pageSize", Location = "query" },
                            new OpenApiParameterExpectation { Route = "/v1.0/api/request-history", Method = "get", Name = "fromUtc", Location = "query" },
                            new OpenApiParameterExpectation { Route = "/v1.0/api/request-history", Method = "get", Name = "toUtc", Location = "query" },
                            new OpenApiParameterExpectation { Route = "/v1.0/api/request-history/summary", Method = "get", Name = "fromUtc", Location = "query" },
                            new OpenApiParameterExpectation { Route = "/v1.0/api/request-history/summary", Method = "get", Name = "toUtc", Location = "query" },
                            new OpenApiParameterExpectation { Route = "/v1.0/api/request-history/summary", Method = "get", Name = "bucketMinutes", Location = "query" },
                            new OpenApiParameterExpectation { Route = "/v1.0/api/request-history/analytics/overview", Method = "get", Name = "fromUtc", Location = "query" },
                            new OpenApiParameterExpectation { Route = "/v1.0/api/request-history/analytics/overview", Method = "get", Name = "toUtc", Location = "query" }
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
                            new OpenApiRouteExpectation { Route = "/v1/embeddings", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/api/chat", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/api/generate", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/api/embed", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/api/embeddings", Method = "post" }
                        })
                        {
                            if (!OpenApiHasRequestBody(openApi, route.Route, route.Method)) throw new InvalidOperationException("OpenAPI missing editable request body on " + route.Method.ToUpperInvariant() + " " + route.Route + ".");
                        }

                        if (!openApi.Contains("\"stream_options\"", StringComparison.Ordinal) || !openApi.Contains("\"include_usage\"", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("OpenAPI completion request bodies do not include streamed usage telemetry options.");
                        }

                        string swagger = await httpClient.GetStringAsync(baseUrl + "/swagger").ConfigureAwait(false);
                        if (!swagger.Contains("xeno-auth-panel", StringComparison.Ordinal) || !swagger.Contains("xeno.swagger.auth", StringComparison.Ordinal) || !swagger.Contains("requestInterceptor", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Swagger UI does not include Xeno authentication loading support.");
                        }

                        if (swagger.Contains("xeno-secret-key", StringComparison.OrdinalIgnoreCase) || swagger.Contains("Secret key", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException("Swagger UI exposed legacy secret-key authentication controls.");
                        }

                        await AuthenticateSeedAdminAsync(httpClient, baseUrl).ConfigureAwait(false);
                        DateTime summaryFromUtc = DateTime.UtcNow.AddMinutes(-5);
                        using (HttpResponseMessage completionResponse = await PostJsonAsync(httpClient, baseUrl + "/v1/chat/completions", "{\"model\":\"llama3.2:latest\",\"messages\":[{\"role\":\"user\",\"content\":\"ping\"}]}").ConfigureAwait(false))
                        {
                            _ = await completionResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                        }

                        DateTime summaryToUtc = DateTime.UtcNow.AddMinutes(5);
                        string encodedSummaryUrl = baseUrl
                            + "/v1.0/api/request-history/summary?fromUtc="
                            + Uri.EscapeDataString(summaryFromUtc.ToString("O"))
                            + "&toUtc="
                            + Uri.EscapeDataString(summaryToUtc.ToString("O"))
                            + "&bucketMinutes=15";
                        string encodedSummary = await httpClient.GetStringAsync(encodedSummaryUrl).ConfigureAwait(false);
                        if (!encodedSummary.Contains("\"count\":1", StringComparison.Ordinal) && !encodedSummary.Contains("\"count\": 1", StringComparison.Ordinal))
                        {
                            throw new InvalidOperationException("Request history summary did not honor encoded date query values: " + encodedSummary);
                        }

                        string historyJson = await httpClient.GetStringAsync(baseUrl + "/v1.0/api/request-history").ConfigureAwait(false);
                        RequestHistorySearchResult historyRecords = DeserializeRequired<RequestHistorySearchResult>(historyJson);
                        if (historyRecords.Total <= 0) throw new InvalidOperationException("Request history search result did not report a total.");
                        RequestHistoryEntry? history = historyRecords.Items.FirstOrDefault();
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

                        await AuthenticateSeedAdminAsync(httpClient, baseUrl).ConfigureAwait(false);
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
                            active = true
                        })).ConfigureAwait(false))
                        {
                            string createCredentialJson = await createCredentialResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!createCredentialResponse.IsSuccessStatusCode || !createCredentialJson.Contains("touchstone-create-access", StringComparison.Ordinal)) throw new InvalidOperationException("Credential create failed: " + createCredentialJson);
                            if (createCredentialJson.Contains("secretKey", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Credential create response exposed secret key material.");
                        }

                        string createdPermissionId = String.Empty;
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
                            createdPermissionId = FirstRecordId("[" + createPermissionJson + "]");
                        }

                        string createdRoleId = String.Empty;
                        using (HttpResponseMessage createRoleResponse = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/roles", JsonUtil.Serialize(new
                        {
                            name = "Touchstone Created Role",
                            permissionIds = Array.Empty<string>(),
                            active = true
                        })).ConfigureAwait(false))
                        {
                            string createRoleJson = await createRoleResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!createRoleResponse.IsSuccessStatusCode || !createRoleJson.Contains("Touchstone Created Role", StringComparison.Ordinal)) throw new InvalidOperationException("Role create failed: " + createRoleJson);
                            createdRoleId = FirstRecordId("[" + createRoleJson + "]");
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
                            if (!loginResponse.IsSuccessStatusCode || !loginJson.Contains("accessKey", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Updated user password did not validate: " + loginJson);
                            if (loginJson.Contains("secretKey", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Login response exposed secret key material: " + loginJson);
                        }

                        string credentialId = await FirstRecordIdAsync(httpClient, baseUrl + "/v1.0/api/credentials").ConfigureAwait(false);
                        using (HttpResponseMessage credentialResponse = await PutJsonAsync(httpClient, baseUrl + "/v1.0/api/credentials/" + Uri.EscapeDataString(credentialId), JsonUtil.Serialize(new
                        {
                            userId,
                            accessKey = "touchstone-access",
                            active = true
                        })).ConfigureAwait(false))
                        {
                            string credentialJson = await credentialResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!credentialResponse.IsSuccessStatusCode || !credentialJson.Contains("touchstone-access", StringComparison.Ordinal)) throw new InvalidOperationException("Credential update failed: " + credentialJson);
                            if (credentialJson.Contains("touchstone-secret", StringComparison.Ordinal)) throw new InvalidOperationException("Credential update response exposed raw secret key.");
                        }

                        using (HttpResponseMessage tokenResponse = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/auth/token", "{\"accessKey\":\"touchstone-access\"}").ConfigureAwait(false))
                        {
                            string tokenJson = await tokenResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!tokenResponse.IsSuccessStatusCode || !tokenJson.Contains("token", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Updated credential did not validate: " + tokenJson);
                        }

                        string roleId = createdRoleId;
                        string permissionId = createdPermissionId;
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
                                    },
                                    new
                                    {
                                        name = "updated-ollama-provider",
                                        apiType = "Ollama",
                                        models = new[]
                                        {
                                            new { name = "touchstone-ollama-model", supportsCompletions = true, supportsEmbeddings = true }
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
                        string baseUrl = "http://127.0.0.1:" + port;
                        await AuthenticateSeedAdminAsync(httpClient, baseUrl).ConfigureAwait(false);
                        string clients = await httpClient.GetStringAsync(baseUrl + "/v1.0/api/clients").ConfigureAwait(false);
                        if (!clients.Contains("touchstone-updated-model", StringComparison.Ordinal)) throw new InvalidOperationException("Updated client models did not include inventory update model: " + clients);

                        string models = await httpClient.GetStringAsync(baseUrl + "/v1/models").ConfigureAwait(false);
                        if (!models.Contains("touchstone-updated-model", StringComparison.Ordinal)) throw new InvalidOperationException("OpenAI model list did not include inventory update model: " + models);
                        if (!models.Contains("\"created\"", StringComparison.Ordinal)) throw new InvalidOperationException("OpenAI model list did not include created timestamps: " + models);

                        string ollamaTags = await httpClient.GetStringAsync(baseUrl + "/api/tags").ConfigureAwait(false);
                        if (!ollamaTags.Contains("\"models\"", StringComparison.Ordinal)) throw new InvalidOperationException("Ollama tags response did not include models array: " + ollamaTags);
                        if (!ollamaTags.Contains("touchstone-ollama-model", StringComparison.Ordinal)) throw new InvalidOperationException("Ollama tags response did not include Ollama inventory model: " + ollamaTags);
                        if (!ollamaTags.Contains("\"details\"", StringComparison.Ordinal)) throw new InvalidOperationException("Ollama tags response did not include details objects: " + ollamaTags);
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
                    clientSettings.LocalDashboard.Enabled = true;
                    clientSettings.LocalDashboard.Host = "127.0.0.1";
                    clientSettings.LocalDashboard.Port = dashboardPort;
                    clientSettings.LocalDashboard.Database.Filename = Path.Combine(clientHome, "local-dashboard.db");
                    File.WriteAllText(Path.Combine(clientHome, "client.json"), JsonUtil.Serialize(clientSettings, true));

                    ClientCommandResult connect = await RunXenoClientAsync(new[] { "start", "--timeout", "15000" }, clientHome).ConfigureAwait(false);
                    if (connect.ExitCode != 0) throw new InvalidOperationException("Background connect failed: " + connect.StandardError + connect.StandardOutput);
                    if (!connect.StandardOutput.Contains("Xeno client connected in the background", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Background start did not report connected: " + connect.StandardOutput);
                    if (!connect.StandardOutput.Contains("Server WebSocket: ws://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture) + settings.WebSocket.Path, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Background start did not include server WebSocket URL: " + connect.StandardOutput);
                    if (!connect.StandardOutput.Contains("Server REST     : http://127.0.0.1:" + port.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Background start did not include server REST URL: " + connect.StandardOutput);
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
                            await AuthenticateSeedAdminAsync(httpClient, "http://127.0.0.1:" + port).ConfigureAwait(false);
                            request.Headers.Add("X-Forwarded-For", "203.0.113.66");
                            request.Headers.Add("X-Xeno-Requestor-IP", "198.51.100.77");
                            request.Headers.Add("X-Xeno-Tenant-ID", "ten_spoofed");
                            request.Headers.Add("X-Xeno-Request-ID", "req_spoofed");
                            request.Headers.Add("X-Api-Key", "provider-secret");
                            request.Headers.Add("x-access-key", "caller-access-key");
                            request.Content = new StringContent(
                                "{\"model\":\"llama3.2:latest\",\"messages\":[{\"role\":\"user\",\"content\":\"ping\"}],\"apiKey\":\"body-secret\",\"nested\":{\"accessKey\":\"inner-secret\",\"safe\":\"visible\"}}",
                                Encoding.UTF8,
                                "application/json");

                            string historyId = String.Empty;
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
                                historyId = HeaderValue(response, "X-Xeno-Request-History-ID");
                            }

                            using (HttpResponseMessage historyResponse = await httpClient.GetAsync("http://127.0.0.1:" + port + "/api/v1/requesthistory/" + Uri.EscapeDataString(historyId)).ConfigureAwait(false))
                            {
                                string historyJson = await historyResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                                if (!historyResponse.IsSuccessStatusCode) throw new InvalidOperationException("Request history lookup failed: " + historyJson);
                                RequestHistoryEntry history = DeserializeRequired<RequestHistoryEntry>(historyJson);
                                if (!String.Equals(history.RequestorTenantId, Constants.DefaultTenantId, StringComparison.Ordinal)) throw new InvalidOperationException("Request history accepted spoofed tenant ID.");
                                if (String.Equals(history.SourceIp, "203.0.113.66", StringComparison.Ordinal) || String.Equals(history.SourceIp, "198.51.100.77", StringComparison.Ordinal)) throw new InvalidOperationException("Request history accepted spoofed source IP.");
                                if (!String.Equals(history.RequestHeaders["X-Api-Key"], "(redacted)", StringComparison.Ordinal)) throw new InvalidOperationException("Request history did not redact X-Api-Key.");
                                if (!String.Equals(history.RequestHeaders["x-access-key"], "(redacted)", StringComparison.Ordinal)) throw new InvalidOperationException("Request history did not redact x-access-key.");
                                if (history.RequestHeaders.TryGetValue("X-Xeno-Tenant-ID", out string? tenantHeader) && String.Equals(tenantHeader, "ten_spoofed", StringComparison.Ordinal)) throw new InvalidOperationException("Request history retained spoofed Xeno tenant header.");
                                if (history.RequestHeaders.TryGetValue("X-Xeno-Request-ID", out string? requestIdHeader) && String.Equals(requestIdHeader, "req_spoofed", StringComparison.Ordinal)) throw new InvalidOperationException("Request history retained spoofed Xeno request ID.");
                                if ((history.RequestBody ?? String.Empty).Contains("body-secret", StringComparison.Ordinal) || (history.RequestBody ?? String.Empty).Contains("inner-secret", StringComparison.Ordinal)) throw new InvalidOperationException("Request history persisted sensitive request body fields.");
                                if (!(history.RequestBody ?? String.Empty).Contains("(redacted)", StringComparison.Ordinal)) throw new InvalidOperationException("Request history did not mark redacted request body fields.");
                                if ((history.ResponseBody ?? String.Empty).Contains("response-secret", StringComparison.Ordinal)) throw new InvalidOperationException("Request history persisted sensitive response body fields.");
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

                    using (ClientWebSocket secretSocket = new ClientWebSocket())
                    {
                        secretSocket.Options.SetRequestHeader("x-tenant-guid", Constants.DefaultTenantId);
                        secretSocket.Options.SetRequestHeader("x-access-key", "default");
                        secretSocket.Options.SetRequestHeader("x-secret-key", "legacy-secret");
                        await secretSocket.ConnectAsync(new Uri("ws://127.0.0.1:" + port + settings.WebSocket.Path), clientTokenSource.Token).ConfigureAwait(false);
                        string? message = await ReceiveWebSocketTextAsync(secretSocket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(message) || !message.Contains("server.unauthorized", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException("Secret-key WebSocket authentication was not rejected: " + message);
                        }
                    }

                    using (ClientWebSocket wrongTenantSocket = new ClientWebSocket())
                    {
                        wrongTenantSocket.Options.SetRequestHeader("x-tenant-guid", "ten_wrong");
                        wrongTenantSocket.Options.SetRequestHeader("x-access-key", "default");
                        await wrongTenantSocket.ConnectAsync(new Uri("ws://127.0.0.1:" + port + settings.WebSocket.Path), clientTokenSource.Token).ConfigureAwait(false);
                        string? message = await ReceiveWebSocketTextAsync(wrongTenantSocket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(message) || !message.Contains("server.unauthorized", StringComparison.OrdinalIgnoreCase))
                        {
                            throw new InvalidOperationException("Wrong-tenant WebSocket credential was not rejected: " + message);
                        }
                    }

                    using (ClientWebSocket malformedSocket = new ClientWebSocket())
                    {
                        ApplySeedCredentialHeaders(malformedSocket);
                        await malformedSocket.ConnectAsync(new Uri("ws://127.0.0.1:" + port + settings.WebSocket.Path), clientTokenSource.Token).ConfigureAwait(false);
                        string? connected = await ReceiveWebSocketTextAsync(malformedSocket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(connected) || !connected.Contains("server.connected", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Authenticated WebSocket did not connect before malformed JSON test: " + connected);
                        await SendWebSocketTextAsync(malformedSocket, "{", clientTokenSource.Token).ConfigureAwait(false);
                        string? message = await ReceiveWebSocketTextAsync(malformedSocket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(message) || !message.Contains("server.invalid-json", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Malformed WebSocket JSON was not rejected: " + message);
                    }

                    using (ClientWebSocket protocolSocket = new ClientWebSocket())
                    {
                        ApplySeedCredentialHeaders(protocolSocket);
                        await protocolSocket.ConnectAsync(new Uri("ws://127.0.0.1:" + port + settings.WebSocket.Path), clientTokenSource.Token).ConfigureAwait(false);
                        string? connected = await ReceiveWebSocketTextAsync(protocolSocket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(connected) || !connected.Contains("server.connected", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Authenticated WebSocket did not connect before protocol abuse test: " + connected);
                        await SendWebSocketJsonAsync(protocolSocket, new { type = "client.inventory.update", clientId = "xcl_unregistered", tenantId = Constants.DefaultTenantId, inventory = new { runners = Array.Empty<object>() } }, clientTokenSource.Token).ConfigureAwait(false);
                        string? inventoryMessage = await ReceiveWebSocketTextAsync(protocolSocket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(inventoryMessage) || !inventoryMessage.Contains("server.invalid-message", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unregistered inventory update was not rejected: " + inventoryMessage);

                        await SendWebSocketJsonAsync(protocolSocket, new { type = "client.telemetry.snapshot", requestId = "tel_abuse", tenantId = Constants.DefaultTenantId, clientId = "xcl_unregistered", snapshot = new { metadata = new { clientId = "xcl_unregistered", tenantId = Constants.DefaultTenantId } } }, clientTokenSource.Token).ConfigureAwait(false);
                        string? telemetryMessage = await ReceiveWebSocketTextAsync(protocolSocket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(telemetryMessage) || !telemetryMessage.Contains("server.unauthorized", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unregistered telemetry frame was not rejected: " + telemetryMessage);

                        await SendWebSocketJsonAsync(protocolSocket, new { type = "client.register", clientId = "xcl_wrong_tenant", tenantId = "ten_wrong", name = "wrong-tenant", inventory = new { runners = Array.Empty<object>() } }, clientTokenSource.Token).ConfigureAwait(false);
                        string? registerMessage = await ReceiveWebSocketTextAsync(protocolSocket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(registerMessage) || !registerMessage.Contains("server.unauthorized", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Wrong-tenant registration frame was not rejected: " + registerMessage);
                    }

                    string otherOwnerAccessKey;
                    using (HttpClient adminClient = new HttpClient())
                    {
                        string baseUrl = "http://127.0.0.1:" + port;
                        await AuthenticateSeedAdminAsync(adminClient, baseUrl).ConfigureAwait(false);
                        string otherOwnerUserId = await CreateUserViaApiAsync(adminClient, baseUrl, Constants.DefaultTenantId, "client-owner@example.com", "password", false, false).ConfigureAwait(false);
                        using (HttpResponseMessage credentialResponse = await PostJsonAsync(adminClient, baseUrl + "/v1.0/api/credentials", JsonUtil.Serialize(new
                        {
                            userId = otherOwnerUserId,
                            accessKey = "owner-access-" + Guid.NewGuid().ToString("N"),
                            active = true
                        })).ConfigureAwait(false))
                        {
                            string credentialJson = await credentialResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!credentialResponse.IsSuccessStatusCode) throw new InvalidOperationException("Owner credential create failed: " + credentialJson);
                            Credential credential = DeserializeRequired<Credential>(credentialJson);
                            otherOwnerAccessKey = credential.AccessKey;
                        }
                    }

                    using (ClientWebSocket ownerSocket = new ClientWebSocket())
                    {
                        ApplySeedCredentialHeaders(ownerSocket);
                        await ownerSocket.ConnectAsync(new Uri("ws://127.0.0.1:" + port + settings.WebSocket.Path), clientTokenSource.Token).ConfigureAwait(false);
                        string? connected = await ReceiveWebSocketTextAsync(ownerSocket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(connected) || !connected.Contains("server.connected", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Owner WebSocket did not connect: " + connected);
                        await SendWebSocketJsonAsync(ownerSocket, new { type = "client.register", clientId = "xcl_owner_security", tenantId = Constants.DefaultTenantId, name = "owned-client", inventory = new { runners = Array.Empty<object>() } }, clientTokenSource.Token).ConfigureAwait(false);
                        string? registered = await ReceiveWebSocketTextAsync(ownerSocket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(registered) || !registered.Contains("server.registered", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Owner WebSocket did not register: " + registered);

                        using (HttpClient adminClient = new HttpClient())
                        {
                            await AuthenticateSeedAdminAsync(adminClient, "http://127.0.0.1:" + port).ConfigureAwait(false);
                            string clientsJson = await adminClient.GetStringAsync("http://127.0.0.1:" + port + "/v1.0/api/clients").ConfigureAwait(false);
                            List<XenoClientRecord> clients = DeserializeRequired<List<XenoClientRecord>>(clientsJson);
                            XenoClientRecord? ownedClient = clients.FirstOrDefault(item => String.Equals(item.Id, "xcl_owner_security", StringComparison.OrdinalIgnoreCase));
                            if (ownedClient == null) throw new InvalidOperationException("Owned client was not returned by management API.");
                            if (String.IsNullOrWhiteSpace(ownedClient.OwnerUserId)) throw new InvalidOperationException("Owned client was missing owner user ID.");
                            if (String.IsNullOrWhiteSpace(ownedClient.OwnerCredentialId)) throw new InvalidOperationException("Owned client was missing owner credential ID.");
                            if (ownedClient.OwnerPrincipalType != PrincipalTypeEnum.Credential) throw new InvalidOperationException("Owned client principal type mismatch.");
                        }

                        await ownerSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "owner test complete", clientTokenSource.Token).ConfigureAwait(false);
                        await Task.Delay(100, clientTokenSource.Token).ConfigureAwait(false);
                    }

                    using (ClientWebSocket otherOwnerSocket = new ClientWebSocket())
                    {
                        ApplyCredentialHeaders(otherOwnerSocket, otherOwnerAccessKey);
                        await otherOwnerSocket.ConnectAsync(new Uri("ws://127.0.0.1:" + port + settings.WebSocket.Path), clientTokenSource.Token).ConfigureAwait(false);
                        string? connected = await ReceiveWebSocketTextAsync(otherOwnerSocket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(connected) || !connected.Contains("server.connected", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Other owner WebSocket did not connect: " + connected);
                        await SendWebSocketJsonAsync(otherOwnerSocket, new { type = "client.register", clientId = "xcl_owner_security", tenantId = Constants.DefaultTenantId, name = "owned-client-stolen", inventory = new { runners = Array.Empty<object>() } }, clientTokenSource.Token).ConfigureAwait(false);
                        string? stolenRegister = await ReceiveWebSocketTextAsync(otherOwnerSocket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(stolenRegister) || !stolenRegister.Contains("server.unauthorized", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Different user was able to claim an owned client ID: " + stolenRegister);
                    }

                    using (ClientWebSocket firstDuplicateSocket = new ClientWebSocket())
                    using (ClientWebSocket secondDuplicateSocket = new ClientWebSocket())
                    {
                        ApplySeedCredentialHeaders(firstDuplicateSocket);
                        ApplySeedCredentialHeaders(secondDuplicateSocket);
                        await firstDuplicateSocket.ConnectAsync(new Uri("ws://127.0.0.1:" + port + settings.WebSocket.Path), clientTokenSource.Token).ConfigureAwait(false);
                        string? firstConnected = await ReceiveWebSocketTextAsync(firstDuplicateSocket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(firstConnected) || !firstConnected.Contains("server.connected", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("First duplicate socket did not connect: " + firstConnected);
                        await SendWebSocketJsonAsync(firstDuplicateSocket, new { type = "client.register", clientId = "xcl_duplicate_security", tenantId = Constants.DefaultTenantId, name = "duplicate-one", inventory = new { runners = Array.Empty<object>() } }, clientTokenSource.Token).ConfigureAwait(false);
                        string? firstRegistered = await ReceiveWebSocketTextAsync(firstDuplicateSocket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(firstRegistered) || !firstRegistered.Contains("server.registered", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("First duplicate socket did not register: " + firstRegistered);

                        await secondDuplicateSocket.ConnectAsync(new Uri("ws://127.0.0.1:" + port + settings.WebSocket.Path), clientTokenSource.Token).ConfigureAwait(false);
                        string? secondConnected = await ReceiveWebSocketTextAsync(secondDuplicateSocket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(secondConnected) || !secondConnected.Contains("server.connected", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Second duplicate socket did not connect: " + secondConnected);
                        await SendWebSocketJsonAsync(secondDuplicateSocket, new { type = "client.register", clientId = "xcl_duplicate_security", tenantId = Constants.DefaultTenantId, name = "duplicate-two", inventory = new { runners = Array.Empty<object>() } }, clientTokenSource.Token).ConfigureAwait(false);
                        string? secondRegistered = await ReceiveWebSocketTextAsync(secondDuplicateSocket, clientTokenSource.Token).ConfigureAwait(false);
                        if (String.IsNullOrWhiteSpace(secondRegistered) || !secondRegistered.Contains("server.unauthorized", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Duplicate active client registration was not rejected: " + secondRegistered);
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
                            await AuthenticateSeedAdminAsync(httpClient, "http://127.0.0.1:" + port).ConfigureAwait(false);
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
                                    if (!String.Equals(root.RequestorTenantId, Constants.DefaultTenantId, StringComparison.Ordinal)) throw new InvalidOperationException("Streaming request history did not capture requestor tenant ID.");
                                    if (root.RequestorPrincipalType != PrincipalTypeEnum.User) throw new InvalidOperationException("Streaming request history did not resolve the bearer requestor as a user principal.");
                                    if (String.IsNullOrWhiteSpace(root.RequestorPrincipalId) || !String.Equals(root.RequestorPrincipalId, root.UserId, StringComparison.Ordinal)) throw new InvalidOperationException("Streaming request history did not capture the resolved requestor principal ID.");
                                    if (root.AuthScheme != AuthSchemeEnum.Bearer) throw new InvalidOperationException("Streaming request history did not capture bearer authentication.");
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
                            if (String.IsNullOrWhiteSpace(accessKey)) throw new InvalidOperationException("Login did not return a credential.");
                            if (responseBody.Contains("secretKey", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Login response exposed secret key material.");

                            ApplyCredentialHeaders(socket, accessKey);
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
                if (String.Equals(requestorIp, "203.0.113.66", StringComparison.Ordinal) || String.Equals(requestorIp, "198.51.100.77", StringComparison.Ordinal)) throw new InvalidOperationException("Server request accepted a spoofed requestor IP.");
                if (!String.Equals(serverRequest.RequestorTenantId, Constants.DefaultTenantId, StringComparison.Ordinal)) throw new InvalidOperationException("Server request did not include requestor tenant ID.");
                if (!String.Equals(serverRequest.RequestorPrincipalType, PrincipalTypeEnum.User.ToString(), StringComparison.Ordinal)) throw new InvalidOperationException("Server request did not resolve requestor principal type.");
                if (String.IsNullOrWhiteSpace(serverRequest.RequestorPrincipalId)) throw new InvalidOperationException("Server request did not include requestor principal ID.");
                Dictionary<string, string> headers = serverRequest.Headers ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string forwardedFor = headers.TryGetValue("X-Forwarded-For", out string? forwardedForValue) ? forwardedForValue : String.Empty;
                if (!forwardedFor.Contains(requestorIp, StringComparison.Ordinal)) throw new InvalidOperationException("X-Forwarded-For did not include requestor IP.");
                string realIp = headers.TryGetValue("X-Real-IP", out string? realIpValue) ? realIpValue : String.Empty;
                string xenoRequestorIp = headers.TryGetValue("X-Xeno-Requestor-IP", out string? requestorIpValue) ? requestorIpValue : String.Empty;
                string xenoRequestId = headers.TryGetValue("X-Xeno-Request-ID", out string? requestIdValue) ? requestIdValue : String.Empty;
                if (realIp != requestorIp) throw new InvalidOperationException("X-Real-IP did not match requestor IP.");
                if (xenoRequestorIp != requestorIp) throw new InvalidOperationException("X-Xeno-Requestor-IP did not match requestor IP.");
                if (xenoRequestId != requestId) throw new InvalidOperationException("Forwarded request ID did not match server request ID.");
                if (headers.TryGetValue("X-Xeno-Tenant-ID", out string? forwardedTenantId) && !String.Equals(forwardedTenantId, Constants.DefaultTenantId, StringComparison.Ordinal)) throw new InvalidOperationException("Server request forwarded a spoofed Xeno tenant ID.");
                if (String.Equals(xenoRequestId, "req_spoofed", StringComparison.Ordinal)) throw new InvalidOperationException("Server request forwarded a spoofed Xeno request ID.");
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

                    string completion = "{\"id\":\"chatcmpl_touchstone\",\"object\":\"chat.completion\",\"providerApiKey\":\"response-secret\",\"choices\":[{\"index\":0,\"message\":{\"role\":\"assistant\",\"content\":\"relay-ok\"},\"finish_reason\":\"stop\"}]}";
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
                await AuthenticateSeedAdminAsync(httpClient, "http://127.0.0.1:" + port).ConfigureAwait(false);
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

        private static async Task SendWebSocketTextAsync(ClientWebSocket socket, string value, CancellationToken token)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, token).ConfigureAwait(false);
        }

        private static void ApplySeedCredentialHeaders(ClientWebSocket socket)
        {
            ApplyCredentialHeaders(socket, "default");
        }

        private static void ApplyCredentialHeaders(ClientWebSocket socket, string accessKey)
        {
            socket.Options.SetRequestHeader("x-tenant-guid", Constants.DefaultTenantId);
            socket.Options.SetRequestHeader("x-access-key", accessKey);
        }

        private static async Task AuthenticateSeedAdminAsync(HttpClient httpClient, string baseUrl)
        {
            await AuthenticateUserAsync(httpClient, baseUrl, Constants.DefaultTenantId, "admin@xeno", "password").ConfigureAwait(false);
        }

        private static async Task AuthenticateUserAsync(HttpClient httpClient, string baseUrl, string tenantId, string email, string password)
        {
            string loginBody = JsonUtil.Serialize(new AuthLoginRequest
            {
                TenantId = tenantId,
                Email = email,
                Password = password
            });
            using (HttpResponseMessage loginResponse = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/auth/dashboard/login", loginBody).ConfigureAwait(false))
            {
                string loginJson = await loginResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!loginResponse.IsSuccessStatusCode) throw new InvalidOperationException("Authentication failed for " + email + ": " + loginJson);
                AuthDashboardLoginResponse login = DeserializeRequired<AuthDashboardLoginResponse>(loginJson);
                ApplyBearerHeaders(httpClient, login.TenantId, login.Token);
            }
        }

        private static void ApplyBearerHeaders(HttpClient httpClient, string tenantId, string token)
        {
            httpClient.DefaultRequestHeaders.Remove("Authorization");
            httpClient.DefaultRequestHeaders.Remove("x-token");
            httpClient.DefaultRequestHeaders.Remove("x-tenant-guid");
            httpClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
            httpClient.DefaultRequestHeaders.Add("x-token", token);
        }

        private static async Task<string> CreateUserViaApiAsync(HttpClient httpClient, string baseUrl, string tenantId, string email, string password, bool isAdmin, bool isTenantAdmin)
        {
            using (HttpResponseMessage response = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/users", JsonUtil.Serialize(new
            {
                tenantId,
                username = email,
                email,
                password,
                isAdmin,
                isTenantAdmin,
                active = true
            })).ConfigureAwait(false))
            {
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("User create failed for " + email + ": " + json);
                return FirstRecordId("[" + json + "]");
            }
        }

        private static async Task<string> CreatePermissionViaApiAsync(HttpClient httpClient, string baseUrl, string resourceType, string operationType)
        {
            using (HttpResponseMessage response = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/permissions", JsonUtil.Serialize(new
            {
                resourceType,
                operationType,
                permit = true,
                active = true
            })).ConfigureAwait(false))
            {
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Permission create failed: " + json);
                return FirstRecordId("[" + json + "]");
            }
        }

        private static async Task<string> CreateRoleViaApiAsync(HttpClient httpClient, string baseUrl, string name, string permissionId)
        {
            using (HttpResponseMessage response = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/roles", JsonUtil.Serialize(new
            {
                name,
                permissionIds = new[] { permissionId },
                active = true
            })).ConfigureAwait(false))
            {
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Role create failed: " + json);
                return FirstRecordId("[" + json + "]");
            }
        }

        private static async Task AssignRoleViaApiAsync(HttpClient httpClient, string baseUrl, string roleId, PrincipalTypeEnum principalType, string principalId)
        {
            using (HttpResponseMessage response = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/role-assignments", JsonUtil.Serialize(new
            {
                roleId,
                principalType = principalType.ToString(),
                principalId,
                active = true
            })).ConfigureAwait(false))
            {
                string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Role assignment failed: " + json);
            }
        }

        private static async Task AssertProtectedGetRouteAsync(HttpClient anonymousClient, HttpClient noAccessClient, HttpClient adminClient, string url, string name)
        {
            using (HttpResponseMessage anonymous = await anonymousClient.GetAsync(url).ConfigureAwait(false))
            {
                string body = await anonymous.Content.ReadAsStringAsync().ConfigureAwait(false);
                if ((int)anonymous.StatusCode != 401) throw new InvalidOperationException("Anonymous caller should receive 401 for " + name + ": " + (int)anonymous.StatusCode + " " + body);
            }

            using (HttpResponseMessage noAccess = await noAccessClient.GetAsync(url).ConfigureAwait(false))
            {
                string body = await noAccess.Content.ReadAsStringAsync().ConfigureAwait(false);
                if ((int)noAccess.StatusCode != 403) throw new InvalidOperationException("Unprivileged caller should receive 403 for " + name + ": " + (int)noAccess.StatusCode + " " + body);
            }

            using (HttpResponseMessage admin = await adminClient.GetAsync(url).ConfigureAwait(false))
            {
                string body = await admin.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!admin.IsSuccessStatusCode) throw new InvalidOperationException("Administrator caller should access " + name + ": " + (int)admin.StatusCode + " " + body);
            }
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

        private static async Task RunTelemetryOpenApiSmokeAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-telemetry-openapi-" + Guid.NewGuid().ToString("N") + ".db");
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
                        string openApi = await httpClient.GetStringAsync("http://127.0.0.1:" + port + "/openapi.json").ConfigureAwait(false);
                        foreach (OpenApiRouteExpectation route in new List<OpenApiRouteExpectation>
                        {
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/status", Method = "get" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/latest", Method = "get" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/collect", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/history/search", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/history/rollups", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/history/{id}", Method = "get" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/history/{id}", Method = "delete" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/history/delete", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/providers/latest", Method = "get" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/providers/collect", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/providers/history/search", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/providers/history/rollups", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/providers/history/{id}", Method = "get" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/providers/history/{id}", Method = "delete" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/providers/history/delete", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/collection-attempts", Method = "get" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/metrics", Method = "get" }
                        })
                        {
                            if (!OpenApiHasMethod(openApi, route.Route, route.Method)) throw new InvalidOperationException("OpenAPI missing " + route.Method.ToUpperInvariant() + " " + route.Route + ".");
                        }

                        foreach (OpenApiRouteExpectation route in new List<OpenApiRouteExpectation>
                        {
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/collect", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/history/search", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/history/rollups", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/history/delete", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/providers/collect", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/providers/history/search", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/providers/history/rollups", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/client-telemetry/providers/history/delete", Method = "post" },
                            new OpenApiRouteExpectation { Route = "/v1.0/api/routing-policies/validate", Method = "post" }
                        })
                        {
                            if (!OpenApiHasRequestBody(openApi, route.Route, route.Method)) throw new InvalidOperationException("OpenAPI missing request body on " + route.Method.ToUpperInvariant() + " " + route.Route + ".");
                        }
                    }
                }
                finally
                {
                    await StopServerAsync(server, serverTask, serverTokenSource, dbFile).ConfigureAwait(false);
                }
            }
        }

        private static async Task RunRoutingPolicyValidationSmokeAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-routing-validation-" + Guid.NewGuid().ToString("N") + ".db");
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
                        await AuthenticateSeedAdminAsync(httpClient, baseUrl).ConfigureAwait(false);
                        RoutingPolicySaveRequest invalidRequest = new RoutingPolicySaveRequest
                        {
                            Name = "Invalid telemetry policy",
                            ModelName = "*",
                            ClientName = "*",
                            MaxTelemetryAgeMs = 1,
                            TelemetryFilters = new List<RoutingPolicyCondition>
                            {
                                new RoutingPolicyCondition
                                {
                                    Metric = "missing.metric",
                                    ValueType = RoutingPolicyMetricValueTypeEnum.Number,
                                    Value = "abc"
                                },
                                new RoutingPolicyCondition
                                {
                                    Metric = "telemetry.gpu.available",
                                    ValueType = RoutingPolicyMetricValueTypeEnum.Number,
                                    Value = "1"
                                }
                            },
                            TelemetryRanking = new List<RoutingPolicyRankingRule>
                            {
                                new RoutingPolicyRankingRule
                                {
                                    Metric = "provider.healthStatus",
                                    Weight = 0
                                }
                            }
                        };

                        using (HttpResponseMessage response = await PostJsonAsync(httpClient, baseUrl + "/v1.0/api/routing-policies/validate", JsonUtil.Serialize(invalidRequest)).ConfigureAwait(false))
                        {
                            string json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                            if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Routing validation route failed: " + json);
                            RoutingPolicyValidationResult result = DeserializeRequired<RoutingPolicyValidationResult>(json);
                            if (result.Valid) throw new InvalidOperationException("Invalid telemetry policy was accepted.");
                            if (!result.Issues.Any(issue => issue.Code == "UnsupportedMetric")) throw new InvalidOperationException("Validation did not report unsupported metric.");
                            if (!result.Issues.Any(issue => issue.Code == "WrongValueType")) throw new InvalidOperationException("Validation did not report wrong value type.");
                            if (!result.Issues.Any(issue => issue.Code == "InvalidRankingWeight")) throw new InvalidOperationException("Validation did not report invalid ranking weight.");
                            if (!result.Issues.Any(issue => issue.Code == "InvalidTelemetryAge")) throw new InvalidOperationException("Validation did not report invalid telemetry age.");
                        }
                    }
                }
                finally
                {
                    await StopServerAsync(server, serverTask, serverTokenSource, dbFile).ConfigureAwait(false);
                }
            }
        }

        private static void AssertIdPrefix(string id, string prefix)
        {
            if (id.Length != IdGenerator.MaxIdLength) throw new InvalidOperationException("ID length mismatch: " + id);
            if (!id.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("ID prefix mismatch: " + id);
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
            if (!String.Equals(settings.Server.Url, "ws://app.xenocloud.ai:9000/ws/v1/clients", StringComparison.Ordinal)) throw new InvalidOperationException("Reset did not restore the default client server URL.");
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

        private static async Task InsertTelemetryRemnantRowsAsync(string dbFile, string tenantId, string providerSampleId)
        {
            using (SqliteConnection connection = new SqliteConnection("Data Source=" + dbFile + ";Pooling=False;"))
            {
                await connection.OpenAsync().ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = """
INSERT INTO client_telemetry_gpu_samples (id, sample_id, tenant_id, client_id, collected_utc, gpu_uuid, name, utilization_percent)
VALUES ('ctgpu_orphan', 'ctel_orphan', $tenantId, 'xcl_purge', $now, 'gpu-orphan', 'Orphan GPU', 99);
INSERT INTO client_provider_telemetry_models (id, sample_id, tenant_id, client_id, provider_name, model_name)
VALUES ('ptm_child', $providerSampleId, $tenantId, 'xcl_purge', 'purge-provider', 'purge-model');
INSERT INTO client_provider_telemetry_models (id, sample_id, tenant_id, client_id, provider_name, model_name)
VALUES ('ptm_orphan', 'ptel_orphan', $tenantId, 'xcl_purge', 'purge-provider', 'orphan-model');
INSERT INTO client_provider_telemetry_metrics (id, sample_id, tenant_id, client_id, provider_name, model_name, metric_name, metric_kind, metric_value, unit, source, labels_json)
VALUES ('ptmet_child', $providerSampleId, $tenantId, 'xcl_purge', 'purge-provider', 'purge-model', 'active_requests', 'gauge', 1, 'count', 'Unknown', NULL);
INSERT INTO client_provider_telemetry_metrics (id, sample_id, tenant_id, client_id, provider_name, model_name, metric_name, metric_kind, metric_value, unit, source, labels_json)
VALUES ('ptmet_orphan', 'ptel_orphan', $tenantId, 'xcl_purge', 'purge-provider', 'orphan-model', 'active_requests', 'gauge', 1, 'count', 'Unknown', NULL);
""";
                    command.Parameters.AddWithValue("$tenantId", tenantId);
                    command.Parameters.AddWithValue("$providerSampleId", providerSampleId);
                    command.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
                    await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                }
            }
        }

        private static async Task<int> CountRowsForTenantAsync(string dbFile, string tableName, string tenantId)
        {
            using (SqliteConnection connection = new SqliteConnection("Data Source=" + dbFile + ";Pooling=False;"))
            {
                await connection.OpenAsync().ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT COUNT(*) FROM " + tableName + " WHERE tenant_id = $tenantId;";
                    command.Parameters.AddWithValue("$tenantId", tenantId);
                    object? value = await command.ExecuteScalarAsync().ConfigureAwait(false);
                    return Convert.ToInt32(value);
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

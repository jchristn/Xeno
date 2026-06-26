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

                                ClientCommandResult deleted = await RunXenoClientAsync(new[] { "model", "delete", "--name", "llama3.2:latest" }, clientHome).ConfigureAwait(false);
                                if (deleted.ExitCode != 0) throw new InvalidOperationException("Model delete failed: " + deleted.StandardError + deleted.StandardOutput);
                                if (!deleted.StandardOutput.Contains("Deleted model", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Model delete did not report success.");

                                ClientCommandResult emptyList = await RunXenoClientAsync(new[] { "--json", "model", "list" }, clientHome).ConfigureAwait(false);
                                if (emptyList.ExitCode != 0) throw new InvalidOperationException("Model list after delete failed: " + emptyList.StandardError + emptyList.StandardOutput);
                                List<TestCliModelRecord> modelList = DeserializeRequired<List<TestCliModelRecord>>(emptyList.StandardOutput);
                                if (modelList.Count != 0) throw new InvalidOperationException("Model delete did not remove the configured model.");

                                ClientCommandResult missing = await RunXenoClientAsync(new[] { "model", "delete", "--name", "llama3.2:latest" }, clientHome).ConfigureAwait(false);
                                if (missing.ExitCode == 0) throw new InvalidOperationException("Deleting a missing model should fail.");
                                if (!missing.StandardOutput.Contains("Model not found", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Missing model delete did not print a useful error.");

                                ClientCommandResult added = await RunXenoClientAsync(new[] { "model", "add", "--provider", "local-ollama", "--name", "llama3.2:latest" }, clientHome).ConfigureAwait(false);
                                if (added.ExitCode != 0) throw new InvalidOperationException("Model add before scoped delete failed: " + added.StandardError + added.StandardOutput);

                                ClientCommandResult scopedDelete = await RunXenoClientAsync(new[] { "model", "delete", "--provider", "local-ollama", "--name", "llama3.2:latest" }, clientHome).ConfigureAwait(false);
                                if (scopedDelete.ExitCode != 0) throw new InvalidOperationException("Provider-scoped model delete failed: " + scopedDelete.StandardError + scopedDelete.StandardOutput);
                            }
                            finally
                            {
                                if (Directory.Exists(clientHome)) Directory.Delete(clientHome, true);
                            }
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
                            platform.Clients[0].Connected = true;
                            RoutingService routing = new RoutingService(platform);
                            RoutingDecision decision = await routing.RouteAsync(new RequestContext { TenantId = Constants.DefaultTenantId, IsAuthenticated = true }, "llama3.2:latest", false).ConfigureAwait(false);
                            if (!decision.Success) throw new InvalidOperationException(decision.Message);
                            if (String.IsNullOrWhiteSpace(decision.SelectedClientId)) throw new InvalidOperationException("Client not selected.");
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
                            RoutingService routing = new RoutingService(platform, new RoutingSettings { LoadBalancingMode = LoadBalancingModeEnum.Random });
                            RoutingDecision decision = await routing.RouteAsync(new RequestContext { TenantId = Constants.DefaultTenantId, IsAuthenticated = true }, "llama3.2:latest", false).ConfigureAwait(false);
                            if (!decision.Success) throw new InvalidOperationException(decision.Message);
                            if (decision.LoadBalancingMode != LoadBalancingModeEnum.Random) throw new InvalidOperationException("Random mode was not recorded.");
                            if (!decision.CandidateClientIds.Contains(decision.SelectedClientId ?? String.Empty)) throw new InvalidOperationException("Random selected a non-candidate client.");
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
                            new OpenApiRouteExpectation { Route = "/v1.0/api/auth/logout", Method = "post" }
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
                        foreach (string route in new[] { "/v1.0/api/tenants/{id}", "/v1.0/api/users/{id}", "/v1.0/api/credentials/{id}", "/v1.0/api/roles/{id}", "/v1.0/api/permissions/{id}" })
                        {
                            if (!OpenApiHasMethod(openApi, route, "put")) throw new InvalidOperationException("OpenAPI missing PUT " + route + ".");
                        }

                        foreach (string route in new[] { "/v1.0/api/tenants", "/v1.0/api/users", "/v1.0/api/credentials", "/v1.0/api/roles", "/v1.0/api/permissions" })
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
                                        Provider = "Ollama",
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
                    }

                    using (HttpClient httpClient = new HttpClient())
                    {
                        string clients = await httpClient.GetStringAsync("http://127.0.0.1:" + port + "/v1.0/api/clients").ConfigureAwait(false);
                        if (!clients.Contains("touchstone-pascal-model", StringComparison.Ordinal)) throw new InvalidOperationException("Registered client models did not include PascalCase inventory model: " + clients);

                        string models = await httpClient.GetStringAsync("http://127.0.0.1:" + port + "/v1/models").ConfigureAwait(false);
                        if (!models.Contains("touchstone-pascal-model", StringComparison.Ordinal)) throw new InvalidOperationException("OpenAI model list did not include PascalCase inventory model: " + models);
                    }
                }
                finally
                {
                    await StopServerAsync(server, serverTask, serverTokenSource, dbFile).ConfigureAwait(false);
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
                                        provider = "OpenAICompatible",
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
                                if (HeaderValue(response, "X-Xeno-Selected-Provider") != "OpenAICompatible") throw new InvalidOperationException("Relay response did not include selected provider header.");
                                if (HeaderValue(response, "X-Xeno-Selected-Source") != "touchstone-runner") throw new InvalidOperationException("Relay response did not include selected source header.");
                                if (HeaderValue(response, "X-Xeno-Routing-Candidate-Count") != "1") throw new InvalidOperationException("Relay response did not include candidate count header.");
                                if (HeaderValue(response, "X-Xeno-Load-Balancing-Mode") != "RoundRobin") throw new InvalidOperationException("Relay response did not include load-balancing mode header.");
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
                                        provider = "OpenAICompatible",
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
            using (HttpClient httpClient = new HttpClient())
            {
                for (int i = 0; i < 50; i++)
                {
                    using (HttpResponseMessage response = await httpClient.GetAsync("http://127.0.0.1:" + port + "/v1.0/api/clients").ConfigureAwait(false))
                    {
                        string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                        if (response.IsSuccessStatusCode && body.Contains("xcl_touchstone", StringComparison.Ordinal)) return;
                    }

                    await Task.Delay(100).ConfigureAwait(false);
                }
            }

            throw new InvalidOperationException("Fake client did not register.");
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

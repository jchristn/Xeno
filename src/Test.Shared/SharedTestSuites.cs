namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Sockets;
    using System.Net.WebSockets;
    using System.Text;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
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
                                if (id.Length > IdGenerator.MaxIdLength) throw new InvalidOperationException("ID exceeded max length: " + id);
                                if (!id.StartsWith(prefix, StringComparison.Ordinal)) throw new InvalidOperationException("ID prefix mismatch: " + id);
                            }

                            List<string> ids = Enumerable.Range(0, 64).Select(_ => IdGenerator.NewId("req_")).ToList();
                            if (ids.Distinct(StringComparer.Ordinal).Count() != ids.Count) throw new InvalidOperationException("Generated IDs were not unique.");
                            for (int i = 1; i < ids.Count; i++)
                            {
                                if (String.CompareOrdinal(ids[i - 1], ids[i]) > 0) throw new InvalidOperationException("Generated IDs were not k-sortable.");
                            }

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
                        "LeastInFlightSelectsLowerLoad",
                        "Direct routing selects the lower-load client",
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
                            RoutingService routing = new RoutingService(platform);
                            RoutingDecision decision = await routing.RouteAsync(new RequestContext { TenantId = Constants.DefaultTenantId, IsAuthenticated = true }, "llama3.2:latest", false).ConfigureAwait(false);
                            if (!decision.Success) throw new InvalidOperationException(decision.Message);
                            if (decision.SelectedClientId != client.Id) throw new InvalidOperationException("Least in-flight client was not selected.");
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
                        executeAsync: delegate { return RunManagementSmokeAsync(); })
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

                        using (JsonDocument document = JsonDocument.Parse(responseBody))
                        using (ClientWebSocket socket = new ClientWebSocket())
                        {
                            string accessKey = TryGetString(document.RootElement, "accessKey") ?? String.Empty;
                            string secretKey = TryGetString(document.RootElement, "secretKey") ?? String.Empty;
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

                using (JsonDocument document = JsonDocument.Parse(message))
                {
                    string requestId = TryGetString(document.RootElement, "requestId") ?? Guid.NewGuid().ToString("N");
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

        private static string? TryGetString(JsonElement element, string propertyName)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property)) return null;
            if (property.ValueKind == JsonValueKind.Null || property.ValueKind == JsonValueKind.Undefined) return null;
            return property.ToString();
        }
    }
}

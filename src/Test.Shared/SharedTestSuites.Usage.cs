namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Touchstone.Core;
    using Xeno.Core;
    using Xeno.Core.Analytics;
    using Xeno.Core.Chat;
    using Xeno.Core.Database;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Enums;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;
    using Xeno.Core.Settings;
    using Xeno.Server;

    /// <summary>
    /// Server usage analytics: typed analytics columns and their backfill on every database provider, tenant and user
    /// scoping, and the usage, chat usage, and provider health routes with their validation and authorization.
    /// </summary>
    public static partial class SharedTestSuites
    {
        /// <summary>
        /// Server usage suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor ServerUsageSuite()
        {
            return new TestSuiteDescriptor("ServerUsage", "Server usage analytics", new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor("ServerUsage", "UsageColumnsBackfillAndScope", "Request history and chat turns keep typed usage columns, backfill old rows, and never cross tenants", executeAsync: async delegate { await RunUsageStorageAsync().ConfigureAwait(false); }),
                new TestCaseDescriptor("ServerUsage", "UsageRoutesValidateAndAuthorize", "Usage, chat usage, and provider health routes answer, reject bad input with 400, and enforce roles", executeAsync: async delegate { await RunUsageRoutesAsync().ConfigureAwait(false); })
            });
        }

        private static async Task RunUsageStorageAsync()
        {
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-usage-" + Guid.NewGuid().ToString("N") + ".db");
            DatabaseDriverBase driver = await DatabaseDriverFactory.CreateAndInitializeAsync(TestDatabase.Settings(dbFile), null, CancellationToken.None).ConfigureAwait(false);
            try
            {
                if (driver.RequestHistory == null || driver.Chat == null) throw new InvalidOperationException("The driver has no request history or chat store.");
                DateTime now = DateTime.UtcNow;
                await driver.RequestHistory.InsertAsync(UsageEntry("rh_a1", "ten_a", "usr_1", "llama3", now.AddMinutes(-10), 200, 1000, 300, 600, 12, 40), CancellationToken.None).ConfigureAwait(false);
                await driver.RequestHistory.InsertAsync(UsageEntry("rh_a2", "ten_a", "usr_2", "qwen3", now.AddMinutes(-5), 502, 50, null, null, null, null), CancellationToken.None).ConfigureAwait(false);
                await driver.RequestHistory.InsertAsync(UsageEntry("rh_b1", "ten_b", "usr_1", "llama3", now.AddMinutes(-5), 200, 700, 100, 500, 5, 9), CancellationToken.None).ConfigureAwait(false);

                IConnectedClientSampleMethods samples = driver.ConnectedClientSamples ?? throw new InvalidOperationException("The driver has no connected client samples.");
                await samples.InsertAsync(new ConnectedClientSample { TenantId = "ten_a", SampledUtc = now.AddMinutes(-40), Connected = 1, Total = 3 }, CancellationToken.None).ConfigureAwait(false);
                await samples.InsertAsync(new ConnectedClientSample { TenantId = "ten_a", SampledUtc = now.AddMinutes(-39.5), Connected = 2, Total = 3 }, CancellationToken.None).ConfigureAwait(false);
                await samples.InsertAsync(new ConnectedClientSample { TenantId = "ten_b", SampledUtc = now.AddMinutes(-40), Connected = 9, Total = 9 }, CancellationToken.None).ConfigureAwait(false);
                await samples.InsertAsync(new ConnectedClientSample { TenantId = "ten_a", SampledUtc = now.AddDays(-31), Connected = 7, Total = 7 }, CancellationToken.None).ConfigureAwait(false);
                UsageCheck(await samples.DeleteOlderThanAsync(now.AddDays(-30), CancellationToken.None).ConfigureAwait(false) == 1, "Old connected client samples were not pruned.");
                List<ConnectedClientSample> tenantSamples = await samples.ListAsync("ten_a", now.AddHours(-1), now, CancellationToken.None).ConfigureAwait(false);
                UsageCheck(tenantSamples.Count == 2 && tenantSamples.All(item => item.TenantId == "ten_a" && item.Total == 3), "Connected client samples are not scoped to the tenant.");
                ConnectedClientHistory bucketed = ConnectedClientHistoryBuilder.Build(tenantSamples, now.AddHours(-1), now, 15);
                UsageCheck(bucketed.Buckets.Count == 4 && bucketed.Buckets[1].Connected == 2 && bucketed.Buckets[0].Connected == null && bucketed.Buckets[3].Total == null, "Connected client buckets should take the last sample and leave gaps empty.");
                await samples.DeleteByTenantAsync("ten_a", CancellationToken.None).ConfigureAwait(false);
                UsageCheck((await samples.ListAsync("ten_a", now.AddHours(-1), now, CancellationToken.None).ConfigureAwait(false)).Count == 0, "Deleting a tenant should remove its samples.");

                List<UsageRecord> a = await ToListAsync(driver.RequestHistory.EnumerateUsageAsync(new RequestHistoryUsageFilter { TenantId = "ten_a", FromUtc = now.AddHours(-1), ToUtc = now.AddMinutes(1) }, CancellationToken.None)).ConfigureAwait(false);
                UsageCheck(a.Count == 2 && a.All(item => item.TenantId == "ten_a"), "Tenant A sees " + a.Count + " rows.");
                UsageRecord llama = a.Single(item => item.Model == "llama3");
                UsageCheck(llama.TimeToFirstTokenMs == 300 && llama.StreamingMs == 600 && llama.PromptTokens == 12 && llama.CompletionTokens == 40 && llama.Provider == "ollama" && llama.ClientId == "client-1" && llama.Success, "Typed columns were not written.");
                UsageCheck(llama.Stages.Any(item => item.Stage == UsageStageEnum.Streaming && item.Milliseconds == 600), "Stages were not derived from typed columns.");
                UsageCheck(!a.Single(item => item.Model == "qwen3").Success, "A 502 must count as failed.");
                List<UsageRecord> user = await ToListAsync(driver.RequestHistory.EnumerateUsageAsync(new RequestHistoryUsageFilter { TenantId = "ten_a", UserId = "usr_2", FromUtc = now.AddHours(-1), ToUtc = now.AddMinutes(1) }, CancellationToken.None)).ConfigureAwait(false);
                UsageCheck(user.Count == 1 && user[0].UserId == "usr_2", "The user filter is wrong.");
                List<UsageRecord> every = await ToListAsync(driver.RequestHistory.EnumerateUsageAsync(new RequestHistoryUsageFilter { TenantId = null, FromUtc = now.AddHours(-1), ToUtc = now.AddMinutes(1) }, CancellationToken.None)).ConfigureAwait(false);
                UsageCheck(every.Count == 3, "Reading every tenant is wrong.");
                UsageCheck(await driver.RequestHistory.BackfillUsageAsync(100, CancellationToken.None).ConfigureAwait(false) == 0, "New rows should not need a backfill.");

                if (TestDatabase.Provider == DatabaseTypeEnum.Sqlite)
                {
                    // Simulate rows written before the analytics columns existed, then backfill them in two batches.
                    using (SqliteConnection connection = new SqliteConnection("Data Source=" + dbFile + ";Pooling=False;"))
                    {
                        await connection.OpenAsync().ConfigureAwait(false);
                        using (SqliteCommand command = connection.CreateCommand())
                        {
                            command.CommandText = "UPDATE request_history SET usage_version = NULL, ttft_ms = NULL, streaming_ms = NULL, prompt_tokens = NULL, completion_tokens = NULL, selected_provider = NULL;";
                            await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                        }
                    }

                    UsageCheck(await driver.RequestHistory.BackfillUsageAsync(2, CancellationToken.None).ConfigureAwait(false) == 2, "The first batch is wrong.");
                    UsageCheck(await driver.RequestHistory.BackfillUsageAsync(2, CancellationToken.None).ConfigureAwait(false) == 1, "The backfill did not resume.");
                    UsageCheck(await driver.RequestHistory.BackfillUsageAsync(2, CancellationToken.None).ConfigureAwait(false) == 0, "The backfill did not finish.");
                    List<UsageRecord> restored = await ToListAsync(driver.RequestHistory.EnumerateUsageAsync(new RequestHistoryUsageFilter { TenantId = "ten_a", Model = "llama3", FromUtc = now.AddHours(-1), ToUtc = now.AddMinutes(1) }, CancellationToken.None)).ConfigureAwait(false);
                    UsageCheck(restored.Single().TimeToFirstTokenMs == 300 && restored.Single().CompletionTokens == 40 && restored.Single().Provider == "ollama", "The backfill did not restore values from entry_json.");
                }

                IChatStore alice = driver.Chat.For("ten_a", "usr_alice");
                IChatStore bob = driver.Chat.For("ten_a", "usr_bob");
                IChatStore other = driver.Chat.For("ten_b", "usr_alice");
                await alice.UpsertTurnAsync(new ChatTurn { Id = "chturn_u1", ConversationId = "c1", Model = "llama3", Route = "network", Provider = "client-1", Status = ChatTurnStatusEnum.Success, TotalMs = 2000, ModelMs = 1500, TimeToFirstTokenMs = 200, StreamingMs = 1200, ToolMs = 300, ApprovalMs = 100, ToolCallCount = 2, ToolDenied = 1, PromptTokens = 30, CachedPromptTokens = 10, CompletionTokens = 60, CreatedUtc = now.AddMinutes(-4) }, CancellationToken.None).ConfigureAwait(false);
                await bob.UpsertTurnAsync(new ChatTurn { Id = "chturn_u2", ConversationId = "c2", Model = "qwen3", Route = "network", Status = ChatTurnStatusEnum.Error, TotalMs = 10, CreatedUtc = now.AddMinutes(-3) }, CancellationToken.None).ConfigureAwait(false);
                await bob.UpsertTurnAsync(new ChatTurn { Id = "chturn_u3", ConversationId = "c2", Model = "qwen3", Route = "network", Status = ChatTurnStatusEnum.Running, CreatedUtc = now.AddMinutes(-2) }, CancellationToken.None).ConfigureAwait(false);
                await other.UpsertTurnAsync(new ChatTurn { Id = "chturn_u4", ConversationId = "c3", Model = "llama3", Route = "network", Status = ChatTurnStatusEnum.Success, TotalMs = 10, CreatedUtc = now.AddMinutes(-2) }, CancellationToken.None).ConfigureAwait(false);

                List<UsageRecord> mine = await ToListAsync(alice.EnumerateUsageAsync(now.AddHours(-1), now.AddMinutes(1), CancellationToken.None)).ConfigureAwait(false);
                UsageCheck(mine.Count == 1 && mine[0].TimeToFirstTokenMs == 200 && mine[0].StreamingMs == 1200 && mine[0].CachedPromptTokens == 10 && mine[0].ToolDenied == 1 && mine[0].ToolCalls == 1 && mine[0].UserId == "usr_alice", "Alice's chat usage is wrong.");
                UsageCheck(mine[0].Stages.Any(item => item.Stage == UsageStageEnum.Approval && item.Milliseconds == 100), "Chat stages are wrong.");
                List<UsageRecord> tenant = await ToListAsync(driver.Chat.For("ten_a", null).EnumerateUsageAsync(now.AddHours(-1), now.AddMinutes(1), CancellationToken.None)).ConfigureAwait(false);
                UsageCheck(tenant.Count == 2 && tenant.All(item => item.TenantId == "ten_a"), "Tenant chat usage must skip running turns and other tenants: " + tenant.Count);
            }
            finally
            {
                driver.Dispose();
                await TestDatabase.CleanupAsync(dbFile).ConfigureAwait(false);
            }
        }

        private static async Task RunUsageRoutesAsync()
        {
            int port = GetFreePort();
            string dbFile = Path.Combine(Path.GetTempPath(), "xeno-usage-routes-" + Guid.NewGuid().ToString("N") + ".db");
            XenoSettings settings = new XenoSettings
            {
                Rest = new RestSettings { Hostname = "127.0.0.1", Port = port, Ssl = false },
                Database = TestDatabase.Settings(dbFile)
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
                    using (HttpClient tenantAdminClient = new HttpClient())
                    {
                        await AuthenticateSeedAdminAsync(adminClient, baseUrl).ConfigureAwait(false);
                        await CreateUserViaApiAsync(adminClient, baseUrl, Constants.DefaultTenantId, "usage-no-access@usage.test", "password", false, false).ConfigureAwait(false);
                        await AuthenticateUserAsync(noAccessClient, baseUrl, Constants.DefaultTenantId, "usage-no-access@usage.test", "password").ConfigureAwait(false);
                        await CreateUserViaApiAsync(adminClient, baseUrl, Constants.DefaultTenantId, "usage-tenant-admin@usage.test", "password", false, true).ConfigureAwait(false);
                        await AuthenticateUserAsync(tenantAdminClient, baseUrl, Constants.DefaultTenantId, "usage-tenant-admin@usage.test", "password").ConfigureAwait(false);

                        using (HttpResponseMessage completion = await PostJsonAsync(adminClient, baseUrl + "/v1/chat/completions", "{\"model\":\"missing-model\",\"messages\":[{\"role\":\"user\",\"content\":\"hello\"}]}").ConfigureAwait(false))
                        {
                            UsageCheck((int)completion.StatusCode >= 400, "The missing model request should fail.");
                        }

                        DateTime to = DateTime.UtcNow.AddMinutes(1);
                        string range = "fromUtc=" + Uri.EscapeDataString(to.AddHours(-1).ToString("O")) + "&toUtc=" + Uri.EscapeDataString(to.ToString("O")) + "&bucketMinutes=1";
                        UsageAnalytics analytics = await GetUsageAsync(adminClient, baseUrl + "/v1.0/api/request-history/analytics/usage?" + range + "&utcOffsetMinutes=-480").ConfigureAwait(false);
                        for (int attempt = 0; attempt < 50 && analytics.Totals.Requests == 0; attempt++)
                        {
                            // Request history is written in the background after the response.
                            await Task.Delay(100).ConfigureAwait(false);
                            analytics = await GetUsageAsync(adminClient, baseUrl + "/v1.0/api/request-history/analytics/usage?" + range + "&utcOffsetMinutes=-480").ConfigureAwait(false);
                        }

                        UsageCheck(analytics.Dataset == UsageDatasetEnum.NetworkRequests && analytics.Buckets.Count == 60 && analytics.BucketMinutes == 1, "Usage buckets are wrong: " + analytics.Buckets.Count);
                        UsageCheck(analytics.Totals.Requests >= 1 && analytics.Totals.Failed >= 1 && analytics.GroupKeys.Count >= 1, "The failed request is missing from usage: " + analytics.Totals.Requests + " " + analytics.Totals.Failed + " " + String.Join(",", analytics.GroupKeys) + " " + analytics.TruncatedReason);
                        UsageCheck(analytics.Heatmap != null && analytics.Heatmap.UtcOffsetMinutes == -480, "The heatmap offset was not applied.");
                        UsageAnalytics byTenant = await GetUsageAsync(adminClient, baseUrl + "/v1.0/api/request-history/analytics/usage?" + range + "&groupBy=Tenant&sections=Totals,Groups").ConfigureAwait(false);
                        UsageCheck(byTenant.GroupKeys.Contains(Constants.DefaultTenantId) && byTenant.Heatmap == null, "A global administrator should group by tenant, and sections should limit the work.");

                        await AssertProtectedGetRouteAsync(anonymousClient, noAccessClient, adminClient, baseUrl + "/v1.0/api/request-history/analytics/usage?" + range, "usage analytics").ConfigureAwait(false);
                        await AssertProtectedGetRouteAsync(anonymousClient, noAccessClient, adminClient, baseUrl + "/v1.0/api/client-telemetry/providers/health?" + range, "provider health").ConfigureAwait(false);
                        await ExpectStatusAsync(tenantAdminClient, baseUrl + "/v1.0/api/request-history/analytics/usage?" + range + "&groupBy=Tenant", 403).ConfigureAwait(false);
                        using (HttpResponseMessage crossTenant = await tenantAdminClient.GetAsync(baseUrl + "/v1.0/api/request-history/analytics/usage?" + range + "&tenantId=ten_other").ConfigureAwait(false))
                        {
                            // tenantId selects the tenant a request runs in, so a tenant administrator naming another tenant is turned away before the route runs.
                            UsageCheck((int)crossTenant.StatusCode == 401 || (int)crossTenant.StatusCode == 403, "Another tenant's usage must be denied: " + (int)crossTenant.StatusCode);
                        }
                        await ExpectStatusAsync(tenantAdminClient, baseUrl + "/v1.0/api/request-history/analytics/usage?" + range + "&groupBy=User", 200).ConfigureAwait(false);
                        await ExpectStatusAsync(adminClient, baseUrl + "/v1.0/api/request-history/analytics/usage?fromUtc=" + Uri.EscapeDataString(to.AddDays(-30).ToString("O")) + "&toUtc=" + Uri.EscapeDataString(to.ToString("O")) + "&bucketMinutes=1", 400).ConfigureAwait(false);
                        await ExpectStatusAsync(adminClient, baseUrl + "/v1.0/api/request-history/analytics/usage?" + range + "&groupBy=Nope", 400).ConfigureAwait(false);
                        await ExpectStatusAsync(adminClient, baseUrl + "/v1.0/api/request-history/analytics/usage?" + range + "&sections=Nope", 400).ConfigureAwait(false);
                        await ExpectStatusAsync(adminClient, baseUrl + "/v1.0/api/request-history/analytics/usage?fromUtc=yesterday", 400).ConfigureAwait(false);

                        UsageAnalytics chat = await GetUsageAsync(adminClient, baseUrl + "/v1.0/api/chat/analytics/usage?" + range).ConfigureAwait(false);
                        UsageCheck(chat.Dataset == UsageDatasetEnum.Chat && chat.Buckets.Count == 60 && chat.StageOrder.Contains(UsageStageEnum.Tools), "Chat usage is wrong.");
                        await ExpectStatusAsync(adminClient, baseUrl + "/v1.0/api/chat/analytics/usage?" + range + "&groupBy=User", 400).ConfigureAwait(false);
                        await ExpectStatusAsync(adminClient, baseUrl + "/v1.0/api/chat/analytics/usage?" + range + "&groupBy=User&scope=tenant", 200).ConfigureAwait(false);

                        string health = await adminClient.GetStringAsync(baseUrl + "/v1.0/api/client-telemetry/providers/health?" + range).ConfigureAwait(false);
                        ProviderHealthStrip strip = JsonUtil.Deserialize<ProviderHealthStrip>(health) ?? throw new InvalidOperationException("No health strip.");
                        UsageCheck(strip.BucketMinutes == 1, "The health strip is wrong.");

                        // The server samples connected clients when it starts and every minute after.
                        ConnectedClientHistory? connected = null;
                        for (int attempt = 0; attempt < 50 && (connected == null || !connected.Buckets.Any(item => item.Total.HasValue)); attempt++)
                        {
                            DateTime window = DateTime.UtcNow.AddMinutes(1);
                            string connectedRange = "fromUtc=" + Uri.EscapeDataString(window.AddHours(-1).ToString("O")) + "&toUtc=" + Uri.EscapeDataString(window.ToString("O")) + "&bucketMinutes=1";
                            connected = JsonUtil.Deserialize<ConnectedClientHistory>(await adminClient.GetStringAsync(baseUrl + "/v1.0/api/client-telemetry/connected-history?" + connectedRange).ConfigureAwait(false));
                            if (connected == null || !connected.Buckets.Any(item => item.Total.HasValue)) await Task.Delay(100).ConfigureAwait(false);
                        }

                        UsageCheck(connected != null && connected.Buckets.Count == 60 && connected.Buckets.Count(item => item.Total.HasValue) >= 1 && connected.Buckets.Count(item => !item.Total.HasValue) >= 50, "Connected client history is wrong.");
                        await ExpectStatusAsync(adminClient, baseUrl + "/v1.0/api/client-telemetry/connected-history?fromUtc=yesterday", 400).ConfigureAwait(false);

                        string summary = await adminClient.GetStringAsync(baseUrl + "/v1.0/api/request-history/summary").ConfigureAwait(false);
                        UsageCheck(summary.Contains("\"bucketMinutes\"", StringComparison.Ordinal) && summary.Contains("\"startUtc\"", StringComparison.Ordinal) && summary.Contains("\"failed\"", StringComparison.Ordinal) && !summary.Contains("\"StartUtc\"", StringComparison.Ordinal), "The summary JSON changed shape: " + summary.Substring(0, Math.Min(200, summary.Length)));

                        string openApi = await adminClient.GetStringAsync(baseUrl + "/openapi.json").ConfigureAwait(false);
                        UsageCheck(openApi.Contains("/v1.0/api/request-history/analytics/usage", StringComparison.Ordinal) && openApi.Contains("/v1.0/api/client-telemetry/providers/health", StringComparison.Ordinal) && openApi.Contains("/v1.0/api/chat/analytics/usage", StringComparison.Ordinal), "The OpenAPI document lacks the usage routes.");
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
                    await TestDatabase.CleanupAsync(dbFile).ConfigureAwait(false);
                }
            }
        }

        private static RequestHistoryEntry UsageEntry(string id, string tenant, string user, string model, DateTime created, int status, long durationMs, long? ttft, long? streaming, int? prompt, int? completion)
        {
            RequestHistoryEntry entry = new RequestHistoryEntry
            {
                Id = id,
                TenantId = tenant,
                RequestorTenantId = tenant,
                UserId = user,
                Model = model,
                SelectedClientId = "client-1",
                Path = "/v1/chat/completions",
                Method = "POST",
                StatusCode = status,
                DurationMs = durationMs,
                CreatedUtc = created,
                RoutingDecision = new RoutingDecision { SelectedProvider = "ollama" }
            };
            entry.Telemetry.TimeToFirstTokenMs = ttft;
            entry.Telemetry.TotalStreamingTimeMs = streaming;
            entry.Telemetry.PromptTokens = prompt;
            entry.Telemetry.CompletionTokens = completion;
            entry.Telemetry.ClientRoundTripMs = durationMs - 10;
            return entry;
        }

        private static async Task<UsageAnalytics> GetUsageAsync(HttpClient client, string url)
        {
            using (HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false))
            {
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (!response.IsSuccessStatusCode) throw new InvalidOperationException("Usage request failed: " + (int)response.StatusCode + " " + body);
                return JsonUtil.Deserialize<UsageAnalytics>(body) ?? throw new InvalidOperationException("Empty usage response.");
            }
        }

        private static async Task ExpectStatusAsync(HttpClient client, string url, int status)
        {
            using (HttpResponseMessage response = await client.GetAsync(url).ConfigureAwait(false))
            {
                string body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                if ((int)response.StatusCode != status) throw new InvalidOperationException("Expected " + status + " from " + url + ", got " + (int)response.StatusCode + ": " + body);
            }
        }

        private static async Task<List<UsageRecord>> ToListAsync(IAsyncEnumerable<UsageRecord> records)
        {
            List<UsageRecord> list = new List<UsageRecord>();
            await foreach (UsageRecord record in records.ConfigureAwait(false)) list.Add(record);
            return list;
        }

        private static void UsageCheck(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client;
    using Xeno.Client.Analytics;
    using Xeno.Client.Chat;
    using Xeno.Client.LocalDashboard.Database;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Routing;
    using Xeno.Core.Analytics;
    using Xeno.Core.Chat;

    /// <summary>
    /// Usage analytics on this machine: the local API routes for served requests, local endpoint requests, chat, and
    /// provider health, read through the client every front end uses.
    /// </summary>
    public static class LocalUsageTestSuites
    {
        /// <summary>
        /// Local usage suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor LocalUsageSuite()
        {
            const string suite = "LocalUsage";
            return new TestSuiteDescriptor(suite, "Local usage analytics", new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor(suite, "LocalApiServesUsage", "Served, local endpoint, and chat usage and provider health come from the local API, with 400 for bad queries", executeAsync: async delegate
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (ChatApiFixture fixture = new ChatApiFixture("Hi there"))
                    using (XenoChatClient chat = new XenoChatClient())
                    using (XenoUsageClient usage = new XenoUsageClient())
                    {
                        DateTime now = DateTime.UtcNow;
                        UsageAnalyticsQuery query = new UsageAnalyticsQuery { FromUtc = now.AddHours(-1), ToUtc = now.AddMinutes(1), BucketMinutes = 1 };

                        ChatConversation conversation = await chat.CreateConversationAsync(new ChatConversationRequest { Model = "llama3.1:8b" }, CancellationToken.None).ConfigureAwait(false);
                        await chat.SendAsync(conversation.Id, new ChatSendRequest { Content = "hello" }, null, CancellationToken.None).ConfigureAwait(false);
                        UsageAnalytics chatUsage = await usage.GetUsageAsync(UsageDatasetEnum.Chat, query, CancellationToken.None).ConfigureAwait(false);
                        Check(chatUsage.Dataset == UsageDatasetEnum.Chat && chatUsage.Totals.Requests == 1 && chatUsage.Totals.CompletionTokens == 2 && chatUsage.GroupKeys.SequenceEqual(new[] { "llama3.1:8b" }), "Chat usage is wrong: " + chatUsage.Totals.Requests);
                        Check(chatUsage.Totals.Fallbacks == 0 && chatUsage.StageAverages.Any(item => item.Stage == UsageStageEnum.ModelFirstToken), "Chat fallbacks or stages are missing.");

                        LocalDashboardDatabaseDriverBase driver = LocalDashboardDatabaseDriverFactory.Create(fixture.Settings.LocalDashboard.Database);
                        await driver.InitializeAsync(CancellationToken.None).ConfigureAwait(false);
                        LocalRequestHistoryEntry served = new LocalRequestHistoryEntry { Id = "lrh_usage1", ReceivedUtc = now.AddMinutes(-5), Method = "POST", Path = "/v1/chat/completions", ProviderName = "box", ModelName = "llama3.1:8b", StatusCode = 200, Outcome = LocalRequestOutcomeEnum.Success };
                        served.Telemetry.DurationMs = 900;
                        served.Telemetry.TimeToHeadersMs = 100;
                        served.Telemetry.TimeToFirstByteMs = 300;
                        served.Telemetry.PromptTokens = 7;
                        served.Telemetry.CompletionTokens = 30;
                        await driver.RequestHistory.UpsertAsync(served, new ClientLocalHistorySettings(), CancellationToken.None).ConfigureAwait(false);
                        UsageAnalytics servedUsage = await usage.GetUsageAsync(UsageDatasetEnum.ServedRequests, query, CancellationToken.None).ConfigureAwait(false);
                        Check(servedUsage.Totals.Requests == 1 && servedUsage.Totals.TimeToFirstToken.P50 == 300 && servedUsage.Totals.CompletionTokens == 30 && servedUsage.StageAverages.Single(item => item.Stage == UsageStageEnum.Streaming).AverageMs == 600, "Served usage is wrong.");

                        LocalInferenceRequestLog.Append(new LocalInferenceRequestRecord { Id = "lir_usage1", ReceivedUtc = now.AddMinutes(-2), Method = "POST", Path = "/v1/chat/completions", Model = "llama3.1:8b", Route = "network", Provider = String.Empty, StatusCode = 200, Fallback = true, LatencyMs = 1200, FirstByteMs = 400, QueuedMs = 50, PromptTokens = 3, CompletionTokens = 12 });
                        UsageAnalytics endpoint = await usage.GetUsageAsync(UsageDatasetEnum.LocalEndpoint, query, CancellationToken.None).ConfigureAwait(false);
                        Check(endpoint.Totals.Requests == 1 && endpoint.Totals.Fallbacks == 1 && endpoint.Totals.TimeToFirstToken.P50 == 450 && endpoint.StageAverages.Single(item => item.Stage == UsageStageEnum.Queue).AverageMs == 50, "Local endpoint usage is wrong.");
                        UsageAnalytics again = await usage.GetUsageAsync(UsageDatasetEnum.LocalEndpoint, query, CancellationToken.None).ConfigureAwait(false);
                        Check(again.Totals.Requests == 1, "Ingesting the log twice must not duplicate records.");

                        ProviderHealthStrip health = await usage.GetProviderHealthAsync(new ProviderHealthQuery { FromUtc = query.FromUtc, ToUtc = query.ToUtc, BucketMinutes = 1 }, CancellationToken.None).ConfigureAwait(false);
                        Check(health.BucketMinutes == 1 && health.Providers.Count == 0, "Provider health without samples is wrong.");

                        await ExpectThrowsAsync<ArgumentException>(() => usage.GetUsageAsync(UsageDatasetEnum.Chat, new UsageAnalyticsQuery { FromUtc = now.AddDays(-30), ToUtc = now, BucketMinutes = 1 }, CancellationToken.None)).ConfigureAwait(false);
                        await ExpectThrowsAsync<ArgumentException>(() => usage.GetUsageAsync(UsageDatasetEnum.ServedRequests, new UsageAnalyticsQuery { FromUtc = query.FromUtc, ToUtc = query.ToUtc, BucketMinutes = 1, GroupBy = UsageGroupByEnum.User }, CancellationToken.None)).ConfigureAwait(false);
                        ExpectThrows<ArgumentException>(() => XenoUsageClient.RelativePath(UsageDatasetEnum.NetworkRequests));
                        driver.Dispose();
                    }

                    using (ClientHomeScope home = new ClientHomeScope())
                    using (XenoUsageClient missing = new XenoUsageClient())
                    {
                        Check(!missing.IsAvailable, "Usage looked available without a running client.");
                        await ExpectThrowsAsync<InvalidOperationException>(() => missing.GetUsageAsync(UsageDatasetEnum.Chat, new UsageAnalyticsQuery(), CancellationToken.None)).ConfigureAwait(false);
                    }
                })
                ,
                new TestCaseDescriptor(suite, "UsageSettingsAreBackfilledAndClamped", "client.json gains the Analytics section and the local endpoint retention, the settings editors list them, and limits stay in range", executeAsync: delegate
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        System.IO.File.WriteAllText(ClientSettings.SettingsFile, "{\"Name\":\"old\",\"LocalInferenceServer\":{\"Port\":11435}}");
                        ClientSettings loaded = ClientSettings.Load();
                        string written = System.IO.File.ReadAllText(ClientSettings.SettingsFile);
                        Check(loaded.Analytics.MaxRecordsPerQuery == 250000 && loaded.Analytics.DefaultTopN == 8 && loaded.LocalInferenceServer.RequestRetentionDays == 30, "Defaults are wrong.");
                        Check(written.Contains("\"Analytics\"", StringComparison.Ordinal) && written.Contains("\"RequestRetentionDays\"", StringComparison.Ordinal), "client.json was not backfilled.");
                    }

                    ClientAnalyticsSettings analytics = new ClientAnalyticsSettings { MaxRecordsPerQuery = 5, DefaultTopN = 99 };
                    Check(analytics.MaxRecordsPerQuery == 10000 && analytics.DefaultTopN == 20 && analytics.ToOptions().MaxRecords == 10000, "Analytics limits were not clamped.");
                    Xeno.Core.Settings.AnalyticsSettings server = new Xeno.Core.Settings.AnalyticsSettings { MaxRecordsPerQuery = 99999999, DefaultTopN = 0 };
                    Check(server.MaxRecordsPerQuery == 5000000 && server.DefaultTopN == 1, "Server analytics limits were not clamped.");
                    Check(new ClientLocalInferenceServerSettings { RequestRetentionDays = 0 }.RequestRetentionDays == 1, "Retention was not clamped.");
                    HashSet<string> keys = new HashSet<string>(Xeno.Client.Presentation.XenoSettingsCatalog.Fields.Select(item => item.Key), StringComparer.Ordinal);
                    Check(keys.Contains("analytics.maxRecords") && keys.Contains("analytics.topN") && keys.Contains("inference.requestRetention"), "The settings catalog lacks the usage settings.");
                    return Task.CompletedTask;
                })
                ,
                new TestCaseDescriptor(suite, "CliAndMcpReportUsage", "xeno usage and the xeno_usage MCP tool return the same numbers as the local API, and bad options fail with a message", executeAsync: async delegate
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        CliResult notRunning = await RunCliAsync(home.Directory, "usage").ConfigureAwait(false);
                        Check(notRunning.ExitCode != 0 && (notRunning.Output + notRunning.Error).Contains("client running", StringComparison.Ordinal), "usage without a client did not explain itself: " + notRunning.Output + notRunning.Error);
                        using (ChatApiFixture fixture = new ChatApiFixture("Usage via CLI"))
                        using (XenoChatClient chat = new XenoChatClient())
                        using (Xeno.Client.Services.XenoWorkspace workspace = new Xeno.Client.Services.XenoWorkspace())
                        {
                            ChatConversation conversation = await chat.CreateConversationAsync(new ChatConversationRequest { Model = "llama3.1:8b" }, CancellationToken.None).ConfigureAwait(false);
                            await chat.SendAsync(conversation.Id, new ChatSendRequest { Content = "hi" }, null, CancellationToken.None).ConfigureAwait(false);

                            CliResult json = await RunCliAsync(home.Directory, "usage", "--dataset", "chat", "--range", "hour", "--json").ConfigureAwait(false);
                            Check(json.ExitCode == 0, "xeno usage --json failed: " + json.Error);
                            UsageAnalytics parsed = Xeno.Core.Serialization.JsonUtil.Deserialize<UsageAnalytics>(json.Output) ?? throw new InvalidOperationException("No JSON.");
                            Check(parsed.Dataset == UsageDatasetEnum.Chat && parsed.Totals.Requests == 1 && parsed.Buckets.Count == 60, "CLI usage is wrong: " + json.Output.Substring(0, Math.Min(300, json.Output.Length)));
                            CliResult text = await RunCliAsync(home.Directory, "usage", "--dataset", "chat").ConfigureAwait(false);
                            Check(text.ExitCode == 0 && text.Output.Contains("Turns:", StringComparison.Ordinal) && text.Output.Contains("llama3.1:8b", StringComparison.Ordinal) && text.Output.Contains("Average time by stage", StringComparison.Ordinal), "CLI usage text is wrong: " + text.Output + text.Error);
                            CliResult bad = await RunCliAsync(home.Directory, "usage", "--dataset", "nope").ConfigureAwait(false);
                            Check(bad.ExitCode != 0 && (bad.Output + bad.Error).Contains("--dataset must be", StringComparison.Ordinal), "A bad option should fail with a message: " + bad.Output + bad.Error);

                            Xeno.Client.Tools.XenoToolCatalog tools = new Xeno.Client.Tools.XenoToolCatalog(workspace, token => Task.FromResult<IChatStore>(fixture.Store), null);
                            Check(tools.Find("xeno_usage")?.Mutating == false, "xeno_usage should be read-only.");
                            string result = await tools.InvokeAsync("xeno_usage", "{\"dataset\":\"chat\",\"range\":\"hour\"}", CancellationToken.None).ConfigureAwait(false);
                            UsageAnalytics fromTool = Xeno.Core.Serialization.JsonUtil.Deserialize<UsageAnalytics>(result) ?? throw new InvalidOperationException("No tool result.");
                            Check(fromTool.Totals.Requests == parsed.Totals.Requests && fromTool.Totals.CompletionTokens == parsed.Totals.CompletionTokens, "The MCP tool and the CLI disagree.");
                            await ExpectThrowsAsync<ArgumentException>(() => tools.InvokeAsync("xeno_usage", "{\"dataset\":\"bogus\"}", CancellationToken.None)).ConfigureAwait(false);
                        }
                    }
                })
            });
        }

        internal static async Task<CliResult> RunCliAsync(string home, params string[] args)
        {
            System.Diagnostics.ProcessStartInfo info = new System.Diagnostics.ProcessStartInfo("dotnet")
            {
                RedirectStandardError = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            };
            info.ArgumentList.Add(typeof(Xeno.Client.Program).Assembly.Location);
            foreach (string arg in args) info.ArgumentList.Add(arg);
            info.Environment["XENO_CLIENT_HOME"] = home;
            info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
            using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(info) ?? throw new InvalidOperationException("Could not start xeno."))
            {
                process.StandardInput.Close();
                Task<string> output = process.StandardOutput.ReadToEndAsync();
                Task<string> error = process.StandardError.ReadToEndAsync();
                using (CancellationTokenSource timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60)))
                {
                    await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                }

                return new CliResult { ExitCode = process.ExitCode, Output = await output.ConfigureAwait(false), Error = await error.ConfigureAwait(false) };
            }
        }

        internal sealed class CliResult
        {
            internal int ExitCode { get; set; } = 0;

            internal string Output { get; set; } = String.Empty;

            internal string Error { get; set; } = String.Empty;
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

        private static void ExpectThrows<T>(Action action) where T : Exception
        {
            try
            {
                action();
            }
            catch (T)
            {
                return;
            }

            throw new InvalidOperationException("Expected " + typeof(T).Name + ".");
        }
    }
}

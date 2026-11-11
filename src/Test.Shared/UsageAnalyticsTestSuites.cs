namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Core.Analytics;
    using Xeno.Core.Chat;
    using Xeno.Core.Models;

    /// <summary>
    /// The usage analytics engine every chart reads: buckets, percentiles, groups, heatmap, stages, throughput, and limits.
    /// </summary>
    public static class UsageAnalyticsTestSuites
    {
        private static readonly DateTime To = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

        /// <summary>
        /// Engine suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor EngineSuite()
        {
            const string suite = "UsageAnalytics";
            return new TestSuiteDescriptor(suite, "Usage analytics engine", new List<TestCaseDescriptor>
            {
                Case(suite, "BucketsAreGapFilledForEveryPreset", "Every preset gives 60, 96, 84, and 120 buckets, empty ones included", () =>
                {
                    int[] minutes = { UsageRanges.HourBucketMinutes, UsageRanges.DayBucketMinutes, UsageRanges.WeekBucketMinutes, UsageRanges.MonthBucketMinutes };
                    TimeSpan[] spans = { TimeSpan.FromHours(1), TimeSpan.FromDays(1), TimeSpan.FromDays(7), TimeSpan.FromDays(30) };
                    int[] counts = { 60, 96, 84, 120 };
                    for (int i = 0; i < minutes.Length; i++)
                    {
                        UsageAnalytics analytics = UsageAnalyticsBuilder.Build(UsageDatasetEnum.LocalEndpoint, new[] { Record(To.AddMinutes(-0.5), 100) }, Query(To - spans[i], minutes[i]));
                        Check(analytics.Buckets.Count == counts[i], "Preset " + i + " has " + analytics.Buckets.Count + " buckets.");
                        Check(analytics.Buckets.Last().Requests == 1 && analytics.Buckets.Take(analytics.Buckets.Count - 1).All(item => item.Requests == 0), "The record landed in the wrong bucket.");
                        Check(analytics.Buckets[0].BucketStartUtc == To - spans[i] && analytics.Buckets.Last().BucketEndUtc == To, "Bucket edges are wrong.");
                        Check(UsageRanges.DefaultBucketMinutes(To - spans[i], To) == minutes[i], "Default bucket minutes are wrong.");
                    }

                    ExpectThrows<ArgumentException>(() => UsageAnalyticsBuilder.Build(UsageDatasetEnum.Chat, new List<UsageRecord>(), Query(To.AddDays(-30), 1)));
                    ExpectThrows<ArgumentException>(() => UsageAnalyticsBuilder.Build(UsageDatasetEnum.Chat, new List<UsageRecord>(), new UsageAnalyticsQuery { FromUtc = To, ToUtc = To, BucketMinutes = 1 }));
                    return Task.CompletedTask;
                }),
                Case(suite, "PercentilesAreExactNearestRank", "Average, p50, p95, p99, and maximum match hand-computed nearest-rank values", () =>
                {
                    List<double> hundred = Enumerable.Range(1, 100).Select(item => (double)item).Reverse().ToList();
                    UsageDistribution d = UsageAnalyticsBuilder.Distribution(hundred);
                    Check(d.Count == 100 && d.Average == 50.5 && d.P50 == 50 && d.P95 == 95 && d.P99 == 99 && d.Maximum == 100, "1..100 is wrong.");
                    UsageDistribution one = UsageAnalyticsBuilder.Distribution(new List<double> { 7 });
                    Check(one.P50 == 7 && one.P95 == 7 && one.P99 == 7 && one.Maximum == 7, "One value is wrong.");
                    UsageDistribution two = UsageAnalyticsBuilder.Distribution(new List<double> { 10, 20 });
                    Check(two.P50 == 10 && two.P95 == 20 && two.Average == 15, "Two values are wrong.");
                    UsageDistribution ties = UsageAnalyticsBuilder.Distribution(new List<double> { 5, 5, 5, 9 });
                    Check(ties.P50 == 5 && ties.P95 == 9, "Ties are wrong.");
                    UsageDistribution none = UsageAnalyticsBuilder.Distribution(new List<double>());
                    Check(none.Count == 0 && !none.P50.HasValue && !none.Average.HasValue, "An empty distribution must have no values.");

                    List<UsageRecord> records = new List<UsageRecord>();
                    for (int i = 1; i <= 20; i++) records.Add(Record(To.AddMinutes(-30), i * 100));
                    UsageAnalytics analytics = UsageAnalyticsBuilder.Build(UsageDatasetEnum.LocalEndpoint, records, Query(To.AddHours(-1), 1));
                    Check(analytics.Totals.Latency.P50 == 1000 && analytics.Totals.Latency.P95 == 1900 && analytics.Totals.Latency.Maximum == 2000, "Range latency is wrong.");
                    Check(analytics.Buckets[30].Latency.Count == 20 && analytics.Buckets[30].Latency.P99 == 2000, "Bucket latency is wrong.");
                    return Task.CompletedTask;
                }),
                Case(suite, "TopGroupsKeepNamesAndFoldTheRest", "The top N groups keep their names in every bucket; the rest are Other and missing values are Unknown", () =>
                {
                    List<UsageRecord> records = new List<UsageRecord>();
                    string[] models = { "a", "a", "a", "b", "b", "c", "d" };
                    foreach (string model in models) records.Add(Record(To.AddMinutes(-10), 50, model));
                    records.Add(Record(To.AddMinutes(-5), 50, null));
                    UsageAnalyticsQuery query = Query(To.AddHours(-1), 1);
                    query.TopN = 2;
                    UsageAnalytics analytics = UsageAnalyticsBuilder.Build(UsageDatasetEnum.LocalEndpoint, records, query);
                    Check(String.Join(",", analytics.GroupKeys) == "a,b,Other", "Group keys are " + String.Join(",", analytics.GroupKeys));
                    Check(analytics.GroupTotals.Single(item => item.Key == "Other").Requests == 3, "Other should hold c, d, and Unknown.");
                    Check(analytics.Buckets.All(item => item.Groups.Select(group => group.Key).SequenceEqual(analytics.GroupKeys)), "Every bucket must carry every group key in order.");
                    Check(analytics.Buckets[50].Groups[0].Requests == 3, "Bucket values are wrong.");

                    query.TopN = 10;
                    query.GroupBy = UsageGroupByEnum.Route;
                    analytics = UsageAnalyticsBuilder.Build(UsageDatasetEnum.LocalEndpoint, records, query);
                    Check(analytics.GroupKeys.SequenceEqual(new[] { "local" }), "Local routes should collapse to local: " + String.Join(",", analytics.GroupKeys));
                    query.GroupBy = UsageGroupByEnum.User;
                    analytics = UsageAnalyticsBuilder.Build(UsageDatasetEnum.LocalEndpoint, records, query);
                    Check(analytics.GroupKeys.SequenceEqual(new[] { UsageAnalyticsBuilder.UnknownKey }), "A missing value should be Unknown.");
                    query.GroupBy = UsageGroupByEnum.Model;
                    query.GroupMetric = UsageGroupMetricEnum.Tokens;
                    records.Add(new UsageRecord { TimestampUtc = To.AddMinutes(-3), Model = "d", PromptTokens = 10000, CompletionTokens = 1, TotalMs = 1 });
                    analytics = UsageAnalyticsBuilder.Build(UsageDatasetEnum.LocalEndpoint, records, query);
                    Check(analytics.GroupKeys[0] == "d", "Ranking by tokens should put d first.");
                    return Task.CompletedTask;
                }),
                Case(suite, "HeatmapLayoutsFollowTheRangeAndOffset", "Hour by minute, day by hour, and weekday by hour cells honor the viewer's UTC offset", () =>
                {
                    UsageRecord record = Record(To.AddMinutes(-25), 10);
                    record.PromptTokens = 40;
                    record.CompletionTokens = 2;
                    UsageAnalytics hour = UsageAnalyticsBuilder.Build(UsageDatasetEnum.Chat, new[] { record }, Query(To.AddHours(-1), 1));
                    Check(hour.Heatmap != null && hour.Heatmap.Layout == UsageHeatmapLayoutEnum.HourByMinute && hour.Heatmap.Rows == 6 && hour.Heatmap.Columns == 10, "Hour layout is wrong.");
                    UsageHeatmapCell hot = hour.Heatmap!.Cells.Single(item => item.Tokens > 0);
                    Check(hot.Row == 3 && hot.Column == 5 && hot.Tokens == 42 && hot.StartUtc == To.AddMinutes(-25) && hour.Heatmap.MaximumTokens == 42, "Hour cell is wrong.");

                    foreach (int offset in new[] { -480, 0, 330 })
                    {
                        UsageAnalyticsQuery day = Query(To.AddDays(-1), 15);
                        day.UtcOffsetMinutes = offset;
                        UsageAnalytics analytics = UsageAnalyticsBuilder.Build(UsageDatasetEnum.Chat, new[] { record }, day);
                        DateTime local = record.TimestampUtc.AddMinutes(offset);
                        Check(analytics.Heatmap!.Layout == UsageHeatmapLayoutEnum.DayByHour && analytics.Heatmap.Columns == 24, "Day layout is wrong.");
                        UsageHeatmapCell cell = analytics.Heatmap.Cells.Single(item => item.Tokens > 0);
                        Check(cell.Column == local.Hour && cell.Row == (int)(local.Date - To.AddDays(-1).AddMinutes(offset).Date).TotalDays, "Day cell is wrong for offset " + offset);
                        Check(cell.StartUtc <= record.TimestampUtc && cell.StartUtc.AddHours(1) > record.TimestampUtc, "Day cell start is wrong for offset " + offset);

                        UsageAnalyticsQuery week = Query(To.AddDays(-7), 120);
                        week.UtcOffsetMinutes = offset;
                        analytics = UsageAnalyticsBuilder.Build(UsageDatasetEnum.Chat, new[] { record }, week);
                        cell = analytics.Heatmap!.Cells.Single(item => item.Tokens > 0);
                        Check(analytics.Heatmap.Layout == UsageHeatmapLayoutEnum.WeekdayByHour && analytics.Heatmap.Rows == 7 && cell.Row == (int)local.DayOfWeek && cell.Column == local.Hour, "Week cell is wrong for offset " + offset);
                    }

                    return Task.CompletedTask;
                }),
                Case(suite, "StagesAreDerivedClampedAndOrdered", "Stage formulas skip missing inputs, clamp negative results, and average per bucket", () =>
                {
                    RequestHistoryEntry entry = new RequestHistoryEntry { CreatedUtc = To.AddMinutes(-2), StatusCode = 200, DurationMs = 1000, Model = "m" };
                    entry.Telemetry.ClientRoundTripMs = 900;
                    entry.Telemetry.UpstreamTotalMs = 800;
                    entry.Telemetry.RequestToHeadersMs = 100;
                    entry.Telemetry.TimeToFirstTokenMs = 300;
                    entry.Telemetry.TotalStreamingTimeMs = 500;
                    UsageRecord network = UsageRecordMapper.FromNetworkRequest(entry);
                    Check(Stage(network, UsageStageEnum.ServerOverhead) == 100 && Stage(network, UsageStageEnum.RelayAndClient) == 100 && Stage(network, UsageStageEnum.ProviderHeaders) == 100 && Stage(network, UsageStageEnum.ProviderFirstToken) == 200 && Stage(network, UsageStageEnum.Streaming) == 500, "Network stages are wrong.");
                    Check(network.Fallback == null && network.Route == "network", "Network requests cannot know about fallback.");

                    RequestHistoryEntry sparse = new RequestHistoryEntry { CreatedUtc = To.AddMinutes(-2), StatusCode = 502, DurationMs = 50 };
                    sparse.Telemetry.ClientRoundTripMs = 80;
                    UsageRecord partial = UsageRecordMapper.FromNetworkRequest(sparse);
                    Check(partial.Stages.Count == 1 && Stage(partial, UsageStageEnum.ServerOverhead) == 0 && partial.StageAnomalies == 1 && !partial.Success, "Missing inputs must be skipped and negatives clamped.");

                    UsageRecord local = UsageRecordMapper.FromLocalEndpointRequest(To.AddMinutes(-1), "m", "local:ollama", "ollama", 200, false, 1000, 250, 150, 10, null, 20);
                    Check(Stage(local, UsageStageEnum.Queue) == 150 && Stage(local, UsageStageEnum.ProviderFirstToken) == 250 && Stage(local, UsageStageEnum.Streaming) == 600 && local.TimeToFirstTokenMs == 400, "Local endpoint stages are wrong.");

                    ChatTurn turn = new ChatTurn { CreatedUtc = To.AddMinutes(-1), Status = ChatTurnStatusEnum.Success, Model = "m", Route = "local:ollama", TotalMs = 5000, ModelMs = 3000, TimeToFirstTokenMs = 500, ToolMs = 800, ApprovalMs = 700, ToolCallCount = 2, ToolDenied = 1, FeedbackRating = 1 };
                    UsageRecord chat = UsageRecordMapper.FromChatTurn(turn);
                    Check(Stage(chat, UsageStageEnum.ModelFirstToken) == 500 && Stage(chat, UsageStageEnum.ModelStreaming) == 2500 && Stage(chat, UsageStageEnum.Tools) == 800 && Stage(chat, UsageStageEnum.Approval) == 700 && Stage(chat, UsageStageEnum.Other) == 500, "Chat stages are wrong.");
                    Check(chat.ToolCalls == 1 && chat.ToolDenied == 1 && chat.Rating == 1, "Chat tool and rating counts are wrong.");

                    UsageAnalytics analytics = UsageAnalyticsBuilder.Build(UsageDatasetEnum.NetworkRequests, new[] { network, partial }, Query(To.AddHours(-1), 1));
                    Check(analytics.StageOrder.First() == UsageStageEnum.ServerOverhead && analytics.StageAnomalies == 1, "Stage order or anomalies are wrong.");
                    UsageStageAverage overhead = analytics.StageAverages.Single(item => item.Stage == UsageStageEnum.ServerOverhead);
                    Check(overhead.Count == 2 && overhead.AverageMs == 50 && overhead.P95Ms == 100, "Stage averages are wrong.");
                    Check(analytics.Buckets[58].Stages.Count == 5, "Bucket stages are wrong.");
                    return Task.CompletedTask;
                }),
                Case(suite, "ThroughputFeedbackToolsAndFallback", "Throughput uses streaming time when known; feedback, tools, fallback, and cached tokens add up", () =>
                {
                    Check(UsageAnalyticsBuilder.Throughput(new UsageRecord { CompletionTokens = 100, StreamingMs = 2000, TotalMs = 4000 }) == 50, "Streaming time should be used.");
                    Check(UsageAnalyticsBuilder.Throughput(new UsageRecord { CompletionTokens = 100, TotalMs = 4000 }) == 25, "Total time should be the fallback.");
                    Check(UsageAnalyticsBuilder.Throughput(new UsageRecord { TotalMs = 4000 }) == null && UsageAnalyticsBuilder.Throughput(new UsageRecord { CompletionTokens = 5 }) == null, "Unmeasurable throughput must be null, not zero.");

                    List<UsageRecord> records = new List<UsageRecord>
                    {
                        new UsageRecord { TimestampUtc = To.AddMinutes(-1), Rating = 1, ToolCalls = 2, ToolFailures = 1, Fallback = true, PromptTokens = 100, CachedPromptTokens = 40, CompletionTokens = 10, Success = true },
                        new UsageRecord { TimestampUtc = To.AddMinutes(-1), Rating = -1, ToolDenied = 1, Fallback = false, PromptTokens = 50, CompletionTokens = 5, Success = false }
                    };
                    UsageAnalytics analytics = UsageAnalyticsBuilder.Build(UsageDatasetEnum.Chat, records, Query(To.AddHours(-1), 1));
                    UsageBucket bucket = analytics.Buckets[59];
                    Check(bucket.ThumbsUp == 1 && bucket.ThumbsDown == 1 && bucket.ToolCalls == 2 && bucket.ToolFailures == 1 && bucket.ToolDenied == 1, "Feedback or tools are wrong.");
                    Check(bucket.Fallbacks == 1 && analytics.Buckets[0].Fallbacks == 0 && analytics.Totals.Fallbacks == 1, "Fallbacks are wrong.");
                    Check(bucket.PromptTokens == 110 && bucket.CachedPromptTokens == 40 && analytics.Totals.PromptTokens == 150 && analytics.Totals.CachedPromptTokens == 40, "Token types are wrong.");
                    Check(analytics.Totals.ErrorRate == 0.5 && analytics.Totals.Failed == 1, "Error rate is wrong.");

                    UsageAnalytics unknown = UsageAnalyticsBuilder.Build(UsageDatasetEnum.NetworkRequests, new[] { Record(To.AddMinutes(-1), 5) }, Query(To.AddHours(-1), 1));
                    Check(unknown.Totals.Fallbacks == null && unknown.Buckets.All(item => item.Fallbacks == null) && unknown.Totals.CachedPromptTokens == null, "Unknown fallback and cached tokens must stay null.");
                    return Task.CompletedTask;
                }),
                Case(suite, "FiltersTruncationAndEmptyDatasets", "Filters apply, scans stop at the limit with a reason, and an empty dataset still has every bucket", async () =>
                {
                    List<UsageRecord> records = new List<UsageRecord>();
                    for (int i = 0; i < 50; i++) records.Add(Record(To.AddMinutes(-i - 1), 10, i % 2 == 0 ? "even" : "odd"));
                    UsageAnalyticsQuery query = Query(To.AddHours(-1), 1);
                    query.Model = "even";
                    UsageAnalytics filtered = UsageAnalyticsBuilder.Build(UsageDatasetEnum.LocalEndpoint, records, query);
                    Check(filtered.Totals.Requests == 25 && filtered.GroupKeys.SequenceEqual(new[] { "even" }), "The model filter did not apply.");

                    UsageAnalytics truncated = UsageAnalyticsBuilder.Build(UsageDatasetEnum.LocalEndpoint, records, Query(To.AddHours(-1), 1), new UsageAnalyticsOptions { MaxRecords = 10 });
                    Check(truncated.Truncated && truncated.RecordsScanned == 10 && truncated.TruncatedReason != null, "Truncation is not reported.");

                    UsageAnalytics empty = UsageAnalyticsBuilder.Build(UsageDatasetEnum.Chat, new List<UsageRecord>(), Query(To.AddDays(-1), 15));
                    Check(empty.Buckets.Count == 96 && empty.Totals.Requests == 0 && empty.Totals.ErrorRate == null && empty.GroupKeys.Count == 0 && empty.Heatmap!.MaximumTokens == 0, "An empty dataset is wrong.");

                    UsageAnalytics streamed = await UsageAnalyticsBuilder.BuildAsync(UsageDatasetEnum.LocalEndpoint, Stream(records, CancellationToken.None), Query(To.AddHours(-1), 1), null, CancellationToken.None).ConfigureAwait(false);
                    Check(streamed.Totals.Requests == 50, "The async builder lost records.");
                    using (CancellationTokenSource cancel = new CancellationTokenSource())
                    {
                        cancel.Cancel();
                        bool thrown = false;
                        try
                        {
                            await UsageAnalyticsBuilder.BuildAsync(UsageDatasetEnum.LocalEndpoint, Stream(records, cancel.Token), Query(To.AddHours(-1), 1), null, cancel.Token).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            thrown = true;
                        }

                        Check(thrown, "A cancelled token was ignored.");
                    }
                }),
                Case(suite, "HundredThousandRecordsWithinBudget", "100,000 records with every section build within the budget (2 seconds)", () =>
                {
                    Random random = new Random(7);
                    List<UsageRecord> records = new List<UsageRecord>(100000);
                    for (int i = 0; i < 100000; i++)
                    {
                        UsageRecord record = new UsageRecord { TimestampUtc = To.AddMinutes(-random.NextDouble() * 60 * 24 * 30), Model = "model-" + (i % 25), Provider = "p" + (i % 3), Route = "local:p", TotalMs = random.Next(50, 5000), TimeToFirstTokenMs = random.Next(10, 900), StreamingMs = random.Next(10, 4000), PromptTokens = random.Next(1, 2000), CompletionTokens = random.Next(1, 800), Success = i % 50 != 0 };
                        UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.Queue, random.Next(0, 50));
                        UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.Streaming, record.StreamingMs);
                        records.Add(record);
                    }

                    System.Diagnostics.Stopwatch clock = System.Diagnostics.Stopwatch.StartNew();
                    UsageAnalytics analytics = UsageAnalyticsBuilder.Build(UsageDatasetEnum.LocalEndpoint, records, Query(To.AddDays(-30), UsageRanges.MonthBucketMinutes));
                    clock.Stop();
                    Check(analytics.Totals.Requests == 100000 && analytics.Buckets.Count == 120 && analytics.GroupKeys.Count == 9, "The large build is wrong.");
                    Check(clock.ElapsedMilliseconds < 2000, "Building 100,000 records took " + clock.ElapsedMilliseconds + " ms, over the 2000 ms budget.");
                    return Task.CompletedTask;
                }),
                Case(suite, "ProviderHealthStrip", "Health cells are healthy, degraded, unhealthy, or empty, with uptime and the failing streak", () =>
                {
                    List<ProviderHealthSample> samples = new List<ProviderHealthSample>
                    {
                        new ProviderHealthSample { TimestampUtc = To.AddMinutes(-59.5), ProviderName = "ollama", Healthy = true },
                        new ProviderHealthSample { TimestampUtc = To.AddMinutes(-58.5), ProviderName = "ollama", Healthy = true },
                        new ProviderHealthSample { TimestampUtc = To.AddMinutes(-58.2), ProviderName = "ollama", Healthy = false },
                        new ProviderHealthSample { TimestampUtc = To.AddMinutes(-1.5), ProviderName = "ollama", Healthy = false },
                        new ProviderHealthSample { TimestampUtc = To.AddMinutes(-0.5), ProviderName = "ollama", Healthy = false },
                        new ProviderHealthSample { TimestampUtc = To.AddMinutes(-0.5), ProviderName = "vllm", Healthy = true }
                    };
                    ProviderHealthStrip strip = ProviderHealthBuilder.Build(samples, new ProviderHealthQuery { FromUtc = To.AddHours(-1), ToUtc = To, BucketMinutes = 1 });
                    ProviderHealthRow ollama = strip.Providers.Single(item => item.ProviderName == "ollama");
                    Check(strip.Providers.Count == 2 && ollama.Cells.Count == 60, "Rows or cells are wrong.");
                    Check(ollama.Cells[0].Status == ProviderHealthStatusEnum.Healthy && ollama.Cells[1].Status == ProviderHealthStatusEnum.Degraded && ollama.Cells[2].Status == ProviderHealthStatusEnum.NoData && ollama.Cells[59].Status == ProviderHealthStatusEnum.Unhealthy, "Cell status is wrong.");
                    Check(ollama.UptimePercent == 40 && ollama.ConsecutiveFailures == 3, "Uptime or streak is wrong: " + ollama.UptimePercent + " " + ollama.ConsecutiveFailures);
                    ProviderHealthStrip one = ProviderHealthBuilder.Build(samples, new ProviderHealthQuery { FromUtc = To.AddHours(-1), ToUtc = To, BucketMinutes = 1, ProviderName = "vllm" });
                    Check(one.Providers.Count == 1 && one.Providers[0].UptimePercent == 100, "The provider filter did not apply.");
                    return Task.CompletedTask;
                })
            });
        }

        private static async IAsyncEnumerable<UsageRecord> Stream(IEnumerable<UsageRecord> records, [EnumeratorCancellation] CancellationToken token)
        {
            foreach (UsageRecord record in records)
            {
                token.ThrowIfCancellationRequested();
                await Task.Yield();
                yield return record;
            }
        }

        private static UsageAnalyticsQuery Query(DateTime from, int bucketMinutes)
        {
            return new UsageAnalyticsQuery { FromUtc = from, ToUtc = To, BucketMinutes = bucketMinutes };
        }

        private static UsageRecord Record(DateTime when, double totalMs, string? model = "m")
        {
            return new UsageRecord { TimestampUtc = when, TotalMs = totalMs, Model = model, Route = "local:ollama", Success = true };
        }

        private static double? Stage(UsageRecord record, UsageStageEnum stage)
        {
            UsageStageDuration? found = record.Stages.FirstOrDefault(item => item.Stage == stage);
            return found?.Milliseconds;
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

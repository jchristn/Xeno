namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client.Presentation;
    using Xeno.Core.Analytics;

    /// <summary>
    /// The shared usage chart contract: every catalog chart builds from analytics, the web's copy of the catalog matches
    /// the C# catalog, and every chart kind downsamples for narrow displays.
    /// </summary>
    public static class UsageChartTestSuites
    {
        /// <summary>
        /// Chart contract suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor ChartSuite()
        {
            const string suite = "UsageCharts";
            return new TestSuiteDescriptor(suite, "Usage chart contract", new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor(suite, "EveryChartBuildsFromAnalytics", "Every Usage page chart builds from seeded analytics with the catalog's kind, series, and drill-down windows", executeAsync: delegate
                {
                    UsageAnalytics analytics = Seeded(UsageDatasetEnum.Chat);
                    foreach (string id in XenoUsageCharts.ChartsFor(UsageDatasetEnum.Chat))
                    {
                        XenoChart chart = XenoUsageCharts.Build(id, analytics, "last hour");
                        XenoChartDefinition definition = XenoChartCatalog.Find(id);
                        Check(chart.Id == id && chart.Kind == definition.Kind && chart.HasData, id + " did not build with data.");
                        if (!definition.DynamicSeries && definition.Kind != XenoChartKindEnum.Heatmap) Check(chart.Series.Select(item => item.Name).All(name => definition.Series.Any(series => series.Name == name)), id + " has a series outside the catalog.");
                        if (definition.Kind == XenoChartKindEnum.StackedColumns || definition.Kind == XenoChartKindEnum.Lines) Check(chart.Buckets.Count == analytics.Buckets.Count && chart.Labels.Count == analytics.Buckets.Count, id + " lost its drill-down windows.");
                    }

                    Check(!XenoUsageCharts.ChartsFor(UsageDatasetEnum.LocalEndpoint).Contains(XenoChartCatalog.Feedback) && XenoUsageCharts.ChartsFor(UsageDatasetEnum.Chat).Contains(XenoChartCatalog.Tools), "Chat-only charts are wrong.");
                    XenoChart outcome = XenoUsageCharts.Build(XenoChartCatalog.Outcome, analytics, "last hour");
                    Check(outcome.Series.Count == 3 && outcome.Series[1].Values.Sum() == 1 && outcome.Series[0].Values.Sum() == 1, "Fallback answers must not be counted twice.");
                    XenoChart latency = XenoUsageCharts.Build(XenoChartCatalog.Latency, analytics, "last hour");
                    Check(Double.IsNaN(latency.Series[0].Values[0]) && latency.Series[0].Values[59] == 500 && latency.Series[3].Values[59] == 1000 && latency.FormatValue(1500) == "1.5 s" && latency.FormatValue(850) == "850 ms", "Latency values or formatting are wrong.");
                    XenoChart groups = XenoUsageCharts.Build(XenoChartCatalog.Groups, analytics, "last hour");
                    Check(groups.Series.Select(item => item.Name).SequenceEqual(new[] { "llama3", "qwen3" }) && groups.Series[0].Tone == XenoChartToneEnum.Series1, "Group series are wrong.");
                    XenoChart heatmap = XenoUsageCharts.Build(XenoChartCatalog.Heatmap, analytics, "last hour");
                    Check(heatmap.RowLabels.Count == 6 && heatmap.Grid.Count == 6 && heatmap.Grid.All(row => row.Count == 10) && heatmap.Maximum > 0, "The heatmap grid is wrong.");
                    XenoChart network = XenoUsageCharts.Build(XenoChartCatalog.Outcome, Seeded(UsageDatasetEnum.NetworkRequests, false), "last hour");
                    Check(network.Series.Count == 2, "Without fallback data the fallback series must be left out.");

                    ProviderHealthStrip strip = ProviderHealthBuilder.Build(new[] { new ProviderHealthSample { TimestampUtc = DateTime.UtcNow.AddMinutes(-1), ProviderName = "ollama", Healthy = false } }, new ProviderHealthQuery { FromUtc = DateTime.UtcNow.AddHours(-1), ToUtc = DateTime.UtcNow, BucketMinutes = 1 });
                    XenoChart health = XenoUsageCharts.Health(strip, "last hour");
                    Check(health.Kind == XenoChartKindEnum.StatusStrip && health.RowLabels.Single().StartsWith("ollama", StringComparison.Ordinal) && health.Grid.Single().Contains((double)XenoChartStatusEnum.Unhealthy) && health.HasData, "The health strip is wrong.");
                    XenoChart timing = XenoUsageCharts.RequestTiming(new List<UsageStageDuration> { new UsageStageDuration { Stage = UsageStageEnum.Queue, Milliseconds = 25 }, new UsageStageDuration { Stage = UsageStageEnum.Streaming, Milliseconds = 75 } });
                    Check(timing.Labels.SequenceEqual(new[] { "Waiting for a slot (25%)", "Streaming (75%)" }), "Request timing labels are wrong.");

                    XenoChart narrow = latency.Downsample(10);
                    Check(narrow.BucketCount == 10 && narrow.Buckets.Count == 10 && narrow.Buckets[0].StartUtc == latency.Buckets[0].StartUtc && narrow.Buckets[9].EndUtc == latency.Buckets[59].EndUtc, "Downsampling lost the windows.");
                    Check(narrow.Series[3].Values[9] == 1000, "Downsampled lines should keep the slowest value, not average it away.");
                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "WebCatalogMatches", "dashboard/src/charts/chartCatalog.json is exactly XenoChartCatalog (set XENO_WRITE_CHART_CATALOG=1 to regenerate)", executeAsync: delegate
                {
                    string path = Path.Combine(RepositoryRoot(), "dashboard", "src", "charts", "chartCatalog.json");
                    string expected = XenoChartCatalog.ToJson();
                    if (String.Equals(Environment.GetEnvironmentVariable("XENO_WRITE_CHART_CATALOG"), "1", StringComparison.Ordinal))
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                        File.WriteAllText(path, expected);
                    }

                    Check(File.Exists(path), "The web chart catalog is missing: " + path);
                    Check(File.ReadAllText(path).Replace("\r\n", "\n", StringComparison.Ordinal) == expected, "dashboard/src/charts/chartCatalog.json differs from XenoChartCatalog. Run the tests with XENO_WRITE_CHART_CATALOG=1 and commit the file.");
                    return Task.CompletedTask;
                })
            });
        }

        /// <summary>
        /// Seeded analytics for the last hour: three chat turns across two models, one fallback, one failure, ratings,
        /// tools, and stages.
        /// </summary>
        /// <param name="dataset">Dataset.</param>
        /// <param name="fallbackKnown">False to leave fallback unknown.</param>
        /// <returns>Analytics.</returns>
        internal static UsageAnalytics Seeded(UsageDatasetEnum dataset, bool fallbackKnown = true)
        {
            DateTime to = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
            List<UsageRecord> records = new List<UsageRecord>
            {
                new UsageRecord { TimestampUtc = to.AddSeconds(-30), Model = "llama3", Route = "local:ollama", Success = true, Fallback = fallbackKnown ? false : null, TotalMs = 1000, TimeToFirstTokenMs = 200, StreamingMs = 800, PromptTokens = 100, CachedPromptTokens = 20, CompletionTokens = 50, Rating = 1, ToolCalls = 1 },
                new UsageRecord { TimestampUtc = to.AddSeconds(-20), Model = "llama3", Route = "network", Success = true, Fallback = fallbackKnown ? true : null, TotalMs = 500, TimeToFirstTokenMs = 100, StreamingMs = 400, PromptTokens = 10, CompletionTokens = 5, Rating = -1, ToolDenied = 1 },
                new UsageRecord { TimestampUtc = to.AddMinutes(-30), Model = "qwen3", Route = "local:ollama", Success = false, Fallback = fallbackKnown ? false : null, TotalMs = 50, ToolFailures = 1 }
            };
            foreach (UsageRecord record in records)
            {
                UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.ModelFirstToken, record.TimeToFirstTokenMs);
                UsageAnalyticsBuilder.AddStage(record, UsageStageEnum.ModelStreaming, record.StreamingMs);
            }

            return UsageAnalyticsBuilder.Build(dataset, records, new UsageAnalyticsQuery { FromUtc = to.AddHours(-1), ToUtc = to, BucketMinutes = 1 });
        }

        private static string RepositoryRoot()
        {
            DirectoryInfo? directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null)
            {
                if (Directory.Exists(Path.Combine(directory.FullName, "dashboard")) && Directory.Exists(Path.Combine(directory.FullName, "src"))) return directory.FullName;
                directory = directory.Parent;
            }

            throw new InvalidOperationException("Could not find the repository root from " + AppContext.BaseDirectory + ".");
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Analytics;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Core.Analytics;

    /// <summary>
    /// The <c>xeno usage</c> command: usage analytics for this machine's served requests, local endpoint, or chat,
    /// read from the running client's local API like every other surface.
    /// </summary>
    public static partial class Program
    {
        #region Private-Methods

        private static async Task<int> HandleUsageAsync(string[] args)
        {
            if (args.Any(item => item == "--help" || item == "-h"))
            {
                PrintUsageHelp();
                return 0;
            }

            UsageDatasetEnum dataset = ParseUsageDataset(ChatOption(args, "--dataset", "-d") ?? "served");
            HistoryRangeEnum range = ParseUsageRange(ChatOption(args, "--range", "-r") ?? "day");
            UsageGroupByEnum groupBy = ParseUsageGroupBy(ChatOption(args, "--group-by", "-g") ?? "model");
            string metric = (ChatOption(args, "--metric", null) ?? "requests").ToLowerInvariant();
            if (metric != "requests" && metric != "tokens") throw new ArgumentException("--metric must be requests or tokens.");
            DateTime to = DateTime.UtcNow;
            UsageAnalyticsQuery query = new UsageAnalyticsQuery
            {
                FromUtc = HistoryRanges.StartUtc(range, to),
                ToUtc = to,
                BucketMinutes = HistoryRanges.DefaultBucketMinutes(range),
                GroupBy = groupBy,
                GroupMetric = metric == "tokens" ? UsageGroupMetricEnum.Tokens : UsageGroupMetricEnum.Requests,
                Model = ChatOption(args, "--model", "-m"),
                UtcOffsetMinutes = (int)TimeZoneInfo.Local.GetUtcOffset(to).TotalMinutes
            };

            using (XenoUsageClient usage = new XenoUsageClient())
            {
                UsageAnalytics analytics = await usage.GetUsageAsync(dataset, query, CancellationToken.None).ConfigureAwait(false);
                if (_UseJson)
                {
                    WriteJson(analytics);
                    return 0;
                }

                PrintUsage(analytics, range);
                return 0;
            }
        }

        private static void PrintUsage(UsageAnalytics analytics, HistoryRangeEnum range)
        {
            UsageTotals totals = analytics.Totals;
            List<string> groupLines = BuildUsageGroupTable(analytics).Render(_Columns);
            List<string> stageLines = BuildUsageStageTable(analytics).Render(null);
            WriteInfoHeading(XenoUsageCharts.DatasetLabel(analytics.Dataset) + ", " + HistoryRanges.Describe(range));
            if (analytics.Truncated && analytics.TruncatedReason != null) Console.Error.WriteLine("Note: " + analytics.TruncatedReason);
            Console.WriteLine("  " + (analytics.Dataset == UsageDatasetEnum.Chat ? "Turns" : "Requests") + ":       " + totals.Requests.ToString("N0", CultureInfo.CurrentCulture) + " (" + totals.Failed.ToString("N0", CultureInfo.CurrentCulture) + " failed" + (totals.ErrorRate.HasValue ? ", " + Math.Round(totals.ErrorRate.Value * 100, 1).ToString(CultureInfo.CurrentCulture) + "%" : String.Empty) + (totals.Fallbacks.HasValue ? ", " + totals.Fallbacks.Value.ToString("N0", CultureInfo.CurrentCulture) + " network fallbacks" : String.Empty) + ")");
            Console.WriteLine("  Latency:        " + Distribution(totals.Latency));
            Console.WriteLine("  First token:    " + Distribution(totals.TimeToFirstToken));
            Console.WriteLine("  Streaming:      " + Distribution(totals.Streaming));
            Console.WriteLine("  Throughput:     " + (totals.Throughput.P50.HasValue ? "median " + totals.Throughput.P50.Value.ToString("0.#", CultureInfo.CurrentCulture) + " tok/s, 95th " + (totals.Throughput.P95 ?? 0).ToString("0.#", CultureInfo.CurrentCulture) + " tok/s" : "-"));
            Console.WriteLine("  Tokens:         " + totals.PromptTokens.ToString("N0", CultureInfo.CurrentCulture) + " in" + (totals.CachedPromptTokens.HasValue ? " (" + totals.CachedPromptTokens.Value.ToString("N0", CultureInfo.CurrentCulture) + " cached)" : String.Empty) + ", " + totals.CompletionTokens.ToString("N0", CultureInfo.CurrentCulture) + " out");
            if (analytics.Dataset == UsageDatasetEnum.Chat)
            {
                Console.WriteLine("  Ratings:        " + totals.ThumbsUp.ToString(CultureInfo.CurrentCulture) + " up, " + totals.ThumbsDown.ToString(CultureInfo.CurrentCulture) + " down");
                Console.WriteLine("  Tool calls:     " + totals.ToolCalls.ToString(CultureInfo.CurrentCulture) + " ran, " + totals.ToolFailures.ToString(CultureInfo.CurrentCulture) + " failed, " + totals.ToolDenied.ToString(CultureInfo.CurrentCulture) + " denied");
            }

            Console.WriteLine();
            WriteInfoHeading("By " + XenoUsageCharts.GroupByLabel(analytics.GroupBy).ToLowerInvariant());
            if (analytics.GroupTotals.Count == 0) Console.WriteLine("  (none)");
            else foreach (string line in groupLines) Console.WriteLine(line);

            if (analytics.StageAverages.Count > 0)
            {
                Console.WriteLine();
                WriteInfoHeading("Average time by stage");
                foreach (string line in stageLines) Console.WriteLine(line);
            }
        }

        private static CliTable BuildUsageGroupTable(UsageAnalytics analytics)
        {
            // --columns picks this table's columns; the stage table below always shows its own columns.
            CliTable table = new CliTable()
                .AddColumn("name", XenoUsageCharts.GroupByLabel(analytics.GroupBy).ToUpperInvariant(), 40)
                .AddColumn("requests", "REQUESTS", 0, true)
                .AddColumn("failed", "FAILED", 0, true)
                .AddColumn("tokens", "TOKENS", 0, true)
                .AddColumn("median", "MEDIAN", 0, true)
                .AddColumn("p95", "95TH", 0, true)
                .AddColumn("p99", "99TH", 0, true, false)
                .AddColumn("average", "AVERAGE", 0, true, false);
            foreach (UsageGroupTotal group in analytics.GroupTotals)
            {
                table.AddRow(
                    XenoUsageCharts.KeyLabel(group.Key),
                    group.Requests.ToString("N0", CultureInfo.CurrentCulture),
                    group.Failed.ToString("N0", CultureInfo.CurrentCulture),
                    (group.PromptTokens + group.CompletionTokens).ToString("N0", CultureInfo.CurrentCulture),
                    Ms(group.Latency.P50),
                    Ms(group.Latency.P95),
                    Ms(group.Latency.P99),
                    Ms(group.Latency.Average));
            }

            return table;
        }

        private static CliTable BuildUsageStageTable(UsageAnalytics analytics)
        {
            CliTable table = new CliTable()
                .AddColumn("stage", "STAGE", 30)
                .AddColumn("average", "AVERAGE", 0, true)
                .AddColumn("p95", "95TH", 0, true)
                .AddColumn("count", "COUNT", 0, true);
            foreach (UsageStageAverage stage in analytics.StageAverages)
            {
                table.AddRow(XenoUsageCharts.StageLabel(stage.Stage), Ms(stage.AverageMs), Ms(stage.P95Ms), stage.Count.ToString("N0", CultureInfo.CurrentCulture));
            }

            return table;
        }

        private static string Distribution(UsageDistribution distribution)
        {
            if (distribution.Count == 0) return "-";
            return "median " + Ms(distribution.P50) + ", 95th " + Ms(distribution.P95) + ", 99th " + Ms(distribution.P99) + ", slowest " + Ms(distribution.Maximum);
        }

        private static string Ms(double? value)
        {
            return value.HasValue ? XenoChart.FormatMilliseconds(value.Value) : "-";
        }

        private static UsageDatasetEnum ParseUsageDataset(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "served":
                case "requests": return UsageDatasetEnum.ServedRequests;
                case "endpoint":
                case "local": return UsageDatasetEnum.LocalEndpoint;
                case "chat": return UsageDatasetEnum.Chat;
                default: throw new ArgumentException("--dataset must be served, endpoint, or chat.");
            }
        }

        private static HistoryRangeEnum ParseUsageRange(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "hour": return HistoryRangeEnum.Hour;
                case "day": return HistoryRangeEnum.Day;
                case "week": return HistoryRangeEnum.Week;
                case "month": return HistoryRangeEnum.Month;
                default: throw new ArgumentException("--range must be hour, day, week, or month.");
            }
        }

        private static UsageGroupByEnum ParseUsageGroupBy(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "model": return UsageGroupByEnum.Model;
                case "provider": return UsageGroupByEnum.Provider;
                case "route": return UsageGroupByEnum.Route;
                default: throw new ArgumentException("--group-by must be model, provider, or route.");
            }
        }

        private static void PrintUsageHelp()
        {
            Console.WriteLine("xeno usage [--dataset served|endpoint|chat] [--range hour|day|week|month] [--group-by model|provider|route] [--metric requests|tokens] [--model <name>] [--columns <names>] [--json]");
            Console.WriteLine();
            Console.WriteLine("  Usage for this machine: requests, failures, latency, time to first token, streaming, throughput, tokens,");
            Console.WriteLine("  totals by group, and average time by stage. Needs the client running. --json prints the full analytics,");
            Console.WriteLine("  the same data the Usage page in every Xeno app draws.");
            Console.WriteLine();
            Console.WriteLine("  served     Network requests this machine answered (default)");
            Console.WriteLine("  endpoint   Requests to the local inference endpoint");
            Console.WriteLine("  chat       Chat turns");
            Console.WriteLine();
            Console.WriteLine("  --columns picks the group table's columns: name, requests, failed, tokens, median, p95, p99, average (or all).");
        }

        #endregion
    }
}

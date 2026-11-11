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
            int width = Math.Max(10, analytics.GroupTotals.Select(item => XenoUsageCharts.KeyLabel(item.Key).Length).DefaultIfEmpty(10).Max() + 2);
            foreach (UsageGroupTotal group in analytics.GroupTotals)
            {
                Console.WriteLine("  " + XenoUsageCharts.KeyLabel(group.Key).PadRight(width) + group.Requests.ToString("N0", CultureInfo.CurrentCulture).PadLeft(8) + " req  " + group.Failed.ToString("N0", CultureInfo.CurrentCulture).PadLeft(6) + " failed  " + (group.PromptTokens + group.CompletionTokens).ToString("N0", CultureInfo.CurrentCulture).PadLeft(10) + " tok  median " + Ms(group.Latency.P50));
            }

            if (analytics.StageAverages.Count > 0)
            {
                Console.WriteLine();
                WriteInfoHeading("Average time by stage");
                foreach (UsageStageAverage stage in analytics.StageAverages)
                {
                    Console.WriteLine("  " + XenoUsageCharts.StageLabel(stage.Stage).PadRight(24) + Ms(stage.AverageMs).PadLeft(9) + "   95th " + Ms(stage.P95Ms));
                }
            }
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
            Console.WriteLine("xeno usage [--dataset served|endpoint|chat] [--range hour|day|week|month] [--group-by model|provider|route] [--metric requests|tokens] [--model <name>] [--json]");
            Console.WriteLine();
            Console.WriteLine("  Usage for this machine: requests, failures, latency, time to first token, streaming, throughput, tokens,");
            Console.WriteLine("  totals by group, and average time by stage. Needs the client running. --json prints the full analytics,");
            Console.WriteLine("  the same data the Usage page in every Xeno app draws.");
            Console.WriteLine();
            Console.WriteLine("  served     Network requests this machine answered (default)");
            Console.WriteLine("  endpoint   Requests to the local inference endpoint");
            Console.WriteLine("  chat       Chat turns");
        }

        #endregion
    }
}

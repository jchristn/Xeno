namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Xeno.Core.Analytics;

    /// <summary>
    /// Builds every usage chart from <see cref="UsageAnalytics"/> and <see cref="ProviderHealthStrip"/>, following
    /// <see cref="XenoChartCatalog"/>. The desktop and terminal apps draw only what this returns; the web dashboards
    /// build the same series from the same catalog. Thread safe (stateless).
    /// </summary>
    public static class XenoUsageCharts
    {
        #region Public-Methods

        /// <summary>
        /// Build a Usage page chart.
        /// </summary>
        /// <param name="id">Catalog id (see <see cref="XenoChartCatalog"/>).</param>
        /// <param name="analytics">Analytics.</param>
        /// <param name="rangeText">Range description, for example "last day".</param>
        /// <returns>Chart.</returns>
        /// <exception cref="ArgumentNullException">analytics is null.</exception>
        /// <exception cref="KeyNotFoundException">The id is not a Usage page chart.</exception>
        public static XenoChart Build(string id, UsageAnalytics analytics, string rangeText)
        {
            ArgumentNullException.ThrowIfNull(analytics);
            XenoChartDefinition definition = XenoChartCatalog.Find(id);
            List<UsageBucket> buckets = analytics.Buckets;
            List<string> labels = buckets.Select(item => Label(item.BucketStartUtc, analytics.BucketMinutes)).ToList();
            List<XenoChartBucket> windows = buckets.Select(item => new XenoChartBucket(item.BucketStartUtc, item.BucketEndUtc)).ToList();
            string range = rangeText ?? String.Empty;
            string? notice = analytics.Truncated ? analytics.TruncatedReason : null;
            switch (id)
            {
                case XenoChartCatalog.Outcome:
                {
                    List<XenoChartSeries> series = new List<XenoChartSeries>
                    {
                        Fixed(definition, "succeeded", buckets.Select(item => (double)Math.Max(0, item.Succeeded - (item.Fallbacks ?? 0)))),
                        Fixed(definition, "fallback", buckets.Select(item => (double)(item.Fallbacks ?? 0))),
                        Fixed(definition, "failed", buckets.Select(item => (double)item.Failed))
                    };
                    if (!analytics.Totals.Fallbacks.HasValue) series.RemoveAt(1);
                    return Chart(definition, range, labels, series, "No requests in this range.", analytics.Totals.Requests > 0, windows, notice);
                }

                case XenoChartCatalog.Latency:
                    return PercentileChart(definition, range, labels, buckets.Select(item => item.Latency).ToList(), windows, notice, "No requests with timing in this range.");
                case XenoChartCatalog.TimeToFirstToken:
                    return PercentileChart(definition, range, labels, buckets.Select(item => item.TimeToFirstToken).ToList(), windows, notice, "No first-token timing in this range.");
                case XenoChartCatalog.Streaming:
                    return PercentileChart(definition, range, labels, buckets.Select(item => item.Streaming).ToList(), windows, notice, "No streamed answers in this range.");
                case XenoChartCatalog.Throughput:
                {
                    List<XenoChartSeries> series = new List<XenoChartSeries>
                    {
                        Fixed(definition, "p50", buckets.Select(item => Value(item.Throughput.P50))),
                        Fixed(definition, "p95", buckets.Select(item => Value(item.Throughput.P95)))
                    };
                    return Chart(definition, range, labels, series, "No token counts in this range.", buckets.Any(item => item.Throughput.Count > 0), windows, notice);
                }

                case XenoChartCatalog.Tokens:
                {
                    List<XenoChartSeries> series = new List<XenoChartSeries>
                    {
                        Fixed(definition, "prompt", buckets.Select(item => (double)item.PromptTokens)),
                        Fixed(definition, "cached", buckets.Select(item => (double)(item.CachedPromptTokens ?? 0))),
                        Fixed(definition, "completion", buckets.Select(item => (double)item.CompletionTokens))
                    };
                    if (!analytics.Totals.CachedPromptTokens.HasValue) series.RemoveAt(1);
                    return Chart(definition, range, labels, series, "No token counts in this range.", analytics.Totals.PromptTokens + analytics.Totals.CompletionTokens > 0, windows, notice);
                }

                case XenoChartCatalog.Groups:
                {
                    bool tokens = analytics.GroupMetric == UsageGroupMetricEnum.Tokens;
                    List<XenoChartSeries> series = new List<XenoChartSeries>();
                    for (int i = 0; i < analytics.GroupKeys.Count; i++)
                    {
                        string key = analytics.GroupKeys[i];
                        series.Add(new XenoChartSeries(KeyLabel(key), GroupTone(key, i), buckets.Select(item => (double)(item.Groups.FirstOrDefault(value => value.Key == key) is UsageGroupValue found ? (tokens ? found.Tokens : found.Requests) : 0)).ToList()));
                    }

                    string title = (tokens ? "Tokens" : "Requests") + " by " + GroupByLabel(analytics.GroupBy).ToLowerInvariant();
                    return new XenoChart(definition.Id, definition.Kind, title, range, tokens ? " tok" : " req", labels, series, "No requests in this range.", true, analytics.Totals.Requests > 0, true, null, null, windows, notice);
                }

                case XenoChartCatalog.GroupTotals:
                {
                    bool tokens = analytics.GroupMetric == UsageGroupMetricEnum.Tokens;
                    List<string> bars = analytics.GroupTotals.Select(item => KeyLabel(item.Key)).ToList();
                    List<XenoChartSeries> series = tokens
                        ? new List<XenoChartSeries>
                        {
                            new XenoChartSeries("Prompt", XenoChartToneEnum.Info, analytics.GroupTotals.Select(item => (double)item.PromptTokens).ToList()),
                            new XenoChartSeries("Completion", XenoChartToneEnum.Success, analytics.GroupTotals.Select(item => (double)item.CompletionTokens).ToList())
                        }
                        : new List<XenoChartSeries>
                        {
                            new XenoChartSeries("Succeeded", XenoChartToneEnum.Success, analytics.GroupTotals.Select(item => (double)(item.Requests - item.Failed)).ToList()),
                            new XenoChartSeries("Failed", XenoChartToneEnum.Danger, analytics.GroupTotals.Select(item => (double)item.Failed).ToList())
                        };
                    string title = (tokens ? "Tokens" : "Requests") + " by " + GroupByLabel(analytics.GroupBy).ToLowerInvariant() + ", whole range";
                    return new XenoChart(definition.Id, definition.Kind, title, range, tokens ? " tok" : " req", bars, series, "No requests in this range.", true, analytics.GroupTotals.Count > 0, true, null, null, null, notice);
                }

                case XenoChartCatalog.Heatmap:
                {
                    UsageHeatmap? heatmap = analytics.Heatmap;
                    if (heatmap == null) return new XenoChart(definition.Id, definition.Kind, definition.Title, range, definition.Unit, new List<string>(), new List<XenoChartSeries>(), "No heatmap for this range.", true, false, false, null, null, null, notice);
                    List<string> columns = HeatmapColumns(heatmap);
                    List<string> rows = HeatmapRows(heatmap);
                    List<IReadOnlyList<double>> grid = new List<IReadOnlyList<double>>();
                    for (int row = 0; row < heatmap.Rows; row++)
                    {
                        grid.Add(heatmap.Cells.Where(item => item.Row == row).OrderBy(item => item.Column).Select(item => (double)item.Tokens).ToList());
                    }

                    List<XenoChartSeries> series = new List<XenoChartSeries> { Fixed(definition, "tokens", grid.SelectMany(item => item)) };
                    return new XenoChart(definition.Id, definition.Kind, definition.Title, range + ", " + HeatmapLayoutLabel(heatmap.Layout), definition.Unit, columns, series, "No tokens in this range.", true, heatmap.MaximumTokens > 0, false, rows, grid, null, notice);
                }

                case XenoChartCatalog.Stages:
                {
                    List<XenoChartSeries> series = new List<XenoChartSeries>();
                    int index = 0;
                    foreach (UsageStageEnum stage in analytics.StageOrder)
                    {
                        series.Add(new XenoChartSeries(StageLabel(stage), StageTone(index++), buckets.Select(item => item.Stages.FirstOrDefault(value => value.Stage == stage)?.AverageMs ?? 0).ToList()));
                    }

                    return Chart(definition, range, labels, series, "No stage timing in this range.", analytics.StageAverages.Count > 0, windows, notice);
                }

                case XenoChartCatalog.StageAverages:
                {
                    List<UsageStageAverage> averages = analytics.StageOrder.Select(stage => analytics.StageAverages.FirstOrDefault(item => item.Stage == stage)).Where(item => item != null).Select(item => item!).ToList();
                    List<string> bars = averages.Select(item => StageLabel(item.Stage)).ToList();
                    List<XenoChartSeries> series = new List<XenoChartSeries>
                    {
                        Fixed(definition, "average", averages.Select(item => item.AverageMs ?? 0)),
                        Fixed(definition, "p95", averages.Select(item => item.P95Ms ?? 0))
                    };
                    return new XenoChart(definition.Id, definition.Kind, definition.Title, range, definition.Unit, bars, series, "No stage timing in this range.", false, averages.Count > 0, false, null, null, null, notice);
                }

                case XenoChartCatalog.Feedback:
                {
                    List<XenoChartSeries> series = new List<XenoChartSeries>
                    {
                        Fixed(definition, "up", buckets.Select(item => (double)item.ThumbsUp)),
                        Fixed(definition, "down", buckets.Select(item => (double)item.ThumbsDown))
                    };
                    return Chart(definition, range, labels, series, "No rated answers in this range.", analytics.Totals.ThumbsUp + analytics.Totals.ThumbsDown > 0, windows, notice);
                }

                case XenoChartCatalog.Tools:
                {
                    List<XenoChartSeries> series = new List<XenoChartSeries>
                    {
                        Fixed(definition, "succeeded", buckets.Select(item => (double)item.ToolCalls)),
                        Fixed(definition, "failed", buckets.Select(item => (double)item.ToolFailures)),
                        Fixed(definition, "denied", buckets.Select(item => (double)item.ToolDenied))
                    };
                    return Chart(definition, range, labels, series, "No tool calls in this range.", analytics.Totals.ToolCalls + analytics.Totals.ToolFailures + analytics.Totals.ToolDenied > 0, windows, notice);
                }

                default:
                    throw new KeyNotFoundException("'" + id + "' is not a Usage page chart.");
            }
        }

        /// <summary>
        /// Usage page charts that apply to a dataset, in order (feedback and tool calls apply only to chat).
        /// </summary>
        /// <param name="dataset">Dataset.</param>
        /// <returns>Chart ids.</returns>
        public static IReadOnlyList<string> ChartsFor(UsageDatasetEnum dataset)
        {
            return XenoChartCatalog.UsagePageCharts.Where(item => !item.ChatOnly || dataset == UsageDatasetEnum.Chat).Select(item => item.Id).ToList();
        }

        /// <summary>
        /// The provider health strip.
        /// </summary>
        /// <param name="strip">Strip.</param>
        /// <param name="rangeText">Range description.</param>
        /// <returns>Chart.</returns>
        /// <exception cref="ArgumentNullException">strip is null.</exception>
        public static XenoChart Health(ProviderHealthStrip strip, string rangeText)
        {
            ArgumentNullException.ThrowIfNull(strip);
            XenoChartDefinition definition = XenoChartCatalog.Find(XenoChartCatalog.Health);
            List<ProviderHealthCell> first = strip.Providers.Count == 0 ? new List<ProviderHealthCell>() : strip.Providers[0].Cells;
            List<string> labels = first.Select(item => Label(item.StartUtc, strip.BucketMinutes)).ToList();
            List<string> rows = strip.Providers.Select(item => item.ProviderName + (item.UptimePercent.HasValue ? " (" + item.UptimePercent.Value.ToString("0.#", CultureInfo.InvariantCulture) + "% up)" : String.Empty)).ToList();
            List<IReadOnlyList<double>> grid = strip.Providers.Select(item => (IReadOnlyList<double>)item.Cells.Select(cell => (double)Status(cell.Status)).ToList()).ToList();
            List<XenoChartBucket> windows = first.Select(item => new XenoChartBucket(item.StartUtc, item.StartUtc.AddMinutes(strip.BucketMinutes))).ToList();
            List<XenoChartSeries> series = definition.Series.Select(item => new XenoChartSeries(item.Name, item.Tone, new List<double>())).ToList();
            bool hasData = strip.Providers.Any(item => item.Cells.Any(cell => cell.Status != ProviderHealthStatusEnum.NoData));
            return new XenoChart(definition.Id, definition.Kind, definition.Title, rangeText ?? String.Empty, String.Empty, labels, series, "No provider health samples in this range. Turn on provider telemetry to collect them.", false, hasData, false, rows, grid, windows, null);
        }

        /// <summary>
        /// Where one request's or chat turn's time went, stage by stage.
        /// </summary>
        /// <param name="stages">Stage durations.</param>
        /// <returns>Chart.</returns>
        /// <exception cref="ArgumentNullException">stages is null.</exception>
        public static XenoChart RequestTiming(IReadOnlyList<UsageStageDuration> stages)
        {
            ArgumentNullException.ThrowIfNull(stages);
            XenoChartDefinition definition = XenoChartCatalog.Find(XenoChartCatalog.RequestTiming);
            double total = stages.Sum(item => item.Milliseconds);
            List<string> bars = stages.Select(item => StageLabel(item.Stage) + (total > 0 ? " (" + Math.Round(item.Milliseconds * 100 / total).ToString(CultureInfo.InvariantCulture) + "%)" : String.Empty)).ToList();
            List<XenoChartSeries> series = new List<XenoChartSeries> { new XenoChartSeries("Time", XenoChartToneEnum.Info, stages.Select(item => item.Milliseconds).ToList()) };
            return new XenoChart(definition.Id, definition.Kind, definition.Title, String.Empty, definition.Unit, bars, series, "This request has no stage timing.", false, stages.Count > 0, true, null, null, null, null);
        }

        /// <summary>
        /// Stages of one request this machine served, for its detail view.
        /// </summary>
        /// <param name="entry">Entry.</param>
        /// <returns>Stages.</returns>
        /// <exception cref="ArgumentNullException">entry is null.</exception>
        public static IReadOnlyList<UsageStageDuration> StagesFor(Xeno.Client.LocalDashboard.Models.LocalRequestHistoryEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            return UsageRecordMapper.FromServedRequest(entry.ReceivedUtc, entry.ModelName, entry.ProviderName, entry.StatusCode, entry.Outcome != Xeno.Client.LocalDashboard.Models.LocalRequestOutcomeEnum.Failure, entry.Telemetry.DurationMs, entry.Telemetry.TimeToHeadersMs, entry.Telemetry.TimeToFirstByteMs, entry.Telemetry.PromptTokens, entry.Telemetry.CompletionTokens).Stages;
        }

        /// <summary>
        /// Stages of one chat turn, for its detail view.
        /// </summary>
        /// <param name="turn">Turn.</param>
        /// <returns>Stages.</returns>
        /// <exception cref="ArgumentNullException">turn is null.</exception>
        public static IReadOnlyList<UsageStageDuration> StagesFor(Xeno.Core.Chat.ChatTurn turn)
        {
            ArgumentNullException.ThrowIfNull(turn);
            return UsageRecordMapper.FromChatTurn(turn).Stages;
        }

        /// <summary>
        /// Text bars for one request's timing, for the terminal app's detail views, for example
        /// "Provider to headers   ████░░░░░░  120 ms  12%".
        /// </summary>
        /// <param name="stages">Stage durations.</param>
        /// <param name="barWidth">Bar width in characters. Default 24.</param>
        /// <returns>Lines, or an empty list without stages.</returns>
        /// <exception cref="ArgumentNullException">stages is null.</exception>
        public static List<string> TimingLines(IReadOnlyList<UsageStageDuration> stages, int barWidth = 24)
        {
            ArgumentNullException.ThrowIfNull(stages);
            List<string> lines = new List<string>();
            double total = stages.Sum(item => item.Milliseconds);
            if (stages.Count == 0) return lines;
            int width = Math.Max(4, barWidth);
            int labelWidth = stages.Max(item => StageLabel(item.Stage).Length) + 2;
            foreach (UsageStageDuration stage in stages)
            {
                int filled = total <= 0 ? 0 : (int)Math.Round(stage.Milliseconds / total * width);
                string share = total <= 0 ? "0%" : Math.Round(stage.Milliseconds * 100 / total).ToString(CultureInfo.InvariantCulture) + "%";
                lines.Add(StageLabel(stage.Stage).PadRight(labelWidth) + new string('█', filled) + new string('░', width - filled) + "  " + XenoChart.FormatMilliseconds(stage.Milliseconds).PadLeft(8) + "  " + share.PadLeft(4));
            }

            return lines;
        }

        /// <summary>
        /// Markdown section with a request's timing bars, or null without stages.
        /// </summary>
        /// <param name="stages">Stage durations.</param>
        /// <returns>Markdown, or null.</returns>
        /// <exception cref="ArgumentNullException">stages is null.</exception>
        public static string? TimingMarkdown(IReadOnlyList<UsageStageDuration> stages)
        {
            ArgumentNullException.ThrowIfNull(stages);
            if (stages.Count == 0) return null;
            return XenoMarkdown.Heading("Where the time went") + "\n\n" + XenoMarkdown.CodeBlock(String.Join("\n", TimingLines(stages)), "text");
        }

        /// <summary>
        /// English label for a stage (the web translates the code).
        /// </summary>
        /// <param name="stage">Stage.</param>
        /// <returns>Label.</returns>
        public static string StageLabel(UsageStageEnum stage)
        {
            switch (stage)
            {
                case UsageStageEnum.ServerOverhead: return "Xeno server";
                case UsageStageEnum.RelayAndClient: return "Relay and client";
                case UsageStageEnum.Queue: return "Waiting for a slot";
                case UsageStageEnum.ProviderHeaders: return "Provider to headers";
                case UsageStageEnum.ProviderFirstToken: return "To first token";
                case UsageStageEnum.Streaming: return "Streaming";
                case UsageStageEnum.ModelFirstToken: return "Model to first token";
                case UsageStageEnum.ModelStreaming: return "Model writing";
                case UsageStageEnum.Tools: return "Tools";
                case UsageStageEnum.Approval: return "Waiting for approval";
                default: return "Other";
            }
        }

        /// <summary>
        /// English label for a dataset.
        /// </summary>
        /// <param name="dataset">Dataset.</param>
        /// <returns>Label.</returns>
        public static string DatasetLabel(UsageDatasetEnum dataset)
        {
            switch (dataset)
            {
                case UsageDatasetEnum.NetworkRequests: return "Network requests";
                case UsageDatasetEnum.ServedRequests: return "Served requests";
                case UsageDatasetEnum.LocalEndpoint: return "Local endpoint";
                default: return "Chat";
            }
        }

        /// <summary>
        /// English label for a grouping.
        /// </summary>
        /// <param name="groupBy">Grouping.</param>
        /// <returns>Label.</returns>
        public static string GroupByLabel(UsageGroupByEnum groupBy)
        {
            switch (groupBy)
            {
                case UsageGroupByEnum.Provider: return "Provider";
                case UsageGroupByEnum.Route: return "Route";
                case UsageGroupByEnum.Client: return "Client";
                case UsageGroupByEnum.Tenant: return "Tenant";
                case UsageGroupByEnum.User: return "User";
                case UsageGroupByEnum.Credential: return "Credential";
                default: return "Model";
            }
        }

        /// <summary>
        /// Display text for a group key: Other and Unknown are spelled out; local and network routes are named.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>Label.</returns>
        public static string KeyLabel(string key)
        {
            if (key == UsageAnalyticsBuilder.OtherKey) return "Other";
            if (key == UsageAnalyticsBuilder.UnknownKey) return "Unknown";
            if (key == "local") return "This machine";
            if (key == "network") return "Xeno network";
            return key;
        }

        /// <summary>
        /// The tone for the group key at an index: categorical colors in order, neutral for Other and Unknown.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="index">Index among group keys.</param>
        /// <returns>Tone.</returns>
        public static XenoChartToneEnum GroupTone(string key, int index)
        {
            if (key == UsageAnalyticsBuilder.OtherKey || key == UsageAnalyticsBuilder.UnknownKey) return XenoChartToneEnum.Neutral;
            return XenoChartCatalog.CategoryTones[Math.Abs(index) % XenoChartCatalog.CategoryTones.Count];
        }

        /// <summary>
        /// Bucket label in local time: "HH:mm" for buckets up to 15 minutes, otherwise "MM-dd HH:mm".
        /// </summary>
        /// <param name="startUtc">Bucket start.</param>
        /// <param name="bucketMinutes">Bucket minutes.</param>
        /// <returns>Label.</returns>
        public static string Label(DateTime startUtc, int bucketMinutes)
        {
            DateTime local = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc).ToLocalTime();
            return local.ToString(bucketMinutes <= 15 ? "HH:mm" : "MM-dd HH:mm", CultureInfo.InvariantCulture);
        }

        #endregion

        #region Private-Methods

        private static XenoChart Chart(XenoChartDefinition definition, string range, List<string> labels, List<XenoChartSeries> series, string empty, bool hasData, List<XenoChartBucket> windows, string? notice)
        {
            return new XenoChart(definition.Id, definition.Kind, definition.Title, range, definition.Unit, labels, series, empty, definition.Additive, hasData, definition.Stacked, null, null, windows, notice);
        }

        private static XenoChart PercentileChart(XenoChartDefinition definition, string range, List<string> labels, List<UsageDistribution> distributions, List<XenoChartBucket> windows, string? notice, string empty)
        {
            List<XenoChartSeries> series = new List<XenoChartSeries>
            {
                Fixed(definition, "p50", distributions.Select(item => Value(item.P50))),
                Fixed(definition, "p95", distributions.Select(item => Value(item.P95))),
                Fixed(definition, "p99", distributions.Select(item => Value(item.P99))),
                Fixed(definition, "maximum", distributions.Select(item => Value(item.Maximum)))
            };
            return Chart(definition, range, labels, series, empty, distributions.Any(item => item.Count > 0), windows, notice);
        }

        private static XenoChartSeries Fixed(XenoChartDefinition definition, string key, IEnumerable<double> values)
        {
            XenoChartSeriesDefinition series = definition.Series.First(item => item.Key == key);
            return new XenoChartSeries(series.Name, series.Tone, values.ToList());
        }

        private static double Value(double? value)
        {
            return value ?? Double.NaN;
        }

        private static XenoChartToneEnum StageTone(int index)
        {
            return XenoChartCatalog.CategoryTones[index % XenoChartCatalog.CategoryTones.Count];
        }

        private static int Status(ProviderHealthStatusEnum status)
        {
            switch (status)
            {
                case ProviderHealthStatusEnum.Healthy: return (int)XenoChartStatusEnum.Healthy;
                case ProviderHealthStatusEnum.Degraded: return (int)XenoChartStatusEnum.Degraded;
                case ProviderHealthStatusEnum.Unhealthy: return (int)XenoChartStatusEnum.Unhealthy;
                default: return (int)XenoChartStatusEnum.NoData;
            }
        }

        private static List<string> HeatmapColumns(UsageHeatmap heatmap)
        {
            if (heatmap.Layout == UsageHeatmapLayoutEnum.HourByMinute) return Enumerable.Range(0, heatmap.Columns).Select(item => ":" + item.ToString(CultureInfo.InvariantCulture)).ToList();
            return Enumerable.Range(0, heatmap.Columns).Select(item => item.ToString("00", CultureInfo.InvariantCulture)).ToList();
        }

        private static List<string> HeatmapRows(UsageHeatmap heatmap)
        {
            if (heatmap.Layout == UsageHeatmapLayoutEnum.WeekdayByHour) return new List<string> { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" };
            List<string> rows = new List<string>();
            for (int row = 0; row < heatmap.Rows; row++)
            {
                UsageHeatmapCell? first = heatmap.Cells.FirstOrDefault(item => item.Row == row && item.Column == 0);
                DateTime local = first == null ? DateTime.MinValue : first.StartUtc.AddMinutes(heatmap.UtcOffsetMinutes);
                rows.Add(heatmap.Layout == UsageHeatmapLayoutEnum.HourByMinute ? local.ToString("HH:mm", CultureInfo.InvariantCulture) : local.ToString("ddd MM-dd", CultureInfo.InvariantCulture));
            }

            return rows;
        }

        private static string HeatmapLayoutLabel(UsageHeatmapLayoutEnum layout)
        {
            switch (layout)
            {
                case UsageHeatmapLayoutEnum.HourByMinute: return "by minute";
                case UsageHeatmapLayoutEnum.DayByHour: return "by hour of day";
                default: return "by weekday and hour";
            }
        }

        #endregion
    }
}

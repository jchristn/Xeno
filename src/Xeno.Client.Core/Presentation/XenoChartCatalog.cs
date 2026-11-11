namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Xeno.Core.Serialization;

    /// <summary>
    /// The definition of every usage chart, shared by every Xeno surface: id, kind, title, unit, series (key, tone,
    /// label), and the i18n keys the web dashboards use. The desktop and terminal apps build charts from these through
    /// <see cref="XenoUsageCharts"/>; the web dashboards read the same definitions from
    /// dashboard/src/charts/chartCatalog.json, which a test keeps identical to <see cref="ToJson"/>.
    /// </summary>
    public static class XenoChartCatalog
    {
        #region Public-Members

        /// <summary>
        /// Requests by outcome, with network fallback.
        /// </summary>
        public const string Outcome = "usage.outcome";

        /// <summary>
        /// Total latency percentiles.
        /// </summary>
        public const string Latency = "usage.latency";

        /// <summary>
        /// Streaming time percentiles.
        /// </summary>
        public const string Streaming = "usage.streaming";

        /// <summary>
        /// Time to first token percentiles.
        /// </summary>
        public const string TimeToFirstToken = "usage.ttft";

        /// <summary>
        /// Tokens per second.
        /// </summary>
        public const string Throughput = "usage.throughput";

        /// <summary>
        /// Tokens by type.
        /// </summary>
        public const string Tokens = "usage.tokens";

        /// <summary>
        /// Activity by group over time.
        /// </summary>
        public const string Groups = "usage.groups";

        /// <summary>
        /// Totals by group.
        /// </summary>
        public const string GroupTotals = "usage.groupTotals";

        /// <summary>
        /// Token activity heatmap.
        /// </summary>
        public const string Heatmap = "usage.heatmap";

        /// <summary>
        /// Timing by stage over time.
        /// </summary>
        public const string Stages = "usage.stages";

        /// <summary>
        /// Average time by stage.
        /// </summary>
        public const string StageAverages = "usage.stageAverages";

        /// <summary>
        /// Thumbs up and down over time.
        /// </summary>
        public const string Feedback = "usage.feedback";

        /// <summary>
        /// Tool calls over time.
        /// </summary>
        public const string Tools = "usage.tools";

        /// <summary>
        /// Provider health strip.
        /// </summary>
        public const string Health = "usage.health";

        /// <summary>
        /// One request's timing by stage.
        /// </summary>
        public const string RequestTiming = "request.timing";

        /// <summary>
        /// Every chart, in Usage page order.
        /// </summary>
        public static readonly IReadOnlyList<XenoChartDefinition> All = new List<XenoChartDefinition>
        {
            Define(Outcome, XenoChartKindEnum.StackedColumns, "Requests by outcome", "usageChartOutcome", " req", true, true, false, false, Series("succeeded", "Succeeded", "usageSeriesSucceeded", XenoChartToneEnum.Success), Series("fallback", "Answered by network fallback", "usageSeriesFallback", XenoChartToneEnum.Warning), Series("failed", "Failed", "usageSeriesFailed", XenoChartToneEnum.Danger)),
            Define(Latency, XenoChartKindEnum.Lines, "Latency", "usageChartLatency", " ms", false, false, false, false, Percentiles()),
            Define(TimeToFirstToken, XenoChartKindEnum.Lines, "Time to first token", "usageChartTimeToFirstToken", " ms", false, false, false, false, Percentiles()),
            Define(Streaming, XenoChartKindEnum.Lines, "Streaming time", "usageChartStreaming", " ms", false, false, false, false, Percentiles()),
            Define(Throughput, XenoChartKindEnum.Lines, "Throughput", "usageChartThroughput", " tok/s", false, false, false, false, Series("p50", "Median", "usageSeriesMedian", XenoChartToneEnum.Info), Series("p95", "95th percentile", "usageSeriesP95", XenoChartToneEnum.Warning)),
            Define(Tokens, XenoChartKindEnum.StackedColumns, "Tokens by type", "usageChartTokens", " tok", true, true, false, false, Series("prompt", "Prompt", "usageSeriesPrompt", XenoChartToneEnum.Info), Series("cached", "Cached prompt", "usageSeriesCached", XenoChartToneEnum.Accent), Series("completion", "Completion", "usageSeriesCompletion", XenoChartToneEnum.Success)),
            Define(Groups, XenoChartKindEnum.StackedColumns, "Activity by group", "usageChartGroups", String.Empty, true, true, true, false),
            Define(GroupTotals, XenoChartKindEnum.HorizontalBars, "Totals by group", "usageChartGroupTotals", String.Empty, true, true, true, false),
            Define(Heatmap, XenoChartKindEnum.Heatmap, "Token activity", "usageChartHeatmap", " tok", true, false, false, false, Series("tokens", "Tokens", "usageSeriesTokens", XenoChartToneEnum.Accent)),
            Define(Stages, XenoChartKindEnum.StackedColumns, "Timing by stage", "usageChartStages", " ms", false, true, true, false),
            Define(StageAverages, XenoChartKindEnum.HorizontalBars, "Average time by stage", "usageChartStageAverages", " ms", false, false, false, false, Series("average", "Average", "usageSeriesAverage", XenoChartToneEnum.Info), Series("p95", "95th percentile", "usageSeriesP95", XenoChartToneEnum.Warning)),
            Define(Feedback, XenoChartKindEnum.StackedColumns, "Feedback", "usageChartFeedback", String.Empty, true, true, false, true, Series("up", "Thumbs up", "usageSeriesThumbsUp", XenoChartToneEnum.Success), Series("down", "Thumbs down", "usageSeriesThumbsDown", XenoChartToneEnum.Danger)),
            Define(Tools, XenoChartKindEnum.StackedColumns, "Tool calls", "usageChartTools", " calls", true, true, false, true, Series("succeeded", "Ran", "usageSeriesToolsRan", XenoChartToneEnum.Success), Series("failed", "Failed", "usageSeriesFailed", XenoChartToneEnum.Danger), Series("denied", "Denied", "usageSeriesToolsDenied", XenoChartToneEnum.Warning)),
            Define(Health, XenoChartKindEnum.StatusStrip, "Provider health", "usageChartHealth", String.Empty, false, false, false, false, Series("healthy", "Healthy", "usageStatusHealthy", XenoChartToneEnum.Success), Series("degraded", "Degraded", "usageStatusDegraded", XenoChartToneEnum.Warning), Series("unhealthy", "Unhealthy", "usageStatusUnhealthy", XenoChartToneEnum.Danger), Series("noData", "No data", "usageStatusNoData", XenoChartToneEnum.Neutral)),
            Define(RequestTiming, XenoChartKindEnum.HorizontalBars, "Where the time went", "usageChartRequestTiming", " ms", false, true, true, false)
        };

        /// <summary>
        /// Charts the Usage page cycles through, in order (the health strip and request timing live on other pages).
        /// </summary>
        public static IReadOnlyList<XenoChartDefinition> UsagePageCharts
        {
            get { return All.Where(item => item.Id != Health && item.Id != RequestTiming).ToList(); }
        }

        /// <summary>
        /// Ordered categorical tones for group and stage series; Other and Unknown use <see cref="XenoChartToneEnum.Neutral"/>.
        /// </summary>
        public static readonly IReadOnlyList<XenoChartToneEnum> CategoryTones = new List<XenoChartToneEnum>
        {
            XenoChartToneEnum.Series1, XenoChartToneEnum.Series2, XenoChartToneEnum.Series3, XenoChartToneEnum.Series4,
            XenoChartToneEnum.Series5, XenoChartToneEnum.Series6, XenoChartToneEnum.Series7, XenoChartToneEnum.Series8
        };

        #endregion

        #region Public-Methods

        /// <summary>
        /// A chart definition by id.
        /// </summary>
        /// <param name="id">Id.</param>
        /// <returns>Definition.</returns>
        /// <exception cref="KeyNotFoundException">No chart has the id.</exception>
        public static XenoChartDefinition Find(string id)
        {
            return All.FirstOrDefault(item => String.Equals(item.Id, id, StringComparison.Ordinal)) ?? throw new KeyNotFoundException("No chart '" + id + "'.");
        }

        /// <summary>
        /// The catalog as JSON, exactly as committed in dashboard/src/charts/chartCatalog.json.
        /// </summary>
        /// <returns>JSON with a trailing newline.</returns>
        public static string ToJson()
        {
            return JsonUtil.Serialize(new XenoChartCatalogDocument { Charts = All.ToList(), CategoryTones = CategoryTones.ToList(), CategoryColors = XenoChartPalette.Categories.ToList(), NeutralColor = XenoChartPalette.Neutral }, true).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
        }

        #endregion

        #region Private-Methods

        private static XenoChartDefinition Define(string id, XenoChartKindEnum kind, string title, string titleKey, string unit, bool additive, bool stacked, bool dynamicSeries, bool chatOnly, params XenoChartSeriesDefinition[] series)
        {
            return new XenoChartDefinition { Id = id, Kind = kind, Title = title, TitleKey = titleKey, Unit = unit, Additive = additive, Stacked = stacked, DynamicSeries = dynamicSeries, ChatOnly = chatOnly, Series = series.ToList() };
        }

        private static XenoChartSeriesDefinition Series(string key, string name, string labelKey, XenoChartToneEnum tone)
        {
            return new XenoChartSeriesDefinition { Key = key, Name = name, LabelKey = labelKey, Tone = tone };
        }

        private static XenoChartSeriesDefinition[] Percentiles()
        {
            return new[]
            {
                Series("p50", "Median", "usageSeriesMedian", XenoChartToneEnum.Info),
                Series("p95", "95th percentile", "usageSeriesP95", XenoChartToneEnum.Warning),
                Series("p99", "99th percentile", "usageSeriesP99", XenoChartToneEnum.Danger),
                Series("maximum", "Slowest", "usageSeriesMaximum", XenoChartToneEnum.Neutral)
            };
        }

        #endregion
    }
}

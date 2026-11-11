namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Usage analytics for one dataset and range: gap-filled buckets, totals, groups, heatmap, and stages. Every Xeno surface draws this shape and none computes it.
    /// </summary>
    public sealed class UsageAnalytics
    {
        /// <summary>
        /// Dataset.
        /// </summary>
        public UsageDatasetEnum Dataset { get; set; } = UsageDatasetEnum.NetworkRequests;

        /// <summary>
        /// Range start, UTC.
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Range end, UTC.
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Bucket size in minutes.
        /// </summary>
        public int BucketMinutes { get; set; } = 0;

        /// <summary>
        /// Range totals.
        /// </summary>
        public UsageTotals Totals { get; set; } = new UsageTotals();

        /// <summary>
        /// Every bucket in the range, empty ones included.
        /// </summary>
        public List<UsageBucket> Buckets { get; set; } = new List<UsageBucket>();

        /// <summary>
        /// What groups are.
        /// </summary>
        public UsageGroupByEnum GroupBy { get; set; } = UsageGroupByEnum.Model;

        /// <summary>
        /// What ranks groups.
        /// </summary>
        public UsageGroupMetricEnum GroupMetric { get; set; } = UsageGroupMetricEnum.Requests;

        /// <summary>
        /// Top group keys in rank order, then Other when anything was folded.
        /// </summary>
        public List<string> GroupKeys { get; set; } = new List<string>();

        /// <summary>
        /// Totals per group key, in GroupKeys order.
        /// </summary>
        public List<UsageGroupTotal> GroupTotals { get; set; } = new List<UsageGroupTotal>();

        /// <summary>
        /// Token activity heatmap, when requested.
        /// </summary>
        public UsageHeatmap? Heatmap { get; set; } = null;

        /// <summary>
        /// Average and 95th percentile per stage for the range.
        /// </summary>
        public List<UsageStageAverage> StageAverages { get; set; } = new List<UsageStageAverage>();

        /// <summary>
        /// Stages this dataset can have, in display order.
        /// </summary>
        public List<UsageStageEnum> StageOrder { get; set; } = new List<UsageStageEnum>();

        /// <summary>
        /// Stage values that came out negative and were clamped.
        /// </summary>
        public int StageAnomalies { get; set; } = 0;

        /// <summary>
        /// Records read.
        /// </summary>
        public long RecordsScanned { get; set; } = 0;

        /// <summary>
        /// True when more records matched than the scan limit, so the range was not read in full.
        /// </summary>
        public bool Truncated { get; set; } = false;

        /// <summary>
        /// Why the range was not read in full, when Truncated.
        /// </summary>
        public string? TruncatedReason { get; set; } = null;
    }
}

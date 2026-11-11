namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Usage analytics for one dataset and range.
    /// </summary>
    public class UsageAnalytics
    {
        /// <summary>
        /// NetworkRequests or Chat.
        /// </summary>
        public string Dataset { get; set; } = String.Empty;

        /// <summary>
        /// Range start, UTC.
        /// </summary>
        public DateTime FromUtc { get; set; }

        /// <summary>
        /// Range end, UTC.
        /// </summary>
        public DateTime ToUtc { get; set; }

        /// <summary>
        /// Bucket size in minutes.
        /// </summary>
        public int BucketMinutes { get; set; }

        /// <summary>
        /// Range totals.
        /// </summary>
        public UsageTotals Totals { get; set; } = new UsageTotals();

        /// <summary>
        /// Every bucket in the range.
        /// </summary>
        public List<UsageBucket> Buckets { get; set; } = new List<UsageBucket>();

        /// <summary>
        /// What groups are.
        /// </summary>
        public string GroupBy { get; set; } = String.Empty;

        /// <summary>
        /// What ranks groups.
        /// </summary>
        public string GroupMetric { get; set; } = String.Empty;

        /// <summary>
        /// Top group keys, then Other when anything was folded.
        /// </summary>
        public List<string> GroupKeys { get; set; } = new List<string>();

        /// <summary>
        /// Totals per group key.
        /// </summary>
        public List<UsageGroupTotal> GroupTotals { get; set; } = new List<UsageGroupTotal>();

        /// <summary>
        /// Token heatmap, when requested.
        /// </summary>
        public UsageHeatmap? Heatmap { get; set; }

        /// <summary>
        /// Average per stage for the range.
        /// </summary>
        public List<UsageStageAverage> StageAverages { get; set; } = new List<UsageStageAverage>();

        /// <summary>
        /// Stages this dataset can have, in order.
        /// </summary>
        public List<string> StageOrder { get; set; } = new List<string>();

        /// <summary>
        /// Stage values clamped from negative.
        /// </summary>
        public int StageAnomalies { get; set; }

        /// <summary>
        /// Records read.
        /// </summary>
        public long RecordsScanned { get; set; }

        /// <summary>
        /// True when the range was not read in full.
        /// </summary>
        public bool Truncated { get; set; }

        /// <summary>
        /// Why the range was not read in full.
        /// </summary>
        public string? TruncatedReason { get; set; }
    }
}

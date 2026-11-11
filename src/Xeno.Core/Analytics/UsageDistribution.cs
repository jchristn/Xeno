namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Distribution of one measurement: count, average, and nearest-rank percentiles. Values are null when Count is 0.
    /// </summary>
    public sealed class UsageDistribution
    {
        /// <summary>
        /// Values measured.
        /// </summary>
        public int Count { get; set; } = 0;

        /// <summary>
        /// Mean.
        /// </summary>
        public double? Average { get; set; } = null;

        /// <summary>
        /// Median.
        /// </summary>
        public double? P50 { get; set; } = null;

        /// <summary>
        /// 95th percentile.
        /// </summary>
        public double? P95 { get; set; } = null;

        /// <summary>
        /// 99th percentile.
        /// </summary>
        public double? P99 { get; set; } = null;

        /// <summary>
        /// Largest value.
        /// </summary>
        public double? Maximum { get; set; } = null;
    }
}

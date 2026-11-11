namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Count, average, and nearest-rank percentiles of one measurement. Values are null when Count is 0.
    /// </summary>
    public class UsageDistribution
    {
        /// <summary>
        /// Values measured.
        /// </summary>
        public int Count { get; set; }

        /// <summary>
        /// Mean.
        /// </summary>
        public double? Average { get; set; }

        /// <summary>
        /// Median.
        /// </summary>
        public double? P50 { get; set; }

        /// <summary>
        /// 95th percentile.
        /// </summary>
        public double? P95 { get; set; }

        /// <summary>
        /// 99th percentile.
        /// </summary>
        public double? P99 { get; set; }

        /// <summary>
        /// Largest value.
        /// </summary>
        public double? Maximum { get; set; }
    }
}

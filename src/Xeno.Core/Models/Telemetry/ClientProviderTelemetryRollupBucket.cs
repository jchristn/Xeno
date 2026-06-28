namespace Xeno.Core.Models.Telemetry
{
    using System;

    /// <summary>
    /// Provider telemetry rollup bucket.
    /// </summary>
    public class ClientProviderTelemetryRollupBucket
    {
        /// <summary>
        /// Bucket start timestamp.
        /// </summary>
        public DateTime StartUtc { get; set; }

        /// <summary>
        /// Bucket end timestamp.
        /// </summary>
        public DateTime EndUtc { get; set; }

        /// <summary>
        /// Display label.
        /// </summary>
        public string Label { get; set; } = string.Empty;

        /// <summary>
        /// Sample count.
        /// </summary>
        public int Count { get; set; } = 0;

        /// <summary>
        /// Average value.
        /// </summary>
        public double? Average { get; set; } = null;

        /// <summary>
        /// Minimum value.
        /// </summary>
        public double? Minimum { get; set; } = null;

        /// <summary>
        /// Maximum value.
        /// </summary>
        public double? Maximum { get; set; } = null;
    }
}

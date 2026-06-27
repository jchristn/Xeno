namespace Xeno.Client.Telemetry.Models
{
    using System;

    /// <summary>
    /// Telemetry rollup bucket.
    /// </summary>
    public class ClientTelemetryRollupBucket
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
        /// Number of samples.
        /// </summary>
        public int Count { get; set; } = 0;

        /// <summary>
        /// Average metric value.
        /// </summary>
        public double? Average { get; set; } = null;

        /// <summary>
        /// Minimum metric value.
        /// </summary>
        public double? Minimum { get; set; } = null;

        /// <summary>
        /// Maximum metric value.
        /// </summary>
        public double? Maximum { get; set; } = null;
    }
}

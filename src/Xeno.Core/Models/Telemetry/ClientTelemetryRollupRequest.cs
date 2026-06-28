namespace Xeno.Core.Models.Telemetry
{
    using System;

    /// <summary>
    /// Telemetry rollup request.
    /// </summary>
    public class ClientTelemetryRollupRequest
    {
        /// <summary>
        /// Inclusive start timestamp.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// Exclusive end timestamp.
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;

        /// <summary>
        /// Bucket width in minutes.
        /// </summary>
        public int BucketMinutes { get; set; } = 5;

        /// <summary>
        /// Metric to roll up.
        /// </summary>
        public ClientTelemetryMetricEnum Metric { get; set; } = ClientTelemetryMetricEnum.CpuUtilizationPercent;
    }
}

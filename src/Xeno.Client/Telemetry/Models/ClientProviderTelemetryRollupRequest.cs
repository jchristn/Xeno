namespace Xeno.Client.Telemetry.Models
{
    using System;

    /// <summary>
    /// Provider telemetry rollup request.
    /// </summary>
    public class ClientProviderTelemetryRollupRequest
    {
        /// <summary>
        /// From timestamp.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// To timestamp.
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;

        /// <summary>
        /// Bucket width in minutes.
        /// </summary>
        public int BucketMinutes { get; set; } = 1;

        /// <summary>
        /// Provider filter.
        /// </summary>
        public string? ProviderName { get; set; } = null;

        /// <summary>
        /// Model filter.
        /// </summary>
        public string? ModelName { get; set; } = null;

        /// <summary>
        /// Metric.
        /// </summary>
        public ClientProviderTelemetryMetricEnum Metric { get; set; } = ClientProviderTelemetryMetricEnum.ActiveRequests;
    }
}

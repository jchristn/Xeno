namespace Xeno.Core.Models.Telemetry
{
    using System;

    /// <summary>
    /// Provider telemetry search request.
    /// </summary>
    public class ClientProviderTelemetrySearchRequest
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
        /// Provider filter.
        /// </summary>
        public string? ProviderName { get; set; } = null;

        /// <summary>
        /// Model filter.
        /// </summary>
        public string? ModelName { get; set; } = null;

        /// <summary>
        /// Status filter.
        /// </summary>
        public ClientProviderTelemetryStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Metric for filtering and display.
        /// </summary>
        public ClientProviderTelemetryMetricEnum Metric { get; set; } = ClientProviderTelemetryMetricEnum.ActiveRequests;

        /// <summary>
        /// Limit.
        /// </summary>
        public int Limit { get; set; } = 100;

        /// <summary>
        /// Offset.
        /// </summary>
        public int Offset { get; set; } = 0;
    }
}

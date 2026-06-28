namespace Xeno.Core.Models.Telemetry
{
    using System;

    /// <summary>
    /// Telemetry history search request.
    /// </summary>
    public class ClientTelemetrySearchRequest
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
        /// Metric filter.
        /// </summary>
        public ClientTelemetryMetricEnum Metric { get; set; } = ClientTelemetryMetricEnum.CpuUtilizationPercent;

        /// <summary>
        /// Minimum metric value.
        /// </summary>
        public double? MinValue { get; set; } = null;

        /// <summary>
        /// Maximum metric value.
        /// </summary>
        public double? MaxValue { get; set; } = null;

        /// <summary>
        /// Optional platform or OS filter.
        /// </summary>
        public string? Platform { get; set; } = null;

        /// <summary>
        /// Optional GPU UUID filter.
        /// </summary>
        public string? GpuUuid { get; set; } = null;

        /// <summary>
        /// Optional GPU model filter.
        /// </summary>
        public string? GpuModel { get; set; } = null;

        /// <summary>
        /// Maximum rows to return.
        /// </summary>
        public int Limit { get; set; } = 100;

        /// <summary>
        /// Offset.
        /// </summary>
        public int Offset { get; set; } = 0;
    }
}

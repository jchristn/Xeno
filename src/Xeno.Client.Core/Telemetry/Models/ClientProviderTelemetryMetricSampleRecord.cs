namespace Xeno.Client.Telemetry.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Provider telemetry metric sample record.
    /// </summary>
    public class ClientProviderTelemetryMetricSampleRecord
    {
        /// <summary>
        /// Row ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Sample ID.
        /// </summary>
        public string SampleId { get; set; } = string.Empty;

        /// <summary>
        /// Provider name.
        /// </summary>
        public string ProviderName { get; set; } = string.Empty;

        /// <summary>
        /// Model name.
        /// </summary>
        public string? ModelName { get; set; } = null;

        /// <summary>
        /// Metric name.
        /// </summary>
        public string MetricName { get; set; } = string.Empty;

        /// <summary>
        /// Metric kind.
        /// </summary>
        public string MetricKind { get; set; } = string.Empty;

        /// <summary>
        /// Metric value.
        /// </summary>
        public double MetricValue { get; set; } = 0;

        /// <summary>
        /// Metric unit.
        /// </summary>
        public string? MetricUnit { get; set; } = null;

        /// <summary>
        /// Metric source.
        /// </summary>
        public ClientProviderTelemetryMetricSourceEnum Source { get; set; } = ClientProviderTelemetryMetricSourceEnum.Unknown;

        /// <summary>
        /// Labels.
        /// </summary>
        public List<ClientProviderTelemetryLabel> Labels { get; set; } = new List<ClientProviderTelemetryLabel>();
    }
}

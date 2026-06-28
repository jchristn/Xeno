namespace Xeno.Core.Models.Telemetry
{
    using System.Collections.Generic;

    /// <summary>
    /// Provider telemetry metric.
    /// </summary>
    public class ClientProviderTelemetryMetric
    {
        /// <summary>
        /// Metric name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Metric kind.
        /// </summary>
        public string Kind { get; set; } = string.Empty;

        /// <summary>
        /// Metric value.
        /// </summary>
        public double Value { get; set; } = 0;

        /// <summary>
        /// Metric unit.
        /// </summary>
        public string? Unit { get; set; } = null;

        /// <summary>
        /// Source.
        /// </summary>
        public ClientProviderTelemetryMetricSourceEnum Source { get; set; } = ClientProviderTelemetryMetricSourceEnum.Unknown;

        /// <summary>
        /// Associated model name.
        /// </summary>
        public string? ModelName { get; set; } = null;

        /// <summary>
        /// Metric labels.
        /// </summary>
        public List<ClientProviderTelemetryLabel> Labels { get; set; } = new List<ClientProviderTelemetryLabel>();
    }
}

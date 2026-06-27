namespace Xeno.Client.Telemetry.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Parsed Prometheus metric sample.
    /// </summary>
    public class PrometheusMetricSample
    {
        /// <summary>
        /// Metric name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Metric value.
        /// </summary>
        public double Value { get; set; } = 0;

        /// <summary>
        /// Optional timestamp.
        /// </summary>
        public long? Timestamp { get; set; } = null;

        /// <summary>
        /// Labels.
        /// </summary>
        public List<ClientProviderTelemetryLabel> Labels { get; set; } = new List<ClientProviderTelemetryLabel>();
    }
}

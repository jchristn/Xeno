namespace Xeno.Client.Telemetry.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Parsed Prometheus metric document.
    /// </summary>
    public class PrometheusMetricDocument
    {
        /// <summary>
        /// Metric samples.
        /// </summary>
        public List<PrometheusMetricSample> Samples { get; set; } = new List<PrometheusMetricSample>();
    }
}

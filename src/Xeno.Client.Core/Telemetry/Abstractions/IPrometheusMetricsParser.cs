namespace Xeno.Client.Telemetry.Abstractions
{
    using System.Threading;
    using Xeno.Client.Telemetry.Models;

    /// <summary>
    /// Prometheus metrics parser.
    /// </summary>
    public interface IPrometheusMetricsParser
    {
        /// <summary>
        /// Parse Prometheus text exposition metrics.
        /// </summary>
        /// <param name="metrics">Metrics text.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Metric document.</returns>
        PrometheusMetricDocument Parse(string metrics, CancellationToken token);
    }
}

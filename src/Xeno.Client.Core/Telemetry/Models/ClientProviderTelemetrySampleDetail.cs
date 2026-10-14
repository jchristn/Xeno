namespace Xeno.Client.Telemetry.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Provider telemetry sample detail.
    /// </summary>
    public class ClientProviderTelemetrySampleDetail
    {
        /// <summary>
        /// Sample record.
        /// </summary>
        public ClientProviderTelemetrySampleRecord Sample { get; set; } = new ClientProviderTelemetrySampleRecord();

        /// <summary>
        /// Model records.
        /// </summary>
        public List<ClientProviderTelemetryModelSampleRecord> Models { get; set; } = new List<ClientProviderTelemetryModelSampleRecord>();

        /// <summary>
        /// Metric records.
        /// </summary>
        public List<ClientProviderTelemetryMetricSampleRecord> Metrics { get; set; } = new List<ClientProviderTelemetryMetricSampleRecord>();

        /// <summary>
        /// Original snapshot.
        /// </summary>
        public ClientProviderTelemetryProviderStatus? Snapshot { get; set; } = null;
    }
}

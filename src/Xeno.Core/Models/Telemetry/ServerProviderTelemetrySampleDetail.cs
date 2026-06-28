namespace Xeno.Core.Models.Telemetry
{
    using System.Collections.Generic;

    /// <summary>
    /// Server provider telemetry detail response.
    /// </summary>
    public class ServerProviderTelemetrySampleDetail
    {
        /// <summary>
        /// Provider telemetry sample.
        /// </summary>
        public ServerProviderTelemetrySampleRecord Sample { get; set; } = new ServerProviderTelemetrySampleRecord();

        /// <summary>
        /// Provider model rows.
        /// </summary>
        public List<ServerProviderTelemetryModelSampleRecord> Models { get; set; } = new List<ServerProviderTelemetryModelSampleRecord>();

        /// <summary>
        /// Provider metric rows.
        /// </summary>
        public List<ServerProviderTelemetryMetricSampleRecord> Metrics { get; set; } = new List<ServerProviderTelemetryMetricSampleRecord>();

        /// <summary>
        /// Bounded raw snapshot JSON, when persisted.
        /// </summary>
        public string? SnapshotJson { get; set; } = null;
    }
}

namespace Xeno.Core.Models.Telemetry
{
    using System.Collections.Generic;

    /// <summary>
    /// Detailed telemetry sample response.
    /// </summary>
    public class ClientTelemetrySampleDetail
    {
        /// <summary>
        /// Sample record.
        /// </summary>
        public ClientTelemetrySampleRecord Sample { get; set; } = new ClientTelemetrySampleRecord();

        /// <summary>
        /// GPU samples.
        /// </summary>
        public List<ClientTelemetryGpuSampleRecord> Gpus { get; set; } = new List<ClientTelemetryGpuSampleRecord>();

        /// <summary>
        /// Raw snapshot JSON.
        /// </summary>
        public string? SnapshotJson { get; set; } = null;
    }
}

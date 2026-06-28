namespace Xeno.Core.Models.Telemetry
{
    using System.Collections.Generic;

    /// <summary>
    /// Server host telemetry detail response.
    /// </summary>
    public class ServerClientTelemetrySampleDetail
    {
        /// <summary>
        /// Host telemetry sample.
        /// </summary>
        public ServerClientTelemetrySampleRecord Sample { get; set; } = new ServerClientTelemetrySampleRecord();

        /// <summary>
        /// GPU telemetry rows.
        /// </summary>
        public List<ServerClientTelemetryGpuSampleRecord> Gpus { get; set; } = new List<ServerClientTelemetryGpuSampleRecord>();

        /// <summary>
        /// Bounded raw snapshot JSON, when persisted.
        /// </summary>
        public string? SnapshotJson { get; set; } = null;
    }
}

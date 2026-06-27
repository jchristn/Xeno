namespace Xeno.Client.Telemetry.Models
{
    /// <summary>
    /// Complete client telemetry snapshot.
    /// </summary>
    public class ClientTelemetrySnapshot
    {
        /// <summary>
        /// Snapshot metadata.
        /// </summary>
        public ClientTelemetryMetadata Metadata { get; set; } = new ClientTelemetryMetadata();

        /// <summary>
        /// System telemetry.
        /// </summary>
        public ClientSystemTelemetry System { get; set; } = new ClientSystemTelemetry();

        /// <summary>
        /// CPU telemetry.
        /// </summary>
        public ClientCpuTelemetry Cpu { get; set; } = new ClientCpuTelemetry();

        /// <summary>
        /// Memory telemetry.
        /// </summary>
        public ClientMemoryTelemetry Memory { get; set; } = new ClientMemoryTelemetry();

        /// <summary>
        /// Disk telemetry.
        /// </summary>
        public ClientDiskTelemetry Disk { get; set; } = new ClientDiskTelemetry();

        /// <summary>
        /// GPU telemetry.
        /// </summary>
        public ClientGpuTelemetry Gpu { get; set; } = new ClientGpuTelemetry();
    }
}

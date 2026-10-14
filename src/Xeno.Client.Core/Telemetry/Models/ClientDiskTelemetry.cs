namespace Xeno.Client.Telemetry.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Disk telemetry.
    /// </summary>
    public class ClientDiskTelemetry
    {
        /// <summary>
        /// Total disk bytes.
        /// </summary>
        public long TotalBytes { get; set; } = 0;

        /// <summary>
        /// Used disk bytes.
        /// </summary>
        public long UsedBytes { get; set; } = 0;

        /// <summary>
        /// Available disk bytes.
        /// </summary>
        public long AvailableBytes { get; set; } = 0;

        /// <summary>
        /// Disk utilization percentage.
        /// </summary>
        public double? UtilizationPercent { get; set; } = null;

        /// <summary>
        /// Disk read bytes per second when available.
        /// </summary>
        public double? ReadBytesPerSecond { get; set; } = null;

        /// <summary>
        /// Disk write bytes per second when available.
        /// </summary>
        public double? WriteBytesPerSecond { get; set; } = null;

        /// <summary>
        /// Disk volumes.
        /// </summary>
        public List<ClientDiskVolumeTelemetry> Volumes { get; set; } = new List<ClientDiskVolumeTelemetry>();

        /// <summary>
        /// Disk collection status.
        /// </summary>
        public ClientTelemetryCollectionStatus Status { get; set; } = new ClientTelemetryCollectionStatus();
    }
}

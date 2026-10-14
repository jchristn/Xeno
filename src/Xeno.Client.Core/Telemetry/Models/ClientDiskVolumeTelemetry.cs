namespace Xeno.Client.Telemetry.Models
{
    /// <summary>
    /// Disk volume telemetry.
    /// </summary>
    public class ClientDiskVolumeTelemetry
    {
        /// <summary>
        /// Volume name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Volume format.
        /// </summary>
        public string Format { get; set; } = string.Empty;

        /// <summary>
        /// Total bytes.
        /// </summary>
        public long TotalBytes { get; set; } = 0;

        /// <summary>
        /// Used bytes.
        /// </summary>
        public long UsedBytes { get; set; } = 0;

        /// <summary>
        /// Available bytes.
        /// </summary>
        public long AvailableBytes { get; set; } = 0;

        /// <summary>
        /// Utilization percentage.
        /// </summary>
        public double UtilizationPercent { get; set; } = 0;
    }
}

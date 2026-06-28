namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Memory telemetry.
    /// </summary>
    public class ClientMemoryTelemetry
    {
        /// <summary>
        /// Total memory bytes.
        /// </summary>
        public long? TotalBytes { get; set; } = null;

        /// <summary>
        /// Used memory bytes.
        /// </summary>
        public long? UsedBytes { get; set; } = null;

        /// <summary>
        /// Available memory bytes.
        /// </summary>
        public long? AvailableBytes { get; set; } = null;

        /// <summary>
        /// Memory utilization percentage.
        /// </summary>
        public double? UtilizationPercent { get; set; } = null;

        /// <summary>
        /// Memory collection status.
        /// </summary>
        public ClientTelemetryCollectionStatus Status { get; set; } = new ClientTelemetryCollectionStatus();
    }
}

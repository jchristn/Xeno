namespace Xeno.Core.Telemetry
{
    /// <summary>
    /// Latest host resource utilization reported by a connected client.
    /// </summary>
    public class FleetHostGauge
    {
        /// <summary>
        /// Client identifier.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// CPU utilization percent, or null if unreported.
        /// </summary>
        public double? CpuPercent { get; set; } = null;

        /// <summary>
        /// Memory utilization percent, or null if unreported.
        /// </summary>
        public double? MemoryPercent { get; set; } = null;

        /// <summary>
        /// Disk utilization percent, or null if unreported.
        /// </summary>
        public double? DiskPercent { get; set; } = null;

        /// <summary>
        /// GPU utilization percent, or null if unreported.
        /// </summary>
        public double? GpuPercent { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public FleetHostGauge()
        {
        }
    }
}

namespace Xeno.Client.Telemetry.Models
{
    /// <summary>
    /// CPU telemetry.
    /// </summary>
    public class ClientCpuTelemetry
    {
        /// <summary>
        /// CPU utilization percentage.
        /// </summary>
        public double? UtilizationPercent { get; set; } = null;

        /// <summary>
        /// Logical processor count.
        /// </summary>
        public int LogicalProcessorCount { get; set; } = 0;

        /// <summary>
        /// CPU collection status.
        /// </summary>
        public ClientTelemetryCollectionStatus Status { get; set; } = new ClientTelemetryCollectionStatus();
    }
}

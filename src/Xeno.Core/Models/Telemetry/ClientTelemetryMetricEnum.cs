namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Telemetry metric used for analytics.
    /// </summary>
    public enum ClientTelemetryMetricEnum
    {
        /// <summary>
        /// CPU utilization percentage.
        /// </summary>
        CpuUtilizationPercent,

        /// <summary>
        /// Memory utilization percentage.
        /// </summary>
        MemoryUtilizationPercent,

        /// <summary>
        /// Disk utilization percentage.
        /// </summary>
        DiskUtilizationPercent,

        /// <summary>
        /// GPU utilization percentage.
        /// </summary>
        GpuUtilizationPercent,

        /// <summary>
        /// GPU memory utilization percentage.
        /// </summary>
        GpuMemoryUtilizationPercent,

        /// <summary>
        /// GPU temperature in Celsius.
        /// </summary>
        GpuTemperatureCelsius,

        /// <summary>
        /// GPU power draw in watts.
        /// </summary>
        GpuPowerWatts
    }
}

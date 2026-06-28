namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// GPU device telemetry.
    /// </summary>
    public class ClientGpuDeviceTelemetry
    {
        /// <summary>
        /// GPU UUID.
        /// </summary>
        public string Uuid { get; set; } = string.Empty;

        /// <summary>
        /// GPU name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// GPU bus ID.
        /// </summary>
        public string? BusId { get; set; } = null;

        /// <summary>
        /// Driver version.
        /// </summary>
        public string? DriverVersion { get; set; } = null;

        /// <summary>
        /// GPU utilization percentage.
        /// </summary>
        public double? UtilizationPercent { get; set; } = null;

        /// <summary>
        /// Total GPU memory bytes.
        /// </summary>
        public long? MemoryTotalBytes { get; set; } = null;

        /// <summary>
        /// Used GPU memory bytes.
        /// </summary>
        public long? MemoryUsedBytes { get; set; } = null;

        /// <summary>
        /// Free GPU memory bytes.
        /// </summary>
        public long? MemoryFreeBytes { get; set; } = null;

        /// <summary>
        /// GPU memory utilization percentage.
        /// </summary>
        public double? MemoryUtilizationPercent { get; set; } = null;

        /// <summary>
        /// GPU temperature in Celsius.
        /// </summary>
        public double? TemperatureCelsius { get; set; } = null;

        /// <summary>
        /// GPU power draw in watts.
        /// </summary>
        public double? PowerWatts { get; set; } = null;
    }
}

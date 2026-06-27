namespace Xeno.Client.Telemetry.Models
{
    using System;

    /// <summary>
    /// Persisted telemetry sample summary record.
    /// </summary>
    public class ClientTelemetrySampleRecord
    {
        /// <summary>
        /// Telemetry sample ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Client ID.
        /// </summary>
        public string? ClientId { get; set; } = null;

        /// <summary>
        /// Machine name.
        /// </summary>
        public string MachineName { get; set; } = string.Empty;

        /// <summary>
        /// Collection timestamp.
        /// </summary>
        public DateTime CollectedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Persisted timestamp.
        /// </summary>
        public DateTime PersistedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Collection elapsed milliseconds.
        /// </summary>
        public double CollectionElapsedMs { get; set; } = 0;

        /// <summary>
        /// Operating system description.
        /// </summary>
        public string OsDescription { get; set; } = string.Empty;

        /// <summary>
        /// Architecture.
        /// </summary>
        public string Architecture { get; set; } = string.Empty;

        /// <summary>
        /// Processor count.
        /// </summary>
        public int ProcessorCount { get; set; } = 0;

        /// <summary>
        /// CPU utilization percentage.
        /// </summary>
        public double? CpuUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Memory total bytes.
        /// </summary>
        public long? MemoryTotalBytes { get; set; } = null;

        /// <summary>
        /// Memory used bytes.
        /// </summary>
        public long? MemoryUsedBytes { get; set; } = null;

        /// <summary>
        /// Memory available bytes.
        /// </summary>
        public long? MemoryAvailableBytes { get; set; } = null;

        /// <summary>
        /// Memory utilization percentage.
        /// </summary>
        public double? MemoryUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Disk total bytes.
        /// </summary>
        public long? DiskTotalBytes { get; set; } = null;

        /// <summary>
        /// Disk used bytes.
        /// </summary>
        public long? DiskUsedBytes { get; set; } = null;

        /// <summary>
        /// Disk available bytes.
        /// </summary>
        public long? DiskAvailableBytes { get; set; } = null;

        /// <summary>
        /// Disk utilization percentage.
        /// </summary>
        public double? DiskUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Disk read bytes per second.
        /// </summary>
        public double? DiskReadBytesPerSecond { get; set; } = null;

        /// <summary>
        /// Disk write bytes per second.
        /// </summary>
        public double? DiskWriteBytesPerSecond { get; set; } = null;

        /// <summary>
        /// GPU provider.
        /// </summary>
        public ClientTelemetryGpuProviderEnum GpuProvider { get; set; } = ClientTelemetryGpuProviderEnum.None;

        /// <summary>
        /// GPU device count.
        /// </summary>
        public int GpuDeviceCount { get; set; } = 0;

        /// <summary>
        /// GPU average utilization percentage.
        /// </summary>
        public double? GpuAverageUtilizationPercent { get; set; } = null;

        /// <summary>
        /// GPU average memory utilization percentage.
        /// </summary>
        public double? GpuAverageMemoryUtilizationPercent { get; set; } = null;

        /// <summary>
        /// GPU average temperature in Celsius.
        /// </summary>
        public double? GpuAverageTemperatureCelsius { get; set; } = null;

        /// <summary>
        /// GPU average power draw in watts.
        /// </summary>
        public double? GpuAveragePowerWatts { get; set; } = null;

        /// <summary>
        /// GPU total memory bytes.
        /// </summary>
        public long? GpuTotalMemoryBytes { get; set; } = null;

        /// <summary>
        /// GPU used memory bytes.
        /// </summary>
        public long? GpuUsedMemoryBytes { get; set; } = null;
    }
}

namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno server client telemetry sample record DTO.
    /// </summary>
    public class ServerClientTelemetrySampleRecord
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the tenant id.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the client id.
        /// </summary>
        public string ClientId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the client name.
        /// </summary>
        public string ClientName { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the machine name.
        /// </summary>
        public string MachineName { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the collected utc.
        /// </summary>
        public DateTime? CollectedUtc { get; set; }

        /// <summary>
        /// Gets or sets the persisted utc.
        /// </summary>
        public DateTime? PersistedUtc { get; set; }

        /// <summary>
        /// Gets or sets the cpu utilization percent.
        /// </summary>
        public double CpuUtilizationPercent { get; set; }

        /// <summary>
        /// Gets or sets the memory utilization percent.
        /// </summary>
        public double MemoryUtilizationPercent { get; set; }

        /// <summary>
        /// Gets or sets the disk utilization percent.
        /// </summary>
        public double DiskUtilizationPercent { get; set; }

        /// <summary>
        /// Gets or sets the gpu device count.
        /// </summary>
        public int GpuDeviceCount { get; set; }

        /// <summary>
        /// Gets or sets the gpu average utilization percent.
        /// </summary>
        public double GpuAverageUtilizationPercent { get; set; }
    }
}

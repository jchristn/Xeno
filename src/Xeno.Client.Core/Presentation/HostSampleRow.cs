namespace Xeno.Client.Presentation
{
    using System;
    using Xeno.Client.Services;
    using Xeno.Client.Telemetry.Models;

    /// <summary>
    /// A host telemetry sample formatted for tables.
    /// </summary>
    public sealed class HostSampleRow
    {
        /// <summary>
        /// Sample identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Collection time in local time.
        /// </summary>
        public string Collected { get; set; } = String.Empty;

        /// <summary>
        /// Collection time (UTC), for sorting.
        /// </summary>
        public DateTime CollectedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// CPU utilization.
        /// </summary>
        public string Cpu { get; set; } = String.Empty;

        /// <summary>
        /// Memory utilization.
        /// </summary>
        public string Memory { get; set; } = String.Empty;

        /// <summary>
        /// Disk utilization.
        /// </summary>
        public string Disk { get; set; } = String.Empty;

        /// <summary>
        /// GPU utilization.
        /// </summary>
        public string Gpu { get; set; } = String.Empty;

        /// <summary>
        /// GPU memory utilization.
        /// </summary>
        public string GpuMemory { get; set; } = String.Empty;

        /// <summary>
        /// GPU temperature.
        /// </summary>
        public string GpuTemperature { get; set; } = String.Empty;

        /// <summary>
        /// Build a row.
        /// </summary>
        /// <param name="sample">Sample.</param>
        /// <returns>Row.</returns>
        /// <exception cref="ArgumentNullException">Sample is null.</exception>
        public static HostSampleRow From(ClientTelemetrySampleRecord sample)
        {
            ArgumentNullException.ThrowIfNull(sample);
            return new HostSampleRow
            {
                Id = sample.Id,
                Collected = DisplayFormat.Local(sample.CollectedUtc),
                CollectedUtc = sample.CollectedUtc,
                Cpu = DisplayFormat.Percent(sample.CpuUtilizationPercent),
                Memory = DisplayFormat.Percent(sample.MemoryUtilizationPercent),
                Disk = DisplayFormat.Percent(sample.DiskUtilizationPercent),
                Gpu = sample.GpuDeviceCount == 0 ? DisplayFormat.Dash : DisplayFormat.Percent(sample.GpuAverageUtilizationPercent),
                GpuMemory = sample.GpuDeviceCount == 0 ? DisplayFormat.Dash : DisplayFormat.Percent(sample.GpuAverageMemoryUtilizationPercent),
                GpuTemperature = sample.GpuAverageTemperatureCelsius == null ? DisplayFormat.Dash : sample.GpuAverageTemperatureCelsius.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " C"
            };
        }
    }
}

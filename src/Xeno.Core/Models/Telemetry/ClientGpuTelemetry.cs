namespace Xeno.Core.Models.Telemetry
{
    using System.Collections.Generic;

    /// <summary>
    /// GPU telemetry.
    /// </summary>
    public class ClientGpuTelemetry
    {
        /// <summary>
        /// GPU provider.
        /// </summary>
        public ClientTelemetryGpuProviderEnum Provider { get; set; } = ClientTelemetryGpuProviderEnum.None;

        /// <summary>
        /// GPU device count.
        /// </summary>
        public int DeviceCount { get; set; } = 0;

        /// <summary>
        /// Average GPU utilization percentage.
        /// </summary>
        public double? AverageUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Average GPU memory utilization percentage.
        /// </summary>
        public double? AverageMemoryUtilizationPercent { get; set; } = null;

        /// <summary>
        /// Average GPU temperature in Celsius.
        /// </summary>
        public double? AverageTemperatureCelsius { get; set; } = null;

        /// <summary>
        /// Average GPU power draw in watts.
        /// </summary>
        public double? AveragePowerWatts { get; set; } = null;

        /// <summary>
        /// Total GPU memory bytes.
        /// </summary>
        public long? TotalMemoryBytes { get; set; } = null;

        /// <summary>
        /// Used GPU memory bytes.
        /// </summary>
        public long? UsedMemoryBytes { get; set; } = null;

        /// <summary>
        /// GPU devices.
        /// </summary>
        public List<ClientGpuDeviceTelemetry> Devices { get; set; } = new List<ClientGpuDeviceTelemetry>();

        /// <summary>
        /// GPU collection status.
        /// </summary>
        public ClientTelemetryCollectionStatus Status { get; set; } = new ClientTelemetryCollectionStatus();
    }
}

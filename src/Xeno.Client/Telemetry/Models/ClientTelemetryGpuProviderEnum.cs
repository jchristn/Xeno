namespace Xeno.Client.Telemetry.Models
{
    /// <summary>
    /// GPU telemetry provider.
    /// </summary>
    public enum ClientTelemetryGpuProviderEnum
    {
        /// <summary>
        /// No GPU provider was used.
        /// </summary>
        None,

        /// <summary>
        /// NVIDIA DCGM exporter provider.
        /// </summary>
        NvidiaDcgm,

        /// <summary>
        /// NVIDIA SMI provider.
        /// </summary>
        NvidiaSmi
    }
}

namespace Xeno.Client.Telemetry.Models
{
    /// <summary>
    /// Provider health status.
    /// </summary>
    public enum ClientProviderTelemetryHealthStatusEnum
    {
        /// <summary>
        /// Health is unknown.
        /// </summary>
        Unknown,

        /// <summary>
        /// Provider is healthy.
        /// </summary>
        Healthy,

        /// <summary>
        /// Provider is degraded.
        /// </summary>
        Degraded,

        /// <summary>
        /// Provider is unhealthy.
        /// </summary>
        Unhealthy
    }
}

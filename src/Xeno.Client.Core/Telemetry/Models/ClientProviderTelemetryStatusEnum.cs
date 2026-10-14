namespace Xeno.Client.Telemetry.Models
{
    /// <summary>
    /// Provider telemetry collection status.
    /// </summary>
    public enum ClientProviderTelemetryStatusEnum
    {
        /// <summary>
        /// Telemetry is disabled.
        /// </summary>
        Disabled,

        /// <summary>
        /// Telemetry is available.
        /// </summary>
        Available,

        /// <summary>
        /// Telemetry is partially available.
        /// </summary>
        Partial,

        /// <summary>
        /// Provider is unavailable.
        /// </summary>
        Unavailable,

        /// <summary>
        /// Telemetry collection failed.
        /// </summary>
        Failed
    }
}

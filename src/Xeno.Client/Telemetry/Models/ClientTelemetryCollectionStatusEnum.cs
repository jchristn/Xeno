namespace Xeno.Client.Telemetry.Models
{
    /// <summary>
    /// Telemetry collection status.
    /// </summary>
    public enum ClientTelemetryCollectionStatusEnum
    {
        /// <summary>
        /// Collection status is unknown.
        /// </summary>
        Unknown,

        /// <summary>
        /// Collection succeeded.
        /// </summary>
        Available,

        /// <summary>
        /// Collection is disabled.
        /// </summary>
        Disabled,

        /// <summary>
        /// Collection is unavailable on this platform.
        /// </summary>
        Unavailable,

        /// <summary>
        /// Collection failed.
        /// </summary>
        Error
    }
}

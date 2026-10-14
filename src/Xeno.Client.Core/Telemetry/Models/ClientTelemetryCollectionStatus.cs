namespace Xeno.Client.Telemetry.Models
{
    /// <summary>
    /// Status for one telemetry collection section.
    /// </summary>
    public class ClientTelemetryCollectionStatus
    {
        /// <summary>
        /// Status code.
        /// </summary>
        public ClientTelemetryCollectionStatusEnum Status { get; set; } = ClientTelemetryCollectionStatusEnum.Unknown;

        /// <summary>
        /// Optional status message.
        /// </summary>
        public string? Message { get; set; } = null;
    }
}

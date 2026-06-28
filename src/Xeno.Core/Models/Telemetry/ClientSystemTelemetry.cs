namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// System telemetry.
    /// </summary>
    public class ClientSystemTelemetry
    {
        /// <summary>
        /// Operating system description.
        /// </summary>
        public string OsDescription { get; set; } = string.Empty;

        /// <summary>
        /// Process architecture.
        /// </summary>
        public string Architecture { get; set; } = string.Empty;

        /// <summary>
        /// Logical processor count.
        /// </summary>
        public int ProcessorCount { get; set; } = 0;

        /// <summary>
        /// System collection status.
        /// </summary>
        public ClientTelemetryCollectionStatus Status { get; set; } = new ClientTelemetryCollectionStatus();
    }
}

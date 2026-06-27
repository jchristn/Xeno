namespace Xeno.Client.Telemetry.Models
{
    using System;

    /// <summary>
    /// Telemetry snapshot metadata.
    /// </summary>
    public class ClientTelemetryMetadata
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
        /// Client machine name.
        /// </summary>
        public string MachineName { get; set; } = Environment.MachineName;

        /// <summary>
        /// Collection timestamp.
        /// </summary>
        public DateTime CollectedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Collection elapsed milliseconds.
        /// </summary>
        public double CollectionElapsedMs { get; set; } = 0;
    }
}

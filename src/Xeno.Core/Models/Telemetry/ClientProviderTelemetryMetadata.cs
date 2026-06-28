namespace Xeno.Core.Models.Telemetry
{
    using System;

    /// <summary>
    /// Provider telemetry snapshot metadata.
    /// </summary>
    public class ClientProviderTelemetryMetadata
    {
        /// <summary>
        /// Provider telemetry sample ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Client ID.
        /// </summary>
        public string? ClientId { get; set; } = null;

        /// <summary>
        /// Machine name.
        /// </summary>
        public string MachineName { get; set; } = string.Empty;

        /// <summary>
        /// Collection timestamp.
        /// </summary>
        public DateTime CollectedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Collection elapsed milliseconds.
        /// </summary>
        public double CollectionElapsedMs { get; set; } = 0;

        /// <summary>
        /// Number of providers collected.
        /// </summary>
        public int ProviderCount { get; set; } = 0;
    }
}

namespace Xeno.Core.Models.Telemetry
{
    using System;

    /// <summary>
    /// Server telemetry collection service status response.
    /// </summary>
    public class ServerClientTelemetryCollectionStatusResponse
    {
        /// <summary>
        /// Whether telemetry collection is enabled in settings.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Whether the collection service is currently running.
        /// </summary>
        public bool Running { get; set; } = false;

        /// <summary>
        /// Collection interval in seconds after clamp.
        /// </summary>
        public int CollectionIntervalSeconds { get; set; } = 60;

        /// <summary>
        /// Telemetry request timeout in milliseconds after clamp.
        /// </summary>
        public int RequestTimeoutMs { get; set; } = 5000;

        /// <summary>
        /// Retention period in days after clamp.
        /// </summary>
        public int RetentionDays { get; set; } = 30;

        /// <summary>
        /// Last completed collection cycle timestamp.
        /// </summary>
        public DateTime? LastCycleUtc { get; set; } = null;

        /// <summary>
        /// Estimated next collection cycle timestamp.
        /// </summary>
        public DateTime? NextCycleUtc { get; set; } = null;

        /// <summary>
        /// Number of currently connected clients.
        /// </summary>
        public int ConnectedClientCount { get; set; } = 0;

        /// <summary>
        /// Number of clients eligible for telemetry collection.
        /// </summary>
        public int EligibleClientCount { get; set; } = 0;

        /// <summary>
        /// Recent collection failure count.
        /// </summary>
        public int RecentFailureCount { get; set; } = 0;
    }
}

namespace Xeno.Core.Models.Telemetry
{
    using System;

    /// <summary>
    /// Telemetry persistence status.
    /// </summary>
    public class ClientTelemetryPersistenceStatus
    {
        /// <summary>
        /// Whether telemetry is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Whether the telemetry service is running.
        /// </summary>
        public bool Running { get; set; } = false;

        /// <summary>
        /// Collection interval in milliseconds.
        /// </summary>
        public int CollectionIntervalMs { get; set; } = 15000;

        /// <summary>
        /// Retention days.
        /// </summary>
        public int RetentionDays { get; set; } = 30;

        /// <summary>
        /// Total samples.
        /// </summary>
        public int Samples { get; set; } = 0;

        /// <summary>
        /// Oldest sample timestamp.
        /// </summary>
        public DateTime? OldestSampleUtc { get; set; } = null;

        /// <summary>
        /// Newest sample timestamp.
        /// </summary>
        public DateTime? NewestSampleUtc { get; set; } = null;

        /// <summary>
        /// Last collection timestamp.
        /// </summary>
        public DateTime? LastCollectionUtc { get; set; } = null;

        /// <summary>
        /// Last prune timestamp.
        /// </summary>
        public DateTime? LastPruneUtc { get; set; } = null;

        /// <summary>
        /// Last status message.
        /// </summary>
        public string? LastMessage { get; set; } = null;
    }
}

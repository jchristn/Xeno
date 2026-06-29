namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno server client telemetry collection status response DTO.
    /// </summary>
    public class ServerClientTelemetryCollectionStatusResponse
    {
        /// <summary>
        /// Gets or sets the enabled.
        /// </summary>
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets the running.
        /// </summary>
        public bool Running { get; set; }

        /// <summary>
        /// Gets or sets the collection interval seconds.
        /// </summary>
        public int CollectionIntervalSeconds { get; set; }

        /// <summary>
        /// Gets or sets the request timeout ms.
        /// </summary>
        public int RequestTimeoutMs { get; set; }

        /// <summary>
        /// Gets or sets the retention days.
        /// </summary>
        public int RetentionDays { get; set; }

        /// <summary>
        /// Gets or sets the last cycle utc.
        /// </summary>
        public DateTime? LastCycleUtc { get; set; }

        /// <summary>
        /// Gets or sets the next cycle utc.
        /// </summary>
        public DateTime? NextCycleUtc { get; set; }

        /// <summary>
        /// Gets or sets the connected client count.
        /// </summary>
        public int ConnectedClientCount { get; set; }

        /// <summary>
        /// Gets or sets the eligible client count.
        /// </summary>
        public int EligibleClientCount { get; set; }

        /// <summary>
        /// Gets or sets the recent failure count.
        /// </summary>
        public int RecentFailureCount { get; set; }
    }
}

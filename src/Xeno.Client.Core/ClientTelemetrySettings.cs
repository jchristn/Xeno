namespace Xeno.Client
{
    using System;

    /// <summary>
    /// Xeno client telemetry settings.
    /// </summary>
    public class ClientTelemetrySettings
    {
        private int _CollectionIntervalMs = 15000;
        private int _PruneIntervalMinutes = 60;
        private int _RetentionDays = 30;

        /// <summary>
        /// Whether telemetry collection is enabled.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Collection interval in milliseconds.
        /// </summary>
        public int CollectionIntervalMs
        {
            get
            {
                return _CollectionIntervalMs;
            }
            set
            {
                _CollectionIntervalMs = Math.Max(5000, value);
            }
        }

        /// <summary>
        /// Retention period in days.
        /// </summary>
        public int RetentionDays
        {
            get
            {
                return _RetentionDays;
            }
            set
            {
                _RetentionDays = Math.Clamp(value, 1, 365);
            }
        }

        /// <summary>
        /// Retention pruning interval in minutes.
        /// </summary>
        public int PruneIntervalMinutes
        {
            get
            {
                return _PruneIntervalMinutes;
            }
            set
            {
                _PruneIntervalMinutes = Math.Clamp(value, 1, 1440);
            }
        }

        /// <summary>
        /// Whether the full snapshot JSON should be persisted.
        /// </summary>
        public bool PersistRawSnapshot { get; set; } = true;

        /// <summary>
        /// GPU telemetry settings.
        /// </summary>
        public ClientGpuTelemetrySettings Gpu { get; set; } = new ClientGpuTelemetrySettings();

        /// <summary>
        /// Provider runtime telemetry settings.
        /// </summary>
        public ClientProviderTelemetrySettings ProviderTelemetry { get; set; } = new ClientProviderTelemetrySettings();
    }
}

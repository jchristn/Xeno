namespace Xeno.Core.Settings
{
    /// <summary>
    /// Server-controlled fleet telemetry collection settings.
    /// </summary>
    public class TelemetryCollectionSettings
    {
        private int _CollectionIntervalSeconds = 60;
        private int _RequestTimeoutMs = 5000;
        private int _RetentionDays = 30;
        private int _PruneIntervalMinutes = 60;
        private int _MaxRawSnapshotBytes = 262144;
        private int _MaxSamplesPerClientResponse = 1;
        private int _MaxProviderMetricRowsPerResponse = 1000;
        private int _MaxConcurrentClientRequests = 8;

        /// <summary>
        /// Enable server-requested fleet telemetry collection. Defaults to true.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Collection interval in seconds. Defaults to 60. Minimum is 10 and maximum is 300.
        /// </summary>
        public int CollectionIntervalSeconds
        {
            get { return _CollectionIntervalSeconds; }
            set { _CollectionIntervalSeconds = Clamp(value, 10, 300); }
        }

        /// <summary>
        /// Timeout for one server-to-client telemetry request in milliseconds. Defaults to 5000. Minimum is 1000 and maximum is 60000.
        /// </summary>
        public int RequestTimeoutMs
        {
            get { return _RequestTimeoutMs; }
            set { _RequestTimeoutMs = Clamp(value, 1000, 60000); }
        }

        /// <summary>
        /// Telemetry retention in days. Defaults to 30. Minimum is 1 and maximum is 3650.
        /// </summary>
        public int RetentionDays
        {
            get { return _RetentionDays; }
            set { _RetentionDays = Clamp(value, 1, 3650); }
        }

        /// <summary>
        /// Retention prune interval in minutes. Defaults to 60. Minimum is 1 and maximum is 1440.
        /// </summary>
        public int PruneIntervalMinutes
        {
            get { return _PruneIntervalMinutes; }
            set { _PruneIntervalMinutes = Clamp(value, 1, 1440); }
        }

        /// <summary>
        /// Collect host telemetry such as CPU, memory, disk, and GPU. Defaults to true.
        /// </summary>
        public bool CollectHostTelemetry { get; set; } = true;

        /// <summary>
        /// Collect provider telemetry such as health, queues, loaded models, and metrics. Defaults to true.
        /// </summary>
        public bool CollectProviderTelemetry { get; set; } = true;

        /// <summary>
        /// Persist bounded raw telemetry snapshots with normalized rows. Defaults to true.
        /// </summary>
        public bool PersistRawSnapshots { get; set; } = true;

        /// <summary>
        /// Maximum raw snapshot JSON bytes per stored snapshot. Defaults to 262144. Minimum is 0 and maximum is 1048576.
        /// </summary>
        public int MaxRawSnapshotBytes
        {
            get { return _MaxRawSnapshotBytes; }
            set { _MaxRawSnapshotBytes = Clamp(value, 0, 1048576); }
        }

        /// <summary>
        /// Maximum samples a client may return for one request. Defaults to 1. Minimum is 1 and maximum is 250.
        /// </summary>
        public int MaxSamplesPerClientResponse
        {
            get { return _MaxSamplesPerClientResponse; }
            set { _MaxSamplesPerClientResponse = Clamp(value, 1, 250); }
        }

        /// <summary>
        /// Maximum provider metric rows accepted for one response. Defaults to 1000. Minimum is 0 and maximum is 10000.
        /// </summary>
        public int MaxProviderMetricRowsPerResponse
        {
            get { return _MaxProviderMetricRowsPerResponse; }
            set { _MaxProviderMetricRowsPerResponse = Clamp(value, 0, 10000); }
        }

        /// <summary>
        /// Maximum concurrent clients requested by one collection cycle. Defaults to 8. Minimum is 1 and maximum is 64.
        /// </summary>
        public int MaxConcurrentClientRequests
        {
            get { return _MaxConcurrentClientRequests; }
            set { _MaxConcurrentClientRequests = Clamp(value, 1, 64); }
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum) return minimum;
            if (value > maximum) return maximum;
            return value;
        }
    }
}

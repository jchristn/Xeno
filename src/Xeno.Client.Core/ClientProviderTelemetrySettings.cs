namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provider telemetry settings.
    /// </summary>
    public class ClientProviderTelemetrySettings
    {
        private int _CollectionIntervalMs = 15000;
        private int _ConcurrentProviderLimit = 4;
        private int _RawMetricsMaxBytes = 262144;
        private int _RequestTimeoutMs = 5000;

        /// <summary>
        /// Whether provider telemetry collection is enabled.
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
        /// Upstream telemetry request timeout in milliseconds.
        /// </summary>
        public int RequestTimeoutMs
        {
            get
            {
                return _RequestTimeoutMs;
            }
            set
            {
                _RequestTimeoutMs = Math.Clamp(value, 500, 300000);
            }
        }

        /// <summary>
        /// Maximum number of provider endpoints collected concurrently.
        /// </summary>
        public int ConcurrentProviderLimit
        {
            get
            {
                return _ConcurrentProviderLimit;
            }
            set
            {
                _ConcurrentProviderLimit = Math.Clamp(value, 1, 16);
            }
        }

        /// <summary>
        /// Whether likely metrics endpoints should be discovered automatically.
        /// </summary>
        public bool DiscoverMetricsEndpoints { get; set; } = true;

        /// <summary>
        /// Whether raw provider metrics should be persisted.
        /// </summary>
        public bool PersistRawMetrics { get; set; } = false;

        /// <summary>
        /// Maximum raw metrics bytes to persist.
        /// </summary>
        public int RawMetricsMaxBytes
        {
            get
            {
                return _RawMetricsMaxBytes;
            }
            set
            {
                _RawMetricsMaxBytes = Math.Clamp(value, 0, 1048576);
            }
        }

        /// <summary>
        /// Whether provider model details should be collected.
        /// </summary>
        public bool CollectModelDetails { get; set; } = true;

        /// <summary>
        /// Whether provider process correlation should be collected when possible.
        /// </summary>
        public bool CollectProcessCorrelation { get; set; } = true;

        /// <summary>
        /// Whether cloud provider response headers should be treated as telemetry.
        /// </summary>
        public bool CollectCloudHeaderTelemetry { get; set; } = true;

        /// <summary>
        /// Endpoint-specific overrides.
        /// </summary>
        public List<ClientProviderTelemetryEndpointOverride> EndpointOverrides { get; set; } = new List<ClientProviderTelemetryEndpointOverride>();
    }
}

namespace Xeno.Core.Settings
{
    using System;

    /// <summary>
    /// Metrics telemetry settings.
    /// </summary>
    public class TelemetryMetricsSettings
    {
        /// <summary>
        /// Enable metrics export.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Metric export interval in milliseconds. Default value is 15000.
        /// Minimum value is 1000. Maximum value is 300000.
        /// </summary>
        public int ExportIntervalMs
        {
            get => _ExportIntervalMs;
            set => _ExportIntervalMs = value < 1000 ? 1000 : (value > 300000 ? 300000 : value);
        }

        /// <summary>
        /// Include the tenant identifier as a metric label. Default value is false to
        /// bound metric cardinality. When false, tenant remains available as a trace
        /// span tag and a log field. When true, relay and request metrics carry a
        /// tenant label; cardinality scales with tenant count.
        /// </summary>
        public bool IncludeTenantLabel { get; set; } = false;

        /// <summary>
        /// Export server-collected fleet host and provider telemetry as gauges.
        /// Default value is true.
        /// </summary>
        public bool ExportFleetTelemetry { get; set; } = true;

        private int _ExportIntervalMs = 15000;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TelemetryMetricsSettings()
        {
        }
    }
}

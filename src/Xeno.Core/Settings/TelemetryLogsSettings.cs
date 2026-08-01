namespace Xeno.Core.Settings
{
    using System;

    /// <summary>
    /// Log telemetry settings. Log shipping is performed by the OpenTelemetry
    /// Collector's filelog receiver tailing the server log directory; this flag
    /// documents intent and drives observability status surfaces.
    /// </summary>
    public class TelemetryLogsSettings
    {
        /// <summary>
        /// Enable log shipping to the telemetry pipeline.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TelemetryLogsSettings()
        {
        }
    }
}

namespace Xeno.Core.Settings
{
    using System;

    /// <summary>
    /// OpenTelemetry (metrics, traces, logs) settings.
    /// </summary>
    public class TelemetrySettings
    {
        /// <summary>
        /// Master enable for telemetry. When false, no telemetry provider is created.
        /// </summary>
        public bool Enable { get; set; } = true;

        /// <summary>
        /// Service name reported as the OpenTelemetry resource service.name.
        /// Default value is "xeno-server".
        /// </summary>
        public string ServiceName { get; set; } = "xeno-server";

        /// <summary>
        /// Metrics settings.
        /// </summary>
        public TelemetryMetricsSettings Metrics
        {
            get => _Metrics;
            set => _Metrics = value ?? new TelemetryMetricsSettings();
        }

        /// <summary>
        /// Trace settings.
        /// </summary>
        public TelemetryTracesSettings Traces
        {
            get => _Traces;
            set => _Traces = value ?? new TelemetryTracesSettings();
        }

        /// <summary>
        /// Log settings.
        /// </summary>
        public TelemetryLogsSettings Logs
        {
            get => _Logs;
            set => _Logs = value ?? new TelemetryLogsSettings();
        }

        /// <summary>
        /// OTLP exporter settings.
        /// </summary>
        public OtlpExporterSettings Otlp
        {
            get => _Otlp;
            set => _Otlp = value ?? new OtlpExporterSettings();
        }

        private TelemetryMetricsSettings _Metrics = new TelemetryMetricsSettings();
        private TelemetryTracesSettings _Traces = new TelemetryTracesSettings();
        private TelemetryLogsSettings _Logs = new TelemetryLogsSettings();
        private OtlpExporterSettings _Otlp = new OtlpExporterSettings();

        /// <summary>
        /// Instantiate.
        /// </summary>
        public TelemetrySettings()
        {
        }
    }
}

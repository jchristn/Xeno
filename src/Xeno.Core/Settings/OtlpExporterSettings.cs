namespace Xeno.Core.Settings
{
    using System;

    /// <summary>
    /// OTLP exporter settings for shipping telemetry to an OpenTelemetry Collector.
    /// </summary>
    public class OtlpExporterSettings
    {
        /// <summary>
        /// Collector OTLP endpoint. Default value is "http://localhost:4317".
        /// </summary>
        public string Endpoint { get; set; } = "http://localhost:4317";

        /// <summary>
        /// OTLP protocol. Supported values are "grpc" and "httpprotobuf".
        /// Default value is "grpc". Invalid values throw on assignment.
        /// </summary>
        public string Protocol
        {
            get => _Protocol;
            set
            {
                if (String.IsNullOrWhiteSpace(value)) throw new ArgumentNullException(nameof(Protocol));
                string normalized = value.Trim().ToLowerInvariant();
                if (normalized != "grpc" && normalized != "httpprotobuf") throw new ArgumentException("Protocol must be 'grpc' or 'httpprotobuf'.", nameof(Protocol));
                _Protocol = normalized;
            }
        }

        /// <summary>
        /// Export request timeout in milliseconds. Default value is 10000.
        /// Minimum value is 1000. Maximum value is 120000.
        /// </summary>
        public int TimeoutMs
        {
            get => _TimeoutMs;
            set => _TimeoutMs = value < 1000 ? 1000 : (value > 120000 ? 120000 : value);
        }

        /// <summary>
        /// Optional OTLP headers (for example, authentication) in the OTLP header format.
        /// Sensitive; redact when logging or serializing for display.
        /// </summary>
        public string? Headers { get; set; } = null;

        private string _Protocol = "grpc";
        private int _TimeoutMs = 10000;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public OtlpExporterSettings()
        {
        }
    }
}

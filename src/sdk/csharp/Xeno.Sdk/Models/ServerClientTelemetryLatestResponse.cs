namespace Xeno.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Xeno server client telemetry latest response DTO.
    /// </summary>
    public class ServerClientTelemetryLatestResponse
    {
        /// <summary>
        /// Gets or sets the host telemetry.
        /// </summary>
        public List<ServerClientTelemetrySampleRecord> HostTelemetry { get; set; } = new List<ServerClientTelemetrySampleRecord>();

        /// <summary>
        /// Gets or sets the provider telemetry.
        /// </summary>
        public List<ServerProviderTelemetrySampleRecord> ProviderTelemetry { get; set; } = new List<ServerProviderTelemetrySampleRecord>();
    }
}

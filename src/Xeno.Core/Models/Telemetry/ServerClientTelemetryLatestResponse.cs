namespace Xeno.Core.Models.Telemetry
{
    using System.Collections.Generic;

    /// <summary>
    /// Latest server-collected telemetry response.
    /// </summary>
    public class ServerClientTelemetryLatestResponse
    {
        /// <summary>
        /// Latest host telemetry by client.
        /// </summary>
        public List<ServerClientTelemetrySampleRecord> HostTelemetry { get; set; } = new List<ServerClientTelemetrySampleRecord>();

        /// <summary>
        /// Latest provider telemetry by client and provider.
        /// </summary>
        public List<ServerProviderTelemetrySampleRecord> ProviderTelemetry { get; set; } = new List<ServerProviderTelemetrySampleRecord>();
    }
}

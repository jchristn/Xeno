namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Manual server telemetry collection request.
    /// </summary>
    public class ServerClientTelemetryCollectRequest
    {
        /// <summary>
        /// Optional client identifier. Omit to collect from all connected clients.
        /// </summary>
        public string? ClientId { get; set; } = null;

        /// <summary>
        /// Whether host telemetry should be collected.
        /// </summary>
        public bool CollectHostTelemetry { get; set; } = true;

        /// <summary>
        /// Whether provider telemetry should be collected.
        /// </summary>
        public bool CollectProviderTelemetry { get; set; } = true;

        /// <summary>
        /// Whether the API should wait for collection completion when supported.
        /// </summary>
        public bool Wait { get; set; } = false;
    }
}

namespace Xeno.Core.Models.Telemetry
{
    using System;

    /// <summary>
    /// Server-to-client telemetry collection request frame.
    /// </summary>
    public class ServerTelemetryCollectionRequestMessage
    {
        /// <summary>
        /// WebSocket frame type.
        /// </summary>
        public string Type { get; set; } = "server.telemetry.request";

        /// <summary>
        /// Telemetry request identifier.
        /// </summary>
        public string RequestId { get; set; } = string.Empty;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Client identifier.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// UTC timestamp when the server sent the request.
        /// </summary>
        public DateTime SentUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether host telemetry is requested.
        /// </summary>
        public bool CollectHostTelemetry { get; set; } = true;

        /// <summary>
        /// Whether provider telemetry is requested.
        /// </summary>
        public bool CollectProviderTelemetry { get; set; } = true;

        /// <summary>
        /// Whether the client may include raw snapshots.
        /// </summary>
        public bool IncludeRawSnapshot { get; set; } = true;

        /// <summary>
        /// Maximum raw snapshot bytes accepted by the server.
        /// </summary>
        public int MaxRawSnapshotBytes { get; set; } = 262144;

        /// <summary>
        /// Maximum provider metric rows accepted by the server.
        /// </summary>
        public int MaxProviderMetricRows { get; set; } = 1000;

        /// <summary>
        /// Optional lower UTC bound for future bounded history requests.
        /// </summary>
        public DateTime? HistoryFromUtc { get; set; } = null;

        /// <summary>
        /// Optional upper UTC bound for future bounded history requests.
        /// </summary>
        public DateTime? HistoryToUtc { get; set; } = null;

        /// <summary>
        /// Maximum samples accepted for future bounded history requests.
        /// </summary>
        public int MaxSamples { get; set; } = 1;
    }
}

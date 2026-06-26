namespace Xeno.Core.Models
{
    using System.Collections.Generic;
    using Xeno.Core.Enums;

    /// <summary>
    /// Provider and relay telemetry captured for a request history entry.
    /// </summary>
    public class RequestHistoryTelemetry
    {
        /// <summary>
        /// End-to-end elapsed time measured by the Xeno server.
        /// </summary>
        public long? TotalTimeMs { get; set; } = null;

        /// <summary>
        /// Round-trip time between Xeno server and selected Xeno client.
        /// </summary>
        public long? ClientRoundTripMs { get; set; } = null;

        /// <summary>
        /// Total upstream provider time measured by the Xeno client.
        /// </summary>
        public long? UpstreamTotalMs { get; set; } = null;

        /// <summary>
        /// Time from upstream request dispatch until response headers.
        /// </summary>
        public long? RequestToHeadersMs { get; set; } = null;

        /// <summary>
        /// Time from upstream request dispatch until the first streamed byte or token.
        /// </summary>
        public long? TimeToFirstTokenMs { get; set; } = null;

        /// <summary>
        /// Time between first streamed byte or token and the end of the stream.
        /// </summary>
        public long? TotalStreamingTimeMs { get; set; } = null;

        /// <summary>
        /// Provider-reported prompt token count, when available.
        /// </summary>
        public int? PromptTokens { get; set; } = null;

        /// <summary>
        /// Provider-reported completion token count, when available.
        /// </summary>
        public int? CompletionTokens { get; set; } = null;

        /// <summary>
        /// Provider-reported total token count, or a best-effort streamed-token estimate.
        /// </summary>
        public int? TotalTokens { get; set; } = null;

        /// <summary>
        /// Number of streamed chunks relayed by the Xeno client.
        /// </summary>
        public int? StreamChunkCount { get; set; } = null;

        /// <summary>
        /// Number of server-sent events or JSON lines observed in a streaming response.
        /// </summary>
        public int? StreamEventCount { get; set; } = null;

        /// <summary>
        /// Request body size sent upstream, in bytes.
        /// </summary>
        public long? RequestBytes { get; set; } = null;

        /// <summary>
        /// Response body size received upstream, in bytes.
        /// </summary>
        public long? ResponseBytes { get; set; } = null;

        /// <summary>
        /// Transfer mode observed by the client.
        /// </summary>
        public RequestTransferModeEnum TransferMode { get; set; } = RequestTransferModeEnum.Unknown;

        /// <summary>
        /// Upstream provider request identifier, when exposed safely.
        /// </summary>
        public string? ProviderRequestId { get; set; } = null;

        /// <summary>
        /// Additional provider-neutral metadata.
        /// </summary>
        public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
    }
}

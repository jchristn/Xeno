namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Source-client request asking the server to relay one benchmark sample to the target client.
    /// </summary>
    public class ClientPerformanceTestRelayRequestMessage : WebSocketMessageEnvelope
    {
        /// <summary>
        /// Parent performance test request ID.
        /// </summary>
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Per-sample request ID.
        /// </summary>
        public string SampleId { get; set; } = String.Empty;

        /// <summary>
        /// One-based sample index.
        /// </summary>
        public int Index { get; set; } = 0;

        /// <summary>
        /// Source client ID.
        /// </summary>
        public string SourceClientId { get; set; } = String.Empty;

        /// <summary>
        /// Target client ID.
        /// </summary>
        public string TargetClientId { get; set; } = String.Empty;

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Target API type.
        /// </summary>
        public string? TargetApiType { get; set; } = null;

        /// <summary>
        /// Target provider source name.
        /// </summary>
        public string? TargetSource { get; set; } = null;

        /// <summary>
        /// Model name.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Request type.
        /// </summary>
        public string RequestType { get; set; } = "chatCompletions";

        /// <summary>
        /// HTTP method.
        /// </summary>
        public string Method { get; set; } = "POST";

        /// <summary>
        /// Relative provider path.
        /// </summary>
        public string RelativePath { get; set; } = "/v1/chat/completions";

        /// <summary>
        /// Whether the sample should stream from the target client.
        /// </summary>
        public bool Stream { get; set; } = true;

        /// <summary>
        /// Forwarded headers.
        /// </summary>
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// JSON request body.
        /// </summary>
        public string Body { get; set; } = "{}";
    }
}

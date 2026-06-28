namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Server-to-client routed inference request.
    /// </summary>
    public class ServerRequestMessage : WebSocketMessageEnvelope
    {
        private Dictionary<string, string> _Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Xeno request ID.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Tenant ID.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Tenant ID of the authenticated requestor.
        /// </summary>
        [JsonPropertyName("requestorTenantId")]
        public string? RequestorTenantId { get; set; } = null;

        /// <summary>
        /// Resolved principal type of the authenticated requestor.
        /// </summary>
        [JsonPropertyName("requestorPrincipalType")]
        public string? RequestorPrincipalType { get; set; } = null;

        /// <summary>
        /// Resolved principal ID of the authenticated requestor.
        /// </summary>
        [JsonPropertyName("requestorPrincipalId")]
        public string? RequestorPrincipalId { get; set; } = null;

        /// <summary>
        /// Selected client ID.
        /// </summary>
        [JsonPropertyName("clientId")]
        public string ClientId { get; set; } = String.Empty;

        /// <summary>
        /// Selected client display name.
        /// </summary>
        [JsonPropertyName("clientName")]
        public string? ClientName { get; set; } = null;

        /// <summary>
        /// Upstream API type name.
        /// </summary>
        [JsonPropertyName("apiType")]
        public string? ApiType { get; set; } = null;

        /// <summary>
        /// Backward-compatible upstream API type alias.
        /// </summary>
        [JsonPropertyName("provider")]
        public string? LegacyProvider
        {
            get
            {
                return null;
            }
            set
            {
                if (!String.IsNullOrWhiteSpace(value)) ApiType = value;
            }
        }

        /// <summary>
        /// Client-local provider source name.
        /// </summary>
        [JsonPropertyName("source")]
        public string? Source { get; set; } = null;

        /// <summary>
        /// Backward-compatible runner name.
        /// </summary>
        [JsonPropertyName("runnerName")]
        public string? RunnerName { get; set; } = null;

        /// <summary>
        /// HTTP method to send upstream.
        /// </summary>
        [JsonPropertyName("method")]
        public string Method { get; set; } = "POST";

        /// <summary>
        /// OpenAI-compatible relative path.
        /// </summary>
        [JsonPropertyName("relativePath")]
        public string RelativePath { get; set; } = "/v1/chat/completions";

        /// <summary>
        /// Backward-compatible relative path.
        /// </summary>
        [JsonPropertyName("path")]
        public string? Path { get; set; } = null;

        /// <summary>
        /// Effective model name.
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; } = null;

        /// <summary>
        /// Whether this is an embedding request.
        /// </summary>
        [JsonPropertyName("embeddings")]
        public bool Embeddings { get; set; } = false;

        /// <summary>
        /// Whether the caller requested streaming.
        /// </summary>
        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = false;

        /// <summary>
        /// Original caller IP address.
        /// </summary>
        [JsonPropertyName("requestorIp")]
        public string? RequestorIp { get; set; } = null;

        /// <summary>
        /// Forwarded headers to apply to the upstream request.
        /// </summary>
        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers
        {
            get
            {
                return _Headers;
            }
            set
            {
                _Headers = NormalizeHeaders(value);
            }
        }

        /// <summary>
        /// JSON request body to forward upstream.
        /// </summary>
        [JsonPropertyName("body")]
        public string Body { get; set; } = "{}";

        private static Dictionary<string, string> NormalizeHeaders(Dictionary<string, string>? headers)
        {
            Dictionary<string, string> normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (headers == null) return normalized;
            foreach (KeyValuePair<string, string> header in headers)
            {
                if (String.IsNullOrWhiteSpace(header.Key)) continue;
                normalized[header.Key] = header.Value;
            }

            return normalized;
        }
    }
}

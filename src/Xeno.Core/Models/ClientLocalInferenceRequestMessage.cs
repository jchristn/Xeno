namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Client-to-server local inference proxy request sent through the Xeno websocket tunnel.
    /// </summary>
    public class ClientLocalInferenceRequestMessage : WebSocketMessageEnvelope
    {
        private Dictionary<string, string> _Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Request ID used for websocket response correlation.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Tenant ID of the local client identity.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Client ID sending the local inference request.
        /// </summary>
        [JsonPropertyName("clientId")]
        public string ClientId { get; set; } = String.Empty;

        /// <summary>
        /// HTTP method requested by the localhost caller.
        /// </summary>
        [JsonPropertyName("method")]
        public string Method { get; set; } = "POST";

        /// <summary>
        /// Provider-compatible request path requested by the localhost caller.
        /// </summary>
        [JsonPropertyName("relativePath")]
        public string RelativePath { get; set; } = "/";

        /// <summary>
        /// Model name requested by the localhost caller.
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; } = null;

        /// <summary>
        /// Whether this is an embedding request.
        /// </summary>
        [JsonPropertyName("embeddings")]
        public bool Embeddings { get; set; } = false;

        /// <summary>
        /// Whether the localhost caller requested streaming.
        /// </summary>
        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = false;

        /// <summary>
        /// Provider family detected by the local listener.
        /// </summary>
        [JsonPropertyName("providerFamily")]
        public string ProviderFamily { get; set; } = String.Empty;

        /// <summary>
        /// Original localhost caller IP address.
        /// </summary>
        [JsonPropertyName("requestorIp")]
        public string? RequestorIp { get; set; } = null;

        /// <summary>
        /// HTTP headers forwarded from the localhost caller.
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
        /// JSON request body forwarded from the localhost caller.
        /// </summary>
        [JsonPropertyName("body")]
        public string Body { get; set; } = "{}";

        /// <summary>
        /// When true, the server must not route this request back to the client that sent it. The client sets it when a
        /// local provider failed and the model's route rule allows falling back to the network. Default is false.
        /// Servers older than 0.2.0 ignore the field.
        /// </summary>
        public bool ExcludeRequestingClient { get; set; } = false;

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

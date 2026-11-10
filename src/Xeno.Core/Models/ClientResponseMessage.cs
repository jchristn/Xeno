namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Buffered response, stream-start response, or stream-complete response returned by a Xeno client.
    /// </summary>
    public class ClientResponseMessage : WebSocketMessageEnvelope
    {
        /// <summary>
        /// Request ID the response belongs to.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Client ID sending the response.
        /// </summary>
        [JsonPropertyName("clientId")]
        public string? ClientId { get; set; } = null;

        /// <summary>
        /// Tenant ID.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Whether the client completed the upstream exchange successfully.
        /// </summary>
        [JsonPropertyName("success")]
        public bool Success { get; set; } = false;

        /// <summary>
        /// HTTP status code received upstream.
        /// </summary>
        [JsonPropertyName("statusCode")]
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Upstream content type.
        /// </summary>
        [JsonPropertyName("contentType")]
        public string? ContentType { get; set; } = null;

        /// <summary>
        /// Upstream response headers.
        /// </summary>
        [JsonPropertyName("headers")]
        public Dictionary<string, string> Headers { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Client and upstream telemetry.
        /// </summary>
        [JsonPropertyName("telemetry")]
        public RequestHistoryTelemetry? Telemetry { get; set; } = null;

        /// <summary>
        /// Buffered response body or captured streamed response body.
        /// </summary>
        [JsonPropertyName("body")]
        public string? Body { get; set; } = null;

        /// <summary>
        /// True when the client turned the request away because the provider was at its concurrency limit, before doing any work.
        /// The server may route the request to another client once. Default is false; clients before 0.2.0 never set it.
        /// </summary>
        public bool Busy { get; set; } = false;

        /// <summary>
        /// Error message when the request failed.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; } = null;
    }
}

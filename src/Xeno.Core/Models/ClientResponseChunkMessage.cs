namespace Xeno.Core.Models
{
    using System;
    using System.Text;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Stream chunk returned by a Xeno client.
    /// </summary>
    public class ClientResponseChunkMessage : WebSocketMessageEnvelope
    {
        /// <summary>
        /// Request ID the chunk belongs to.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Client ID sending the chunk.
        /// </summary>
        [JsonPropertyName("clientId")]
        public string? ClientId { get; set; } = null;

        /// <summary>
        /// Tenant ID.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Monotonic sequence number for the request.
        /// </summary>
        [JsonPropertyName("sequence")]
        public int Sequence { get; set; } = 0;

        /// <summary>
        /// Chunk encoding.
        /// </summary>
        [JsonPropertyName("encoding")]
        public string? Encoding { get; set; } = "base64";

        /// <summary>
        /// Chunk byte count before WebSocket serialization.
        /// </summary>
        [JsonPropertyName("byteCount")]
        public int ByteCount { get; set; } = 0;

        /// <summary>
        /// Base64-encoded chunk payload.
        /// </summary>
        [JsonPropertyName("contentBase64")]
        public string? ContentBase64 { get; set; } = null;

        /// <summary>
        /// Plain text chunk payload for backward compatibility.
        /// </summary>
        [JsonPropertyName("content")]
        public string? Content { get; set; } = null;

        /// <summary>
        /// Client send timestamp.
        /// </summary>
        [JsonPropertyName("sentUtc")]
        public DateTime? SentUtc { get; set; } = null;

        /// <summary>
        /// Decode the chunk payload into bytes.
        /// </summary>
        /// <returns>Decoded bytes.</returns>
        public byte[] Decode()
        {
            if (!String.IsNullOrWhiteSpace(ContentBase64))
            {
                try
                {
                    return Convert.FromBase64String(ContentBase64);
                }
                catch (FormatException)
                {
                    return Array.Empty<byte>();
                }
            }

            return String.IsNullOrEmpty(Content) ? Array.Empty<byte>() : System.Text.Encoding.UTF8.GetBytes(Content);
        }
    }
}

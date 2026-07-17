namespace Xeno.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Server-to-client local inference proxy streaming chunk.
    /// </summary>
    public class ServerLocalInferenceChunkMessage : WebSocketMessageEnvelope
    {
        /// <summary>
        /// Request ID the chunk belongs to.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Monotonic sequence number for the stream.
        /// </summary>
        [JsonPropertyName("sequence")]
        public int Sequence { get; set; } = 0;

        /// <summary>
        /// Chunk encoding.
        /// </summary>
        [JsonPropertyName("encoding")]
        public string? Encoding { get; set; } = "base64";

        /// <summary>
        /// Chunk byte count before websocket serialization.
        /// </summary>
        [JsonPropertyName("byteCount")]
        public int ByteCount { get; set; } = 0;

        /// <summary>
        /// Base64-encoded chunk payload.
        /// </summary>
        [JsonPropertyName("contentBase64")]
        public string? ContentBase64 { get; set; } = null;

        /// <summary>
        /// Plain text chunk payload for compatibility.
        /// </summary>
        [JsonPropertyName("content")]
        public string? Content { get; set; } = null;

        /// <summary>
        /// Server send timestamp.
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

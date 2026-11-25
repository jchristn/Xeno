namespace Xeno.Client.Services
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// One line of Ollama's streamed /api/pull response.
    /// </summary>
    public sealed class OllamaPullLine
    {
        /// <summary>
        /// Status text, for example "pulling manifest" or "success".
        /// </summary>
        [JsonPropertyName("status")]
        public string? Status { get; set; } = null;

        /// <summary>
        /// Layer digest the line reports on.
        /// </summary>
        [JsonPropertyName("digest")]
        public string? Digest { get; set; } = null;

        /// <summary>
        /// Layer size in bytes.
        /// </summary>
        [JsonPropertyName("total")]
        public long? Total { get; set; } = null;

        /// <summary>
        /// Layer bytes downloaded.
        /// </summary>
        [JsonPropertyName("completed")]
        public long? Completed { get; set; } = null;

        /// <summary>
        /// Error Ollama reported inside a 200 stream.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; } = null;
    }
}

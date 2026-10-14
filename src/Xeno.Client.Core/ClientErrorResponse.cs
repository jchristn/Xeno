namespace Xeno.Client
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Typed server error response used for friendly CLI output.
    /// </summary>
    internal sealed class ClientErrorResponse
    {
        /// <summary>
        /// Message value.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; } = null;

        /// <summary>
        /// Error value.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; } = null;

        /// <summary>
        /// Title value.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; } = null;

        /// <summary>
        /// Detail value.
        /// </summary>
        [JsonPropertyName("detail")]
        public string? Detail { get; set; } = null;
    }
}

namespace Xeno.Sdk
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Error response returned by the Xeno API.
    /// </summary>
    public sealed class XenoErrorResponse
    {
        /// <summary>
        /// Human-readable message.
        /// </summary>
        [JsonPropertyName("message")]
        public string? Message { get; set; } = null;

        /// <summary>
        /// Error code or message.
        /// </summary>
        [JsonPropertyName("error")]
        public string? Error { get; set; } = null;
    }
}

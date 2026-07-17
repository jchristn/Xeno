namespace Xeno.Client.LocalInference.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI-compatible error detail.
    /// </summary>
    public class OpenAiErrorDetail
    {
        /// <summary>
        /// Error message.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; } = "Request failed.";

        /// <summary>
        /// Error type.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; } = "server_error";

        /// <summary>
        /// Request parameter related to the error.
        /// </summary>
        [JsonPropertyName("param")]
        public string? Param { get; set; } = null;

        /// <summary>
        /// Error code.
        /// </summary>
        [JsonPropertyName("code")]
        public string? Code { get; set; } = null;
    }
}

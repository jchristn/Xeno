namespace Xeno.Client.LocalInference.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI-compatible error response.
    /// </summary>
    public class OpenAiErrorResponse
    {
        /// <summary>
        /// Error detail.
        /// </summary>
        [JsonPropertyName("error")]
        public OpenAiErrorDetail Error { get; set; } = new OpenAiErrorDetail();
    }
}

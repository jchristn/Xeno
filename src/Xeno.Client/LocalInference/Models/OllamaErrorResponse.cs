namespace Xeno.Client.LocalInference.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama-compatible error response.
    /// </summary>
    public class OllamaErrorResponse
    {
        /// <summary>
        /// Error message.
        /// </summary>
        [JsonPropertyName("error")]
        public string Error { get; set; } = "request failed";
    }
}

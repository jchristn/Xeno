namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request body for pulling a model on a client-managed Ollama provider.
    /// </summary>
    public class ClientModelPullRequest
    {
        /// <summary>
        /// Target client ID.
        /// </summary>
        [JsonPropertyName("clientId")]
        public string? ClientId { get; set; } = null;

        /// <summary>
        /// Model name to pull.
        /// </summary>
        [JsonPropertyName("model")]
        public string? Model { get; set; } = null;

        /// <summary>
        /// Client-local provider source name.
        /// </summary>
        [JsonPropertyName("source")]
        public string? Source { get; set; } = null;

        /// <summary>
        /// Whether Ollama may download over insecure connections.
        /// </summary>
        [JsonPropertyName("insecure")]
        public bool Insecure { get; set; } = false;

        /// <summary>
        /// Whether to stream progress events.
        /// </summary>
        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = false;
    }
}

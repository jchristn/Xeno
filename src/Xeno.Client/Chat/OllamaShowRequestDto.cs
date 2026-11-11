namespace Xeno.Client.Chat
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama /api/show request.
    /// </summary>
    public sealed class OllamaShowRequestDto
    {
        /// <summary>Model.</summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = String.Empty;
    }
}

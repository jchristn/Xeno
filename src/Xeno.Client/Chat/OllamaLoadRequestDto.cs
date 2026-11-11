namespace Xeno.Client.Chat
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama /api/generate request with no prompt, which loads a model into memory.
    /// </summary>
    public sealed class OllamaLoadRequestDto
    {
        /// <summary>Model.</summary>
        [JsonPropertyName("model")]
        public string Model { get; set; } = String.Empty;

        /// <summary>How long Ollama keeps the model loaded.</summary>
        [JsonPropertyName("keep_alive")]
        public string KeepAlive { get; set; } = "30m";

        /// <summary>Always false: the load returns one JSON object.</summary>
        [JsonPropertyName("stream")]
        public bool Stream { get; set; } = false;
    }
}

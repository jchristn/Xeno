namespace Xeno.Client.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama-compatible running model list returned by GET /api/ps.
    /// </summary>
    public class LocalOllamaPsResponse
    {
        /// <summary>
        /// Loaded models. Never null.
        /// </summary>
        [JsonPropertyName("models")]
        public List<LocalOllamaRunningModel> Models { get; set; } = new List<LocalOllamaRunningModel>();
    }
}

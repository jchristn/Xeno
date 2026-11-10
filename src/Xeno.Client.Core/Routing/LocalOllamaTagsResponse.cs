namespace Xeno.Client.Routing
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ollama-compatible model list returned by GET /api/tags.
    /// </summary>
    public class LocalOllamaTagsResponse
    {
        /// <summary>
        /// Models. Never null.
        /// </summary>
        [JsonPropertyName("models")]
        public List<LocalOllamaModel> Models { get; set; } = new List<LocalOllamaModel>();
    }
}

namespace Xeno.Client.Telemetry.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Ollama tags response.
    /// </summary>
    public class OllamaTagsResponse
    {
        /// <summary>
        /// Models.
        /// </summary>
        public List<OllamaTagModelResponse> Models { get; set; } = new List<OllamaTagModelResponse>();
    }
}

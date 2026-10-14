namespace Xeno.Client.Telemetry.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Ollama loaded model response.
    /// </summary>
    public class OllamaPsResponse
    {
        /// <summary>
        /// Loaded models.
        /// </summary>
        public List<OllamaLoadedModelResponse> Models { get; set; } = new List<OllamaLoadedModelResponse>();
    }
}

namespace Xeno.Client.Telemetry.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// OpenAI-compatible models response.
    /// </summary>
    public class OpenAiModelsResponse
    {
        /// <summary>
        /// Model data.
        /// </summary>
        public List<OpenAiModelResponse> Data { get; set; } = new List<OpenAiModelResponse>();
    }
}

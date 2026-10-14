namespace Xeno.Client.Telemetry.Models
{
    /// <summary>
    /// Ollama model tag response.
    /// </summary>
    public class OllamaTagModelResponse
    {
        /// <summary>
        /// Model name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Model size.
        /// </summary>
        public long Size { get; set; } = 0;

        /// <summary>
        /// Model details.
        /// </summary>
        public OllamaModelDetailsResponse? Details { get; set; } = null;
    }
}

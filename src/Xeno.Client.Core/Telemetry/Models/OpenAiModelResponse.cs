namespace Xeno.Client.Telemetry.Models
{
    /// <summary>
    /// OpenAI-compatible model response.
    /// </summary>
    public class OpenAiModelResponse
    {
        /// <summary>
        /// Model ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Model object type.
        /// </summary>
        public string Object { get; set; } = string.Empty;

        /// <summary>
        /// Owner.
        /// </summary>
        public string? OwnedBy { get; set; } = null;
    }
}

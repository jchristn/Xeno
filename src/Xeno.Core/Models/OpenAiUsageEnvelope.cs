namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// OpenAI-compatible response envelope with a usage block.
    /// </summary>
    public class OpenAiUsageEnvelope
    {
        /// <summary>
        /// Usage metrics.
        /// </summary>
        [JsonPropertyName("usage")]
        public OpenAiUsage? Usage { get; set; } = null;
    }
}

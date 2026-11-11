namespace Xeno.Client.Tools.Workspace.Web
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Body of a You.com search response.
    /// </summary>
    internal sealed class YouApiResponse
    {
        /// <summary>
        /// Result sections.
        /// </summary>
        [JsonPropertyName("results")]
        public YouApiResults? Results { get; set; } = null;

        /// <summary>
        /// Metadata.
        /// </summary>
        [JsonPropertyName("metadata")]
        public YouApiMetadata? Metadata { get; set; } = null;
    }
}

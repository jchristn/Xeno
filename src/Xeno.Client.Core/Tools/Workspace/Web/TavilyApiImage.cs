namespace Xeno.Client.Tools.Workspace.Web
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// A Tavily image.
    /// </summary>
    internal sealed class TavilyApiImage
    {
        /// <summary>
        /// Image URL.
        /// </summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; } = null;

        /// <summary>
        /// Alternate image URL.
        /// </summary>
        [JsonPropertyName("image_url")]
        public string? ImageUrl { get; set; } = null;

        /// <summary>
        /// Description.
        /// </summary>
        [JsonPropertyName("description")]
        public string? Description { get; set; } = null;

        /// <summary>
        /// Alt text.
        /// </summary>
        [JsonPropertyName("alt")]
        public string? Alt { get; set; } = null;
    }
}

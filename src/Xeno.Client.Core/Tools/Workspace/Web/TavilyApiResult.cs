namespace Xeno.Client.Tools.Workspace.Web
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One Tavily result.
    /// </summary>
    internal sealed class TavilyApiResult
    {
        /// <summary>
        /// Title.
        /// </summary>
        [JsonPropertyName("title")]
        public string? Title { get; set; } = null;

        /// <summary>
        /// URL.
        /// </summary>
        [JsonPropertyName("url")]
        public string? Url { get; set; } = null;

        /// <summary>
        /// Content snippet.
        /// </summary>
        [JsonPropertyName("content")]
        public string? Content { get; set; } = null;

        /// <summary>
        /// Score.
        /// </summary>
        [JsonPropertyName("score")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? Score { get; set; } = null;

        /// <summary>
        /// Raw content.
        /// </summary>
        [JsonPropertyName("raw_content")]
        public string? RawContent { get; set; } = null;

        /// <summary>
        /// Favicon URL.
        /// </summary>
        [JsonPropertyName("favicon")]
        public string? Favicon { get; set; } = null;

        /// <summary>
        /// Published date as given.
        /// </summary>
        [JsonPropertyName("published_date")]
        public string? PublishedDate { get; set; } = null;

        /// <summary>
        /// Images.
        /// </summary>
        [JsonPropertyName("images")]
        [JsonConverter(typeof(TavilyApiImageListConverter))]
        public List<TavilyApiImage>? Images { get; set; } = null;
    }
}

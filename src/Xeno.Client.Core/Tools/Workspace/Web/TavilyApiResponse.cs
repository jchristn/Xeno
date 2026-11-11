namespace Xeno.Client.Tools.Workspace.Web
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Body of a Tavily search response.
    /// </summary>
    internal sealed class TavilyApiResponse
    {
        /// <summary>
        /// Echoed query.
        /// </summary>
        [JsonPropertyName("query")]
        public string? Query { get; set; } = null;

        /// <summary>
        /// Answer text.
        /// </summary>
        [JsonPropertyName("answer")]
        public string? Answer { get; set; } = null;

        /// <summary>
        /// Request id.
        /// </summary>
        [JsonPropertyName("request_id")]
        public string? RequestId { get; set; } = null;

        /// <summary>
        /// Response time in seconds.
        /// </summary>
        [JsonPropertyName("response_time")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? ResponseTime { get; set; } = null;

        /// <summary>
        /// Images (URL strings or objects).
        /// </summary>
        [JsonPropertyName("images")]
        [JsonConverter(typeof(TavilyApiImageListConverter))]
        public List<TavilyApiImage>? Images { get; set; } = null;

        /// <summary>
        /// Results.
        /// </summary>
        [JsonPropertyName("results")]
        public List<TavilyApiResult>? Results { get; set; } = null;
    }
}

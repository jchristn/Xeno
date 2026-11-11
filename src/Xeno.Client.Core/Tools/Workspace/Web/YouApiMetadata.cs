namespace Xeno.Client.Tools.Workspace.Web
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// You.com response metadata.
    /// </summary>
    internal sealed class YouApiMetadata
    {
        /// <summary>
        /// Search id.
        /// </summary>
        [JsonPropertyName("search_uuid")]
        public string? SearchUuid { get; set; } = null;

        /// <summary>
        /// Latency in seconds.
        /// </summary>
        [JsonPropertyName("latency")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double? Latency { get; set; } = null;
    }
}

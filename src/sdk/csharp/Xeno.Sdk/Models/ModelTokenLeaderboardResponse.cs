namespace Xeno.Sdk
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ranked model token usage response.
    /// </summary>
    public class ModelTokenLeaderboardResponse
    {
        /// <summary>
        /// Gets or sets rank-ordered model token usage keyed by model name.
        /// </summary>
        [JsonPropertyName("models")]
        public Dictionary<string, ModelTokenUsage> Models { get; set; } = new Dictionary<string, ModelTokenUsage>();

        /// <summary>
        /// Gets or sets the total number of matching model records before pagination.
        /// </summary>
        [JsonPropertyName("total")]
        public int Total { get; set; }

        /// <summary>
        /// Gets or sets the applied result limit.
        /// </summary>
        [JsonPropertyName("limit")]
        public int Limit { get; set; }

        /// <summary>
        /// Gets or sets the applied result offset.
        /// </summary>
        [JsonPropertyName("offset")]
        public int Offset { get; set; }
    }
}

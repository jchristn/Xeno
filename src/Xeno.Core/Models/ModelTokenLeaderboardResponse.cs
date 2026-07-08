namespace Xeno.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Ranked token usage by model.
    /// </summary>
    public class ModelTokenLeaderboardResponse
    {
        /// <summary>
        /// Rank-ordered model token usage keyed by model name.
        /// </summary>
        public Dictionary<string, ModelTokenUsage> Models { get; set; } = new Dictionary<string, ModelTokenUsage>();

        /// <summary>
        /// Total number of matching models before pagination.
        /// </summary>
        public int Total { get; set; } = 0;

        /// <summary>
        /// Applied result limit.
        /// </summary>
        public int Limit { get; set; } = 100;

        /// <summary>
        /// Applied result offset.
        /// </summary>
        public int Offset { get; set; } = 0;
    }
}

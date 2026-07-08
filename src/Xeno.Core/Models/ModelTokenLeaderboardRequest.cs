namespace Xeno.Core.Models
{
    using System;

    /// <summary>
    /// Model token leaderboard request.
    /// </summary>
    public class ModelTokenLeaderboardRequest
    {
        /// <summary>
        /// Optional tenant identifier. When omitted, results include all tenants.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Inclusive start timestamp.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// Exclusive end timestamp.
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;

        /// <summary>
        /// Maximum number of ranked models to return. Defaults to 100 when omitted or null and is capped at 1000.
        /// </summary>
        public int? Limit { get; set; } = null;

        /// <summary>
        /// Number of ranked models to skip. Defaults to 0 when omitted or null.
        /// </summary>
        public int? Offset { get; set; } = null;
    }
}

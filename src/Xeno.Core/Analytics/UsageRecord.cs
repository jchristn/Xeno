namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One request or chat turn, reduced to what usage analytics read.
    /// </summary>
    public sealed class UsageRecord
    {
        /// <summary>
        /// When the request started, UTC.
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Model.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Provider or client that answered.
        /// </summary>
        public string? Provider { get; set; } = null;

        /// <summary>
        /// Route: local, local:&lt;provider&gt;, or network.
        /// </summary>
        public string? Route { get; set; } = null;

        /// <summary>
        /// Xeno client that answered (server).
        /// </summary>
        public string? ClientId { get; set; } = null;

        /// <summary>
        /// Tenant (server).
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// User (server).
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Credential (server).
        /// </summary>
        public string? CredentialId { get; set; } = null;

        /// <summary>
        /// True when the request succeeded.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// HTTP status, when known.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// True when a network fallback answered; null when the dataset cannot know.
        /// </summary>
        public bool? Fallback { get; set; } = null;

        /// <summary>
        /// Total milliseconds.
        /// </summary>
        public double? TotalMs { get; set; } = null;

        /// <summary>
        /// Milliseconds to the first token or byte.
        /// </summary>
        public double? TimeToFirstTokenMs { get; set; } = null;

        /// <summary>
        /// Milliseconds from the first token to the end.
        /// </summary>
        public double? StreamingMs { get; set; } = null;

        /// <summary>
        /// Prompt tokens, including cached ones.
        /// </summary>
        public long? PromptTokens { get; set; } = null;

        /// <summary>
        /// Cached prompt tokens, when recorded.
        /// </summary>
        public long? CachedPromptTokens { get; set; } = null;

        /// <summary>
        /// Completion tokens.
        /// </summary>
        public long? CompletionTokens { get; set; } = null;

        /// <summary>
        /// Stage durations that could be derived.
        /// </summary>
        public List<UsageStageDuration> Stages { get; set; } = new List<UsageStageDuration>();

        /// <summary>
        /// Stages whose formula came out negative and were clamped to zero.
        /// </summary>
        public int StageAnomalies { get; set; } = 0;

        /// <summary>
        /// Chat: tool calls that ran and succeeded.
        /// </summary>
        public int ToolCalls { get; set; } = 0;

        /// <summary>
        /// Chat: tool calls that failed.
        /// </summary>
        public int ToolFailures { get; set; } = 0;

        /// <summary>
        /// Chat: tool calls that were denied or not approved in time.
        /// </summary>
        public int ToolDenied { get; set; } = 0;

        /// <summary>
        /// Chat: 1 for thumbs up, -1 for thumbs down, null when unrated.
        /// </summary>
        public int? Rating { get; set; } = null;
    }
}

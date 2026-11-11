namespace Xeno.Core.Chat
{
    using System;

    /// <summary>
    /// Filters and paging for chat request history. Every filter is optional.
    /// </summary>
    public sealed class ChatHistoryQuery
    {
        /// <summary>
        /// Model (exact, case-insensitive).
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Route kind: "local" matches every local provider, "network", "unavailable", or an exact "local:&lt;provider&gt;".
        /// </summary>
        public string? Route { get; set; } = null;

        /// <summary>
        /// Local provider name.
        /// </summary>
        public string? Provider { get; set; } = null;

        /// <summary>
        /// Status.
        /// </summary>
        public ChatTurnStatusEnum? Status { get; set; } = null;

        /// <summary>
        /// Surface.
        /// </summary>
        public ChatSurfaceEnum? Surface { get; set; } = null;

        /// <summary>
        /// Rating: 1, -1, or 0 for turns nobody rated.
        /// </summary>
        public int? Rating { get; set; } = null;

        /// <summary>
        /// True or false to filter on network fallback.
        /// </summary>
        public bool? Fallback { get; set; } = null;

        /// <summary>
        /// Conversation identifier.
        /// </summary>
        public string? ConversationId { get; set; } = null;

        /// <summary>
        /// Text to find in the conversation title, model, or error.
        /// </summary>
        public string? Search { get; set; } = null;

        /// <summary>
        /// Earliest start, UTC, inclusive.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// Latest start, UTC, exclusive.
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;

        /// <summary>
        /// Page number, starting at 1.
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size, 1 to 500.
        /// </summary>
        public int PageSize { get; set; } = 50;
    }
}

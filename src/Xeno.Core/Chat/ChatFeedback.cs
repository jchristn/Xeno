namespace Xeno.Core.Chat
{
    using System;

    /// <summary>
    /// Thumbs up or down, with an optional comment, on one assistant message.
    /// </summary>
    public sealed class ChatFeedback
    {
        /// <summary>
        /// Identifier, prefixed "fbk_".
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Conversation identifier.
        /// </summary>
        public string ConversationId { get; set; } = String.Empty;

        /// <summary>
        /// Rated message identifier.
        /// </summary>
        public string MessageId { get; set; } = String.Empty;

        /// <summary>
        /// Turn that wrote the message, or null.
        /// </summary>
        public string? TurnId { get; set; } = null;

        /// <summary>
        /// 1 for thumbs up, -1 for thumbs down.
        /// </summary>
        public int Rating { get; set; } = 1;

        /// <summary>
        /// Free-text comment, or null.
        /// </summary>
        public string? Comment { get; set; } = null;

        /// <summary>
        /// Model that wrote the message.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Route the message took.
        /// </summary>
        public string? Route { get; set; } = null;

        /// <summary>
        /// Conversation title, filled in by queries.
        /// </summary>
        public string? ConversationTitle { get; set; } = null;

        /// <summary>
        /// Start of the rated answer, filled in by queries.
        /// </summary>
        public string? Excerpt { get; set; } = null;

        /// <summary>
        /// Completion tokens of the rated message.
        /// </summary>
        public int? CompletionTokens { get; set; } = null;

        /// <summary>
        /// Milliseconds the rated answer took.
        /// </summary>
        public double? TotalMs { get; set; } = null;

        /// <summary>
        /// Created, UTC.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last changed, UTC.
        /// </summary>
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}

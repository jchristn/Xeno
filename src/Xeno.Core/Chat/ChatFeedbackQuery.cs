namespace Xeno.Core.Chat
{
    using System;

    /// <summary>
    /// Filters and paging for feedback. Every filter is optional.
    /// </summary>
    public sealed class ChatFeedbackQuery
    {
        /// <summary>
        /// 1 or -1.
        /// </summary>
        public int? Rating { get; set; } = null;

        /// <summary>
        /// Model (exact, case-insensitive).
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Route kind or exact route, as in <see cref="ChatHistoryQuery.Route"/>.
        /// </summary>
        public string? Route { get; set; } = null;

        /// <summary>
        /// True for feedback with a comment only, false for feedback without one.
        /// </summary>
        public bool? HasComment { get; set; } = null;

        /// <summary>
        /// Text to find in the comment or conversation title.
        /// </summary>
        public string? Search { get; set; } = null;

        /// <summary>
        /// Earliest, UTC, inclusive.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// Latest, UTC, exclusive.
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

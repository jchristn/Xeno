namespace Xeno.Core.Models
{
    using System;
    using Xeno.Core.Enums;

    /// <summary>
    /// Timeline event for a request history entry.
    /// </summary>
    public class RequestHistoryTimelineEvent
    {
        /// <summary>
        /// Stable stage name.
        /// </summary>
        public RequestHistoryTimelineStageEnum Stage { get; set; } = RequestHistoryTimelineStageEnum.Unknown;

        /// <summary>
        /// Human-readable message.
        /// </summary>
        public string Message { get; set; } = String.Empty;

        /// <summary>
        /// UTC event timestamp.
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Elapsed milliseconds since the server received the request.
        /// </summary>
        public long? ElapsedMs { get; set; } = null;

        /// <summary>
        /// Whether the stage succeeded.
        /// </summary>
        public bool Success { get; set; } = true;
    }
}

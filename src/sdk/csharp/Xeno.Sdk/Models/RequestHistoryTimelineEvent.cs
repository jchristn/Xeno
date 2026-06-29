namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno request history timeline event DTO.
    /// </summary>
    public class RequestHistoryTimelineEvent
    {
        /// <summary>
        /// Gets or sets the stage.
        /// </summary>
        public string Stage { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the timestamp utc.
        /// </summary>
        public DateTime? TimestampUtc { get; set; }

        /// <summary>
        /// Gets or sets the message.
        /// </summary>
        public string Message { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the elapsed ms.
        /// </summary>
        public long ElapsedMs { get; set; }
    }
}

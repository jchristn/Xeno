namespace Xeno.Client.LocalDashboard.Models
{
    using System;

    /// <summary>
    /// Local request timeline event.
    /// </summary>
    public class LocalRequestTimelineEvent
    {
        /// <summary>
        /// Event stage.
        /// </summary>
        public LocalRequestTimelineStageEnum Stage { get; set; } = LocalRequestTimelineStageEnum.Received;

        /// <summary>
        /// Event timestamp.
        /// </summary>
        public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Event message.
        /// </summary>
        public string? Message { get; set; } = null;
    }
}

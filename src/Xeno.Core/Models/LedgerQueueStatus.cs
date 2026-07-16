namespace Xeno.Core.Models
{
    using System;

    /// <summary>
    /// In-memory ledger queue and drain status.
    /// </summary>
    public class LedgerQueueStatus
    {
        /// <summary>
        /// Whether ledgering is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Current queue depth.
        /// </summary>
        public int QueueDepth { get; set; } = 0;

        /// <summary>
        /// Oldest queued event age in milliseconds.
        /// </summary>
        public long? OldestEventAgeMs { get; set; } = null;

        /// <summary>
        /// Last successful drain timestamp.
        /// </summary>
        public DateTime? LastSuccessfulDrainUtc { get; set; } = null;

        /// <summary>
        /// Last failure timestamp.
        /// </summary>
        public DateTime? LastFailureUtc { get; set; } = null;

        /// <summary>
        /// Last failure message.
        /// </summary>
        public string? LastFailure { get; set; } = null;

        /// <summary>
        /// Retry count since process start.
        /// </summary>
        public long RetryCount { get; set; } = 0;

        /// <summary>
        /// Terminal validation failure count since process start.
        /// </summary>
        public long TerminalFailureCount { get; set; } = 0;

        /// <summary>
        /// Successfully processed event count since process start.
        /// </summary>
        public long ProcessedCount { get; set; } = 0;
    }
}

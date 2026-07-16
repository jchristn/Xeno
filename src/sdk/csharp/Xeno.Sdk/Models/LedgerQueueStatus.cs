namespace Xeno.Sdk
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ledger queue status DTO.
    /// </summary>
    public class LedgerQueueStatus
    {
        /// <summary>
        /// Gets or sets whether ledgering is enabled.
        /// </summary>
        [JsonPropertyName("enabled")]
        public bool Enabled { get; set; }

        /// <summary>
        /// Gets or sets current queue depth.
        /// </summary>
        [JsonPropertyName("queueDepth")]
        public int QueueDepth { get; set; }

        /// <summary>
        /// Gets or sets oldest queued event age in milliseconds.
        /// </summary>
        [JsonPropertyName("oldestEventAgeMs")]
        public long? OldestEventAgeMs { get; set; }

        /// <summary>
        /// Gets or sets last successful drain timestamp.
        /// </summary>
        [JsonPropertyName("lastSuccessfulDrainUtc")]
        public DateTime? LastSuccessfulDrainUtc { get; set; }

        /// <summary>
        /// Gets or sets last failure timestamp.
        /// </summary>
        [JsonPropertyName("lastFailureUtc")]
        public DateTime? LastFailureUtc { get; set; }

        /// <summary>
        /// Gets or sets last failure message.
        /// </summary>
        [JsonPropertyName("lastFailure")]
        public string? LastFailure { get; set; }

        /// <summary>
        /// Gets or sets retry count since process start.
        /// </summary>
        [JsonPropertyName("retryCount")]
        public long RetryCount { get; set; }

        /// <summary>
        /// Gets or sets terminal failure count since process start.
        /// </summary>
        [JsonPropertyName("terminalFailureCount")]
        public long TerminalFailureCount { get; set; }

        /// <summary>
        /// Gets or sets processed event count since process start.
        /// </summary>
        [JsonPropertyName("processedCount")]
        public long ProcessedCount { get; set; }
    }
}

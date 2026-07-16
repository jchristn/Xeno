namespace Xeno.Sdk
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ledger status response DTO.
    /// </summary>
    public class LedgerStatusResponse
    {
        /// <summary>
        /// Gets or sets queue status.
        /// </summary>
        [JsonPropertyName("queue")]
        public LedgerQueueStatus Queue { get; set; } = new LedgerQueueStatus();

        /// <summary>
        /// Gets or sets whether NetLedger health succeeded.
        /// </summary>
        [JsonPropertyName("netLedgerHealthy")]
        public bool NetLedgerHealthy { get; set; }

        /// <summary>
        /// Gets or sets NetLedger health message.
        /// </summary>
        [JsonPropertyName("netLedgerMessage")]
        public string? NetLedgerMessage { get; set; }
    }
}

namespace Xeno.Core.Models
{
    /// <summary>
    /// Ledger status response.
    /// </summary>
    public class LedgerStatusResponse
    {
        /// <summary>
        /// Queue status.
        /// </summary>
        public LedgerQueueStatus Queue { get; set; } = new LedgerQueueStatus();

        /// <summary>
        /// Whether NetLedger health check succeeded.
        /// </summary>
        public bool NetLedgerHealthy { get; set; } = false;

        /// <summary>
        /// NetLedger health message.
        /// </summary>
        public string? NetLedgerMessage { get; set; } = null;
    }
}

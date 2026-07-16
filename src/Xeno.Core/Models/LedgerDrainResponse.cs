namespace Xeno.Core.Models
{
    /// <summary>
    /// Ledger drain response.
    /// </summary>
    public class LedgerDrainResponse
    {
        /// <summary>
        /// Number of events processed.
        /// </summary>
        public int Processed { get; set; } = 0;

        /// <summary>
        /// Number of events retried.
        /// </summary>
        public int Retried { get; set; } = 0;

        /// <summary>
        /// Number of terminal failures.
        /// </summary>
        public int TerminalFailures { get; set; } = 0;
    }
}

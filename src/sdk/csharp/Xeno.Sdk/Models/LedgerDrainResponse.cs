namespace Xeno.Sdk
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ledger drain response DTO.
    /// </summary>
    public class LedgerDrainResponse
    {
        /// <summary>
        /// Gets or sets processed event count.
        /// </summary>
        [JsonPropertyName("processed")]
        public int Processed { get; set; }

        /// <summary>
        /// Gets or sets retried event count.
        /// </summary>
        [JsonPropertyName("retried")]
        public int Retried { get; set; }

        /// <summary>
        /// Gets or sets terminal failure count.
        /// </summary>
        [JsonPropertyName("terminalFailures")]
        public int TerminalFailures { get; set; }
    }
}

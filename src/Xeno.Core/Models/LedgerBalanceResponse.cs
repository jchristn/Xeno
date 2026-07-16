namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ledger balance response.
    /// </summary>
    public class LedgerBalanceResponse
    {
        /// <summary>
        /// Xeno tenant identifier.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string XenoTenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Xeno user identifier.
        /// </summary>
        [JsonPropertyName("userId")]
        public string? XenoUserId { get; set; } = null;

        /// <summary>
        /// NetLedger tenant identifier.
        /// </summary>
        [JsonPropertyName("netLedgerTenantId")]
        public string? NetLedgerTenantId { get; set; } = null;

        /// <summary>
        /// NetLedger account identifier.
        /// </summary>
        [JsonPropertyName("netLedgerAccountId")]
        public string? NetLedgerAccountId { get; set; } = null;

        /// <summary>
        /// Current committed balance.
        /// </summary>
        [JsonPropertyName("balance")]
        public decimal Balance { get; set; } = 0m;

        /// <summary>
        /// Total debited tokens.
        /// </summary>
        [JsonPropertyName("totalDebits")]
        public long DebitTotal { get; set; } = 0;

        /// <summary>
        /// Total credited tokens.
        /// </summary>
        [JsonPropertyName("totalCredits")]
        public long CreditTotal { get; set; } = 0;
    }
}

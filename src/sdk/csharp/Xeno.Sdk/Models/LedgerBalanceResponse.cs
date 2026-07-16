namespace Xeno.Sdk
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ledger balance response DTO.
    /// </summary>
    public class LedgerBalanceResponse
    {
        /// <summary>
        /// Gets or sets Xeno tenant identifier.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets Xeno user identifier.
        /// </summary>
        [JsonPropertyName("userId")]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets NetLedger tenant identifier.
        /// </summary>
        [JsonPropertyName("netLedgerTenantId")]
        public string? NetLedgerTenantId { get; set; }

        /// <summary>
        /// Gets or sets NetLedger account identifier.
        /// </summary>
        [JsonPropertyName("netLedgerAccountId")]
        public string? NetLedgerAccountId { get; set; }

        /// <summary>
        /// Gets or sets current balance.
        /// </summary>
        [JsonPropertyName("balance")]
        public long Balance { get; set; }

        /// <summary>
        /// Gets or sets total debits.
        /// </summary>
        [JsonPropertyName("totalDebits")]
        public long TotalDebits { get; set; }

        /// <summary>
        /// Gets or sets total credits.
        /// </summary>
        [JsonPropertyName("totalCredits")]
        public long TotalCredits { get; set; }

        /// <summary>
        /// Gets or sets last ledger activity timestamp.
        /// </summary>
        [JsonPropertyName("lastActivityUtc")]
        public DateTime? LastActivityUtc { get; set; }
    }
}

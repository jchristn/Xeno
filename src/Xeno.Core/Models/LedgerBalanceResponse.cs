namespace Xeno.Core.Models
{
    /// <summary>
    /// Ledger balance response.
    /// </summary>
    public class LedgerBalanceResponse
    {
        /// <summary>
        /// Xeno tenant identifier.
        /// </summary>
        public string XenoTenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Xeno user identifier.
        /// </summary>
        public string? XenoUserId { get; set; } = null;

        /// <summary>
        /// NetLedger tenant identifier.
        /// </summary>
        public string? NetLedgerTenantId { get; set; } = null;

        /// <summary>
        /// NetLedger account identifier.
        /// </summary>
        public string? NetLedgerAccountId { get; set; } = null;

        /// <summary>
        /// Current committed balance.
        /// </summary>
        public decimal Balance { get; set; } = 0m;

        /// <summary>
        /// Total debited tokens.
        /// </summary>
        public long DebitTotal { get; set; } = 0;

        /// <summary>
        /// Total credited tokens.
        /// </summary>
        public long CreditTotal { get; set; } = 0;
    }
}

namespace Xeno.Core.Models
{
    /// <summary>
    /// NetLedger balance contract.
    /// </summary>
    public class NetLedgerBalance
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Account identifier.
        /// </summary>
        public string AccountId { get; set; } = string.Empty;

        /// <summary>
        /// Balance amount.
        /// </summary>
        public decimal Amount { get; set; } = 0m;
    }
}

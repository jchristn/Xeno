namespace Xeno.Core.Models
{
    /// <summary>
    /// Ledger balance search request.
    /// </summary>
    public class LedgerBalanceSearchRequest
    {
        /// <summary>
        /// Optional Xeno tenant identifier.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Optional Xeno user identifier.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Optional search text.
        /// </summary>
        public string? Search { get; set; } = null;

        /// <summary>
        /// Page number.
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Page size.
        /// </summary>
        public int PageSize { get; set; } = 100;
    }
}

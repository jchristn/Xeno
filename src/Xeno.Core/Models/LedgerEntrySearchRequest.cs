namespace Xeno.Core.Models
{
    using System;
    using Xeno.Core.Enums;

    /// <summary>
    /// Ledger entry search request.
    /// </summary>
    public class LedgerEntrySearchRequest
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
        /// Optional entry direction.
        /// </summary>
        public LedgerEntryDirection? Direction { get; set; } = null;

        /// <summary>
        /// Optional selected Xeno client identifier.
        /// </summary>
        public string? SelectedClientId { get; set; } = null;

        /// <summary>
        /// Optional request credential identifier.
        /// </summary>
        public string? RequestCredentialId { get; set; } = null;

        /// <summary>
        /// Optional request history identifier.
        /// </summary>
        public string? RequestHistoryId { get; set; } = null;

        /// <summary>
        /// Optional range start.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// Optional range end.
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;

        /// <summary>
        /// Page size.
        /// </summary>
        public int PageSize { get; set; } = 100;

        /// <summary>
        /// Continuation token.
        /// </summary>
        public string? ContinuationToken { get; set; } = null;
    }
}

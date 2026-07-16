namespace Xeno.Core.Models
{
    using System;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// In-memory ledger event captured from request history.
    /// </summary>
    public class LedgerEvent
    {
        /// <summary>
        /// Ledger event identifier.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewLedgerEventId();

        /// <summary>
        /// Xeno tenant identifier.
        /// </summary>
        public string XenoTenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Request history identifier.
        /// </summary>
        public string RequestHistoryId { get; set; } = String.Empty;

        /// <summary>
        /// Requesting credential identifier, when present.
        /// </summary>
        public string? RequestCredentialId { get; set; } = null;

        /// <summary>
        /// User charged for the request.
        /// </summary>
        public string RequestUserId { get; set; } = String.Empty;

        /// <summary>
        /// Selected client identifier.
        /// </summary>
        public string SelectedClientId { get; set; } = String.Empty;

        /// <summary>
        /// User credited for serving the request.
        /// </summary>
        public string ServingUserId { get; set; } = String.Empty;

        /// <summary>
        /// Request kind.
        /// </summary>
        public LedgerRequestKind RequestKind { get; set; } = LedgerRequestKind.Completion;

        /// <summary>
        /// Total raw token amount.
        /// </summary>
        public int TotalTokens { get; set; } = 0;

        /// <summary>
        /// Business event timestamp.
        /// </summary>
        public DateTime OccurredUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Queue creation timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Retry attempt count.
        /// </summary>
        public int RetryCount { get; set; } = 0;

        /// <summary>
        /// Next retry timestamp.
        /// </summary>
        public DateTime? NextAttemptUtc { get; set; } = null;
    }
}

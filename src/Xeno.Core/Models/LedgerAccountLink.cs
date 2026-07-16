namespace Xeno.Core.Models
{
    using System;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Link between a Xeno user and a NetLedger account.
    /// </summary>
    public class LedgerAccountLink
    {
        /// <summary>
        /// Link identifier.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewLedgerAccountLinkId();

        /// <summary>
        /// Xeno tenant identifier.
        /// </summary>
        public string XenoTenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Xeno user identifier.
        /// </summary>
        public string XenoUserId { get; set; } = String.Empty;

        /// <summary>
        /// NetLedger tenant identifier.
        /// </summary>
        public string NetLedgerTenantId { get; set; } = String.Empty;

        /// <summary>
        /// NetLedger account identifier.
        /// </summary>
        public string NetLedgerAccountId { get; set; } = String.Empty;

        /// <summary>
        /// Creation timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last update timestamp.
        /// </summary>
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}

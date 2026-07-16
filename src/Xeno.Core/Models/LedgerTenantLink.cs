namespace Xeno.Core.Models
{
    using System;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Link between a Xeno tenant and a NetLedger tenant.
    /// </summary>
    public class LedgerTenantLink
    {
        /// <summary>
        /// Link identifier.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewLedgerTenantLinkId();

        /// <summary>
        /// Xeno tenant identifier.
        /// </summary>
        public string XenoTenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// NetLedger tenant identifier.
        /// </summary>
        public string NetLedgerTenantId { get; set; } = String.Empty;

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

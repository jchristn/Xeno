namespace Xeno.Email.Templates
{
    using System;

    /// <summary>
    /// Context for token balance floor warning email.
    /// </summary>
    public class TokenBalanceFloorApproachingEmailTemplateContext
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Tenant name.
        /// </summary>
        public string TenantName { get; set; } = String.Empty;

        /// <summary>
        /// Current token balance.
        /// </summary>
        public long CurrentBalance { get; set; } = 0;

        /// <summary>
        /// Configured floor.
        /// </summary>
        public long Floor { get; set; } = 0;
    }
}

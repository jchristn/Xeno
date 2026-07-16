namespace Xeno.Core.Models
{
    using System;

    /// <summary>
    /// NetLedger tenant contract.
    /// </summary>
    public class NetLedgerTenant
    {
        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Tenant name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Whether the tenant is active.
        /// </summary>
        public bool Active { get; set; } = true;
    }
}

namespace Xeno.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Ledger reconcile response.
    /// </summary>
    public class LedgerReconcileResponse
    {
        /// <summary>
        /// Missing tenant links.
        /// </summary>
        public List<string> MissingTenantLinks { get; set; } = new List<string>();

        /// <summary>
        /// Missing account links.
        /// </summary>
        public List<string> MissingAccountLinks { get; set; } = new List<string>();

        /// <summary>
        /// Whether repair was requested.
        /// </summary>
        public bool RepairRequested { get; set; } = false;
    }
}

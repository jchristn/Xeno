namespace Xeno.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Result from a tenant lifecycle operation.
    /// </summary>
    public class TenantLifecycleResult
    {
        /// <summary>
        /// Whether all required subordinate services completed successfully.
        /// </summary>
        public bool Success { get; set; } = true;

        /// <summary>
        /// Subordinate service statuses.
        /// </summary>
        public List<TenantSubordinateServiceStatus> SubordinateServices { get; set; } = new List<TenantSubordinateServiceStatus>();
    }
}

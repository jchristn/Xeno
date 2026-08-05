namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Tenant record.
    /// </summary>
    public class Tenant
    {
        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string Id { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Tenant name.
        /// </summary>
        public string Name { get; set; } = "Default";

        /// <summary>
        /// Whether the tenant is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Source IP address that created the tenant during onboarding, when known.
        /// </summary>
        public string? CreationIpAddress { get; set; } = null;

        /// <summary>
        /// Created timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Updated timestamp.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Latest subordinate service lifecycle statuses.
        /// </summary>
        public List<TenantSubordinateServiceStatus> SubordinateServices { get; set; } = new List<TenantSubordinateServiceStatus>();
    }
}

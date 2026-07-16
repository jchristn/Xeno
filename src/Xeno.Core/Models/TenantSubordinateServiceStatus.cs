namespace Xeno.Core.Models
{
    using System;

    /// <summary>
    /// Provisioning status for a tenant subordinate service.
    /// </summary>
    public class TenantSubordinateServiceStatus
    {
        /// <summary>
        /// Service name.
        /// </summary>
        public string ServiceName { get; set; } = String.Empty;

        /// <summary>
        /// Last lifecycle operation.
        /// </summary>
        public string Operation { get; set; } = String.Empty;

        /// <summary>
        /// Xeno tenant identifier.
        /// </summary>
        public string XenoTenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Subordinate service tenant identifier.
        /// </summary>
        public string? ExternalTenantId { get; set; } = null;

        /// <summary>
        /// Operation status.
        /// </summary>
        public string Status { get; set; } = "NotConfigured";

        /// <summary>
        /// Error details when the operation failed.
        /// </summary>
        public string? ErrorMessage { get; set; } = null;

        /// <summary>
        /// Last status update timestamp.
        /// </summary>
        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}

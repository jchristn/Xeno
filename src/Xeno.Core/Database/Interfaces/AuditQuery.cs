namespace Xeno.Core.Database.Interfaces
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Filter and paging options for querying persisted audit records.
    /// </summary>
    public class AuditQuery
    {
        /// <summary>
        /// Owning tenant identifier to match. When null or empty, tenant is not filtered.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// When true and <see cref="TenantId"/> is set, match records whose owning tenant OR
        /// target tenant equals the tenant. When false, match only the owning tenant.
        /// </summary>
        public bool MatchTargetTenant { get; set; } = false;

        /// <summary>
        /// User identifier to match. When set, matches records whose principal OR target resource
        /// equals the user identifier.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Explicit record identifiers to match.
        /// </summary>
        public IReadOnlyCollection<string>? Ids { get; set; } = null;

        /// <summary>
        /// Inclusive lower bound on creation time.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// Exclusive upper bound on creation time.
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;

        /// <summary>
        /// Maximum rows to return. Zero or negative returns all matching rows.
        /// </summary>
        public int Limit { get; set; } = 0;

        /// <summary>
        /// Row offset for paging.
        /// </summary>
        public int Offset { get; set; } = 0;

        /// <summary>
        /// Order by creation time descending when true, ascending when false.
        /// </summary>
        public bool OrderDescending { get; set; } = true;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public AuditQuery()
        {
        }
    }
}

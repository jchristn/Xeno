namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Role record.
    /// </summary>
    public class Role
    {
        /// <summary>
        /// Role ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewId("role_");

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Role name.
        /// </summary>
        public string Name { get; set; } = "TenantMember";

        /// <summary>
        /// Whether this role is built in.
        /// </summary>
        public bool BuiltIn { get; set; } = false;

        /// <summary>
        /// Permission IDs.
        /// </summary>
        public List<string> PermissionIds { get; set; } = new List<string>();

        /// <summary>
        /// Created timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last update timestamp.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the role is active.
        /// </summary>
        public bool Active { get; set; } = true;
    }
}

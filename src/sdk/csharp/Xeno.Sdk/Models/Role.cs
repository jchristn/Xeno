namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Xeno role DTO.
    /// </summary>
    public class Role
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the tenant id.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the permission ids.
        /// </summary>
        public List<string> PermissionIds { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets the active.
        /// </summary>
        public bool Active { get; set; }
    }
}

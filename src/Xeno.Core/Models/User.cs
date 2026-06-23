namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// User record.
    /// </summary>
    public class User
    {
        /// <summary>
        /// User ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewId("usr_");

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Email address.
        /// </summary>
        public string Email { get; set; } = "admin@xeno";

        /// <summary>
        /// Whether user is a global admin.
        /// </summary>
        public bool IsAdmin { get; set; } = true;

        /// <summary>
        /// Whether user is a tenant admin.
        /// </summary>
        public bool IsTenantAdmin { get; set; } = true;

        /// <summary>
        /// Whether the user is active.
        /// </summary>
        public bool Active { get; set; } = true;
    }
}

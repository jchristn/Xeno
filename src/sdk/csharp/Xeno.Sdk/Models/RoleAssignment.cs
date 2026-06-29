namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno role assignment DTO.
    /// </summary>
    public class RoleAssignment
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
        /// Gets or sets the role id.
        /// </summary>
        public string RoleId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the principal type.
        /// </summary>
        public string PrincipalType { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the principal id.
        /// </summary>
        public string PrincipalId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the active.
        /// </summary>
        public bool Active { get; set; }
    }
}

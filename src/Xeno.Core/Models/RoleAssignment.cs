namespace Xeno.Core.Models
{
    using System;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Role assignment for a user or credential principal.
    /// </summary>
    public class RoleAssignment
    {
        /// <summary>
        /// Assignment ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewId("rmap_");

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Role ID.
        /// </summary>
        public string RoleId { get; set; } = String.Empty;

        /// <summary>
        /// Principal type.
        /// </summary>
        public PrincipalTypeEnum PrincipalType { get; set; } = PrincipalTypeEnum.User;

        /// <summary>
        /// Principal ID.
        /// </summary>
        public string PrincipalId { get; set; } = String.Empty;

        /// <summary>
        /// Created timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Last update timestamp.
        /// </summary>
        public DateTime LastUpdateUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Whether the assignment is active.
        /// </summary>
        public bool Active { get; set; } = true;
    }
}

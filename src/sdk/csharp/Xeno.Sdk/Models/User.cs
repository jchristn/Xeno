namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno user DTO.
    /// </summary>
    public class User
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
        /// Gets or sets the username.
        /// </summary>
        public string Username { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the email.
        /// </summary>
        public string Email { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the is admin.
        /// </summary>
        public bool IsAdmin { get; set; }

        /// <summary>
        /// Gets or sets the is tenant admin.
        /// </summary>
        public bool IsTenantAdmin { get; set; }

        /// <summary>
        /// Gets or sets the active.
        /// </summary>
        public bool Active { get; set; }
    }
}

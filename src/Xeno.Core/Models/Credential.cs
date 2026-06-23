namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Credential record.
    /// </summary>
    public class Credential
    {
        /// <summary>
        /// Credential ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewId("cred_");

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Owning user ID.
        /// </summary>
        public string UserId { get; set; } = String.Empty;

        /// <summary>
        /// Access key.
        /// </summary>
        public string AccessKey { get; set; } = "access_development";

        /// <summary>
        /// Secret key for development mode.
        /// </summary>
        public string SecretKey { get; set; } = "secret_development";

        /// <summary>
        /// Whether the credential is active.
        /// </summary>
        public bool Active { get; set; } = true;
    }
}

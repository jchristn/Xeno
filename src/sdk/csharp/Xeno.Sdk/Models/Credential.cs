namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno credential DTO.
    /// </summary>
    public class Credential
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
        /// Gets or sets the user id.
        /// </summary>
        public string UserId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the access key.
        /// </summary>
        public string AccessKey { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the auth scheme.
        /// </summary>
        public string AuthScheme { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the active.
        /// </summary>
        public bool Active { get; set; }
    }
}

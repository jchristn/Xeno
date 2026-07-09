namespace Xeno.Email.Workflows
{
    using System;
    using System.Collections.Generic;
    using Xeno.Email;

    /// <summary>
    /// Context for onboarding email notifications.
    /// </summary>
    public class OnboardingEmailContext
    {
        /// <summary>
        /// Whether onboarding created a new tenant.
        /// </summary>
        public bool CreatedTenant { get; set; } = false;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Tenant name.
        /// </summary>
        public string TenantName { get; set; } = String.Empty;

        /// <summary>
        /// User identifier.
        /// </summary>
        public string UserId { get; set; } = String.Empty;

        /// <summary>
        /// Username.
        /// </summary>
        public string Username { get; set; } = String.Empty;

        /// <summary>
        /// User email.
        /// </summary>
        public string UserEmail { get; set; } = String.Empty;

        /// <summary>
        /// Whether the user is a tenant administrator.
        /// </summary>
        public bool IsTenantAdmin { get; set; } = false;

        /// <summary>
        /// Source IP address.
        /// </summary>
        public string SourceIp { get; set; } = String.Empty;

        /// <summary>
        /// Correlation identifier.
        /// </summary>
        public string CorrelationId { get; set; } = String.Empty;

        /// <summary>
        /// Event timestamp.
        /// </summary>
        public DateTime EventUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Platform administrator recipients.
        /// </summary>
        public List<EmailAddress> PlatformAdministrators { get; set; } = new List<EmailAddress>();

        /// <summary>
        /// Tenant administrator recipients.
        /// </summary>
        public List<EmailAddress> TenantAdministrators { get; set; } = new List<EmailAddress>();
    }
}

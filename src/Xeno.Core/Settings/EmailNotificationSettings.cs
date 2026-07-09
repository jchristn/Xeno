namespace Xeno.Core.Settings
{
    using System.Collections.Generic;

    /// <summary>
    /// Email notification workflow settings.
    /// </summary>
    public class EmailNotificationSettings
    {
        /// <summary>
        /// Whether platform administrators are notified when a tenant is created.
        /// </summary>
        public bool NotifyPlatformAdministratorsOnTenantCreated { get; set; } = true;

        /// <summary>
        /// Whether platform administrators are notified when a user joins an existing tenant.
        /// </summary>
        public bool NotifyPlatformAdministratorsOnUserJoined { get; set; } = true;

        /// <summary>
        /// Whether tenant administrators are notified when a user joins their tenant.
        /// </summary>
        public bool NotifyTenantAdministratorsOnUserJoined { get; set; } = true;

        /// <summary>
        /// Whether a new tenant administrator receives a welcome email.
        /// </summary>
        public bool NotifyNewTenantAdministrator { get; set; } = true;

        /// <summary>
        /// Whether a new tenant member receives a welcome email.
        /// </summary>
        public bool NotifyNewTenantMember { get; set; } = false;

        /// <summary>
        /// Explicit platform administrator notification recipients.
        /// </summary>
        public List<EmailRecipientSettings> PlatformAdministratorRecipients { get; set; } = new List<EmailRecipientSettings>();
    }
}

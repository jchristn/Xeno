namespace Xeno.Core.Settings
{
    using System;
    using Xeno.Core.Enums;

    /// <summary>
    /// Server email settings.
    /// </summary>
    public class EmailSettings
    {
        /// <summary>
        /// Whether email notification workflows are enabled. Default value is <c>false</c>.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// Email provider. Default value is <see cref="EmailProviderTypeEnum.Disabled" />.
        /// </summary>
        public EmailProviderTypeEnum Provider { get; set; } = EmailProviderTypeEnum.Disabled;

        /// <summary>
        /// Sender email address.
        /// </summary>
        public string FromAddress { get; set; } = "noreply@xenocloud.ai";

        /// <summary>
        /// Sender display name.
        /// </summary>
        public string FromName { get; set; } = "Xeno";

        /// <summary>
        /// Optional reply-to email address.
        /// </summary>
        public string ReplyToAddress { get; set; } = "support@xenocloud.ai";

        /// <summary>
        /// Support email address displayed in templates.
        /// </summary>
        public string SupportAddress { get; set; } = "support@xenocloud.ai";

        /// <summary>
        /// Application base URL used by templates.
        /// </summary>
        public string ApplicationBaseUrl { get; set; } = "https://app.xenocloud.ai";

        /// <summary>
        /// Optional subject prefix.
        /// </summary>
        public string SubjectPrefix { get; set; } = String.Empty;

        /// <summary>
        /// Maximum rendered body bytes. Default value is 262144 bytes.
        /// </summary>
        public int MaxBodyBytes { get; set; } = 262144;

        /// <summary>
        /// Mailgun provider settings.
        /// </summary>
        public MailgunEmailSettings Mailgun { get; set; } = new MailgunEmailSettings();

        /// <summary>
        /// File provider settings.
        /// </summary>
        public EmailFileSettings File { get; set; } = new EmailFileSettings();

        /// <summary>
        /// Notification workflow settings.
        /// </summary>
        public EmailNotificationSettings Notifications { get; set; } = new EmailNotificationSettings();
    }
}

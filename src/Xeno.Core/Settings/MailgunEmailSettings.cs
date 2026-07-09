namespace Xeno.Core.Settings
{
    using System;

    /// <summary>
    /// Mailgun email provider settings.
    /// </summary>
    public class MailgunEmailSettings
    {
        /// <summary>
        /// Mailgun sending domain.
        /// </summary>
        public string Domain { get; set; } = String.Empty;

        /// <summary>
        /// Mailgun API key.
        /// </summary>
        public string ApiKey { get; set; } = String.Empty;

        /// <summary>
        /// Mailgun region hint. Default value is <c>US</c>.
        /// </summary>
        public string Region { get; set; } = "US";
    }
}

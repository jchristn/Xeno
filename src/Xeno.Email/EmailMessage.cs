namespace Xeno.Email
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Provider-neutral email message.
    /// </summary>
    public class EmailMessage
    {
        /// <summary>
        /// Sender.
        /// </summary>
        public EmailAddress From { get; set; } = new EmailAddress();

        /// <summary>
        /// Optional reply-to address.
        /// </summary>
        public EmailAddress? ReplyTo { get; set; } = null;

        /// <summary>
        /// To recipients.
        /// </summary>
        public List<EmailAddress> To { get; set; } = new List<EmailAddress>();

        /// <summary>
        /// CC recipients.
        /// </summary>
        public List<EmailAddress> Cc { get; set; } = new List<EmailAddress>();

        /// <summary>
        /// BCC recipients.
        /// </summary>
        public List<EmailAddress> Bcc { get; set; } = new List<EmailAddress>();

        /// <summary>
        /// Subject.
        /// </summary>
        public string Subject { get; set; } = String.Empty;

        /// <summary>
        /// HTML body.
        /// </summary>
        public string HtmlBody { get; set; } = String.Empty;

        /// <summary>
        /// Plain text body.
        /// </summary>
        public string TextBody { get; set; } = String.Empty;

        /// <summary>
        /// Email tags.
        /// </summary>
        public Dictionary<string, string> Tags { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Optional correlation identifier.
        /// </summary>
        public string? CorrelationId { get; set; } = null;

        /// <summary>
        /// Optional template identifier.
        /// </summary>
        public string? TemplateId { get; set; } = null;

        /// <summary>
        /// Attachments.
        /// </summary>
        public List<EmailAttachment> Attachments { get; set; } = new List<EmailAttachment>();
    }
}

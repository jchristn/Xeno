namespace Xeno.Email.Providers
{
    using System;
    using System.Linq;

    /// <summary>
    /// File-provider message metadata.
    /// </summary>
    public class EmailMessageMetadata
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Provider message identifier.
        /// </summary>
        public string ProviderMessageId { get; set; } = String.Empty;

        /// <summary>
        /// Subject.
        /// </summary>
        public string Subject { get; set; } = String.Empty;

        /// <summary>
        /// Sender.
        /// </summary>
        public string From { get; set; } = String.Empty;

        /// <summary>
        /// To recipients.
        /// </summary>
        public string[] To { get; set; } = Array.Empty<string>();

        /// <summary>
        /// CC recipients.
        /// </summary>
        public string[] Cc { get; set; } = Array.Empty<string>();

        /// <summary>
        /// BCC recipients.
        /// </summary>
        public string[] Bcc { get; set; } = Array.Empty<string>();

        /// <summary>
        /// Template identifier.
        /// </summary>
        public string TemplateId { get; set; } = String.Empty;

        /// <summary>
        /// Correlation identifier.
        /// </summary>
        public string CorrelationId { get; set; } = String.Empty;

        /// <summary>
        /// Creation timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Create metadata from a message.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="provider">Provider.</param>
        /// <param name="providerMessageId">Provider message identifier.</param>
        /// <returns>Metadata.</returns>
        public static EmailMessageMetadata FromMessage(EmailMessage message, string provider, string providerMessageId)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            return new EmailMessageMetadata
            {
                Provider = provider,
                ProviderMessageId = providerMessageId,
                Subject = message.Subject,
                From = message.From.ToString(),
                To = message.To.Select(item => item.ToString()).ToArray(),
                Cc = message.Cc.Select(item => item.ToString()).ToArray(),
                Bcc = message.Bcc.Select(item => item.ToString()).ToArray(),
                TemplateId = message.TemplateId ?? String.Empty,
                CorrelationId = message.CorrelationId ?? String.Empty,
                CreatedUtc = DateTime.UtcNow
            };
        }
    }
}

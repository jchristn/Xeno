namespace Xeno.Email.Providers
{
    using System;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using SendWithMailgun;
    using Xeno.Core.Settings;

    /// <summary>
    /// Mailgun email provider.
    /// </summary>
    public class MailgunEmailProvider : IEmailProvider
    {
        private readonly MailgunEmailSettings _Settings;
        private readonly MailgunSender _Sender;

        /// <summary>
        /// Instantiate the provider.
        /// </summary>
        /// <param name="settings">Mailgun settings.</param>
        public MailgunEmailProvider(MailgunEmailSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            if (String.IsNullOrWhiteSpace(_Settings.Domain)) throw new ArgumentException("Mailgun domain is required.", nameof(settings));
            if (String.IsNullOrWhiteSpace(_Settings.ApiKey)) throw new ArgumentException("Mailgun API key is required.", nameof(settings));
            _Sender = new MailgunSender(_Settings.Domain, _Settings.ApiKey);
        }

        /// <summary>
        /// Send an email message.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Send result.</returns>
        public Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (message == null) throw new ArgumentNullException(nameof(message));
            ValidateMessage(message);

            try
            {
                string providerMessageId = _Sender.Send(
                    JoinRecipients(message.To),
                    message.From.ToString(),
                    message.Subject,
                    String.IsNullOrWhiteSpace(message.HtmlBody) ? message.TextBody : message.HtmlBody,
                    !String.IsNullOrWhiteSpace(message.HtmlBody),
                    JoinRecipients(message.Cc),
                    JoinRecipients(message.Bcc));

                EmailSendResult result = new EmailSendResult
                {
                    Accepted = !String.IsNullOrWhiteSpace(providerMessageId),
                    Skipped = false,
                    Provider = "Mailgun",
                    ProviderMessageId = providerMessageId ?? String.Empty,
                    StatusMessage = String.IsNullOrWhiteSpace(providerMessageId) ? "Mailgun returned an empty message identifier." : "Email accepted by Mailgun.",
                    TemplateId = message.TemplateId ?? String.Empty,
                    SentUtc = DateTime.UtcNow
                };
                return Task.FromResult(result);
            }
            catch (Exception ex) when (ex is not EmailDeliveryException)
            {
                throw new EmailDeliveryException("Mailgun email delivery failed for template '" + (message.TemplateId ?? String.Empty) + "' and subject '" + message.Subject + "'.", ex);
            }
        }

        private static void ValidateMessage(EmailMessage message)
        {
            if (message.To.Count == 0 && message.Cc.Count == 0 && message.Bcc.Count == 0) throw new InvalidOperationException("Email message requires at least one recipient.");
            if (String.IsNullOrWhiteSpace(message.From.Address)) throw new InvalidOperationException("Email message sender is required.");
            if (String.IsNullOrWhiteSpace(message.Subject)) throw new InvalidOperationException("Email message subject is required.");
            if (String.IsNullOrWhiteSpace(message.HtmlBody) && String.IsNullOrWhiteSpace(message.TextBody)) throw new InvalidOperationException("Email message body is required.");
        }

        private static string JoinRecipients(System.Collections.Generic.List<EmailAddress> recipients)
        {
            return String.Join(",", recipients.Select(item => item.ToString()));
        }
    }
}

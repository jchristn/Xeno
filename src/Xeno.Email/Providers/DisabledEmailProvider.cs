namespace Xeno.Email.Providers
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Disabled email provider.
    /// </summary>
    public class DisabledEmailProvider : IEmailProvider
    {
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

            EmailSendResult result = new EmailSendResult
            {
                Accepted = false,
                Skipped = true,
                Provider = "Disabled",
                ProviderMessageId = String.Empty,
                StatusMessage = "Email delivery is disabled.",
                TemplateId = message.TemplateId ?? String.Empty,
                SentUtc = DateTime.UtcNow
            };
            return Task.FromResult(result);
        }
    }
}

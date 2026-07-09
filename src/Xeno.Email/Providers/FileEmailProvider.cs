namespace Xeno.Email.Providers
{
    using System;
    using System.IO;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Serialization;
    using Xeno.Core.Settings;

    /// <summary>
    /// File email provider.
    /// </summary>
    public class FileEmailProvider : IEmailProvider
    {
        private readonly EmailFileSettings _Settings;

        /// <summary>
        /// Instantiate the provider.
        /// </summary>
        /// <param name="settings">File settings.</param>
        public FileEmailProvider(EmailFileSettings settings)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        /// <summary>
        /// Send an email message.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Send result.</returns>
        public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (message == null) throw new ArgumentNullException(nameof(message));
            ValidateMessage(message);

            string directory = String.IsNullOrWhiteSpace(_Settings.Directory) ? "data/email-outbox" : _Settings.Directory;
            Directory.CreateDirectory(directory);
            string id = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", System.Globalization.CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N");
            string basePath = Path.Combine(directory, id);
            EmailMessageMetadata metadata = EmailMessageMetadata.FromMessage(message, "File", id);

            await File.WriteAllTextAsync(basePath + ".json", JsonUtil.Serialize(metadata, true), Encoding.UTF8, token).ConfigureAwait(false);
            await File.WriteAllTextAsync(basePath + ".html", message.HtmlBody, Encoding.UTF8, token).ConfigureAwait(false);
            await File.WriteAllTextAsync(basePath + ".txt", message.TextBody, Encoding.UTF8, token).ConfigureAwait(false);

            return new EmailSendResult
            {
                Accepted = true,
                Skipped = false,
                Provider = "File",
                ProviderMessageId = id,
                StatusMessage = "Email written to " + basePath + ".*.",
                TemplateId = message.TemplateId ?? String.Empty,
                SentUtc = DateTime.UtcNow
            };
        }

        private static void ValidateMessage(EmailMessage message)
        {
            if (message.To.Count == 0 && message.Cc.Count == 0 && message.Bcc.Count == 0) throw new InvalidOperationException("Email message requires at least one recipient.");
            if (String.IsNullOrWhiteSpace(message.Subject)) throw new InvalidOperationException("Email message subject is required.");
            if (String.IsNullOrWhiteSpace(message.HtmlBody) && String.IsNullOrWhiteSpace(message.TextBody)) throw new InvalidOperationException("Email message body is required.");
        }
    }
}

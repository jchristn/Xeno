namespace Xeno.Email
{
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Provider-neutral email delivery interface.
    /// </summary>
    public interface IEmailProvider
    {
        /// <summary>
        /// Send an email message.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Send result.</returns>
        Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken token = default);
    }
}

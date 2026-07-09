namespace Xeno.Email
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Email.Templates;
    using Xeno.Email.Workflows;

    /// <summary>
    /// Xeno email service abstraction.
    /// </summary>
    public interface IXenoEmailService
    {
        /// <summary>
        /// Send an email message.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Send result.</returns>
        Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken token = default);

        /// <summary>
        /// Send onboarding notifications.
        /// </summary>
        /// <param name="context">Onboarding context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Send results.</returns>
        Task<IReadOnlyList<EmailSendResult>> SendOnboardingNotificationsAsync(OnboardingEmailContext context, CancellationToken token = default);

        /// <summary>
        /// Render a template.
        /// </summary>
        /// <typeparam name="TContext">Template context type.</typeparam>
        /// <param name="template">Template.</param>
        /// <param name="context">Template context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rendered result.</returns>
        Task<EmailTemplateRenderResult> RenderTemplateAsync<TContext>(IEmailTemplate<TContext> template, TContext context, CancellationToken token = default);
    }
}

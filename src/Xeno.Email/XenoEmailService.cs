namespace Xeno.Email
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Settings;
    using Xeno.Email.Templates;
    using Xeno.Email.Workflows;

    /// <summary>
    /// Xeno email service.
    /// </summary>
    public class XenoEmailService : IXenoEmailService
    {
        private readonly IEmailProvider _Provider;
        private readonly OnboardingEmailWorkflow _OnboardingWorkflow;

        /// <summary>
        /// Instantiate the service.
        /// </summary>
        /// <param name="settings">Email settings.</param>
        /// <param name="provider">Provider.</param>
        public XenoEmailService(EmailSettings settings, IEmailProvider provider)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            _Provider = provider ?? throw new ArgumentNullException(nameof(provider));
            _OnboardingWorkflow = new OnboardingEmailWorkflow(settings, _Provider);
        }

        /// <summary>
        /// Send an email message.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Send result.</returns>
        public async Task<EmailSendResult> SendAsync(EmailMessage message, CancellationToken token = default)
        {
            return await _Provider.SendAsync(message, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Send onboarding notifications.
        /// </summary>
        /// <param name="context">Onboarding context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Send results.</returns>
        public async Task<IReadOnlyList<EmailSendResult>> SendOnboardingNotificationsAsync(OnboardingEmailContext context, CancellationToken token = default)
        {
            return await _OnboardingWorkflow.SendAsync(context, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Render a template.
        /// </summary>
        /// <typeparam name="TContext">Template context type.</typeparam>
        /// <param name="template">Template.</param>
        /// <param name="context">Template context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rendered result.</returns>
        public async Task<EmailTemplateRenderResult> RenderTemplateAsync<TContext>(IEmailTemplate<TContext> template, TContext context, CancellationToken token = default)
        {
            if (template == null) throw new ArgumentNullException(nameof(template));
            return await template.RenderAsync(context, token).ConfigureAwait(false);
        }
    }
}

namespace Xeno.Email.Workflows
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Settings;
    using Xeno.Email.Templates;

    /// <summary>
    /// Onboarding email notification workflow.
    /// </summary>
    public class OnboardingEmailWorkflow
    {
        private readonly EmailSettings _Settings;
        private readonly IEmailProvider _Provider;

        /// <summary>
        /// Instantiate the workflow.
        /// </summary>
        /// <param name="settings">Email settings.</param>
        /// <param name="provider">Email provider.</param>
        public OnboardingEmailWorkflow(EmailSettings settings, IEmailProvider provider)
        {
            _Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _Provider = provider ?? throw new ArgumentNullException(nameof(provider));
        }

        /// <summary>
        /// Send onboarding notifications.
        /// </summary>
        /// <param name="context">Onboarding context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Send results.</returns>
        public async Task<IReadOnlyList<EmailSendResult>> SendAsync(OnboardingEmailContext context, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (context == null) throw new ArgumentNullException(nameof(context));

            List<EmailSendResult> results = new List<EmailSendResult>();
            if (!_Settings.Enabled)
            {
                return results;
            }

            if (context.CreatedTenant && _Settings.Notifications.NotifyPlatformAdministratorsOnTenantCreated)
            {
                await SendToRecipientsAsync(new OnboardingAdminAlertEmailTemplate(_Settings), context, context.PlatformAdministrators, results, token).ConfigureAwait(false);
            }

            if (!context.CreatedTenant && _Settings.Notifications.NotifyPlatformAdministratorsOnUserJoined)
            {
                await SendToRecipientsAsync(new OnboardingAdminAlertEmailTemplate(_Settings), context, context.PlatformAdministrators, results, token).ConfigureAwait(false);
            }

            if (!context.CreatedTenant && _Settings.Notifications.NotifyTenantAdministratorsOnUserJoined)
            {
                List<EmailAddress> recipients = context.TenantAdministrators
                    .Where(item => !String.Equals(item.Address, context.UserEmail, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                await SendToRecipientsAsync(new TenantUserJoinedEmailTemplate(_Settings), context, recipients, results, token).ConfigureAwait(false);
            }

            if (context.CreatedTenant && _Settings.Notifications.NotifyNewTenantAdministrator)
            {
                await SendToRecipientsAsync(new TenantCreatedWelcomeEmailTemplate(_Settings), context, new List<EmailAddress> { EmailAddress.Create(context.UserEmail, context.Username) }, results, token).ConfigureAwait(false);
            }

            if (!context.CreatedTenant && _Settings.Notifications.NotifyNewTenantMember)
            {
                await SendToRecipientsAsync(new TenantMemberWelcomeEmailTemplate(_Settings), context, new List<EmailAddress> { EmailAddress.Create(context.UserEmail, context.Username) }, results, token).ConfigureAwait(false);
            }

            return results;
        }

        private async Task SendToRecipientsAsync(IEmailTemplate<OnboardingEmailContext> template, OnboardingEmailContext context, List<EmailAddress> recipients, List<EmailSendResult> results, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (recipients == null || recipients.Count == 0) return;

            EmailTemplateRenderResult rendered = await template.RenderAsync(context, token).ConfigureAwait(false);
            EmailMessage message = new EmailMessage
            {
                From = EmailAddress.Create(_Settings.FromAddress, _Settings.FromName),
                ReplyTo = String.IsNullOrWhiteSpace(_Settings.ReplyToAddress) ? null : EmailAddress.Create(_Settings.ReplyToAddress),
                To = recipients,
                Subject = rendered.Subject,
                HtmlBody = rendered.HtmlBody,
                TextBody = rendered.TextBody,
                TemplateId = rendered.TemplateId,
                CorrelationId = context.CorrelationId
            };
            message.Tags["workflow"] = "onboarding";
            message.Tags["template"] = rendered.TemplateId;
            message.Tags["tenantId"] = context.TenantId;
            EmailSendResult result = await _Provider.SendAsync(message, token).ConfigureAwait(false);
            results.Add(result);
        }
    }
}

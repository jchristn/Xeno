namespace Xeno.Email.Templates
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Settings;
    using Xeno.Email.Workflows;

    /// <summary>
    /// New tenant administrator welcome template.
    /// </summary>
    public class TenantCreatedWelcomeEmailTemplate : EmailTemplateBase<OnboardingEmailContext>
    {
        /// <summary>
        /// Instantiate the template.
        /// </summary>
        /// <param name="settings">Email settings.</param>
        public TenantCreatedWelcomeEmailTemplate(EmailSettings settings) : base(settings)
        {
        }

        /// <summary>
        /// Template identifier.
        /// </summary>
        public override string TemplateId => "onboarding.tenant-created-welcome.v1";

        /// <summary>
        /// Render the template.
        /// </summary>
        /// <param name="context">Template context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rendered content.</returns>
        public override Task<EmailTemplateRenderResult> RenderAsync(OnboardingEmailContext context, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (context == null) throw new ArgumentNullException(nameof(context));

            EmailTemplateField[] fields = new EmailTemplateField[]
            {
                EmailTemplateField.Create("Tenant", context.TenantName),
                EmailTemplateField.Create("Tenant ID", context.TenantId),
                EmailTemplateField.Create("Username", context.Username)
            };
            string bodyHtml =
                RenderHeading("Your Xeno tenant is ready") +
                RenderParagraph("Your organization has been created. Use the dashboard to configure clients, credentials, roles, routing, and request history.") +
                RenderKeyValueTable(fields);
            string bodyText = "Your Xeno tenant is ready" + Environment.NewLine + Environment.NewLine + RenderKeyValueText(fields);
            string ctaUrl = String.IsNullOrWhiteSpace(Settings.ApplicationBaseUrl) ? String.Empty : Settings.ApplicationBaseUrl.TrimEnd('/');
            EmailTemplateRenderResult result = RenderLayout("Your Xeno tenant is ready", "Your Xeno tenant is ready", "Your Xeno tenant is ready.", bodyHtml, bodyText, "Open Xeno", ctaUrl);
            return Task.FromResult(result);
        }
    }
}

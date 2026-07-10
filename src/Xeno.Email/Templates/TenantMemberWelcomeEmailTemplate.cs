namespace Xeno.Email.Templates
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Settings;
    using Xeno.Email.Workflows;

    /// <summary>
    /// New tenant member welcome template.
    /// </summary>
    public class TenantMemberWelcomeEmailTemplate : EmailTemplateBase<OnboardingEmailContext>
    {
        /// <summary>
        /// Instantiate the template.
        /// </summary>
        /// <param name="settings">Email settings.</param>
        public TenantMemberWelcomeEmailTemplate(EmailSettings settings) : base(settings)
        {
        }

        /// <summary>
        /// Template identifier.
        /// </summary>
        public override string TemplateId => "onboarding.tenant-member-welcome.v1";

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

            string bodyHtml = RenderHeading("Welcome to Xeno") + RenderParagraph("Your account was created for " + context.TenantName + ".");
            string bodyText = "Welcome to Xeno" + Environment.NewLine + Environment.NewLine + "Your account was created for " + context.TenantName + ".";
            string ctaUrl = String.IsNullOrWhiteSpace(Settings.ApplicationBaseUrl) ? String.Empty : Settings.ApplicationBaseUrl.TrimEnd('/');
            EmailTemplateRenderResult result = RenderLayout("Welcome to Xeno", "Welcome to Xeno", "Your Xeno account is ready.", bodyHtml, bodyText, "Open Xeno", ctaUrl);
            return Task.FromResult(result);
        }
    }
}

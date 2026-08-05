namespace Xeno.Email.Templates
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Settings;

    /// <summary>
    /// Approval template sent when an administrator activates an onboarded tenant.
    /// </summary>
    public class TenantApprovedEmailTemplate : EmailTemplateBase<TenantApprovedEmailContext>
    {
        /// <summary>
        /// Instantiate the template.
        /// </summary>
        /// <param name="settings">Email settings.</param>
        public TenantApprovedEmailTemplate(EmailSettings settings) : base(settings)
        {
        }

        /// <summary>
        /// Template identifier.
        /// </summary>
        public override string TemplateId => "onboarding.tenant-approved.v1";

        /// <summary>
        /// Render the template.
        /// </summary>
        /// <param name="context">Template context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rendered content.</returns>
        public override Task<EmailTemplateRenderResult> RenderAsync(TenantApprovedEmailContext context, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (context == null) throw new ArgumentNullException(nameof(context));

            string greetingName = String.IsNullOrWhiteSpace(context.ContactName) ? "there" : context.ContactName;
            EmailTemplateField[] fields = new EmailTemplateField[]
            {
                EmailTemplateField.Create("Organization", context.Organization),
                EmailTemplateField.Create("Sign-in email", context.Email)
            };
            string bodyHtml =
                RenderHeading("Your account is approved") +
                RenderParagraph("Hi " + EmailHtmlEncoder.Encode(greetingName) + ", your Xeno Cloud account for " + EmailHtmlEncoder.Encode(context.Organization) + " has been approved and is now active.") +
                RenderParagraph("You can sign in and start connecting machines, advertising models, and routing inference across your fleet.") +
                RenderKeyValueTable(fields);
            string bodyText =
                "Your account is approved" + Environment.NewLine + Environment.NewLine +
                "Hi " + greetingName + ", your Xeno Cloud account for " + context.Organization + " has been approved and is now active." + Environment.NewLine +
                "Sign in: " + context.SignInUrl + Environment.NewLine + Environment.NewLine +
                RenderKeyValueText(fields);

            EmailTemplateRenderResult result = RenderLayout(
                "Your account is approved",
                "Your Xeno Cloud account is approved",
                "Your Xeno Cloud account is active. Sign in to get started.",
                bodyHtml,
                bodyText,
                "Sign in to Xeno",
                context.SignInUrl);
            return Task.FromResult(result);
        }
    }
}

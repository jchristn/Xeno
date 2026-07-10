namespace Xeno.Email.Templates
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Settings;
    using Xeno.Email.Workflows;

    /// <summary>
    /// Tenant administrator user-joined notification template.
    /// </summary>
    public class TenantUserJoinedEmailTemplate : EmailTemplateBase<OnboardingEmailContext>
    {
        /// <summary>
        /// Instantiate the template.
        /// </summary>
        /// <param name="settings">Email settings.</param>
        public TenantUserJoinedEmailTemplate(EmailSettings settings) : base(settings)
        {
        }

        /// <summary>
        /// Template identifier.
        /// </summary>
        public override string TemplateId => "onboarding.tenant-user-joined.v1";

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

            string subject = "A user joined " + context.TenantName;
            EmailTemplateField[] fields = new EmailTemplateField[]
            {
                EmailTemplateField.Create("Tenant", context.TenantName),
                EmailTemplateField.Create("Username", context.Username),
                EmailTemplateField.Create("Email", context.UserEmail),
                EmailTemplateField.Create("Event UTC", context.EventUtc.ToString("O", CultureInfo.InvariantCulture))
            };
            string bodyHtml =
                RenderHeading("A user joined your organization") +
                RenderParagraph("A new user account was created in your Xeno tenant.") +
                RenderKeyValueTable(fields);
            string bodyText = "A user joined your organization" + Environment.NewLine + Environment.NewLine + RenderKeyValueText(fields);
            EmailTemplateRenderResult result = RenderLayout("A user joined your organization", subject, subject, bodyHtml, bodyText);
            return Task.FromResult(result);
        }
    }
}

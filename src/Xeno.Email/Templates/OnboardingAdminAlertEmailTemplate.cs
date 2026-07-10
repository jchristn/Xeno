namespace Xeno.Email.Templates
{
    using System;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Settings;
    using Xeno.Email.Workflows;

    /// <summary>
    /// Platform administrator onboarding alert template.
    /// </summary>
    public class OnboardingAdminAlertEmailTemplate : EmailTemplateBase<OnboardingEmailContext>
    {
        /// <summary>
        /// Instantiate the template.
        /// </summary>
        /// <param name="settings">Email settings.</param>
        public OnboardingAdminAlertEmailTemplate(EmailSettings settings) : base(settings)
        {
        }

        /// <summary>
        /// Template identifier.
        /// </summary>
        public override string TemplateId => "onboarding.admin-alert.v1";

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

            string action = context.CreatedTenant ? "New tenant onboarded" : "New user onboarded";
            EmailTemplateField[] fields = BuildFields(context);
            string bodyHtml = RenderHeading(action) + RenderParagraph("An onboarding workflow completed in Xeno.") + RenderKeyValueTable(fields);
            string bodyText = action + Environment.NewLine + Environment.NewLine + RenderKeyValueText(fields);
            EmailTemplateRenderResult result = RenderLayout(action, action, action, bodyHtml, bodyText);
            return Task.FromResult(result);
        }

        private static EmailTemplateField[] BuildFields(OnboardingEmailContext context)
        {
            return new EmailTemplateField[]
            {
                EmailTemplateField.Create("Tenant ID", context.TenantId),
                EmailTemplateField.Create("Tenant", context.TenantName),
                EmailTemplateField.Create("User ID", context.UserId),
                EmailTemplateField.Create("Username", context.Username),
                EmailTemplateField.Create("Email", context.UserEmail),
                EmailTemplateField.Create("Tenant admin", context.IsTenantAdmin ? "yes" : "no"),
                EmailTemplateField.Create("Source IP", context.SourceIp),
                EmailTemplateField.Create("Event UTC", context.EventUtc.ToString("O", CultureInfo.InvariantCulture)),
                EmailTemplateField.Create("Correlation ID", context.CorrelationId)
            };
        }
    }
}

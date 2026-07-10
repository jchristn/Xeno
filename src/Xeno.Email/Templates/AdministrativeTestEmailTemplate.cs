namespace Xeno.Email.Templates
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Settings;

    /// <summary>
    /// Administrative test email template.
    /// </summary>
    public class AdministrativeTestEmailTemplate : EmailTemplateBase<AdministrativeTestEmailTemplateContext>
    {
        /// <summary>
        /// Instantiate the template.
        /// </summary>
        /// <param name="settings">Email settings.</param>
        public AdministrativeTestEmailTemplate(EmailSettings settings) : base(settings)
        {
        }

        /// <summary>
        /// Template identifier.
        /// </summary>
        public override string TemplateId
        {
            get
            {
                return "administrative-test";
            }
        }

        /// <summary>
        /// Render the template.
        /// </summary>
        /// <param name="context">Template context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rendered content.</returns>
        public override Task<EmailTemplateRenderResult> RenderAsync(AdministrativeTestEmailTemplateContext context, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (context == null) throw new ArgumentNullException(nameof(context));

            string title = String.IsNullOrWhiteSpace(context.Title) ? "Xeno Test Email" : context.Title.Trim();
            string subject = String.IsNullOrWhiteSpace(context.Subject) ? "Xeno test email" : context.Subject.Trim();
            string body = String.IsNullOrWhiteSpace(context.Body) ? "(empty)" : context.Body.Trim();
            EmailTemplateRenderResult result = RenderLayout(
                title,
                subject,
                "Administrative test email from Xeno.",
                RenderParagraph(body),
                body);
            return Task.FromResult(result);
        }
    }
}

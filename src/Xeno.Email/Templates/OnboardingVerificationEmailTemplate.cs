namespace Xeno.Email.Templates
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Settings;

    /// <summary>
    /// Email verification template sent when a prospective organization signs up for Xeno Cloud.
    /// </summary>
    public class OnboardingVerificationEmailTemplate : EmailTemplateBase<OnboardingVerificationEmailContext>
    {
        /// <summary>
        /// Instantiate the template.
        /// </summary>
        /// <param name="settings">Email settings.</param>
        public OnboardingVerificationEmailTemplate(EmailSettings settings) : base(settings)
        {
        }

        /// <summary>
        /// Template identifier.
        /// </summary>
        public override string TemplateId => "onboarding.verify-email.v1";

        /// <summary>
        /// Render the template.
        /// </summary>
        /// <param name="context">Template context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rendered content.</returns>
        public override Task<EmailTemplateRenderResult> RenderAsync(OnboardingVerificationEmailContext context, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            if (context == null) throw new ArgumentNullException(nameof(context));

            string greetingName = String.IsNullOrWhiteSpace(context.ContactName) ? "there" : context.ContactName;
            string bodyHtml =
                RenderHeading("Verify your email") +
                RenderParagraph("Hi " + EmailHtmlEncoder.Encode(greetingName) + ", thanks for requesting early access to Xeno Cloud for " + EmailHtmlEncoder.Encode(context.Organization) + ".") +
                RenderParagraph("Please confirm your email address to continue. After you verify, our team will review your request and email you when your account is approved.") +
                RenderParagraph("If the button does not work, copy and paste this link into your browser:") +
                RenderParagraph(EmailHtmlEncoder.Encode(context.VerificationUrl));
            string bodyText =
                "Verify your email" + Environment.NewLine + Environment.NewLine +
                "Hi " + greetingName + ", thanks for requesting early access to Xeno Cloud for " + context.Organization + "." + Environment.NewLine +
                "Confirm your email address to continue: " + context.VerificationUrl + Environment.NewLine +
                "After you verify, we will review your request and email you when your account is approved.";

            EmailTemplateRenderResult result = RenderLayout(
                "Verify your email",
                "Verify your email for Xeno Cloud",
                "Confirm your email to continue your Xeno Cloud request.",
                bodyHtml,
                bodyText,
                "Verify email",
                context.VerificationUrl);
            return Task.FromResult(result);
        }
    }
}

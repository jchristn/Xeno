namespace Xeno.Email.Templates
{
    using System;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Settings;

    /// <summary>
    /// Base email template.
    /// </summary>
    /// <typeparam name="TContext">Template context type.</typeparam>
    public abstract class EmailTemplateBase<TContext> : IEmailTemplate<TContext>
    {
        /// <summary>
        /// Email settings.
        /// </summary>
        protected EmailSettings Settings { get; }

        /// <summary>
        /// Email theme.
        /// </summary>
        protected EmailTemplateTheme Theme { get; }

        /// <summary>
        /// Instantiate the template.
        /// </summary>
        /// <param name="settings">Email settings.</param>
        protected EmailTemplateBase(EmailSettings settings)
        {
            Settings = settings ?? throw new ArgumentNullException(nameof(settings));
            Theme = new EmailTemplateTheme();
        }

        /// <summary>
        /// Template identifier.
        /// </summary>
        public abstract string TemplateId { get; }

        /// <summary>
        /// Render the template.
        /// </summary>
        /// <param name="context">Template context.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Rendered content.</returns>
        public abstract Task<EmailTemplateRenderResult> RenderAsync(TContext context, CancellationToken token = default);

        /// <summary>
        /// Render the shared email layout.
        /// </summary>
        /// <param name="subject">Subject.</param>
        /// <param name="preheader">Preheader.</param>
        /// <param name="bodyHtml">Body HTML.</param>
        /// <param name="bodyText">Body text.</param>
        /// <param name="ctaText">Optional call-to-action text.</param>
        /// <param name="ctaUrl">Optional call-to-action URL.</param>
        /// <returns>Rendered result.</returns>
        protected EmailTemplateRenderResult RenderLayout(string subject, string preheader, string bodyHtml, string bodyText, string? ctaText = null, string? ctaUrl = null)
        {
            string finalSubject = String.IsNullOrWhiteSpace(Settings.SubjectPrefix) ? subject : Settings.SubjectPrefix.Trim() + " " + subject;
            string supportAddress = String.IsNullOrWhiteSpace(Settings.SupportAddress) ? Settings.ReplyToAddress : Settings.SupportAddress;
            string encodedSupportAddress = EmailHtmlEncoder.Encode(supportAddress);
            string supportHref = "mailto:" + EmailHtmlEncoder.Attribute(supportAddress);
            string ctaHtml = String.Empty;
            string ctaTextBlock = String.Empty;
            if (!String.IsNullOrWhiteSpace(ctaText) && !String.IsNullOrWhiteSpace(ctaUrl))
            {
                ctaHtml = "<p style=\"margin:24px 0;\"><a href=\"" + EmailHtmlEncoder.Attribute(ctaUrl) + "\" style=\"display:inline-block;background:" + Theme.AccentColor + ";color:#ffffff;text-decoration:none;padding:10px 14px;border-radius:6px;font-weight:700;\">" + EmailHtmlEncoder.Encode(ctaText) + "</a></p>";
                ctaTextBlock = Environment.NewLine + ctaText + ": " + ctaUrl + Environment.NewLine;
            }

            string html =
                "<!doctype html><html><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>" + EmailHtmlEncoder.Encode(finalSubject) + "</title></head>" +
                "<body style=\"margin:0;background:" + Theme.BackgroundColor + ";font-family:Arial,Helvetica,sans-serif;color:" + Theme.TextColor + ";\">" +
                "<span style=\"display:none!important;color:transparent;height:0;max-height:0;max-width:0;opacity:0;overflow:hidden;\">" + EmailHtmlEncoder.Encode(preheader) + "</span>" +
                "<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"background:" + Theme.BackgroundColor + ";padding:24px 0;\"><tr><td align=\"center\">" +
                "<table role=\"presentation\" width=\"640\" cellspacing=\"0\" cellpadding=\"0\" style=\"width:640px;max-width:94%;background:" + Theme.SurfaceColor + ";border:1px solid " + Theme.BorderColor + ";border-radius:8px;overflow:hidden;\">" +
                "<tr><td style=\"padding:20px 24px;border-bottom:1px solid " + Theme.BorderColor + ";font-size:20px;font-weight:700;\">" + EmailHtmlEncoder.Encode(Theme.BrandName) + "</td></tr>" +
                "<tr><td style=\"padding:24px;font-size:14px;line-height:1.55;\">" + bodyHtml + ctaHtml + "</td></tr>" +
                "<tr><td style=\"padding:18px 24px;border-top:1px solid " + Theme.BorderColor + ";font-size:12px;line-height:1.45;color:" + Theme.MutedTextColor + ";\">Questions? Email <a href=\"" + supportHref + "\" style=\"color:" + Theme.AccentColor + ";\">" + encodedSupportAddress + "</a>.</td></tr>" +
                "</table></td></tr></table></body></html>";

            string text = bodyText + ctaTextBlock + Environment.NewLine + "Questions? Email " + supportAddress + ".";

            return new EmailTemplateRenderResult
            {
                TemplateId = TemplateId,
                Subject = finalSubject,
                HtmlBody = BoundBody(html),
                TextBody = BoundBody(text)
            };
        }

        /// <summary>
        /// Render a key-value HTML table.
        /// </summary>
        /// <param name="items">Items.</param>
        /// <returns>HTML table.</returns>
        protected static string RenderKeyValueTable(params EmailTemplateField[] items)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("<table role=\"presentation\" width=\"100%\" cellspacing=\"0\" cellpadding=\"0\" style=\"border-collapse:collapse;margin:16px 0;\">");
            foreach (EmailTemplateField item in items)
            {
                builder.Append("<tr><td style=\"border:1px solid #d8dee9;padding:8px;font-weight:700;width:34%;\">");
                builder.Append(EmailHtmlEncoder.Encode(item.Label));
                builder.Append("</td><td style=\"border:1px solid #d8dee9;padding:8px;\">");
                builder.Append(EmailHtmlEncoder.Encode(item.Value));
                builder.Append("</td></tr>");
            }

            builder.Append("</table>");
            return builder.ToString();
        }

        /// <summary>
        /// Render key-value text.
        /// </summary>
        /// <param name="items">Items.</param>
        /// <returns>Text.</returns>
        protected static string RenderKeyValueText(params EmailTemplateField[] items)
        {
            StringBuilder builder = new StringBuilder();
            foreach (EmailTemplateField item in items)
            {
                builder.Append(item.Label);
                builder.Append(": ");
                builder.AppendLine(item.Value);
            }

            return builder.ToString();
        }

        private string BoundBody(string body)
        {
            int maxBytes = Settings.MaxBodyBytes <= 0 ? 262144 : Settings.MaxBodyBytes;
            byte[] bytes = Encoding.UTF8.GetBytes(body);
            if (bytes.Length <= maxBytes) return body;
            return Encoding.UTF8.GetString(bytes, 0, maxBytes);
        }
    }
}

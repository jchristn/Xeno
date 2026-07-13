namespace Xeno.Email.Templates
{
    using System;
    using System.Collections.Generic;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using HtmlPageBuilder;
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
        /// <param name="title">Email title.</param>
        /// <param name="subject">Subject.</param>
        /// <param name="preheader">Preheader.</param>
        /// <param name="bodyHtml">Body HTML.</param>
        /// <param name="bodyText">Body text.</param>
        /// <param name="ctaText">Optional call-to-action text.</param>
        /// <param name="ctaUrl">Optional call-to-action URL.</param>
        /// <returns>Rendered result.</returns>
        protected EmailTemplateRenderResult RenderLayout(string title, string subject, string preheader, string bodyHtml, string bodyText, string? ctaText = null, string? ctaUrl = null)
        {
            if (String.IsNullOrWhiteSpace(title)) throw new ArgumentException("Email title is required.", nameof(title));

            string finalSubject = String.IsNullOrWhiteSpace(Settings.SubjectPrefix) ? subject : Settings.SubjectPrefix.Trim() + " " + subject;
            string supportAddress = String.IsNullOrWhiteSpace(Settings.SupportAddress) ? Settings.ReplyToAddress : Settings.SupportAddress;
            string encodedSupportAddress = EmailHtmlEncoder.Encode(supportAddress);
            string supportHref = "mailto:" + EmailHtmlEncoder.Attribute(supportAddress);
            HtmlBody emailBody = new HtmlBody();
            HtmlPage page = new HtmlPage();
            page.Head.Title = EmailHtmlEncoder.Encode(finalSubject);
            page.Head.AdditionalElements.Add("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            string ctaTextBlock = String.Empty;
            string contentHtml = bodyHtml;
            if (!String.IsNullOrWhiteSpace(ctaText) && !String.IsNullOrWhiteSpace(ctaUrl))
            {
                string encodedCtaText = EmailHtmlEncoder.Encode(ctaText);
                string encodedCtaUrl = EmailHtmlEncoder.Attribute(ctaUrl);
                contentHtml += emailBody.Paragraph(
                    emailBody.Link(
                        encodedCtaText,
                        encodedCtaUrl,
                        false,
                        null,
                        null,
                        "display:inline-block;background:" + Theme.AccentColor + ";color:#ffffff;text-decoration:none;padding:10px 14px;border-radius:6px;font-weight:700;"),
                    null,
                    null,
                    "margin:24px 0;");
                ctaTextBlock = Environment.NewLine + ctaText + ": " + ctaUrl + Environment.NewLine;
            }

            string headerHtml =
                emailBody.Image(
                    EmailHtmlEncoder.Attribute(Theme.BrandName + " logo"),
                    EmailHtmlEncoder.Attribute(Theme.LogoUrl),
                    null,
                    null,
                    "display:block;width:64px;height:64px;max-width:96px;max-height:96px;margin:0 auto 12px;") +
                emailBody.Div(EmailHtmlEncoder.Encode(title), null, null, "font-size:22px;line-height:1.25;font-weight:700;text-align:center;color:" + Theme.TextColor + ";");
            string footerLinks =
                emailBody.Link("Home", EmailHtmlEncoder.Attribute(Theme.HomeUrl), false, null, null, "color:" + Theme.AccentColor + ";") +
                " &nbsp;|&nbsp; " +
                emailBody.Link("Dashboard", EmailHtmlEncoder.Attribute(Theme.DashboardUrl), false, null, null, "color:" + Theme.AccentColor + ";") +
                " &nbsp;|&nbsp; " +
                emailBody.Link("GitHub", EmailHtmlEncoder.Attribute(Theme.GitHubUrl), false, null, null, "color:" + Theme.AccentColor + ";");
            string footerHtml = footerLinks + emailBody.Div("Questions? Email " + emailBody.Link(encodedSupportAddress, supportHref, false, null, null, "color:" + Theme.AccentColor + ";") + ".", null, null, "margin-top:10px;");
            page.Body.Content =
                emailBody.Div(EmailHtmlEncoder.Encode(preheader), null, null, "display:none!important;color:transparent;height:0;max-height:0;max-width:0;opacity:0;overflow:hidden;") +
                emailBody.Div(
                    emailBody.Div(
                        emailBody.Div(headerHtml, null, null, "padding:20px 24px;border-bottom:1px solid " + Theme.BorderColor + ";") +
                        emailBody.Div(contentHtml, null, null, "padding:24px;font-size:14px;line-height:1.55;") +
                        emailBody.Div(footerHtml, null, null, "padding:18px 24px;border-top:1px solid " + Theme.BorderColor + ";font-size:12px;line-height:1.45;color:" + Theme.MutedTextColor + ";"),
                        null,
                        null,
                        "width:640px;max-width:94%;background:" + Theme.SurfaceColor + ";border:1px solid " + Theme.BorderColor + ";border-radius:8px;overflow:hidden;margin:0 auto;"),
                    null,
                    null,
                    "background:" + Theme.BackgroundColor + ";font-family:Arial,Helvetica,sans-serif;color:" + Theme.TextColor + ";padding:24px 0;");

            string text =
                title +
                Environment.NewLine +
                Environment.NewLine +
                bodyText +
                ctaTextBlock +
                Environment.NewLine +
                "Home: " + Theme.HomeUrl +
                Environment.NewLine +
                "Dashboard: " + Theme.DashboardUrl +
                Environment.NewLine +
                "GitHub: " + Theme.GitHubUrl +
                Environment.NewLine +
                "Questions? Email " + supportAddress + ".";

            return new EmailTemplateRenderResult
            {
                TemplateId = TemplateId,
                Subject = finalSubject,
                HtmlBody = BoundBody(page.ToString()),
                TextBody = BoundBody(text)
            };
        }

        /// <summary>
        /// Render an HTML heading.
        /// </summary>
        /// <param name="text">Heading text.</param>
        /// <returns>HTML heading.</returns>
        protected static string RenderHeading(string text)
        {
            HtmlBody body = new HtmlBody();
            return body.H1Text(EmailHtmlEncoder.Encode(text), null, null, "font-size:20px;line-height:1.3;margin:0 0 12px;");
        }

        /// <summary>
        /// Render an HTML paragraph.
        /// </summary>
        /// <param name="text">Paragraph text.</param>
        /// <returns>HTML paragraph.</returns>
        protected static string RenderParagraph(string text)
        {
            HtmlBody body = new HtmlBody();
            return body.Paragraph(EmailHtmlEncoder.Encode(text), null, null, "margin:0 0 12px;");
        }

        /// <summary>
        /// Render a key-value HTML table.
        /// </summary>
        /// <param name="items">Items.</param>
        /// <returns>HTML table.</returns>
        protected static string RenderKeyValueTable(params EmailTemplateField[] items)
        {
            HtmlBody body = new HtmlBody();
            string headerRow = body.ElementHtml(
                "tr",
                RenderTableHeaderCell(body, "Field") + RenderTableHeaderCell(body, "Value"),
                attributes: TableRowAttributes());

            List<string> rows = new List<string>();
            foreach (EmailTemplateField item in items)
            {
                rows.Add(body.ElementHtml(
                    "tr",
                    RenderTableCell(body, item.Label) + RenderTableCell(body, item.Value),
                    attributes: TableRowAttributes()));
            }

            string tableContent =
                body.ElementHtml("thead", headerRow) +
                body.ElementHtml("tbody", String.Join(String.Empty, rows));
            return body.ElementHtml("table", tableContent, attributes: TableAttributes());
        }

        private static string RenderTableHeaderCell(HtmlBody body, string text)
        {
            return body.ElementText("th", text, attributes: TableCellAttributes());
        }

        private static string RenderTableCell(HtmlBody body, string text)
        {
            return body.ElementText("td", text ?? String.Empty, attributes: TableCellAttributes());
        }

        private static HtmlAttributes TableAttributes()
        {
            return new HtmlAttributes()
                .Style("border-collapse:collapse;margin:16px 0;width:100%;");
        }

        private static HtmlAttributes TableRowAttributes()
        {
            return new HtmlAttributes()
                .Style("text-align:left;");
        }

        private static HtmlAttributes TableCellAttributes()
        {
            return new HtmlAttributes()
                .Set("align", "left")
                .Style("text-align:left;");
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

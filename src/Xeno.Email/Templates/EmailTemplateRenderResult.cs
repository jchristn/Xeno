namespace Xeno.Email.Templates
{
    using System;

    /// <summary>
    /// Rendered email template result.
    /// </summary>
    public class EmailTemplateRenderResult
    {
        /// <summary>
        /// Template identifier.
        /// </summary>
        public string TemplateId { get; set; } = String.Empty;

        /// <summary>
        /// Subject.
        /// </summary>
        public string Subject { get; set; } = String.Empty;

        /// <summary>
        /// HTML body.
        /// </summary>
        public string HtmlBody { get; set; } = String.Empty;

        /// <summary>
        /// Plain text body.
        /// </summary>
        public string TextBody { get; set; } = String.Empty;
    }
}

namespace Xeno.Email.Templates
{
    using System;

    /// <summary>
    /// Administrative test email template context.
    /// </summary>
    public class AdministrativeTestEmailTemplateContext
    {
        /// <summary>
        /// Email title.
        /// </summary>
        public string Title { get; set; } = "Xeno Test Email";

        /// <summary>
        /// Email subject.
        /// </summary>
        public string Subject { get; set; } = "Xeno test email";

        /// <summary>
        /// Email body.
        /// </summary>
        public string Body { get; set; } = String.Empty;
    }
}

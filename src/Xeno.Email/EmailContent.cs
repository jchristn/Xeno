namespace Xeno.Email
{
    using System;

    /// <summary>
    /// Rendered email content.
    /// </summary>
    public class EmailContent
    {
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

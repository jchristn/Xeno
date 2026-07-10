namespace Xeno.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Administrative test email request.
    /// </summary>
    public class EmailTestRequest
    {
        /// <summary>
        /// Recipient email address.
        /// </summary>
        [JsonPropertyName("toEmail")]
        public string ToEmail { get; set; } = String.Empty;

        /// <summary>
        /// Optional recipient display name.
        /// </summary>
        [JsonPropertyName("toName")]
        public string ToName { get; set; } = String.Empty;

        /// <summary>
        /// Optional email title. Defaults to <c>Xeno Test Email</c>.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = "Xeno Test Email";

        /// <summary>
        /// Optional email subject. Defaults to <c>Xeno test email</c>.
        /// </summary>
        [JsonPropertyName("subject")]
        public string Subject { get; set; } = "Xeno test email";

        /// <summary>
        /// Plain text body to render inside the standard email template.
        /// </summary>
        [JsonPropertyName("body")]
        public string Body { get; set; } = String.Empty;
    }
}

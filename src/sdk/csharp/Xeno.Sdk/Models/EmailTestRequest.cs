namespace Xeno.Sdk
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Administrative test email request DTO.
    /// </summary>
    public class EmailTestRequest
    {
        /// <summary>
        /// Gets or sets the recipient email address.
        /// </summary>
        [JsonPropertyName("toEmail")]
        public string ToEmail { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the optional recipient display name.
        /// </summary>
        [JsonPropertyName("toName")]
        public string ToName { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the email title.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title { get; set; } = "Xeno Test Email";

        /// <summary>
        /// Gets or sets the email subject.
        /// </summary>
        [JsonPropertyName("subject")]
        public string Subject { get; set; } = "Xeno test email";

        /// <summary>
        /// Gets or sets the email body.
        /// </summary>
        [JsonPropertyName("body")]
        public string Body { get; set; } = String.Empty;
    }
}

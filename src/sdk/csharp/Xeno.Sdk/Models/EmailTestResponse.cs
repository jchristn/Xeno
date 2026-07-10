namespace Xeno.Sdk
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Administrative test email response DTO.
    /// </summary>
    public class EmailTestResponse
    {
        /// <summary>
        /// Gets or sets whether the provider accepted the message.
        /// </summary>
        [JsonPropertyName("accepted")]
        public bool Accepted { get; set; }

        /// <summary>
        /// Gets or sets whether the provider skipped the message.
        /// </summary>
        [JsonPropertyName("skipped")]
        public bool Skipped { get; set; }

        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the provider message identifier.
        /// </summary>
        [JsonPropertyName("providerMessageId")]
        public string ProviderMessageId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the status message.
        /// </summary>
        [JsonPropertyName("statusMessage")]
        public string StatusMessage { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the template identifier.
        /// </summary>
        [JsonPropertyName("templateId")]
        public string TemplateId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the recipient email address.
        /// </summary>
        [JsonPropertyName("toEmail")]
        public string ToEmail { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the email subject.
        /// </summary>
        [JsonPropertyName("subject")]
        public string Subject { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the send timestamp.
        /// </summary>
        [JsonPropertyName("sentUtc")]
        public DateTime SentUtc { get; set; }
    }
}

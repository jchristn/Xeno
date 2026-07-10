namespace Xeno.Core.Models
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Administrative test email response.
    /// </summary>
    public class EmailTestResponse
    {
        /// <summary>
        /// Whether the provider accepted the message.
        /// </summary>
        [JsonPropertyName("accepted")]
        public bool Accepted { get; set; }

        /// <summary>
        /// Whether the provider intentionally skipped the message.
        /// </summary>
        [JsonPropertyName("skipped")]
        public bool Skipped { get; set; }

        /// <summary>
        /// Provider name.
        /// </summary>
        [JsonPropertyName("provider")]
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Provider message identifier.
        /// </summary>
        [JsonPropertyName("providerMessageId")]
        public string ProviderMessageId { get; set; } = String.Empty;

        /// <summary>
        /// Status message.
        /// </summary>
        [JsonPropertyName("statusMessage")]
        public string StatusMessage { get; set; } = String.Empty;

        /// <summary>
        /// Template identifier.
        /// </summary>
        [JsonPropertyName("templateId")]
        public string TemplateId { get; set; } = String.Empty;

        /// <summary>
        /// Recipient email address.
        /// </summary>
        [JsonPropertyName("toEmail")]
        public string ToEmail { get; set; } = String.Empty;

        /// <summary>
        /// Email subject.
        /// </summary>
        [JsonPropertyName("subject")]
        public string Subject { get; set; } = String.Empty;

        /// <summary>
        /// Send timestamp.
        /// </summary>
        [JsonPropertyName("sentUtc")]
        public DateTime SentUtc { get; set; } = DateTime.UtcNow;
    }
}

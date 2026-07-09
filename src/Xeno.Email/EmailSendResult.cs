namespace Xeno.Email
{
    using System;

    /// <summary>
    /// Email send result.
    /// </summary>
    public class EmailSendResult
    {
        /// <summary>
        /// Whether the provider accepted the message.
        /// </summary>
        public bool Accepted { get; set; } = false;

        /// <summary>
        /// Whether the provider intentionally skipped the message.
        /// </summary>
        public bool Skipped { get; set; } = false;

        /// <summary>
        /// Provider name.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Provider message identifier.
        /// </summary>
        public string ProviderMessageId { get; set; } = String.Empty;

        /// <summary>
        /// Status message.
        /// </summary>
        public string StatusMessage { get; set; } = String.Empty;

        /// <summary>
        /// Template identifier.
        /// </summary>
        public string TemplateId { get; set; } = String.Empty;

        /// <summary>
        /// Send timestamp.
        /// </summary>
        public DateTime SentUtc { get; set; } = DateTime.UtcNow;
    }
}

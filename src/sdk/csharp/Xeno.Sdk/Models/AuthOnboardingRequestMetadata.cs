namespace Xeno.Sdk
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Onboarding request metadata for drill-down and telemetry.
    /// </summary>
    public class AuthOnboardingRequestMetadata
    {
        /// <summary>
        /// Server request identifier.
        /// </summary>
        [JsonPropertyName("requestId")]
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Optional caller-supplied correlation identifier.
        /// </summary>
        [JsonPropertyName("correlationId")]
        public string CorrelationId { get; set; } = String.Empty;

        /// <summary>
        /// Request method.
        /// </summary>
        [JsonPropertyName("method")]
        public string Method { get; set; } = String.Empty;

        /// <summary>
        /// Request path.
        /// </summary>
        [JsonPropertyName("path")]
        public string Path { get; set; } = String.Empty;

        /// <summary>
        /// Direct source IP address observed by the server.
        /// </summary>
        [JsonPropertyName("sourceIp")]
        public string SourceIp { get; set; } = String.Empty;

        /// <summary>
        /// X-Forwarded-For header value when supplied by a trusted upstream.
        /// </summary>
        [JsonPropertyName("forwardedFor")]
        public string ForwardedFor { get; set; } = String.Empty;

        /// <summary>
        /// User-Agent header value.
        /// </summary>
        [JsonPropertyName("userAgent")]
        public string UserAgent { get; set; } = String.Empty;

        /// <summary>
        /// Origin header value.
        /// </summary>
        [JsonPropertyName("origin")]
        public string Origin { get; set; } = String.Empty;

        /// <summary>
        /// Referer header value.
        /// </summary>
        [JsonPropertyName("referer")]
        public string Referer { get; set; } = String.Empty;

        /// <summary>
        /// Host header value.
        /// </summary>
        [JsonPropertyName("host")]
        public string Host { get; set; } = String.Empty;

        /// <summary>
        /// Timestamp when onboarding completed.
        /// </summary>
        [JsonPropertyName("eventUtc")]
        public DateTime EventUtc { get; set; } = DateTime.UtcNow;
    }
}

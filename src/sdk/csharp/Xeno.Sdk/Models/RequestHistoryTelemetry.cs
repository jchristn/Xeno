namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno request history telemetry DTO.
    /// </summary>
    public class RequestHistoryTelemetry
    {
        /// <summary>
        /// Gets or sets the request bytes.
        /// </summary>
        public long RequestBytes { get; set; }

        /// <summary>
        /// Gets or sets the response bytes.
        /// </summary>
        public long ResponseBytes { get; set; }

        /// <summary>
        /// Gets or sets the prompt tokens.
        /// </summary>
        public int PromptTokens { get; set; }

        /// <summary>
        /// Gets or sets the completion tokens.
        /// </summary>
        public int CompletionTokens { get; set; }

        /// <summary>
        /// Gets or sets the total tokens.
        /// </summary>
        public int TotalTokens { get; set; }

        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        public string ProviderName { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the provider model.
        /// </summary>
        public string ProviderModel { get; set; } = String.Empty;
    }
}

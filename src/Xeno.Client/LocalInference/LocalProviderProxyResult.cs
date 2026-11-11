namespace Xeno.Client.LocalInference
{
    using System;

    /// <summary>
    /// Outcome of forwarding one request to a local provider.
    /// </summary>
    public sealed class LocalProviderProxyResult
    {
        /// <summary>
        /// True once response headers were written to the caller. After that the request can no longer fall back to the network.
        /// </summary>
        public bool Started { get; set; } = false;

        /// <summary>
        /// True when the whole response reached the caller.
        /// </summary>
        public bool Completed { get; set; } = false;

        /// <summary>
        /// Provider status code, when a response arrived.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// Failure message, empty on success. Never contains request or response content. Never null.
        /// </summary>
        public string Error { get; set; } = String.Empty;

        /// <summary>
        /// True when the provider did not answer, or finish answering, within the request timeout.
        /// </summary>
        public bool TimedOut { get; set; } = false;

        /// <summary>
        /// Milliseconds from forwarding the request until the provider's response headers arrived, when it responded.
        /// </summary>
        public double? FirstByteMs { get; set; } = null;

        /// <summary>
        /// Response bytes written to the caller.
        /// </summary>
        public long ResponseBytes { get; set; } = 0;

        /// <summary>
        /// Prompt tokens from the response usage, for complete (non-streaming) responses that report it.
        /// </summary>
        public long? PromptTokens { get; set; } = null;

        /// <summary>
        /// Completion tokens from the response usage, for complete (non-streaming) responses that report it.
        /// </summary>
        public long? CompletionTokens { get; set; } = null;
    }
}

namespace Xeno.Client.Routing
{
    using System;

    /// <summary>
    /// Metadata for one request the local inference listener handled. Never holds request or response content.
    /// </summary>
    public class LocalInferenceRequestRecord
    {
        /// <summary>
        /// Listener request ID. Never null.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// When the request arrived (UTC).
        /// </summary>
        public DateTime ReceivedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// HTTP method. Never null.
        /// </summary>
        public string Method { get; set; } = String.Empty;

        /// <summary>
        /// Route the local app called, for example "/v1/chat/completions". Never null.
        /// </summary>
        public string Path { get; set; } = String.Empty;

        /// <summary>
        /// Requested model, or empty for model lists. Never null.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Value of the X-Xeno-Route header the listener returned. Never null.
        /// </summary>
        public string Route { get; set; } = String.Empty;

        /// <summary>
        /// Local provider name, or empty for network requests. Never null.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Router reason code. Never null.
        /// </summary>
        public string Reason { get; set; } = String.Empty;

        /// <summary>
        /// HTTP status returned to the local app.
        /// </summary>
        public int StatusCode { get; set; } = 0;

        /// <summary>
        /// Whether the caller asked for a streaming response.
        /// </summary>
        public bool Stream { get; set; } = false;

        /// <summary>
        /// Whether the request fell back to the network.
        /// </summary>
        public bool Fallback { get; set; } = false;

        /// <summary>
        /// Total time in milliseconds until the response finished.
        /// </summary>
        public double LatencyMs { get; set; } = 0;

        /// <summary>
        /// Milliseconds until the first response byte, when known.
        /// </summary>
        public double? FirstByteMs { get; set; } = null;

        /// <summary>
        /// Milliseconds spent waiting for a concurrency slot.
        /// </summary>
        public double QueuedMs { get; set; } = 0;

        /// <summary>
        /// Response bytes sent to the local app, when known.
        /// </summary>
        public long? ResponseBytes { get; set; } = null;

        /// <summary>
        /// Prompt tokens the provider reported, when the response included usage.
        /// </summary>
        public long? PromptTokens { get; set; } = null;

        /// <summary>
        /// Completion tokens the provider reported, when the response included usage.
        /// </summary>
        public long? CompletionTokens { get; set; } = null;

        /// <summary>
        /// Short error description for failed requests. Never contains request or response content. Never null.
        /// </summary>
        public string Error { get; set; } = String.Empty;
    }
}

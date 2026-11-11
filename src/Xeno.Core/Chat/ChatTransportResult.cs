namespace Xeno.Core.Chat
{
    using System;

    /// <summary>
    /// Where a chat completion request went and how it ended.
    /// </summary>
    public sealed class ChatTransportResult
    {
        /// <summary>
        /// Route: "local:&lt;provider&gt;", "network", or "unavailable".
        /// </summary>
        public string Route { get; set; } = "unavailable";

        /// <summary>
        /// Local provider, or empty for the network.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// True when a local provider failed and the request was retried on the network.
        /// </summary>
        public bool Fallback { get; set; } = false;

        /// <summary>
        /// HTTP status the model returned, or null when nothing came back.
        /// </summary>
        public int? StatusCode { get; set; } = null;

        /// <summary>
        /// True when the whole response arrived.
        /// </summary>
        public bool Completed { get; set; } = false;

        /// <summary>
        /// Error, or null.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// What kind of failure <see cref="Error"/> describes, when the transport knows (a timeout or an unreachable
        /// model); null lets the chat engine work it out from the status code and what arrived.
        /// </summary>
        public ChatFailureKindEnum? FailureKind { get; set; } = null;
    }
}

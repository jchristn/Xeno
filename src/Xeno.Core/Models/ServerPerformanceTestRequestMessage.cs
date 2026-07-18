namespace Xeno.Core.Models
{
    using System;

    /// <summary>
    /// Server-to-client request to execute a model performance benchmark leg.
    /// </summary>
    public class ServerPerformanceTestRequestMessage : WebSocketMessageEnvelope
    {
        /// <summary>
        /// Performance test request ID.
        /// </summary>
        public string RequestId { get; set; } = String.Empty;

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Recipient client ID.
        /// </summary>
        public string ClientId { get; set; } = String.Empty;

        /// <summary>
        /// Benchmark mode: direct or relay.
        /// </summary>
        public string Mode { get; set; } = "direct";

        /// <summary>
        /// Model name.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Request type: chatCompletions, completions, or embeddings.
        /// </summary>
        public string RequestType { get; set; } = "chatCompletions";

        /// <summary>
        /// Number of samples to run.
        /// </summary>
        public int RequestCount { get; set; } = 1;

        /// <summary>
        /// Target client ID that hosts the model.
        /// </summary>
        public string TargetClientId { get; set; } = String.Empty;

        /// <summary>
        /// Target client name.
        /// </summary>
        public string? TargetClientName { get; set; } = null;

        /// <summary>
        /// Target client API type.
        /// </summary>
        public string? TargetApiType { get; set; } = null;

        /// <summary>
        /// Target provider source name.
        /// </summary>
        public string? TargetSource { get; set; } = null;

        /// <summary>
        /// Request timeout in milliseconds.
        /// </summary>
        public int TimeoutMs { get; set; } = 300000;
    }
}

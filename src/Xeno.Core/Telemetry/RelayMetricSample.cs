namespace Xeno.Core.Telemetry
{
    /// <summary>
    /// One relayed-request measurement recorded on completion. Optional timing and
    /// size fields are null when the underlying value was not observed.
    /// </summary>
    public class RelayMetricSample
    {
        /// <summary>
        /// Requested model name (bounded by the fleet's advertised model set).
        /// </summary>
        public string Model { get; set; } = "unknown";

        /// <summary>
        /// Client identifier that serviced the request.
        /// </summary>
        public string ClientId { get; set; } = "unknown";

        /// <summary>
        /// Provider name that serviced the request (bounded set).
        /// </summary>
        public string Provider { get; set; } = "unknown";

        /// <summary>
        /// Outcome bucket: "success", "client_error", "upstream_error", "timeout",
        /// "no_capacity", or "canceled".
        /// </summary>
        public string Outcome { get; set; } = "success";

        /// <summary>
        /// HTTP status code returned to the caller.
        /// </summary>
        public int StatusCode { get; set; } = 0;

        /// <summary>
        /// Whether the response was streamed.
        /// </summary>
        public bool Streamed { get; set; } = false;

        /// <summary>
        /// Tenant identifier, populated only when tenant labeling is enabled.
        /// </summary>
        public string? Tenant { get; set; } = null;

        /// <summary>
        /// Time to first token, in seconds, or null.
        /// </summary>
        public double? TimeToFirstTokenSeconds { get; set; } = null;

        /// <summary>
        /// Total streaming duration, in seconds, or null.
        /// </summary>
        public double? StreamingSeconds { get; set; } = null;

        /// <summary>
        /// Total client round-trip duration, in seconds, or null.
        /// </summary>
        public double? RoundTripSeconds { get; set; } = null;

        /// <summary>
        /// Upstream (client-to-provider) duration, in seconds, or null.
        /// </summary>
        public double? UpstreamSeconds { get; set; } = null;

        /// <summary>
        /// Prompt token count, or null.
        /// </summary>
        public long? PromptTokens { get; set; } = null;

        /// <summary>
        /// Completion token count, or null.
        /// </summary>
        public long? CompletionTokens { get; set; } = null;

        /// <summary>
        /// Total token count, or null.
        /// </summary>
        public long? TotalTokens { get; set; } = null;

        /// <summary>
        /// Number of streamed chunks, or null.
        /// </summary>
        public long? StreamChunks { get; set; } = null;

        /// <summary>
        /// Number of streamed events, or null.
        /// </summary>
        public long? StreamEvents { get; set; } = null;

        /// <summary>
        /// Request payload size in bytes, or null.
        /// </summary>
        public long? RequestBytes { get; set; } = null;

        /// <summary>
        /// Response payload size in bytes, or null.
        /// </summary>
        public long? ResponseBytes { get; set; } = null;

        /// <summary>
        /// Instantiate.
        /// </summary>
        public RelayMetricSample()
        {
        }
    }
}

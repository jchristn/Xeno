namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno server provider telemetry sample record DTO.
    /// </summary>
    public class ServerProviderTelemetrySampleRecord
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the tenant id.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the client id.
        /// </summary>
        public string ClientId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the client name.
        /// </summary>
        public string ClientName { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the provider name.
        /// </summary>
        public string ProviderName { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the provider kind.
        /// </summary>
        public string ProviderKind { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        public string Status { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the health status.
        /// </summary>
        public string HealthStatus { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the collected utc.
        /// </summary>
        public DateTime? CollectedUtc { get; set; }

        /// <summary>
        /// Gets or sets the persisted utc.
        /// </summary>
        public DateTime? PersistedUtc { get; set; }

        /// <summary>
        /// Gets or sets the active requests.
        /// </summary>
        public double ActiveRequests { get; set; }

        /// <summary>
        /// Gets or sets the queued requests.
        /// </summary>
        public double QueuedRequests { get; set; }

        /// <summary>
        /// Gets or sets the loaded model count.
        /// </summary>
        public int LoadedModelCount { get; set; }

        /// <summary>
        /// Gets or sets the latency p95ms.
        /// </summary>
        public double LatencyP95Ms { get; set; }
    }
}

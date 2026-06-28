namespace Xeno.Core.Models
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;
    using Xeno.Core.Enums;
    using Xeno.Core.Settings;

    /// <summary>
    /// Routing policy create or update request.
    /// </summary>
    public class RoutingPolicySaveRequest
    {
        /// <summary>
        /// Tenant ID.
        /// </summary>
        [JsonPropertyName("tenantId")]
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Policy name.
        /// </summary>
        [JsonPropertyName("name")]
        public string? Name { get; set; } = null;

        /// <summary>
        /// Model name match expression. Use "*" to match all models.
        /// </summary>
        [JsonPropertyName("modelName")]
        public string? ModelName { get; set; } = null;

        /// <summary>
        /// Client name match expression. Use "*" to match all clients.
        /// </summary>
        [JsonPropertyName("clientName")]
        public string? ClientName { get; set; } = null;

        /// <summary>
        /// Request timeout in milliseconds.
        /// </summary>
        [JsonPropertyName("requestTimeoutMs")]
        public int? RequestTimeoutMs { get; set; } = null;

        /// <summary>
        /// Load-balancing mode.
        /// </summary>
        [JsonPropertyName("loadBalancingMode")]
        public LoadBalancingModeEnum? LoadBalancingMode { get; set; } = null;

        /// <summary>
        /// Adaptive load-balancing settings.
        /// </summary>
        [JsonPropertyName("adaptive")]
        public AdaptiveLoadBalancingSettings? Adaptive { get; set; } = null;

        /// <summary>
        /// Maximum telemetry age in milliseconds.
        /// </summary>
        [JsonPropertyName("maxTelemetryAgeMs")]
        public int? MaxTelemetryAgeMs { get; set; } = null;

        /// <summary>
        /// Telemetry filter conditions.
        /// </summary>
        [JsonPropertyName("telemetryFilters")]
        public List<RoutingPolicyCondition>? TelemetryFilters { get; set; } = null;

        /// <summary>
        /// Telemetry ranking rules.
        /// </summary>
        [JsonPropertyName("telemetryRanking")]
        public List<RoutingPolicyRankingRule>? TelemetryRanking { get; set; } = null;

        /// <summary>
        /// Telemetry fallback mode.
        /// </summary>
        [JsonPropertyName("telemetryFallbackMode")]
        public RoutingPolicyFallbackModeEnum? TelemetryFallbackMode { get; set; } = null;

        /// <summary>
        /// Whether the policy is active.
        /// </summary>
        [JsonPropertyName("active")]
        public bool? Active { get; set; } = null;
    }
}

namespace Xeno.Client
{
    using System.Collections.Generic;

    internal class ClientSettingsDefaultProbe
    {
        public ClientServerSettings? Server { get; set; } = null;

        public ClientLocalDashboardSettings? LocalDashboard { get; set; } = null;

        public ClientLocalInferenceServerSettingsProbe? LocalInferenceServer { get; set; } = null;

        public ClientTelemetrySettings? Telemetry { get; set; } = null;

        public ClientChatSettings? Chat { get; set; } = null;

        public ClientAnalyticsSettings? Analytics { get; set; } = null;

        public ClientMcpSettings? Mcp { get; set; } = null;

        public List<ClientRunnerSettingsProbe>? Providers { get; set; } = null;

        public int? DefaultMaxConcurrentRequests { get; set; } = null;
    }

    internal class ClientLocalInferenceServerSettingsProbe
    {
        public int? RequestTimeoutMs { get; set; } = null;

        public int? Port { get; set; } = null;

        public string? RoutingMode { get; set; } = null;

        public List<LocalInferenceRouteRule>? Routes { get; set; } = null;

        public bool? DiscoverLocalModels { get; set; } = null;

        public int? HealthCheckIntervalMs { get; set; } = null;

        public int? LocalQueueTimeoutMs { get; set; } = null;

        public int? RequestRetentionDays { get; set; } = null;
    }

    internal class ClientRunnerSettingsProbe
    {
        public string? ApiType { get; set; } = null;

        public string? Provider { get; set; } = null;

        public bool? AllowModelManagement { get; set; } = null;

        public bool? ShareWithNetwork { get; set; } = null;

        public bool? RouteLocalRequests { get; set; } = null;
    }
}

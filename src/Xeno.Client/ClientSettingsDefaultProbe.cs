namespace Xeno.Client
{
    using System.Collections.Generic;

    internal class ClientSettingsDefaultProbe
    {
        public ClientServerSettings? Server { get; set; } = null;

        public ClientLocalDashboardSettings? LocalDashboard { get; set; } = null;

        public ClientLocalInferenceServerSettingsProbe? LocalInferenceServer { get; set; } = null;

        public ClientTelemetrySettings? Telemetry { get; set; } = null;

        public List<ClientRunnerSettings>? Providers { get; set; } = null;
    }

    internal class ClientLocalInferenceServerSettingsProbe
    {
        public int? RequestTimeoutMs { get; set; } = null;
    }
}

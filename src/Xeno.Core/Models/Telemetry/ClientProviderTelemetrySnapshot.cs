namespace Xeno.Core.Models.Telemetry
{
    using System.Collections.Generic;

    /// <summary>
    /// Provider telemetry snapshot.
    /// </summary>
    public class ClientProviderTelemetrySnapshot
    {
        /// <summary>
        /// Snapshot metadata.
        /// </summary>
        public ClientProviderTelemetryMetadata Metadata { get; set; } = new ClientProviderTelemetryMetadata();

        /// <summary>
        /// Provider statuses.
        /// </summary>
        public List<ClientProviderTelemetryProviderStatus> Providers { get; set; } = new List<ClientProviderTelemetryProviderStatus>();
    }
}

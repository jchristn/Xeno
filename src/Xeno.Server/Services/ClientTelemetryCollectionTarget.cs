namespace Xeno.Server.Services
{
    /// <summary>
    /// Connected client target for telemetry collection.
    /// </summary>
    internal sealed class ClientTelemetryCollectionTarget
    {
        /// <summary>
        /// Client identifier.
        /// </summary>
        public string ClientId { get; set; } = string.Empty;

        /// <summary>
        /// Tenant identifier.
        /// </summary>
        public string TenantId { get; set; } = string.Empty;

        /// <summary>
        /// Client display name.
        /// </summary>
        public string? ClientName { get; set; } = null;
    }
}

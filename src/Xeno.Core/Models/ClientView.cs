namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;

    /// <summary>
    /// Tenant-facing projection of a <see cref="XenoClientRecord"/>. A tenant sees its own clients in full;
    /// clients owned by other tenants (shared clients) are returned with the client name, owner identity,
    /// weighting, and live-load fields omitted so that no cross-tenant detail leaks to a consuming tenant.
    /// Advertised model names remain visible so the consuming tenant can still route to them.
    /// </summary>
    public class ClientView
    {
        /// <summary>
        /// Client ID.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Owning tenant ID. Populated only when the viewer owns the client; null for shared clients owned by another tenant.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Client name. Populated only when the viewer owns the client; empty for shared clients owned by another tenant.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Origin of the client relative to the viewer: "tenant" when the viewer owns it, "operator" when it is
        /// owned by the operator/default tenant, or "third-party" when it is owned by another customer tenant.
        /// </summary>
        public string Origin { get; set; } = "tenant";

        /// <summary>
        /// Whether the client is an eligible routing target for tenants other than its owner.
        /// </summary>
        public bool Shared { get; set; } = false;

        /// <summary>
        /// Owning user ID. Populated only for clients the viewer owns; null for shared clients.
        /// </summary>
        public string? OwnerUserId { get; set; } = null;

        /// <summary>
        /// Owning credential ID. Populated only for clients the viewer owns; null for shared clients.
        /// </summary>
        public string? OwnerCredentialId { get; set; } = null;

        /// <summary>
        /// Owning principal type. Populated only for clients the viewer owns; null for shared clients.
        /// </summary>
        public PrincipalTypeEnum? OwnerPrincipalType { get; set; } = null;

        /// <summary>
        /// Whether the client is connected.
        /// </summary>
        public bool Connected { get; set; } = false;

        /// <summary>
        /// Whether the client is active.
        /// </summary>
        public bool Active { get; set; } = false;

        /// <summary>
        /// Client routing state.
        /// </summary>
        public ClientServiceStateEnum ServiceState { get; set; } = ClientServiceStateEnum.Normal;

        /// <summary>
        /// Maximum parallel requests. Populated only for clients the viewer owns; null for shared clients.
        /// </summary>
        public int? MaxParallelRequests { get; set; } = null;

        /// <summary>
        /// Static routing weight. Populated only for clients the viewer owns; null for shared clients.
        /// </summary>
        public int? Weight { get; set; } = null;

        /// <summary>
        /// Current in-flight request count. Populated only for clients the viewer owns; null for shared clients.
        /// </summary>
        public int? InFlightRequests { get; set; } = null;

        /// <summary>
        /// Last heartbeat timestamp.
        /// </summary>
        public DateTime LastHeartbeatUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Provider sources advertised by this client. Empty for shared clients owned by another tenant.
        /// </summary>
        public List<XenoClientProvider> Providers { get; set; } = new List<XenoClientProvider>();

        /// <summary>
        /// Models advertised by this client. For shared clients owned by another tenant this is reduced to
        /// model names and capability flags only.
        /// </summary>
        public List<XenoClientModel> Models { get; set; } = new List<XenoClientModel>();
    }
}

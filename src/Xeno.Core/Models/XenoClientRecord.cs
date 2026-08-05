namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Connected or configured Xeno client.
    /// </summary>
    public class XenoClientRecord
    {
        /// <summary>
        /// Client ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewId("xcl_");

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Client name.
        /// </summary>
        public string Name { get; set; } = "local-client";

        /// <summary>
        /// User ID that owns the client registration. Set from the authenticated WebSocket credential when the client connects.
        /// </summary>
        public string? OwnerUserId { get; set; } = null;

        /// <summary>
        /// Credential ID used for the latest owner-authenticated client connection.
        /// </summary>
        public string? OwnerCredentialId { get; set; } = null;

        /// <summary>
        /// Principal type used for the latest owner-authenticated client connection.
        /// </summary>
        public PrincipalTypeEnum OwnerPrincipalType { get; set; } = PrincipalTypeEnum.None;

        /// <summary>
        /// Whether this client is an eligible routing target for tenants other than its owner.
        /// Initialized at first registration from the owner tenant's default sharing setting, then adjustable
        /// per-client by the owner. Server-authoritative: never set by the connecting node itself.
        /// Default: false (the client is private to its owner tenant).
        /// </summary>
        public bool Shared { get; set; } = false;

        /// <summary>
        /// Whether the client is connected.
        /// </summary>
        public bool Connected { get; set; } = true;

        /// <summary>
        /// Whether the client is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Client routing state.
        /// </summary>
        public ClientServiceStateEnum ServiceState { get; set; } = ClientServiceStateEnum.Normal;

        /// <summary>
        /// Maximum parallel requests.
        /// </summary>
        public int MaxParallelRequests { get; set; } = 4;

        /// <summary>
        /// Static routing weight.
        /// </summary>
        public int Weight { get; set; } = 100;

        /// <summary>
        /// Current in-flight request count.
        /// </summary>
        public int InFlightRequests { get; set; } = 0;

        /// <summary>
        /// Last heartbeat timestamp.
        /// </summary>
        public DateTime LastHeartbeatUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Provider sources advertised by this client.
        /// </summary>
        public List<XenoClientProvider> Providers { get; set; } = new List<XenoClientProvider>();

        /// <summary>
        /// Models advertised by this client.
        /// </summary>
        public List<XenoClientModel> Models { get; set; } = new List<XenoClientModel>();
    }
}

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
        /// Whether the client is connected.
        /// </summary>
        public bool Connected { get; set; } = true;

        /// <summary>
        /// Last heartbeat timestamp.
        /// </summary>
        public DateTime LastHeartbeatUtc { get; set; } = DateTime.UtcNow;
    }
}

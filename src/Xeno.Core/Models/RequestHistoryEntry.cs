namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Request history entry.
    /// </summary>
    public class RequestHistoryEntry
    {
        /// <summary>
        /// Entry ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewRequestHistoryId();

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Request path.
        /// </summary>
        public string Path { get; set; } = String.Empty;

        /// <summary>
        /// HTTP status code.
        /// </summary>
        public int StatusCode { get; set; } = 200;

        /// <summary>
        /// Created timestamp.
        /// </summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Routing decision.
        /// </summary>
        public RoutingDecision? RoutingDecision { get; set; } = null;
    }
}

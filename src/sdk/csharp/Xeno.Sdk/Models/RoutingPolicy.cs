namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno routing policy DTO.
    /// </summary>
    public class RoutingPolicy
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
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the model name.
        /// </summary>
        public string ModelName { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the client name.
        /// </summary>
        public string ClientName { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the request timeout ms.
        /// </summary>
        public int RequestTimeoutMs { get; set; }

        /// <summary>
        /// Gets or sets the load balancing mode.
        /// </summary>
        public string LoadBalancingMode { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the adaptive.
        /// </summary>
        public RawJsonValue Adaptive { get; set; } = new RawJsonValue();

        /// <summary>
        /// Gets or sets the is default.
        /// </summary>
        public bool IsDefault { get; set; }

        /// <summary>
        /// Gets or sets the active.
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Gets or sets the created utc.
        /// </summary>
        public DateTime? CreatedUtc { get; set; }

        /// <summary>
        /// Gets or sets the last update utc.
        /// </summary>
        public DateTime? LastUpdateUtc { get; set; }
    }
}

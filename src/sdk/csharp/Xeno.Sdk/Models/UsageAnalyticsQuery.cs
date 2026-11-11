namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Query for usage analytics. Null values are left out.
    /// </summary>
    public class UsageAnalyticsQuery
    {
        /// <summary>
        /// Range start, UTC. Default 24 hours before ToUtc.
        /// </summary>
        public DateTime? FromUtc { get; set; }

        /// <summary>
        /// Range end, UTC. Default now.
        /// </summary>
        public DateTime? ToUtc { get; set; }

        /// <summary>
        /// Bucket minutes. Default follows the range.
        /// </summary>
        public int? BucketMinutes { get; set; }

        /// <summary>
        /// Only this model.
        /// </summary>
        public string? Model { get; set; }

        /// <summary>
        /// Only this provider.
        /// </summary>
        public string? Provider { get; set; }

        /// <summary>
        /// Only this client.
        /// </summary>
        public string? ClientId { get; set; }

        /// <summary>
        /// Only this user (tenant administrators).
        /// </summary>
        public string? UserId { get; set; }

        /// <summary>
        /// Only this credential (tenant administrators).
        /// </summary>
        public string? CredentialId { get; set; }

        /// <summary>
        /// Another tenant (global administrators).
        /// </summary>
        public string? TenantId { get; set; }

        /// <summary>
        /// Model, Provider, Route, Client, Tenant, User, or Credential.
        /// </summary>
        public string? GroupBy { get; set; }

        /// <summary>
        /// Requests or Tokens.
        /// </summary>
        public string? GroupMetric { get; set; }

        /// <summary>
        /// Groups shown by name, 1 to 20.
        /// </summary>
        public int? TopN { get; set; }

        /// <summary>
        /// Viewer's offset from UTC, for heatmap hours.
        /// </summary>
        public int? UtcOffsetMinutes { get; set; }

        /// <summary>
        /// Comma-separated sections to compute.
        /// </summary>
        public string? Sections { get; set; }

        /// <summary>
        /// Chat only: tenant for every user (needs ChatAdmin).
        /// </summary>
        public string? Scope { get; set; }
    }
}

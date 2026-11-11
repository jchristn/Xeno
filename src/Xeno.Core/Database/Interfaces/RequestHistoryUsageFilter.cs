namespace Xeno.Core.Database.Interfaces
{
    using System;

    /// <summary>
    /// Filter for reading request history usage records. TenantId always scopes the read; only callers that already
    /// checked the caller may read another tenant, and null reads every tenant (global administrators only).
    /// </summary>
    public sealed class RequestHistoryUsageFilter
    {
        /// <summary>
        /// Tenant, or null for every tenant.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// Range start, UTC, inclusive.
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.UtcNow.AddDays(-1);

        /// <summary>
        /// Range end, UTC, exclusive.
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Only this model, or null.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Only this client, or null.
        /// </summary>
        public string? ClientId { get; set; } = null;

        /// <summary>
        /// Only this user, or null.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Only this credential, or null.
        /// </summary>
        public string? CredentialId { get; set; } = null;
    }
}

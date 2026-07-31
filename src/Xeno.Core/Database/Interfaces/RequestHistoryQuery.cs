namespace Xeno.Core.Database.Interfaces
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Filter for querying, counting, and deleting persisted request history.
    /// All supplied predicates are combined with AND, except the requestor-tenant
    /// and user-reference options which broaden the match as documented.
    /// </summary>
    public class RequestHistoryQuery
    {
        /// <summary>
        /// Owning tenant identifier. When null, all tenants are considered.
        /// </summary>
        public string? TenantId { get; set; } = null;

        /// <summary>
        /// When true and <see cref="TenantId"/> is set, entries are matched when either the
        /// owning tenant or the requestor tenant equals <see cref="TenantId"/>.
        /// </summary>
        public bool MatchRequestorTenant { get; set; } = false;

        /// <summary>
        /// Optional set of entry identifiers to match.
        /// </summary>
        public IReadOnlyCollection<string>? Ids { get; set; } = null;

        /// <summary>
        /// Optional exact model filter.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// Optional exact request path filter.
        /// </summary>
        public string? Path { get; set; } = null;

        /// <summary>
        /// Optional exact selected client filter.
        /// </summary>
        public string? SelectedClientId { get; set; } = null;

        /// <summary>
        /// Optional user reference. Matches user_id or requestor_principal_id.
        /// Combined with <see cref="CredentialIds"/> and <see cref="SessionIds"/> as an OR group.
        /// </summary>
        public string? UserId { get; set; } = null;

        /// <summary>
        /// Optional credential identifiers included in the user-reference OR group.
        /// </summary>
        public IReadOnlyCollection<string>? CredentialIds { get; set; } = null;

        /// <summary>
        /// Optional session identifiers included in the user-reference OR group.
        /// </summary>
        public IReadOnlyCollection<string>? SessionIds { get; set; } = null;

        /// <summary>
        /// Inclusive lower bound on creation time.
        /// </summary>
        public DateTime? FromUtc { get; set; } = null;

        /// <summary>
        /// Exclusive upper bound on creation time.
        /// </summary>
        public DateTime? ToUtc { get; set; } = null;

        /// <summary>
        /// Inclusive minimum status code.
        /// </summary>
        public int? MinStatusCode { get; set; } = null;

        /// <summary>
        /// Inclusive maximum status code.
        /// </summary>
        public int? MaxStatusCode { get; set; } = null;

        /// <summary>
        /// When true, only entries with a positive total token count are matched.
        /// </summary>
        public bool RequireTokens { get; set; } = false;

        /// <summary>
        /// Zero-based row offset for paged queries.
        /// </summary>
        public int Offset { get; set; } = 0;

        /// <summary>
        /// Maximum rows to return. Zero or negative returns all matching rows (bounded by the caller).
        /// </summary>
        public int Limit { get; set; } = 0;

        /// <summary>
        /// When true, the full serialized entry (headers, bodies, timeline, telemetry) is hydrated.
        /// When false, only the promoted columns are hydrated for lightweight aggregation.
        /// </summary>
        public bool IncludeBody { get; set; } = true;

        /// <summary>
        /// When true, results are ordered by creation time descending; otherwise ascending.
        /// </summary>
        public bool OrderDescending { get; set; } = true;
    }
}

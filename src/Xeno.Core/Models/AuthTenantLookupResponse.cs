namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Tenant lookup response for a dashboard login email address.
    /// </summary>
    public class AuthTenantLookupResponse
    {
        /// <summary>
        /// Email address used for lookup.
        /// </summary>
        [JsonPropertyName("email")]
        public string Email { get; set; } = String.Empty;

        /// <summary>
        /// Tenants containing an active user with the supplied email address.
        /// </summary>
        [JsonPropertyName("tenants")]
        public List<AuthTenantSummary> Tenants { get; set; } = new List<AuthTenantSummary>();
    }
}

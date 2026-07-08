namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Administrative nuke response.
    /// </summary>
    public class AdminNukeResponse
    {
        /// <summary>Operation status.</summary>
        [JsonPropertyName("status")]
        public string Status { get; set; } = "success";

        /// <summary>Operation identifier.</summary>
        [JsonPropertyName("operationId")]
        public string OperationId { get; set; } = string.Empty;

        /// <summary>Operation type.</summary>
        [JsonPropertyName("operationType")]
        public string OperationType { get; set; } = string.Empty;

        /// <summary>Whether this response represents a preview rather than a deletion.</summary>
        [JsonPropertyName("dryRun")]
        public bool DryRun { get; set; } = true;

        /// <summary>Tenant details.</summary>
        [JsonPropertyName("tenant")]
        public AdminNukeTenantDetails Tenant { get; set; } = new AdminNukeTenantDetails();

        /// <summary>User details for user nuke operations.</summary>
        [JsonPropertyName("user")]
        public AdminNukeUserDetails? User { get; set; } = null;

        /// <summary>Requesting principal details.</summary>
        [JsonPropertyName("requestedBy")]
        public AdminNukeRequestedBy RequestedBy { get; set; } = new AdminNukeRequestedBy();

        /// <summary>Deleted object counts.</summary>
        [JsonPropertyName("deleted")]
        public AdminNukeCounts Deleted { get; set; } = new AdminNukeCounts();

        /// <summary>Updated object counts.</summary>
        [JsonPropertyName("updated")]
        public AdminNukeUpdatedCounts Updated { get; set; } = new AdminNukeUpdatedCounts();

        /// <summary>Non-fatal warnings.</summary>
        [JsonPropertyName("warnings")]
        public List<string> Warnings { get; set; } = new List<string>();

        /// <summary>Operation start timestamp.</summary>
        [JsonPropertyName("startedUtc")]
        public DateTime StartedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Operation completion timestamp.</summary>
        [JsonPropertyName("completedUtc")]
        public DateTime CompletedUtc { get; set; } = DateTime.UtcNow;
    }
}

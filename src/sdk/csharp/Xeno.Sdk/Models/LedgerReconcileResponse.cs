namespace Xeno.Sdk
{
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// Ledger reconcile response DTO.
    /// </summary>
    public class LedgerReconcileResponse
    {
        /// <summary>
        /// Gets or sets missing tenant link count.
        /// </summary>
        [JsonPropertyName("missingTenantLinks")]
        public int MissingTenantLinks { get; set; }

        /// <summary>
        /// Gets or sets missing account link count.
        /// </summary>
        [JsonPropertyName("missingAccountLinks")]
        public int MissingAccountLinks { get; set; }

        /// <summary>
        /// Gets or sets reconciliation messages.
        /// </summary>
        [JsonPropertyName("messages")]
        public List<string> Messages { get; set; } = new List<string>();
    }
}

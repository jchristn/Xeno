namespace Xeno.Client.Services
{
    using System;
    using System.Text.Json.Serialization;

    /// <summary>
    /// A model the Xeno network advertises to this client's tenant (one entry of the OpenAI-compatible model list).
    /// </summary>
    public class NetworkModelEntry
    {
        /// <summary>
        /// Model name.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Object type, normally "model".
        /// </summary>
        [JsonPropertyName("object")]
        public string Object { get; set; } = "model";

        /// <summary>
        /// Last advertisement time as Unix seconds.
        /// </summary>
        [JsonPropertyName("created")]
        public long Created { get; set; } = 0;

        /// <summary>
        /// Owner: "xeno" when the tenant's own clients serve the model, "shared" when other tenants share it.
        /// </summary>
        [JsonPropertyName("owned_by")]
        public string OwnedBy { get; set; } = String.Empty;
    }
}

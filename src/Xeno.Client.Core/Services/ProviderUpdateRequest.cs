namespace Xeno.Client.Services
{
    /// <summary>
    /// Partial update for a configured provider endpoint. Null values leave the existing value unchanged.
    /// </summary>
    public class ProviderUpdateRequest
    {
        /// <summary>
        /// New provider name, or null to keep the current name.
        /// </summary>
        public string? NewName { get; set; } = null;

        /// <summary>
        /// Upstream API type (for example Ollama or OpenAICompatible), or null to keep the current type.
        /// </summary>
        public string? ApiType { get; set; } = null;

        /// <summary>
        /// Upstream endpoint URL, or null to keep the current endpoint.
        /// </summary>
        public string? Endpoint { get; set; } = null;

        /// <summary>
        /// Credential to store, or null to keep the current credential.
        /// </summary>
        public string? Credential { get; set; } = null;

        /// <summary>
        /// True to remove the stored credential. Takes precedence over <see cref="Credential"/>.
        /// </summary>
        public bool ClearCredential { get; set; } = false;

        /// <summary>
        /// Whether the Xeno server may pull and delete models on this provider, or null to keep the current value.
        /// Only Ollama providers support model management.
        /// </summary>
        public bool? AllowModelManagement { get; set; } = null;

        /// <summary>
        /// Upstream request timeout in milliseconds (minimum 1000), or null to keep the current timeout.
        /// </summary>
        public int? TimeoutMs { get; set; } = null;

        /// <summary>
        /// Whether the provider's models are offered to the Xeno network. Null leaves it unchanged.
        /// </summary>
        public bool? ShareWithNetwork { get; set; } = null;

        /// <summary>
        /// Whether the local inference listener may route requests to the provider. Null leaves it unchanged.
        /// </summary>
        public bool? RouteLocalRequests { get; set; } = null;

        /// <summary>
        /// Maximum concurrent requests across the provider's models. Null leaves it unchanged unless <see cref="InheritMaxConcurrentRequests"/> is true. Minimum 1, maximum 256.
        /// </summary>
        public int? MaxConcurrentRequests { get; set; } = null;

        /// <summary>
        /// Clear the provider limit so it inherits the global default. Default is false.
        /// </summary>
        public bool InheritMaxConcurrentRequests { get; set; } = false;
    }
}

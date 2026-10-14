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
    }
}

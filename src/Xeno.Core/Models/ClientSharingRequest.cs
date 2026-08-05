namespace Xeno.Core.Models
{
    using System.Text.Json.Serialization;

    /// <summary>
    /// Request body for updating a client's shared state.
    /// </summary>
    public class ClientSharingRequest
    {
        /// <summary>
        /// Desired shared state. When true the client becomes an eligible routing target for other tenants;
        /// when false it is private to its owner tenant. Required.
        /// </summary>
        [JsonPropertyName("shared")]
        public bool? Shared { get; set; } = null;
    }
}

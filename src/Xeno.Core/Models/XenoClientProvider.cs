namespace Xeno.Core.Models
{
    using System;
    using Xeno.Core.Enums;

    /// <summary>
    /// Provider source advertised by a connected Xeno client.
    /// </summary>
    public class XenoClientProvider
    {
        /// <summary>
        /// Client-local provider source name.
        /// </summary>
        public string Name { get; set; } = "local";

        /// <summary>
        /// Upstream API type used by the provider.
        /// </summary>
        public ApiProviderTypeEnum ApiType { get; set; } = ApiProviderTypeEnum.Ollama;

        /// <summary>
        /// Whether the provider allows server-initiated model management operations.
        /// </summary>
        public bool ModelManagementAllowed { get; set; } = false;

        /// <summary>
        /// Last advertisement timestamp.
        /// </summary>
        public DateTime LastAdvertisedUtc { get; set; } = DateTime.UtcNow;
    }
}

namespace Xeno.Core.Models
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;

    /// <summary>
    /// Model runner endpoint.
    /// </summary>
    public class ModelRunnerEndpoint
    {
        /// <summary>
        /// Endpoint ID.
        /// </summary>
        public string Id { get; set; } = IdGenerator.NewId("mre_");

        /// <summary>
        /// Tenant ID.
        /// </summary>
        public string TenantId { get; set; } = Constants.DefaultTenantId;

        /// <summary>
        /// Client ID.
        /// </summary>
        public string ClientId { get; set; } = String.Empty;

        /// <summary>
        /// Endpoint name.
        /// </summary>
        public string Name { get; set; } = "local-ollama";

        /// <summary>
        /// API provider type.
        /// </summary>
        public ApiProviderTypeEnum Provider { get; set; } = ApiProviderTypeEnum.Ollama;

        /// <summary>
        /// Endpoint URL.
        /// </summary>
        public string EndpointUrl { get; set; } = "http://127.0.0.1:11434";

        /// <summary>
        /// Whether endpoint is active.
        /// </summary>
        public bool Active { get; set; } = true;

        /// <summary>
        /// Service state.
        /// </summary>
        public EndpointServiceStateEnum ServiceState { get; set; } = EndpointServiceStateEnum.Normal;

        /// <summary>
        /// Maximum parallel requests.
        /// </summary>
        public int MaxParallelRequests { get; set; } = 4;

        /// <summary>
        /// Static endpoint weight.
        /// </summary>
        public int Weight { get; set; } = 100;

        /// <summary>
        /// Current in-flight request count.
        /// </summary>
        public int InFlightRequests { get; set; } = 0;

        /// <summary>
        /// Available model names.
        /// </summary>
        public List<string> Models { get; set; } = new List<string> { "llama3.2:latest", "text-embedding-3-small" };
    }
}

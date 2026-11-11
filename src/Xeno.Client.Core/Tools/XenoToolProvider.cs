namespace Xeno.Client.Tools
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A provider as the xeno_list_providers tool reports it. Never carries the credential.
    /// </summary>
    public sealed class XenoToolProvider
    {
        /// <summary>
        /// Name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// API type, for example Ollama or vLLM.
        /// </summary>
        public string ApiType { get; set; } = String.Empty;

        /// <summary>
        /// Endpoint URL.
        /// </summary>
        public string Endpoint { get; set; } = String.Empty;

        /// <summary>
        /// Models added to Xeno for this provider.
        /// </summary>
        public List<string> Models { get; set; } = new List<string>();

        /// <summary>
        /// True when a credential is configured.
        /// </summary>
        public bool HasCredential { get; set; } = false;

        /// <summary>
        /// True when the provider serves Xeno network requests.
        /// </summary>
        public bool ShareWithNetwork { get; set; } = false;

        /// <summary>
        /// True when the provider answers local requests.
        /// </summary>
        public bool RouteLocalRequests { get; set; } = false;
    }
}

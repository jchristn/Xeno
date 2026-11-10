namespace Xeno.Client
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Client model settings.
    /// </summary>
    public class ClientModelSettings
    {
        private int? _MaxConcurrentRequests = null;

        /// <summary>
        /// Model name.
        /// </summary>
        public string Name { get; set; } = "llama3.2:latest";

        /// <summary>
        /// Whether completions are supported.
        /// </summary>
        public bool SupportsCompletions { get; set; } = true;

        /// <summary>
        /// Whether embeddings are supported.
        /// </summary>
        public bool SupportsEmbeddings { get; set; } = false;

        /// <summary>
        /// Maximum concurrent requests for this model on its provider, shared by network and local requests. Null (the default) inherits the provider limit. Minimum 1, maximum 256; out-of-range values are clamped.
        /// </summary>
        public int? MaxConcurrentRequests
        {
            get
            {
                return _MaxConcurrentRequests;
            }
            set
            {
                _MaxConcurrentRequests = value.HasValue ? Math.Min(256, Math.Max(1, value.Value)) : null;
            }
        }
    }
}

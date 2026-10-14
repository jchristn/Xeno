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
    }
}

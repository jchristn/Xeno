namespace Xeno.Client.Tools
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A model as the xeno_list_models tool reports it.
    /// </summary>
    public sealed class XenoToolModel
    {
        /// <summary>
        /// Name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Where it is available, in words.
        /// </summary>
        public string Availability { get; set; } = String.Empty;

        /// <summary>
        /// True when a provider on this machine serves it.
        /// </summary>
        public bool ServedLocally { get; set; } = false;

        /// <summary>
        /// Providers on this machine that serve it.
        /// </summary>
        public List<string> LocalProviders { get; set; } = new List<string>();

        /// <summary>
        /// True when the Xeno network offers it.
        /// </summary>
        public bool OnNetwork { get; set; } = false;

        /// <summary>
        /// Where a request for it goes now.
        /// </summary>
        public string RouteTarget { get; set; } = String.Empty;

        /// <summary>
        /// True when it answers chat; false for embedding-only models.
        /// </summary>
        public bool SupportsChat { get; set; } = true;
    }
}

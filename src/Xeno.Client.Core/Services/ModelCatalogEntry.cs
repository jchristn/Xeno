namespace Xeno.Client.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One model in the unified catalog: what this machine serves, what the Xeno network offers, or both.
    /// This is the shape front ends use to show local and remote models side by side.
    /// </summary>
    public class ModelCatalogEntry
    {
        /// <summary>
        /// Model name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Where the model is available.
        /// </summary>
        public ModelAvailabilityEnum Availability { get; set; } = ModelAvailabilityEnum.ThisMachine;

        /// <summary>
        /// True when a provider on this machine serves the model.
        /// </summary>
        public bool ServedLocally { get; set; } = false;

        /// <summary>
        /// Names of the local providers that serve the model.
        /// </summary>
        public List<string> LocalProviders { get; set; } = new List<string>();

        /// <summary>
        /// API type of the first local provider.
        /// </summary>
        public string LocalApiType { get; set; } = String.Empty;

        /// <summary>
        /// True when a local provider serves completions with this model.
        /// </summary>
        public bool SupportsCompletions { get; set; } = false;

        /// <summary>
        /// True when a local provider serves embeddings with this model.
        /// </summary>
        public bool SupportsEmbeddings { get; set; } = false;

        /// <summary>
        /// True when the Xeno server may pull and delete this model on a local provider.
        /// </summary>
        public bool ModelManagementAllowed { get; set; } = false;

        /// <summary>
        /// True when the Xeno network offers the model to this client's tenant.
        /// </summary>
        public bool OnNetwork { get; set; } = false;

        /// <summary>
        /// Network owner: "xeno" when the tenant's own clients serve it, "shared" when another tenant shares it.
        /// </summary>
        public string NetworkOwner { get; set; } = String.Empty;

        /// <summary>
        /// Where the local inference listener would send a request for this model: "local:&lt;provider&gt;", "network", or "unavailable". Never null.
        /// </summary>
        public string RouteTarget { get; set; } = String.Empty;

        /// <summary>
        /// Stable reason code for <see cref="RouteTarget"/>, as returned by the router (for example "preferLocal" or "rule"). Never null.
        /// </summary>
        public string RouteReason { get; set; } = String.Empty;

        /// <summary>
        /// True when every provider on this machine that serves the model keeps it off the Xeno network.
        /// </summary>
        public bool LocalOnly { get; set; } = false;

        /// <summary>
        /// True when the model is known only because a local provider reported it, not because it is configured in Xeno.
        /// </summary>
        public bool Discovered { get; set; } = false;

        /// <summary>
        /// Display label for <see cref="Availability"/>.
        /// </summary>
        public string AvailabilityText
        {
            get
            {
                switch (Availability)
                {
                    case ModelAvailabilityEnum.Both: return "This machine + network";
                    case ModelAvailabilityEnum.XenoNetwork: return "Xeno network";
                    default: return "This machine";
                }
            }
        }
    }
}

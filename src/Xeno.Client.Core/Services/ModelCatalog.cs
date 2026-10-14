namespace Xeno.Client.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Unified view of the models this machine serves and the models the Xeno network offers it.
    /// </summary>
    public class ModelCatalog
    {
        /// <summary>
        /// Entries, one per model name, sorted by name.
        /// </summary>
        public List<ModelCatalogEntry> Entries { get; set; } = new List<ModelCatalogEntry>();

        /// <summary>
        /// True when the network model list was retrieved.
        /// </summary>
        public bool NetworkAvailable { get; set; } = false;

        /// <summary>
        /// Why the network model list is unavailable, or an empty string.
        /// </summary>
        public string NetworkMessage { get; set; } = String.Empty;

        /// <summary>
        /// When the catalog was built (UTC).
        /// </summary>
        public DateTime BuiltUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Number of models served by this machine.
        /// </summary>
        public int LocalCount
        {
            get { return Entries.Count(item => item.ServedLocally); }
        }

        /// <summary>
        /// Number of models offered by the network.
        /// </summary>
        public int NetworkCount
        {
            get { return Entries.Count(item => item.OnNetwork); }
        }
    }
}

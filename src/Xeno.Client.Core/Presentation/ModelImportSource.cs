namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One provider's section of the Import Models dialog: whether its live inventory could be read, and its models.
    /// </summary>
    public sealed class ModelImportSource
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Provider API type.
        /// </summary>
        public string ApiType { get; set; } = String.Empty;

        /// <summary>
        /// True when the provider answered.
        /// </summary>
        public bool Reachable { get; set; } = false;

        /// <summary>
        /// Why the inventory could not be read, or null.
        /// </summary>
        public string? Error { get; set; } = null;

        /// <summary>
        /// Models the provider reported.
        /// </summary>
        public List<ModelImportCandidate> Candidates { get; set; } = new List<ModelImportCandidate>();

        /// <summary>
        /// One-line heading for the section: the provider and its model count, or the reason it could not be read.
        /// </summary>
        public string Heading
        {
            get
            {
                if (!Reachable) return Provider + ": could not read models (" + (Error ?? "no answer") + ")";
                if (Candidates.Count == 0) return Provider + ": no models";
                return Provider + ": " + Candidates.Count + " model" + (Candidates.Count == 1 ? String.Empty : "s");
            }
        }
    }
}

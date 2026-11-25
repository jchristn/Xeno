namespace Xeno.Client.Presentation
{
    using System;

    /// <summary>
    /// One model in the Import Models dialog: a model a provider reported, whether it is already advertised, whether
    /// the user chose to import it, and whether it is imported as an embeddings model.
    /// </summary>
    public sealed class ModelImportCandidate
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Model name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Size on disk, formatted, or a dash.
        /// </summary>
        public string Size { get; set; } = "-";

        /// <summary>
        /// True when the model is already advertised; it cannot be selected again.
        /// </summary>
        public bool Advertised { get; set; } = false;

        /// <summary>
        /// True when the model will be imported. Never true for an advertised model.
        /// </summary>
        public bool Selected { get; set; } = false;

        /// <summary>
        /// True to import as an embeddings model; false to import as a completions (chat and generate) model.
        /// Prefilled from the model's name and family.
        /// </summary>
        public bool Embeddings { get; set; } = false;

        /// <summary>
        /// Capability label for the current choice: "embeddings" or "completions".
        /// </summary>
        public string Capability
        {
            get { return Embeddings ? "embeddings" : "completions"; }
        }

        /// <summary>
        /// Status label: "advertised" for a model already advertised, otherwise empty.
        /// </summary>
        public string Status
        {
            get { return Advertised ? "advertised" : String.Empty; }
        }
    }
}

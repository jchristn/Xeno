namespace Xeno.Client.Presentation
{
    using System;
    using Xeno.Client.Services;

    /// <summary>
    /// A model catalog entry formatted for tables.
    /// </summary>
    public sealed class ModelRow
    {
        /// <summary>
        /// Model name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Where the model is available.
        /// </summary>
        public string Availability { get; set; } = String.Empty;

        /// <summary>
        /// Local providers serving the model, or a dash.
        /// </summary>
        public string Providers { get; set; } = String.Empty;

        /// <summary>
        /// Capabilities served locally, or a dash for network-only models.
        /// </summary>
        public string Capabilities { get; set; } = String.Empty;

        /// <summary>
        /// Network owner, or a dash.
        /// </summary>
        public string NetworkOwner { get; set; } = String.Empty;

        /// <summary>
        /// "yes" when the Xeno server may manage the model locally.
        /// </summary>
        public string ModelManagement { get; set; } = String.Empty;

        /// <summary>
        /// Source entry.
        /// </summary>
        public ModelCatalogEntry Source { get; set; } = new ModelCatalogEntry();

        /// <summary>
        /// Build a row.
        /// </summary>
        /// <param name="entry">Catalog entry.</param>
        /// <returns>Row.</returns>
        /// <exception cref="ArgumentNullException">Entry is null.</exception>
        public static ModelRow From(ModelCatalogEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            return new ModelRow
            {
                Name = entry.Name,
                Availability = entry.AvailabilityText,
                Providers = entry.LocalProviders.Count == 0 ? DisplayFormat.Dash : String.Join(", ", entry.LocalProviders),
                Capabilities = entry.ServedLocally ? DisplayFormat.Capabilities(entry.SupportsCompletions, entry.SupportsEmbeddings) : DisplayFormat.Dash,
                NetworkOwner = entry.OnNetwork ? DisplayFormat.OrDash(entry.NetworkOwner) : DisplayFormat.Dash,
                ModelManagement = entry.ServedLocally ? DisplayFormat.YesNo(entry.ModelManagementAllowed) : DisplayFormat.Dash,
                Source = entry
            };
        }
    }
}

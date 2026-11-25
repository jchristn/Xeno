namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Services;

    /// <summary>
    /// The Import Models flow shared by the desktop and terminal apps: choose which provider endpoints to read, load
    /// their live inventories, pick the models to advertise and whether each serves completions or embeddings, then
    /// import them through <see cref="XenoWorkspace.ImportModelsAsync"/>.
    /// </summary>
    public static class XenoModelImport
    {
        #region Public-Members

        /// <summary>
        /// Dialog title.
        /// </summary>
        public const string Title = "Import Models";

        /// <summary>
        /// Explains the provider step.
        /// </summary>
        public const string ProvidersIntro = "Choose the provider endpoints to read models from. Xeno lists the models each one has right now so you can pick which to advertise.";

        /// <summary>
        /// Explains the model step.
        /// </summary>
        public const string ModelsIntro = "Checked models are added to client.json and advertised to the Xeno network while the client is connected. Models already advertised are listed but cannot be picked again.";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Provider endpoints the user can read from, in configuration order.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <returns>Provider names.</returns>
        /// <exception cref="ArgumentNullException">Workspace is null.</exception>
        public static List<string> Providers(XenoWorkspace workspace)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            return workspace.LoadSettings().Runners.Select(item => item.Name).ToList();
        }

        /// <summary>
        /// Read the live inventory of each chosen provider. A provider that cannot be read is returned with its error,
        /// never thrown.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <param name="providers">Provider names.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>One section per provider, in the order given.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static async Task<List<ModelImportSource>> LoadAsync(XenoWorkspace workspace, IEnumerable<string> providers, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            ArgumentNullException.ThrowIfNull(providers);
            List<Task<ModelImportSource>> loads = providers.Select(name => LoadOneAsync(workspace, name, token)).ToList();
            ModelImportSource[] sources = await Task.WhenAll(loads).ConfigureAwait(false);
            return sources.ToList();
        }

        /// <summary>
        /// Build a section from an inventory: advertised models are marked and unselected, the rest are selected, and
        /// the completions or embeddings choice is prefilled.
        /// </summary>
        /// <param name="inventory">Inventory.</param>
        /// <returns>Section.</returns>
        /// <exception cref="ArgumentNullException">Inventory is null.</exception>
        public static ModelImportSource FromInventory(ProviderModelInventory inventory)
        {
            ArgumentNullException.ThrowIfNull(inventory);
            return new ModelImportSource
            {
                Provider = inventory.Provider,
                ApiType = inventory.ApiType,
                Reachable = inventory.Reachable,
                Error = inventory.Error,
                Candidates = inventory.Models.Select(model => new ModelImportCandidate
                {
                    Provider = inventory.Provider,
                    Name = model.Name,
                    Size = DisplayFormat.Bytes(model.SizeBytes),
                    Advertised = model.Advertised,
                    Selected = !model.Advertised,
                    Embeddings = model.LooksLikeEmbeddings
                }).ToList()
            };
        }

        /// <summary>
        /// The import request for the selected models that are not already advertised.
        /// </summary>
        /// <param name="sources">Sections.</param>
        /// <returns>Items to import.</returns>
        /// <exception cref="ArgumentNullException">Sources is null.</exception>
        public static List<ModelImportItem> Items(IEnumerable<ModelImportSource> sources)
        {
            ArgumentNullException.ThrowIfNull(sources);
            return sources
                .SelectMany(source => source.Candidates)
                .Where(candidate => candidate.Selected && !candidate.Advertised)
                .Select(candidate => new ModelImportItem
                {
                    Provider = candidate.Provider,
                    Name = candidate.Name,
                    SupportsCompletions = !candidate.Embeddings,
                    SupportsEmbeddings = candidate.Embeddings
                })
                .ToList();
        }

        /// <summary>
        /// Count of models currently selected for import.
        /// </summary>
        /// <param name="sources">Sections.</param>
        /// <returns>Count.</returns>
        public static int SelectedCount(IEnumerable<ModelImportSource> sources)
        {
            return sources == null ? 0 : sources.SelectMany(source => source.Candidates).Count(candidate => candidate.Selected && !candidate.Advertised);
        }

        /// <summary>
        /// Summarize an import for the message line or status bar.
        /// </summary>
        /// <param name="result">Result.</param>
        /// <returns>Message, for example "Imported 2 models; 1 already advertised."</returns>
        /// <exception cref="ArgumentNullException">Result is null.</exception>
        public static string Describe(ModelImportResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            List<string> parts = new List<string>
            {
                "Imported " + result.Added.Count + " model" + (result.Added.Count == 1 ? String.Empty : "s")
            };
            if (result.Skipped.Count > 0) parts.Add(result.Skipped.Count + " already advertised");
            if (result.Failed.Count > 0) parts.Add(result.Failed.Count + " failed: " + String.Join("; ", result.Failed));
            return String.Join("; ", parts) + ".";
        }

        #endregion

        #region Private-Methods

        private static async Task<ModelImportSource> LoadOneAsync(XenoWorkspace workspace, string provider, CancellationToken token)
        {
            try
            {
                ProviderModelInventory inventory = await workspace.GetProviderInventoryAsync(provider, token).ConfigureAwait(false);
                return FromInventory(inventory);
            }
            catch (Exception ex) when (ex is KeyNotFoundException || ex is InvalidOperationException || ex is System.Net.Http.HttpRequestException || ex is System.IO.IOException)
            {
                return new ModelImportSource { Provider = provider, Reachable = false, Error = ex.Message };
            }
        }

        #endregion
    }
}

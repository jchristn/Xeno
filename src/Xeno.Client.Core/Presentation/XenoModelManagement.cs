namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Xeno.Client.Services;

    /// <summary>
    /// Text and rows for the Manage Models dialog and the active-pull indicator, shared by the desktop and terminal
    /// apps. The work itself runs through <see cref="XenoWorkspace"/> (inventory, pull, cancel, delete).
    /// </summary>
    public static class XenoModelManagement
    {
        #region Public-Members

        /// <summary>
        /// Label of the per-provider action that opens the dialog.
        /// </summary>
        public const string ActionText = "Manage Models";

        /// <summary>
        /// Shown for providers that are not Ollama, where Xeno can list models but not pull or delete them.
        /// </summary>
        public const string OllamaOnlyNote = "Pulling and deleting models works with Ollama providers only. This provider's models are listed as it reports them; manage them with the provider's own tools.";

        /// <summary>
        /// Label of the option that advertises a pulled model.
        /// </summary>
        public const string AdvertiseLabel = "Advertise when finished";

        /// <summary>
        /// Placeholder for the model to pull.
        /// </summary>
        public const string PullPlaceholder = "llama3.2:latest";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Dialog title for a provider.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <returns>Title.</returns>
        public static string Title(string provider)
        {
            return "Models on " + (provider ?? String.Empty);
        }

        /// <summary>
        /// Rows for an inventory.
        /// </summary>
        /// <param name="inventory">Inventory.</param>
        /// <returns>Rows.</returns>
        /// <exception cref="ArgumentNullException">Inventory is null.</exception>
        public static List<ProviderInventoryRow> Rows(ProviderModelInventory inventory)
        {
            ArgumentNullException.ThrowIfNull(inventory);
            return inventory.Models.Select(model => ProviderInventoryRow.From(inventory.Provider, model)).ToList();
        }

        /// <summary>
        /// One-line summary of an inventory: how many models, or why it could not be read, plus the Ollama-only note
        /// for other providers.
        /// </summary>
        /// <param name="inventory">Inventory.</param>
        /// <returns>Summary.</returns>
        /// <exception cref="ArgumentNullException">Inventory is null.</exception>
        public static string Summary(ProviderModelInventory inventory)
        {
            ArgumentNullException.ThrowIfNull(inventory);
            string text;
            if (!inventory.Reachable) text = "Could not read models from " + inventory.Provider + ": " + (inventory.Error ?? "no answer") + ".";
            else
            {
                int advertised = inventory.Models.Count(item => item.Advertised);
                text = inventory.Models.Count.ToString(CultureInfo.InvariantCulture) + " model" + (inventory.Models.Count == 1 ? String.Empty : "s") + " on " + inventory.Provider + ", " + advertised.ToString(CultureInfo.InvariantCulture) + " advertised.";
            }

            return inventory.CanPull ? text : text + " " + OllamaOnlyNote;
        }

        /// <summary>
        /// Pulls to show in a provider's dialog: active ones first, then the most recent finished ones.
        /// </summary>
        /// <param name="jobs">Every pull.</param>
        /// <param name="provider">Provider name.</param>
        /// <param name="finished">How many finished pulls to keep.</param>
        /// <returns>Pulls.</returns>
        public static List<ModelPullJob> PullsFor(IEnumerable<ModelPullJob> jobs, string provider, int finished = 3)
        {
            List<ModelPullJob> mine = (jobs ?? Enumerable.Empty<ModelPullJob>()).Where(job => String.Equals(job.Provider, provider, StringComparison.OrdinalIgnoreCase)).ToList();
            return mine.Where(job => job.Active).OrderBy(job => job.StartedUtc)
                .Concat(mine.Where(job => !job.Active).OrderByDescending(job => job.FinishedUtc ?? job.UpdatedUtc).Take(Math.Max(0, finished)))
                .ToList();
        }

        /// <summary>
        /// One line describing a pull, for example "Pulling llama3.2 42%" or "Pulled llama3.2".
        /// </summary>
        /// <param name="job">Pull.</param>
        /// <returns>Line.</returns>
        /// <exception cref="ArgumentNullException">Job is null.</exception>
        public static string PullLine(ModelPullJob job)
        {
            ArgumentNullException.ThrowIfNull(job);
            switch (job.State)
            {
                case ModelPullStateEnum.Succeeded:
                    return "Pulled " + job.Model + (job.Advertised ? " and advertised it" : String.Empty) + (String.IsNullOrWhiteSpace(job.Error) ? String.Empty : " (" + job.Error + ")");
                case ModelPullStateEnum.Failed:
                    return "Pull of " + job.Model + " failed: " + (job.Error ?? "unknown error");
                case ModelPullStateEnum.Cancelled:
                    return "Pull of " + job.Model + " cancelled";
                default:
                    return "Pulling " + job.Model + (job.Percent.HasValue ? " " + PercentText(job.Percent.Value) : String.Empty);
            }
        }

        /// <summary>
        /// Status detail for an active pull: the provider's status and the bytes so far.
        /// </summary>
        /// <param name="job">Pull.</param>
        /// <returns>Detail, for example "pulling 8eeb52dfb3bb: 1.2 GB of 2.0 GB".</returns>
        /// <exception cref="ArgumentNullException">Job is null.</exception>
        public static string PullDetail(ModelPullJob job)
        {
            ArgumentNullException.ThrowIfNull(job);
            string status = String.IsNullOrWhiteSpace(job.Status) ? job.State.ToString().ToLowerInvariant() : job.Status;
            if (job.TotalBytes > 0) status += ": " + DisplayFormat.Bytes(job.CompletedBytes) + " of " + DisplayFormat.Bytes(job.TotalBytes);
            return status;
        }

        /// <summary>
        /// Progress fraction from 0 to 1 for a progress bar, 0 until sizes are known.
        /// </summary>
        /// <param name="job">Pull.</param>
        /// <returns>Fraction.</returns>
        /// <exception cref="ArgumentNullException">Job is null.</exception>
        public static double Fraction(ModelPullJob job)
        {
            ArgumentNullException.ThrowIfNull(job);
            if (job.State == ModelPullStateEnum.Succeeded) return 1;
            return job.Percent.HasValue ? Math.Clamp(job.Percent.Value / 100.0, 0, 1) : 0;
        }

        /// <summary>
        /// Persistent indicator for active pulls on every page, or null when nothing is pulling: "Pulling llama3.2 42%",
        /// or "Pulling 3 models" for several.
        /// </summary>
        /// <param name="jobs">Every pull.</param>
        /// <returns>Indicator, or null.</returns>
        public static string? ActiveIndicator(IEnumerable<ModelPullJob> jobs)
        {
            List<ModelPullJob> active = (jobs ?? Enumerable.Empty<ModelPullJob>()).Where(job => job.Active).OrderBy(job => job.StartedUtc).ToList();
            if (active.Count == 0) return null;
            if (active.Count == 1) return PullLine(active[0]);
            return "Pulling " + active.Count.ToString(CultureInfo.InvariantCulture) + " models";
        }

        /// <summary>
        /// Message for a pull that just finished, for the message line or status bar.
        /// </summary>
        /// <param name="job">Pull.</param>
        /// <returns>Message.</returns>
        /// <exception cref="ArgumentNullException">Job is null.</exception>
        public static string FinishedMessage(ModelPullJob job)
        {
            ArgumentNullException.ThrowIfNull(job);
            return PullLine(job) + (job.State == ModelPullStateEnum.Succeeded ? " onto " + job.Provider + "." : ".");
        }

        /// <summary>
        /// Confirmation text before deleting a model from a provider's disk.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="model">Model name.</param>
        /// <param name="advertised">True when the model is advertised.</param>
        /// <returns>Message.</returns>
        public static string DeleteConfirmation(string provider, string model, bool advertised)
        {
            return "Delete '" + model + "' from " + provider + "? Ollama removes it from disk"
                + (advertised ? ", and Xeno removes its advertisement so the network stops sending requests for it." : ". It is not advertised, so nothing else changes.")
                + " Pull it again to get it back.";
        }

        /// <summary>
        /// Message after a delete.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="model">Model name.</param>
        /// <param name="advertisementsRemoved">Advertisements removed.</param>
        /// <returns>Message.</returns>
        public static string Deleted(string provider, string model, int advertisementsRemoved)
        {
            return "Deleted '" + model + "' from " + provider + (advertisementsRemoved > 0 ? " and removed its advertisement." : ".");
        }

        #endregion

        #region Private-Methods

        private static string PercentText(double percent)
        {
            return Math.Floor(percent).ToString("0", CultureInfo.InvariantCulture) + "%";
        }

        #endregion
    }
}

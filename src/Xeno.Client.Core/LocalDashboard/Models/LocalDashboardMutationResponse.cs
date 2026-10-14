namespace Xeno.Client.LocalDashboard.Models
{
    /// <summary>
    /// Local dashboard mutation response.
    /// </summary>
    public class LocalDashboardMutationResponse
    {
        /// <summary>
        /// Number of removed records.
        /// </summary>
        public int Removed { get; set; } = 0;

        /// <summary>
        /// Whether one record was removed.
        /// </summary>
        public bool RemovedOne { get; set; } = false;

        /// <summary>
        /// Whether the client inventory update flag was requested.
        /// </summary>
        public bool InventoryUpdateRequested { get; set; } = false;

        /// <summary>
        /// Settings file affected by the mutation.
        /// </summary>
        public string? SettingsFile { get; set; } = null;
    }
}

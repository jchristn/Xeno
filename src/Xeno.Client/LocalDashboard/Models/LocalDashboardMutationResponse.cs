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
    }
}

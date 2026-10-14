namespace Xeno.Client.LocalDashboard.Models
{
    /// <summary>
    /// Local dashboard database settings update request.
    /// </summary>
    public class LocalDashboardDatabaseSettingsUpdateRequest
    {
        /// <summary>
        /// Database filename.
        /// </summary>
        public string Filename { get; set; } = string.Empty;

        /// <summary>
        /// Optional connection string override.
        /// </summary>
        public string? ConnectionString { get; set; } = null;
    }
}

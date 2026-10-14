namespace Xeno.Client.LocalDashboard.Models
{
    /// <summary>
    /// Local dashboard database status.
    /// </summary>
    public enum LocalDashboardDatabaseStatusEnum
    {
        /// <summary>
        /// Database status is unknown.
        /// </summary>
        Unknown,

        /// <summary>
        /// Database file exists.
        /// </summary>
        Ready,

        /// <summary>
        /// Database file does not exist yet.
        /// </summary>
        Missing
    }
}

namespace Xeno.Client.LocalDashboard.Database
{
    using Xeno.Client.LocalDashboard.Database.Sqlite;

    /// <summary>
    /// Local dashboard database driver factory.
    /// </summary>
    public static class LocalDashboardDatabaseDriverFactory
    {
        /// <summary>
        /// Create a local dashboard database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <returns>Database driver.</returns>
        public static LocalDashboardDatabaseDriverBase Create(ClientLocalDatabaseSettings settings)
        {
            return new SqliteLocalDashboardDatabaseDriver(settings);
        }
    }
}

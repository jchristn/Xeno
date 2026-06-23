namespace Xeno.Core.Database
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Database.Mysql;
    using Xeno.Core.Database.Postgresql;
    using Xeno.Core.Database.Sqlite;
    using Xeno.Core.Database.SqlServer;
    using Xeno.Core.Enums;
    using Xeno.Core.Settings;

    /// <summary>
    /// Database driver factory.
    /// </summary>
    public static class DatabaseDriverFactory
    {
        /// <summary>
        /// Create a database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <returns>Database driver.</returns>
        public static DatabaseDriverBase Create(DatabaseSettings settings)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            switch (settings.Type)
            {
                case DatabaseTypeEnum.Sqlite:
                    return new SqliteDatabaseDriver();
                case DatabaseTypeEnum.Mysql:
                    return new MysqlDatabaseDriver();
                case DatabaseTypeEnum.Postgresql:
                    return new PostgresqlDatabaseDriver();
                case DatabaseTypeEnum.SqlServer:
                    return new SqlServerDatabaseDriver();
                default:
                    throw new ArgumentException("Unknown database type " + settings.Type + ".", nameof(settings));
            }
        }

        /// <summary>
        /// Create and initialize a database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Initialized driver.</returns>
        public static async Task<DatabaseDriverBase> CreateAndInitializeAsync(DatabaseSettings settings, CancellationToken token = default)
        {
            DatabaseDriverBase driver = Create(settings);
            await driver.InitializeAsync(token).ConfigureAwait(false);
            return driver;
        }
    }
}

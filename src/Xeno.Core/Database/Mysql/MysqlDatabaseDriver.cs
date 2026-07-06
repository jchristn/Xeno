namespace Xeno.Core.Database.Mysql
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using MySql.Data.MySqlClient;
    using Xeno.Core.Database;
    using Xeno.Core.Enums;
    using Xeno.Core.Settings;

    /// <summary>
    /// MySQL database driver.
    /// </summary>
    public class MysqlDatabaseDriver : DatabaseDriverBase
    {
        private const string StateId = "platform";
        private string _ConnectionString = String.Empty;

        /// <summary>
        /// Instantiate the MySQL database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        public MysqlDatabaseDriver(DatabaseSettings settings) : base(settings)
        {
        }

        /// <summary>
        /// Database provider type.
        /// </summary>
        public override DatabaseTypeEnum DatabaseType { get { return DatabaseTypeEnum.Mysql; } }

        /// <inheritdoc />
        protected override async Task<bool> InitializeStorageAsync(CancellationToken token = default)
        {
            if (!TryBuildConnectionString(out _ConnectionString)) return false;

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = """
CREATE TABLE IF NOT EXISTS schema_migrations
(
    id varchar(64) PRIMARY KEY,
    name varchar(256) NOT NULL,
    applied_utc datetime(6) NOT NULL
);

CREATE TABLE IF NOT EXISTS xeno_platform_state
(
    id varchar(64) PRIMARY KEY,
    data longtext NOT NULL,
    created_utc datetime(6) NOT NULL,
    updated_utc datetime(6) NOT NULL,
    active tinyint(1) NOT NULL DEFAULT 1
);

INSERT IGNORE INTO schema_migrations (id, name, applied_utc)
VALUES ('0001', 'initial_platform_state', UTC_TIMESTAMP(6));
""";
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                await MysqlTelemetryMethods.InitializeAsync(connection, token).ConfigureAwait(false);
            }

            MysqlTelemetryMethods telemetryMethods = new MysqlTelemetryMethods(_ConnectionString);
            ClientTelemetry = telemetryMethods;
            ClientProviderTelemetry = telemetryMethods;
            ClientTelemetryCollectionAttempts = telemetryMethods;
            return true;
        }

        /// <inheritdoc />
        protected override async Task<string?> LoadStateJsonAsync(CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(_ConnectionString)) return null;
            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT data FROM xeno_platform_state WHERE id = @id AND active = 1;";
                    command.Parameters.AddWithValue("@id", StateId);
                    object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    return value?.ToString();
                }
            }
        }

        /// <inheritdoc />
        protected override async Task SaveStateJsonAsync(string stateJson, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(_ConnectionString)) return;
            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = """
INSERT INTO xeno_platform_state (id, data, created_utc, updated_utc, active)
VALUES (@id, @data, UTC_TIMESTAMP(6), UTC_TIMESTAMP(6), 1)
ON DUPLICATE KEY UPDATE
    data = VALUES(data),
    updated_utc = UTC_TIMESTAMP(6),
    active = 1;
""";
                    command.Parameters.AddWithValue("@id", StateId);
                    command.Parameters.AddWithValue("@data", stateJson);
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public override async Task<int> PurgeTenantDataAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            token.ThrowIfCancellationRequested();
            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                return await MysqlTelemetryMethods.PurgeTenantAsync(connection, tenantId, token).ConfigureAwait(false);
            }
        }

        private bool TryBuildConnectionString(out string connectionString)
        {
            if (!String.IsNullOrWhiteSpace(Settings.ConnectionString))
            {
                connectionString = Settings.ConnectionString;
                return true;
            }

            if (String.IsNullOrWhiteSpace(Settings.Server) || String.IsNullOrWhiteSpace(Settings.DatabaseName))
            {
                connectionString = String.Empty;
                return false;
            }

            MySqlConnectionStringBuilder builder = new MySqlConnectionStringBuilder
            {
                Server = Settings.Server,
                Port = (uint)(Settings.Port > 0 ? Settings.Port : 3306),
                Database = Settings.DatabaseName,
                UserID = Settings.Username ?? String.Empty,
                Password = Settings.Password ?? String.Empty
            };
            connectionString = builder.ConnectionString;
            return true;
        }
    }
}

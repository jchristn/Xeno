namespace Xeno.Core.Database.Sqlite
{
    using System;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Xeno.Core.Database;
    using Xeno.Core.Enums;
    using Xeno.Core.Settings;

    /// <summary>
    /// SQLite database driver.
    /// </summary>
    public class SqliteDatabaseDriver : DatabaseDriverBase
    {
        private const string StateId = "platform";
        private string _ConnectionString = String.Empty;

        /// <summary>
        /// Instantiate the SQLite database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        public SqliteDatabaseDriver(DatabaseSettings settings) : base(settings)
        {
        }

        /// <summary>
        /// Database provider type.
        /// </summary>
        public override DatabaseTypeEnum DatabaseType { get { return DatabaseTypeEnum.Sqlite; } }

        /// <inheritdoc />
        protected override async Task<bool> InitializeStorageAsync(CancellationToken token = default)
        {
            string filename = String.IsNullOrWhiteSpace(Settings.Filename) ? "data/xeno.db" : Settings.Filename;
            string? directory = Path.GetDirectoryName(Path.GetFullPath(filename));
            if (!String.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);

            _ConnectionString = String.IsNullOrWhiteSpace(Settings.ConnectionString)
                ? "Data Source=" + filename + ";Pooling=False;"
                : Settings.ConnectionString;

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = """
CREATE TABLE IF NOT EXISTS schema_migrations
(
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    applied_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS xeno_platform_state
(
    id TEXT PRIMARY KEY,
    data TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL,
    active INTEGER NOT NULL DEFAULT 1
);

INSERT OR IGNORE INTO schema_migrations (id, name, applied_utc)
VALUES ('0001', 'initial_platform_state', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
""";
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                await SqliteTelemetryMethods.InitializeAsync(connection, token).ConfigureAwait(false);
            }

            SqliteTelemetryMethods telemetryMethods = new SqliteTelemetryMethods(_ConnectionString);
            ClientTelemetry = telemetryMethods;
            ClientProviderTelemetry = telemetryMethods;
            ClientTelemetryCollectionAttempts = telemetryMethods;
            return true;
        }

        /// <inheritdoc />
        protected override async Task<string?> LoadStateJsonAsync(CancellationToken token = default)
        {
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT data FROM xeno_platform_state WHERE id = $id AND active = 1;";
                    command.Parameters.AddWithValue("$id", StateId);
                    object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    return value?.ToString();
                }
            }
        }

        /// <inheritdoc />
        protected override async Task SaveStateJsonAsync(string stateJson, CancellationToken token = default)
        {
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = """
INSERT INTO xeno_platform_state (id, data, created_utc, updated_utc, active)
VALUES ($id, $data, $createdUtc, $updatedUtc, 1)
ON CONFLICT(id) DO UPDATE SET
    data = excluded.data,
    updated_utc = excluded.updated_utc,
    active = 1;
""";
                    string now = DateTime.UtcNow.ToString("O");
                    command.Parameters.AddWithValue("$id", StateId);
                    command.Parameters.AddWithValue("$data", stateJson);
                    command.Parameters.AddWithValue("$createdUtc", now);
                    command.Parameters.AddWithValue("$updatedUtc", now);
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public override async Task<int> PurgeTenantDataAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                return await SqliteTelemetryMethods.PurgeTenantAsync(connection, tenantId, token).ConfigureAwait(false);
            }
        }
    }
}

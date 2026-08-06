namespace Xeno.Core.Database.Sqlite
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Microsoft.Data.Sqlite;
    using Xeno.Core.Database;
    using Xeno.Core.Enums;
    using Xeno.Core.Settings;

    /// <summary>
    /// SQLite database driver.
    /// </summary>
    public class SqliteDatabaseDriver : DatabaseDriverBase
    {
        private string _ConnectionString = String.Empty;

        /// <summary>
        /// Instantiate the SQLite database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <param name="logging">Optional logging module.</param>
        public SqliteDatabaseDriver(DatabaseSettings settings, LoggingModule? logging = null) : base(settings, logging)
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

INSERT OR IGNORE INTO schema_migrations (id, name, applied_utc)
VALUES ('0001', 'initial_platform_state', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
""";
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                await SqliteTelemetryMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqliteRequestHistoryMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqliteAuditMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqliteOnboardingMethods.InitializeAsync(connection, token).ConfigureAwait(false);

                await SqliteTenantMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqliteUserMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqliteCredentialMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqliteAuthSessionMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqliteRoleMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqlitePermissionMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqliteRoleAssignmentMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqliteClientMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqliteModelDefinitionMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqliteModelConfigurationMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqliteRoutingPolicyMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqliteLedgerTenantLinkMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await SqliteLedgerAccountLinkMethods.InitializeAsync(connection, token).ConfigureAwait(false);

                List<SchemaMigration> forwardMigrations = new List<SchemaMigration>
                {
                    // Post-baseline schema changes are applied in ascending version order and recorded in
                    // schema_migrations; add new SchemaMigration entries here as the schema evolves.
                };
                await SchemaMigrationRunner.ApplyAsync(connection, "strftime('%Y-%m-%dT%H:%M:%fZ', 'now')", forwardMigrations, token).ConfigureAwait(false);
            }

            SqliteTelemetryMethods telemetryMethods = new SqliteTelemetryMethods(_ConnectionString);
            ClientTelemetry = telemetryMethods;
            ClientProviderTelemetry = telemetryMethods;
            ClientTelemetryCollectionAttempts = telemetryMethods;
            RequestHistory = new SqliteRequestHistoryMethods(_ConnectionString);
            Audit = new SqliteAuditMethods(_ConnectionString);
            Onboarding = new SqliteOnboardingMethods(_ConnectionString);

            Tenants = new SqliteTenantMethods(_ConnectionString);
            Users = new SqliteUserMethods(_ConnectionString);
            Credentials = new SqliteCredentialMethods(_ConnectionString);
            AuthSessions = new SqliteAuthSessionMethods(_ConnectionString);
            Roles = new SqliteRoleMethods(_ConnectionString);
            Permissions = new SqlitePermissionMethods(_ConnectionString);
            RoleAssignments = new SqliteRoleAssignmentMethods(_ConnectionString);
            Clients = new SqliteClientMethods(_ConnectionString);
            ModelDefinitions = new SqliteModelDefinitionMethods(_ConnectionString);
            ModelConfigurations = new SqliteModelConfigurationMethods(_ConnectionString);
            RoutingPolicies = new SqliteRoutingPolicyMethods(_ConnectionString);
            LedgerTenantLinks = new SqliteLedgerTenantLinkMethods(_ConnectionString);
            LedgerAccountLinks = new SqliteLedgerAccountLinkMethods(_ConnectionString);
            return true;
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

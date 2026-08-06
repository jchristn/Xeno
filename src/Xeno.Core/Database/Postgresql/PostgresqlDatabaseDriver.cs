namespace Xeno.Core.Database.Postgresql
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using Npgsql;
    using Xeno.Core.Database;
    using Xeno.Core.Enums;
    using Xeno.Core.Settings;

    /// <summary>
    /// PostgreSQL database driver.
    /// </summary>
    public class PostgresqlDatabaseDriver : DatabaseDriverBase
    {
        private string _ConnectionString = String.Empty;

        /// <summary>
        /// Instantiate the PostgreSQL database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <param name="logging">Optional logging module.</param>
        public PostgresqlDatabaseDriver(DatabaseSettings settings, LoggingModule? logging = null) : base(settings, logging)
        {
        }

        /// <summary>
        /// Database provider type.
        /// </summary>
        public override DatabaseTypeEnum DatabaseType { get { return DatabaseTypeEnum.Postgresql; } }

        /// <inheritdoc />
        protected override async Task<bool> InitializeStorageAsync(CancellationToken token = default)
        {
            if (!TryBuildConnectionString(out _ConnectionString)) return false;

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = """
CREATE TABLE IF NOT EXISTS schema_migrations
(
    id varchar(64) PRIMARY KEY,
    name varchar(256) NOT NULL,
    applied_utc timestamptz NOT NULL
);

INSERT INTO schema_migrations (id, name, applied_utc)
VALUES ('0001', 'initial_platform_state', now())
ON CONFLICT (id) DO NOTHING;
""";
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                await PostgresqlTelemetryMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await PostgresqlRequestHistoryMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await PostgresqlAuditMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await PostgresqlOnboardingMethods.InitializeAsync(connection, token).ConfigureAwait(false);

                await PostgresqlTenantMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await PostgresqlUserMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await PostgresqlCredentialMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await PostgresqlAuthSessionMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await PostgresqlRoleMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await PostgresqlPermissionMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await PostgresqlRoleAssignmentMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await PostgresqlClientMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await PostgresqlModelDefinitionMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await PostgresqlModelConfigurationMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await PostgresqlRoutingPolicyMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await PostgresqlLedgerTenantLinkMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await PostgresqlLedgerAccountLinkMethods.InitializeAsync(connection, token).ConfigureAwait(false);

                List<SchemaMigration> forwardMigrations = new List<SchemaMigration>
                {
                    // Post-baseline schema changes are applied in ascending version order and recorded in
                    // schema_migrations; add new SchemaMigration entries here as the schema evolves.
                };
                await SchemaMigrationRunner.ApplyAsync(connection, "now()", forwardMigrations, token).ConfigureAwait(false);
            }

            PostgresqlTelemetryMethods telemetryMethods = new PostgresqlTelemetryMethods(_ConnectionString);
            ClientTelemetry = telemetryMethods;
            ClientProviderTelemetry = telemetryMethods;
            ClientTelemetryCollectionAttempts = telemetryMethods;
            RequestHistory = new PostgresqlRequestHistoryMethods(_ConnectionString);
            Audit = new PostgresqlAuditMethods(_ConnectionString);
            Onboarding = new PostgresqlOnboardingMethods(_ConnectionString);

            Tenants = new PostgresqlTenantMethods(_ConnectionString);
            Users = new PostgresqlUserMethods(_ConnectionString);
            Credentials = new PostgresqlCredentialMethods(_ConnectionString);
            AuthSessions = new PostgresqlAuthSessionMethods(_ConnectionString);
            Roles = new PostgresqlRoleMethods(_ConnectionString);
            Permissions = new PostgresqlPermissionMethods(_ConnectionString);
            RoleAssignments = new PostgresqlRoleAssignmentMethods(_ConnectionString);
            Clients = new PostgresqlClientMethods(_ConnectionString);
            ModelDefinitions = new PostgresqlModelDefinitionMethods(_ConnectionString);
            ModelConfigurations = new PostgresqlModelConfigurationMethods(_ConnectionString);
            RoutingPolicies = new PostgresqlRoutingPolicyMethods(_ConnectionString);
            LedgerTenantLinks = new PostgresqlLedgerTenantLinkMethods(_ConnectionString);
            LedgerAccountLinks = new PostgresqlLedgerAccountLinkMethods(_ConnectionString);
            return true;
        }

        /// <inheritdoc />
        public override async Task<int> PurgeTenantDataAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            token.ThrowIfCancellationRequested();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                return await PostgresqlTelemetryMethods.PurgeTenantAsync(connection, tenantId, token).ConfigureAwait(false);
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

            NpgsqlConnectionStringBuilder builder = new NpgsqlConnectionStringBuilder
            {
                Host = Settings.Server,
                Port = Settings.Port > 0 ? Settings.Port : 5432,
                Database = Settings.DatabaseName,
                Username = Settings.Username ?? String.Empty,
                Password = Settings.Password ?? String.Empty
            };
            connectionString = builder.ConnectionString;
            return true;
        }
    }
}

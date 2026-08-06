namespace Xeno.Core.Database.Mysql
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using SyslogLogging;
    using MySql.Data.MySqlClient;
    using Xeno.Core.Database;
    using Xeno.Core.Enums;
    using Xeno.Core.Settings;

    /// <summary>
    /// MySQL database driver.
    /// </summary>
    public class MysqlDatabaseDriver : DatabaseDriverBase
    {
        private string _ConnectionString = String.Empty;

        /// <summary>
        /// Instantiate the MySQL database driver.
        /// </summary>
        /// <param name="settings">Database settings.</param>
        /// <param name="logging">Optional logging module.</param>
        public MysqlDatabaseDriver(DatabaseSettings settings, LoggingModule? logging = null) : base(settings, logging)
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

INSERT IGNORE INTO schema_migrations (id, name, applied_utc)
VALUES ('0001', 'initial_platform_state', UTC_TIMESTAMP(6));
""";
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                await MysqlTelemetryMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await MysqlRequestHistoryMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await MysqlAuditMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await MysqlOnboardingMethods.InitializeAsync(connection, token).ConfigureAwait(false);

                await MysqlTenantMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await MysqlUserMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await MysqlCredentialMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await MysqlAuthSessionMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await MysqlRoleMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await MysqlPermissionMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await MysqlRoleAssignmentMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await MysqlClientMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await MysqlModelDefinitionMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await MysqlModelConfigurationMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await MysqlRoutingPolicyMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await MysqlLedgerTenantLinkMethods.InitializeAsync(connection, token).ConfigureAwait(false);
                await MysqlLedgerAccountLinkMethods.InitializeAsync(connection, token).ConfigureAwait(false);

                List<SchemaMigration> forwardMigrations = new List<SchemaMigration>
                {
                    // Post-baseline schema changes are applied in ascending version order and recorded in
                    // schema_migrations; add new SchemaMigration entries here as the schema evolves.
                };
                await SchemaMigrationRunner.ApplyAsync(connection, "UTC_TIMESTAMP(6)", forwardMigrations, token).ConfigureAwait(false);
            }

            MysqlTelemetryMethods telemetryMethods = new MysqlTelemetryMethods(_ConnectionString);
            ClientTelemetry = telemetryMethods;
            ClientProviderTelemetry = telemetryMethods;
            ClientTelemetryCollectionAttempts = telemetryMethods;
            RequestHistory = new MysqlRequestHistoryMethods(_ConnectionString);
            Audit = new MysqlAuditMethods(_ConnectionString);
            Onboarding = new MysqlOnboardingMethods(_ConnectionString);

            Tenants = new MysqlTenantMethods(_ConnectionString);
            Users = new MysqlUserMethods(_ConnectionString);
            Credentials = new MysqlCredentialMethods(_ConnectionString);
            AuthSessions = new MysqlAuthSessionMethods(_ConnectionString);
            Roles = new MysqlRoleMethods(_ConnectionString);
            Permissions = new MysqlPermissionMethods(_ConnectionString);
            RoleAssignments = new MysqlRoleAssignmentMethods(_ConnectionString);
            Clients = new MysqlClientMethods(_ConnectionString);
            ModelDefinitions = new MysqlModelDefinitionMethods(_ConnectionString);
            ModelConfigurations = new MysqlModelConfigurationMethods(_ConnectionString);
            RoutingPolicies = new MysqlRoutingPolicyMethods(_ConnectionString);
            LedgerTenantLinks = new MysqlLedgerTenantLinkMethods(_ConnectionString);
            LedgerAccountLinks = new MysqlLedgerAccountLinkMethods(_ConnectionString);
            return true;
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

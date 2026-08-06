namespace Xeno.Core.Database.Sqlite
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;

    /// <summary>
    /// SQLite role assignment data access methods. Role assignment fields map to columns on
    /// <c>role_assignments</c>. Role assignments are tenant-owned and every query is tenant-scoped.
    /// No domain object is stored as a JSON document.
    /// </summary>
    public class SqliteRoleAssignmentMethods : IRoleAssignmentMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public SqliteRoleAssignmentMethods(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));
            _ConnectionString = connectionString;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create the role assignment table and indexes if they do not exist.
        /// </summary>
        /// <param name="connection">Open connection.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public static async Task InitializeAsync(SqliteConnection connection, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(connection);
            token.ThrowIfCancellationRequested();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = SchemaSql();
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<RoleAssignment> CreateAsync(RoleAssignment assignment, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(assignment);
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(assignment.Id)) assignment.Id = IdGenerator.NewId("rmap_");

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (DbTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertRowAsync(connection, transaction, assignment, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return assignment;
        }

        /// <inheritdoc />
        public async Task<RoleAssignment?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return null;
            if (String.IsNullOrWhiteSpace(id)) return null;
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + AssignmentColumns + " FROM role_assignments WHERE tenant_id = @tenant_id AND id = @id LIMIT 1;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) return HydrateAssignment(reader);
                    }
                }
            }

            return null;
        }

        /// <inheritdoc />
        public async Task<List<RoleAssignment>> EnumerateAsync(string tenantId, CancellationToken token = default)
        {
            List<RoleAssignment> assignments = new List<RoleAssignment>();
            if (String.IsNullOrWhiteSpace(tenantId)) return assignments;
            token.ThrowIfCancellationRequested();

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + AssignmentColumns + " FROM role_assignments WHERE tenant_id = @tenant_id ORDER BY created_utc ASC, id ASC;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false)) assignments.Add(HydrateAssignment(reader));
                    }
                }
            }

            return assignments;
        }

        /// <inheritdoc />
        public async Task<List<RoleAssignment>> EnumerateByRoleAsync(string tenantId, string roleId, CancellationToken token = default)
        {
            List<RoleAssignment> assignments = new List<RoleAssignment>();
            if (String.IsNullOrWhiteSpace(tenantId)) return assignments;
            if (String.IsNullOrWhiteSpace(roleId)) return assignments;
            token.ThrowIfCancellationRequested();

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + AssignmentColumns + " FROM role_assignments WHERE tenant_id = @tenant_id AND role_id = @role_id ORDER BY created_utc ASC, id ASC;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@role_id", roleId);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false)) assignments.Add(HydrateAssignment(reader));
                    }
                }
            }

            return assignments;
        }

        /// <inheritdoc />
        public async Task<List<RoleAssignment>> EnumerateByPrincipalAsync(string tenantId, string principalId, CancellationToken token = default)
        {
            List<RoleAssignment> assignments = new List<RoleAssignment>();
            if (String.IsNullOrWhiteSpace(tenantId)) return assignments;
            if (String.IsNullOrWhiteSpace(principalId)) return assignments;
            token.ThrowIfCancellationRequested();

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + AssignmentColumns + " FROM role_assignments WHERE tenant_id = @tenant_id AND principal_id = @principal_id ORDER BY created_utc ASC, id ASC;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@principal_id", principalId);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false)) assignments.Add(HydrateAssignment(reader));
                    }
                }
            }

            return assignments;
        }

        /// <inheritdoc />
        public async Task<RoleAssignment> UpdateAsync(RoleAssignment assignment, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(assignment);
            if (String.IsNullOrWhiteSpace(assignment.Id)) throw new ArgumentException("Role assignment ID is required.", nameof(assignment));
            token.ThrowIfCancellationRequested();
            assignment.LastUpdateUtc = DateTime.UtcNow;

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (DbTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertRowAsync(connection, transaction, assignment, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return assignment;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return false;
            if (String.IsNullOrWhiteSpace(id)) return false;
            token.ThrowIfCancellationRequested();

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM role_assignments WHERE tenant_id = @tenant_id AND id = @id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    int rows = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    return rows > 0;
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteByRoleAsync(string tenantId, string roleId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return 0;
            if (String.IsNullOrWhiteSpace(roleId)) return 0;
            token.ThrowIfCancellationRequested();

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM role_assignments WHERE tenant_id = @tenant_id AND role_id = @role_id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@role_id", roleId);
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        #endregion

        #region Private-Methods

        private const string AssignmentColumns =
            "id, tenant_id, role_id, principal_type, principal_id, created_utc, last_update_utc, active";

        private static async Task UpsertRowAsync(SqliteConnection connection, DbTransaction transaction, RoleAssignment assignment, CancellationToken token)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = (SqliteTransaction)transaction;
                command.CommandText = """
INSERT INTO role_assignments (id, tenant_id, role_id, principal_type, principal_id, created_utc, last_update_utc, active)
VALUES (@id, @tenant_id, @role_id, @principal_type, @principal_id, @created_utc, @last_update_utc, @active)
ON CONFLICT(id) DO UPDATE SET
    tenant_id = excluded.tenant_id,
    role_id = excluded.role_id,
    principal_type = excluded.principal_type,
    principal_id = excluded.principal_id,
    last_update_utc = excluded.last_update_utc,
    active = excluded.active;
""";
                command.Parameters.AddWithValue("@id", assignment.Id);
                command.Parameters.AddWithValue("@tenant_id", assignment.TenantId ?? String.Empty);
                command.Parameters.AddWithValue("@role_id", assignment.RoleId ?? String.Empty);
                command.Parameters.AddWithValue("@principal_type", assignment.PrincipalType.ToString());
                command.Parameters.AddWithValue("@principal_id", assignment.PrincipalId ?? String.Empty);
                command.Parameters.AddWithValue("@created_utc", FormatUtc(assignment.CreatedUtc));
                command.Parameters.AddWithValue("@last_update_utc", FormatUtc(assignment.LastUpdateUtc));
                command.Parameters.AddWithValue("@active", assignment.Active ? 1 : 0);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static RoleAssignment HydrateAssignment(DbDataReader reader)
        {
            return new RoleAssignment
            {
                Id = reader.GetString(0),
                TenantId = reader.IsDBNull(1) ? String.Empty : reader.GetString(1),
                RoleId = reader.IsDBNull(2) ? String.Empty : reader.GetString(2),
                PrincipalType = ParseEnum(reader, 3, PrincipalTypeEnum.User),
                PrincipalId = reader.IsDBNull(4) ? String.Empty : reader.GetString(4),
                CreatedUtc = ParseUtc(reader.GetString(5)),
                LastUpdateUtc = ParseUtc(reader.GetString(6)),
                Active = reader.GetInt64(7) != 0
            };
        }

        private static PrincipalTypeEnum ParseEnum(DbDataReader reader, int ordinal, PrincipalTypeEnum fallback)
        {
            if (reader.IsDBNull(ordinal)) return fallback;
            return Enum.TryParse(reader.GetString(ordinal), out PrincipalTypeEnum parsed) ? parsed : fallback;
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);
        }

        private static DateTime ParseUtc(string value)
        {
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out DateTime parsed))
            {
                return DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
            }

            return DateTime.UtcNow;
        }

        private static string SchemaSql()
        {
            return """
CREATE TABLE IF NOT EXISTS role_assignments
(
    id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    role_id TEXT NOT NULL,
    principal_type TEXT NOT NULL,
    principal_id TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL,
    active INTEGER NOT NULL DEFAULT 1
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_role_assignments_unique ON role_assignments(tenant_id, role_id, principal_type, principal_id);
CREATE INDEX IF NOT EXISTS idx_role_assignments_tenant ON role_assignments(tenant_id);
CREATE INDEX IF NOT EXISTS idx_role_assignments_tenant_role ON role_assignments(tenant_id, role_id);
CREATE INDEX IF NOT EXISTS idx_role_assignments_tenant_principal ON role_assignments(tenant_id, principal_type, principal_id);
""";
        }

        #endregion
    }
}

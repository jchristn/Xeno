namespace Xeno.Core.Database.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;

    /// <summary>
    /// SQL Server role assignment data access methods. Role assignment fields map to columns on
    /// <c>dbo.role_assignments</c>. Role assignments are tenant-owned and every query is tenant-scoped.
    /// </summary>
    public class SqlServerRoleAssignmentMethods : IRoleAssignmentMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public SqlServerRoleAssignmentMethods(string connectionString)
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
        public static async Task InitializeAsync(SqlConnection connection, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(connection);
            token.ThrowIfCancellationRequested();
            using (SqlCommand command = connection.CreateCommand())
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

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + AssignmentColumns + " FROM dbo.role_assignments WHERE tenant_id = @tenant_id AND id = @id;";
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

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + AssignmentColumns + " FROM dbo.role_assignments WHERE tenant_id = @tenant_id ORDER BY created_utc ASC, id ASC;";
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

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + AssignmentColumns + " FROM dbo.role_assignments WHERE tenant_id = @tenant_id AND role_id = @role_id ORDER BY created_utc ASC, id ASC;";
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

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + AssignmentColumns + " FROM dbo.role_assignments WHERE tenant_id = @tenant_id AND principal_id = @principal_id ORDER BY created_utc ASC, id ASC;";
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

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM dbo.role_assignments WHERE tenant_id = @tenant_id AND id = @id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    int rows = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    return rows > 0;
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteByTenantAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return 0;
            token.ThrowIfCancellationRequested();

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM dbo.role_assignments WHERE tenant_id = @tenant_id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteByPrincipalAsync(string tenantId, Xeno.Core.Enums.PrincipalTypeEnum principalType, string principalId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return 0;
            if (String.IsNullOrWhiteSpace(principalId)) return 0;
            token.ThrowIfCancellationRequested();

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM dbo.role_assignments WHERE tenant_id = @tenant_id AND principal_type = @principal_type AND principal_id = @principal_id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@principal_type", principalType.ToString());
                    command.Parameters.AddWithValue("@principal_id", principalId);
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteByRoleAsync(string tenantId, string roleId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return 0;
            if (String.IsNullOrWhiteSpace(roleId)) return 0;
            token.ThrowIfCancellationRequested();

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM dbo.role_assignments WHERE tenant_id = @tenant_id AND role_id = @role_id;";
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

        private static async Task UpsertRowAsync(SqlConnection connection, SqlTransaction transaction, RoleAssignment assignment, CancellationToken token)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
IF EXISTS (SELECT 1 FROM dbo.role_assignments WHERE id = @id)
    UPDATE dbo.role_assignments SET
        tenant_id = @tenant_id,
        role_id = @role_id,
        principal_type = @principal_type,
        principal_id = @principal_id,
        last_update_utc = @last_update_utc,
        active = @active
    WHERE id = @id;
ELSE
    INSERT INTO dbo.role_assignments (id, tenant_id, role_id, principal_type, principal_id, created_utc, last_update_utc, active)
    VALUES (@id, @tenant_id, @role_id, @principal_type, @principal_id, @created_utc, @last_update_utc, @active);
""";
                command.Parameters.AddWithValue("@id", assignment.Id);
                command.Parameters.AddWithValue("@tenant_id", assignment.TenantId ?? String.Empty);
                command.Parameters.AddWithValue("@role_id", assignment.RoleId ?? String.Empty);
                command.Parameters.AddWithValue("@principal_type", assignment.PrincipalType.ToString());
                command.Parameters.AddWithValue("@principal_id", assignment.PrincipalId ?? String.Empty);
                command.Parameters.AddWithValue("@created_utc", FormatUtc(assignment.CreatedUtc));
                command.Parameters.AddWithValue("@last_update_utc", FormatUtc(assignment.LastUpdateUtc));
                command.Parameters.AddWithValue("@active", assignment.Active);
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
                Active = reader.GetBoolean(7)
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
IF OBJECT_ID(N'dbo.role_assignments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.role_assignments
    (
        id nvarchar(64) NOT NULL PRIMARY KEY,
        tenant_id nvarchar(64) NOT NULL,
        role_id nvarchar(64) NOT NULL,
        principal_type nvarchar(64) NOT NULL,
        principal_id nvarchar(64) NOT NULL,
        created_utc nvarchar(64) NOT NULL,
        last_update_utc nvarchar(64) NOT NULL,
        active bit NOT NULL DEFAULT 1
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_role_assignments_unique' AND object_id = OBJECT_ID(N'dbo.role_assignments'))
    CREATE UNIQUE INDEX idx_role_assignments_unique ON dbo.role_assignments(tenant_id, role_id, principal_type, principal_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_role_assignments_tenant' AND object_id = OBJECT_ID(N'dbo.role_assignments'))
    CREATE INDEX idx_role_assignments_tenant ON dbo.role_assignments(tenant_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_role_assignments_tenant_role' AND object_id = OBJECT_ID(N'dbo.role_assignments'))
    CREATE INDEX idx_role_assignments_tenant_role ON dbo.role_assignments(tenant_id, role_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_role_assignments_tenant_principal' AND object_id = OBJECT_ID(N'dbo.role_assignments'))
    CREATE INDEX idx_role_assignments_tenant_principal ON dbo.role_assignments(tenant_id, principal_type, principal_id);
""";
        }

        #endregion
    }
}

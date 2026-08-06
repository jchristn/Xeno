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
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;

    /// <summary>
    /// SQL Server role data access methods. Role scalar fields map to columns on <c>dbo.roles</c> and the
    /// <see cref="Role.PermissionIds"/> collection maps to the child table <c>dbo.role_permissions</c>.
    /// </summary>
    public class SqlServerRoleMethods : IRoleMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public SqlServerRoleMethods(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));
            _ConnectionString = connectionString;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create the role tables and indexes if they do not exist.
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
        public async Task<Role> CreateAsync(Role role, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(role);
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(role.Id)) role.Id = IdGenerator.NewId("role_");

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertRoleRowAsync(connection, transaction, role, token).ConfigureAwait(false);
                    await ReplacePermissionsAsync(connection, transaction, role, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return role;
        }

        /// <inheritdoc />
        public async Task<Role?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return null;
            if (String.IsNullOrWhiteSpace(id)) return null;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                Role? role = null;
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + RoleColumns + " FROM dbo.roles WHERE tenant_id = @tenant_id AND id = @id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) role = HydrateRole(reader);
                    }
                }

                if (role == null) return null;
                role.PermissionIds = await ReadPermissionIdsAsync(connection, role.Id, token).ConfigureAwait(false);
                return role;
            }
        }

        /// <inheritdoc />
        public async Task<Role?> ReadByNameAsync(string tenantId, string name, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return null;
            if (String.IsNullOrWhiteSpace(name)) return null;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                Role? role = null;
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + RoleColumns + " FROM dbo.roles WHERE tenant_id = @tenant_id AND name = @name;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@name", name);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) role = HydrateRole(reader);
                    }
                }

                if (role == null) return null;
                role.PermissionIds = await ReadPermissionIdsAsync(connection, role.Id, token).ConfigureAwait(false);
                return role;
            }
        }

        /// <inheritdoc />
        public async Task<List<Role>> EnumerateAsync(string tenantId, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            List<Role> roles = new List<Role>();
            if (String.IsNullOrWhiteSpace(tenantId)) return roles;
            Dictionary<string, Role> byId = new Dictionary<string, Role>(StringComparer.Ordinal);

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + RoleColumns + " FROM dbo.roles WHERE tenant_id = @tenant_id ORDER BY created_utc ASC, id ASC;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            Role role = HydrateRole(reader);
                            roles.Add(role);
                            byId[role.Id] = role;
                        }
                    }
                }

                if (roles.Count > 0)
                {
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT rp.role_id, rp.permission_id FROM dbo.role_permissions rp INNER JOIN dbo.roles r ON rp.role_id = r.id WHERE r.tenant_id = @tenant_id ORDER BY rp.role_id ASC, rp.permission_id ASC;";
                        command.Parameters.AddWithValue("@tenant_id", tenantId);
                        using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync(token).ConfigureAwait(false))
                            {
                                string roleId = reader.GetString(0);
                                if (byId.TryGetValue(roleId, out Role? owner)) owner.PermissionIds.Add(reader.GetString(1));
                            }
                        }
                    }
                }
            }

            return roles;
        }

        /// <inheritdoc />
        public async Task<Role> UpdateAsync(Role role, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(role);
            if (String.IsNullOrWhiteSpace(role.Id)) throw new ArgumentException("Role ID is required.", nameof(role));
            token.ThrowIfCancellationRequested();
            role.LastUpdateUtc = DateTime.UtcNow;

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertRoleRowAsync(connection, transaction, role, token).ConfigureAwait(false);
                    await ReplacePermissionsAsync(connection, transaction, role, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return role;
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
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    using (SqlCommand childCommand = connection.CreateCommand())
                    {
                        childCommand.Transaction = transaction;
                        childCommand.CommandText = "DELETE FROM dbo.role_permissions WHERE role_id = @id;";
                        childCommand.Parameters.AddWithValue("@id", id);
                        await childCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int rows;
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM dbo.roles WHERE tenant_id = @tenant_id AND id = @id;";
                        command.Parameters.AddWithValue("@tenant_id", tenantId);
                        command.Parameters.AddWithValue("@id", id);
                        rows = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    return rows > 0;
                }
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return false;
            if (String.IsNullOrWhiteSpace(id)) return false;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) 1 FROM dbo.roles WHERE tenant_id = @tenant_id AND id = @id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    return value != null;
                }
            }
        }

        #endregion

        #region Private-Methods

        private const string RoleColumns =
            "id, tenant_id, name, built_in, created_utc, last_update_utc, active";

        private static async Task UpsertRoleRowAsync(SqlConnection connection, SqlTransaction transaction, Role role, CancellationToken token)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
IF EXISTS (SELECT 1 FROM dbo.roles WHERE id = @id)
    UPDATE dbo.roles SET
        tenant_id = @tenant_id,
        name = @name,
        built_in = @built_in,
        last_update_utc = @last_update_utc,
        active = @active
    WHERE id = @id;
ELSE
    INSERT INTO dbo.roles (id, tenant_id, name, built_in, created_utc, last_update_utc, active)
    VALUES (@id, @tenant_id, @name, @built_in, @created_utc, @last_update_utc, @active);
""";
                command.Parameters.AddWithValue("@id", role.Id);
                command.Parameters.AddWithValue("@tenant_id", role.TenantId ?? String.Empty);
                command.Parameters.AddWithValue("@name", role.Name ?? String.Empty);
                command.Parameters.AddWithValue("@built_in", role.BuiltIn);
                command.Parameters.AddWithValue("@created_utc", FormatUtc(role.CreatedUtc));
                command.Parameters.AddWithValue("@last_update_utc", FormatUtc(role.LastUpdateUtc));
                command.Parameters.AddWithValue("@active", role.Active);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task ReplacePermissionsAsync(SqlConnection connection, SqlTransaction transaction, Role role, CancellationToken token)
        {
            using (SqlCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM dbo.role_permissions WHERE role_id = @id;";
                delete.Parameters.AddWithValue("@id", role.Id);
                await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            if (role.PermissionIds == null || role.PermissionIds.Count == 0) return;

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string permissionId in role.PermissionIds)
            {
                if (String.IsNullOrWhiteSpace(permissionId)) continue;
                if (!seen.Add(permissionId)) continue;

                using (SqlCommand insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = """
INSERT INTO dbo.role_permissions (role_id, permission_id)
VALUES (@role_id, @permission_id);
""";
                    insert.Parameters.AddWithValue("@role_id", role.Id);
                    insert.Parameters.AddWithValue("@permission_id", permissionId);
                    await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        private static async Task<List<string>> ReadPermissionIdsAsync(SqlConnection connection, string roleId, CancellationToken token)
        {
            List<string> permissionIds = new List<string>();
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT permission_id FROM dbo.role_permissions WHERE role_id = @id ORDER BY permission_id ASC;";
                command.Parameters.AddWithValue("@id", roleId);
                using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false)) permissionIds.Add(reader.GetString(0));
                }
            }

            return permissionIds;
        }

        private static Role HydrateRole(DbDataReader reader)
        {
            return new Role
            {
                Id = reader.GetString(0),
                TenantId = reader.IsDBNull(1) ? String.Empty : reader.GetString(1),
                Name = reader.IsDBNull(2) ? String.Empty : reader.GetString(2),
                BuiltIn = reader.GetBoolean(3),
                CreatedUtc = ParseUtc(reader.GetString(4)),
                LastUpdateUtc = ParseUtc(reader.GetString(5)),
                Active = reader.GetBoolean(6),
                PermissionIds = new List<string>()
            };
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
IF OBJECT_ID(N'dbo.roles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.roles
    (
        id nvarchar(64) NOT NULL PRIMARY KEY,
        tenant_id nvarchar(64) NOT NULL,
        name nvarchar(256) NOT NULL,
        built_in bit NOT NULL DEFAULT 0,
        created_utc nvarchar(64) NOT NULL,
        last_update_utc nvarchar(64) NOT NULL,
        active bit NOT NULL DEFAULT 1
    );
END;

IF OBJECT_ID(N'dbo.role_permissions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.role_permissions
    (
        role_id nvarchar(64) NOT NULL,
        permission_id nvarchar(64) NOT NULL,
        CONSTRAINT pk_role_permissions PRIMARY KEY (role_id, permission_id),
        CONSTRAINT fk_role_permissions_role FOREIGN KEY (role_id) REFERENCES dbo.roles(id) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_roles_tenant_name' AND object_id = OBJECT_ID(N'dbo.roles'))
    CREATE UNIQUE INDEX idx_roles_tenant_name ON dbo.roles(tenant_id, name);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_roles_tenant' AND object_id = OBJECT_ID(N'dbo.roles'))
    CREATE INDEX idx_roles_tenant ON dbo.roles(tenant_id);
""";
        }

        #endregion
    }
}

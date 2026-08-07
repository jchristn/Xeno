namespace Xeno.Core.Database.Mysql
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using MySql.Data.MySqlClient;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;

    /// <summary>
    /// MySQL role data access methods. Role scalar fields map to columns on <c>roles</c> and the
    /// <see cref="Role.PermissionIds"/> collection maps to the child table <c>role_permissions</c>.
    /// </summary>
    public class MysqlRoleMethods : IRoleMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public MysqlRoleMethods(string connectionString)
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
        public static async Task InitializeAsync(MySqlConnection connection, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(connection);
            token.ThrowIfCancellationRequested();
            using (MySqlCommand command = connection.CreateCommand())
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

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlTransaction transaction = (MySqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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
            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                Role? role = null;
                using (MySqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + RoleColumns + " FROM roles WHERE tenant_id = @tenant_id AND id = @id LIMIT 1;";
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
            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                Role? role = null;
                using (MySqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + RoleColumns + " FROM roles WHERE tenant_id = @tenant_id AND name = @name LIMIT 1;";
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

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (MySqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + RoleColumns + " FROM roles WHERE tenant_id = @tenant_id ORDER BY created_utc ASC, id ASC;";
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
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT rp.role_id, rp.permission_id FROM role_permissions rp INNER JOIN roles r ON rp.role_id = r.id WHERE r.tenant_id = @tenant_id ORDER BY rp.role_id ASC, rp.permission_id ASC;";
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

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlTransaction transaction = (MySqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlTransaction transaction = (MySqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    using (MySqlCommand childCommand = connection.CreateCommand())
                    {
                        childCommand.Transaction = transaction;
                        childCommand.CommandText = "DELETE FROM role_permissions WHERE role_id = @id;";
                        childCommand.Parameters.AddWithValue("@id", id);
                        await childCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int rows;
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM roles WHERE tenant_id = @tenant_id AND id = @id;";
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
        public async Task<int> DeleteByTenantAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return 0;
            token.ThrowIfCancellationRequested();

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlTransaction transaction = (MySqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    using (MySqlCommand childCommand = connection.CreateCommand())
                    {
                        childCommand.Transaction = transaction;
                        childCommand.CommandText = "DELETE FROM role_permissions WHERE role_id IN (SELECT id FROM (SELECT id FROM roles WHERE tenant_id = @tenant_id) AS matched);";
                        childCommand.Parameters.AddWithValue("@tenant_id", tenantId);
                        await childCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int rows;
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM roles WHERE tenant_id = @tenant_id;";
                        command.Parameters.AddWithValue("@tenant_id", tenantId);
                        rows = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                    return rows;
                }
            }
        }

        /// <inheritdoc />
        public async Task<bool> ExistsAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return false;
            if (String.IsNullOrWhiteSpace(id)) return false;
            token.ThrowIfCancellationRequested();
            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT 1 FROM roles WHERE tenant_id = @tenant_id AND id = @id LIMIT 1;";
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

        private static async Task UpsertRoleRowAsync(MySqlConnection connection, MySqlTransaction transaction, Role role, CancellationToken token)
        {
            using (MySqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
INSERT INTO roles (id, tenant_id, name, built_in, created_utc, last_update_utc, active)
VALUES (@id, @tenant_id, @name, @built_in, @created_utc, @last_update_utc, @active)
ON DUPLICATE KEY UPDATE
    tenant_id = VALUES(tenant_id),
    name = VALUES(name),
    built_in = VALUES(built_in),
    last_update_utc = VALUES(last_update_utc),
    active = VALUES(active);
""";
                command.Parameters.AddWithValue("@id", role.Id);
                command.Parameters.AddWithValue("@tenant_id", role.TenantId ?? String.Empty);
                command.Parameters.AddWithValue("@name", role.Name ?? String.Empty);
                command.Parameters.AddWithValue("@built_in", role.BuiltIn ? 1 : 0);
                command.Parameters.AddWithValue("@created_utc", FormatUtc(role.CreatedUtc));
                command.Parameters.AddWithValue("@last_update_utc", FormatUtc(role.LastUpdateUtc));
                command.Parameters.AddWithValue("@active", role.Active ? 1 : 0);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task ReplacePermissionsAsync(MySqlConnection connection, MySqlTransaction transaction, Role role, CancellationToken token)
        {
            using (MySqlCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM role_permissions WHERE role_id = @id;";
                delete.Parameters.AddWithValue("@id", role.Id);
                await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            if (role.PermissionIds == null || role.PermissionIds.Count == 0) return;

            HashSet<string> seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (string permissionId in role.PermissionIds)
            {
                if (String.IsNullOrWhiteSpace(permissionId)) continue;
                if (!seen.Add(permissionId)) continue;

                using (MySqlCommand insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = """
INSERT INTO role_permissions (role_id, permission_id)
VALUES (@role_id, @permission_id);
""";
                    insert.Parameters.AddWithValue("@role_id", role.Id);
                    insert.Parameters.AddWithValue("@permission_id", permissionId);
                    await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        private static async Task<List<string>> ReadPermissionIdsAsync(MySqlConnection connection, string roleId, CancellationToken token)
        {
            List<string> permissionIds = new List<string>();
            using (MySqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT permission_id FROM role_permissions WHERE role_id = @id ORDER BY permission_id ASC;";
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
                BuiltIn = reader.GetInt32(3) != 0,
                CreatedUtc = ParseUtc(reader.GetString(4)),
                LastUpdateUtc = ParseUtc(reader.GetString(5)),
                Active = reader.GetInt32(6) != 0,
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
CREATE TABLE IF NOT EXISTS roles
(
    id varchar(64) NOT NULL PRIMARY KEY,
    tenant_id varchar(64) NOT NULL,
    name varchar(256) NOT NULL,
    built_in INT NOT NULL DEFAULT 0,
    created_utc varchar(64) NOT NULL,
    last_update_utc varchar(64) NOT NULL,
    active INT NOT NULL DEFAULT 1,
    UNIQUE INDEX idx_roles_tenant_name (tenant_id, name),
    INDEX idx_roles_tenant (tenant_id)
);

CREATE TABLE IF NOT EXISTS role_permissions
(
    role_id varchar(64) NOT NULL,
    permission_id varchar(64) NOT NULL,
    PRIMARY KEY (role_id, permission_id),
    CONSTRAINT fk_role_permissions_role FOREIGN KEY (role_id) REFERENCES roles(id) ON DELETE CASCADE
);
""";
        }

        #endregion
    }
}

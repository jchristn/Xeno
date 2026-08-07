namespace Xeno.Core.Database.Mysql
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using MySql.Data.MySqlClient;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;

    /// <summary>
    /// MySQL credential data access methods. Credential scalar fields map to columns on <c>credentials</c>.
    /// Credentials are tenant-owned and user-owned; every tenant-scoped query is filtered by <c>tenant_id</c>.
    /// </summary>
    public class MysqlCredentialMethods : ICredentialMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public MysqlCredentialMethods(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));
            _ConnectionString = connectionString;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create the credential tables and indexes if they do not exist.
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
        public async Task<Credential> CreateAsync(Credential credential, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(credential);
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(credential.Id)) credential.Id = IdGenerator.NewId("cred_");

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                await UpsertCredentialRowAsync(connection, credential, token).ConfigureAwait(false);
            }

            return credential;
        }

        /// <inheritdoc />
        public async Task<Credential?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return null;
            if (String.IsNullOrWhiteSpace(id)) return null;
            token.ThrowIfCancellationRequested();
            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + CredentialColumns + " FROM credentials WHERE tenant_id = @tenant_id AND id = @id LIMIT 1;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) return HydrateCredential(reader);
                    }
                }

                return null;
            }
        }

        /// <inheritdoc />
        public async Task<Credential?> ReadByAccessKeyAsync(string tenantId, string accessKey, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return null;
            if (String.IsNullOrWhiteSpace(accessKey)) return null;
            token.ThrowIfCancellationRequested();
            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + CredentialColumns + " FROM credentials WHERE tenant_id = @tenant_id AND access_key = @access_key LIMIT 1;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@access_key", accessKey);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) return HydrateCredential(reader);
                    }
                }

                return null;
            }
        }

        /// <inheritdoc />
        public async Task<List<Credential>> EnumerateByAccessKeyAsync(string accessKey, CancellationToken token = default)
        {
            List<Credential> credentials = new List<Credential>();
            if (String.IsNullOrWhiteSpace(accessKey)) return credentials;
            token.ThrowIfCancellationRequested();

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + CredentialColumns + " FROM credentials WHERE access_key = @access_key ORDER BY id ASC;";
                    command.Parameters.AddWithValue("@access_key", accessKey);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false)) credentials.Add(HydrateCredential(reader));
                    }
                }
            }

            return credentials;
        }

        /// <inheritdoc />
        public async Task<List<Credential>> EnumerateAsync(string tenantId, CancellationToken token = default)
        {
            List<Credential> credentials = new List<Credential>();
            if (String.IsNullOrWhiteSpace(tenantId)) return credentials;
            token.ThrowIfCancellationRequested();

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + CredentialColumns + " FROM credentials WHERE tenant_id = @tenant_id ORDER BY id ASC;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false)) credentials.Add(HydrateCredential(reader));
                    }
                }
            }

            return credentials;
        }

        /// <inheritdoc />
        public async Task<List<Credential>> EnumerateByUserAsync(string tenantId, string userId, CancellationToken token = default)
        {
            List<Credential> credentials = new List<Credential>();
            if (String.IsNullOrWhiteSpace(tenantId)) return credentials;
            if (String.IsNullOrWhiteSpace(userId)) return credentials;
            token.ThrowIfCancellationRequested();

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + CredentialColumns + " FROM credentials WHERE tenant_id = @tenant_id AND user_id = @user_id ORDER BY id ASC;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@user_id", userId);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false)) credentials.Add(HydrateCredential(reader));
                    }
                }
            }

            return credentials;
        }

        /// <inheritdoc />
        public async Task<Credential> UpdateAsync(Credential credential, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(credential);
            if (String.IsNullOrWhiteSpace(credential.Id)) throw new ArgumentException("Credential ID is required.", nameof(credential));
            token.ThrowIfCancellationRequested();

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                await UpsertCredentialRowAsync(connection, credential, token).ConfigureAwait(false);
            }

            return credential;
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
                using (MySqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM credentials WHERE tenant_id = @tenant_id AND id = @id;";
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

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM credentials WHERE tenant_id = @tenant_id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteByUserAsync(string tenantId, string userId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return 0;
            if (String.IsNullOrWhiteSpace(userId)) return 0;
            token.ThrowIfCancellationRequested();

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM credentials WHERE tenant_id = @tenant_id AND user_id = @user_id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@user_id", userId);
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
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
                    command.CommandText = "SELECT 1 FROM credentials WHERE tenant_id = @tenant_id AND id = @id LIMIT 1;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    return value != null;
                }
            }
        }

        #endregion

        #region Private-Methods

        private const string CredentialColumns =
            "id, tenant_id, user_id, access_key, secret_key, active";

        private static async Task UpsertCredentialRowAsync(MySqlConnection connection, Credential credential, CancellationToken token)
        {
            using (MySqlCommand command = connection.CreateCommand())
            {
                command.CommandText = """
INSERT INTO credentials (id, tenant_id, user_id, access_key, secret_key, active)
VALUES (@id, @tenant_id, @user_id, @access_key, @secret_key, @active)
ON DUPLICATE KEY UPDATE
    tenant_id = VALUES(tenant_id),
    user_id = VALUES(user_id),
    access_key = VALUES(access_key),
    secret_key = VALUES(secret_key),
    active = VALUES(active);
""";
                command.Parameters.AddWithValue("@id", credential.Id);
                command.Parameters.AddWithValue("@tenant_id", credential.TenantId ?? String.Empty);
                command.Parameters.AddWithValue("@user_id", credential.UserId ?? String.Empty);
                command.Parameters.AddWithValue("@access_key", credential.AccessKey ?? String.Empty);
                command.Parameters.AddWithValue("@secret_key", credential.SecretKey ?? String.Empty);
                command.Parameters.AddWithValue("@active", credential.Active ? 1 : 0);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static Credential HydrateCredential(DbDataReader reader)
        {
            return new Credential
            {
                Id = reader.GetString(0),
                TenantId = reader.IsDBNull(1) ? String.Empty : reader.GetString(1),
                UserId = reader.IsDBNull(2) ? String.Empty : reader.GetString(2),
                AccessKey = reader.IsDBNull(3) ? String.Empty : reader.GetString(3),
                SecretKey = reader.IsDBNull(4) ? String.Empty : reader.GetString(4),
                Active = reader.GetInt32(5) != 0
            };
        }

        private static string SchemaSql()
        {
            return """
CREATE TABLE IF NOT EXISTS credentials
(
    id varchar(64) NOT NULL PRIMARY KEY,
    tenant_id varchar(64) NOT NULL,
    user_id varchar(64) NOT NULL,
    access_key varchar(256) NOT NULL,
    secret_key varchar(256) NOT NULL,
    active INT NOT NULL DEFAULT 1,
    UNIQUE INDEX idx_credentials_tenant_access_key (tenant_id, access_key),
    INDEX idx_credentials_tenant (tenant_id),
    INDEX idx_credentials_tenant_user (tenant_id, user_id)
);
""";
        }

        #endregion
    }
}

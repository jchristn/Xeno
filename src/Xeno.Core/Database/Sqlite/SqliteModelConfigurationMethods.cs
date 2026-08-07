namespace Xeno.Core.Database.Sqlite
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;

    /// <summary>
    /// SQLite model configuration data access methods. Configuration scalar fields map to columns on
    /// <c>model_configurations</c> and the <see cref="ModelConfiguration.PinnedCompletionsProperties"/> map
    /// maps to the child table <c>model_configuration_pinned_properties</c>, keyed by property name. The
    /// child <c>value_json</c> column is intentionally unmanaged JSON per the repository requirements: it
    /// stores each arbitrary property value as a serialized JSON document.
    /// </summary>
    public class SqliteModelConfigurationMethods : IModelConfigurationMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public SqliteModelConfigurationMethods(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));
            _ConnectionString = connectionString;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create the model configuration tables and indexes if they do not exist.
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
        public async Task<ModelConfiguration> CreateAsync(ModelConfiguration configuration, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(configuration.Id)) configuration.Id = IdGenerator.NewId("mc_");

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (DbTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertConfigurationRowAsync(connection, transaction, configuration, token).ConfigureAwait(false);
                    await ReplacePinnedPropertiesAsync(connection, transaction, configuration, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return configuration;
        }

        /// <inheritdoc />
        public async Task<ModelConfiguration?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return null;
            if (String.IsNullOrWhiteSpace(id)) return null;
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                ModelConfiguration? configuration = null;
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + ConfigurationColumns + " FROM model_configurations WHERE tenant_id = @tenant_id AND id = @id LIMIT 1;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) configuration = HydrateConfiguration(reader);
                    }
                }

                if (configuration == null) return null;
                configuration.PinnedCompletionsProperties = await ReadPinnedPropertiesAsync(connection, configuration.Id, token).ConfigureAwait(false);
                return configuration;
            }
        }

        /// <inheritdoc />
        public async Task<ModelConfiguration?> ReadByNameAsync(string tenantId, string name, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return null;
            if (String.IsNullOrWhiteSpace(name)) return null;
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                ModelConfiguration? configuration = null;
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + ConfigurationColumns + " FROM model_configurations WHERE tenant_id = @tenant_id AND name = @name LIMIT 1;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@name", name);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) configuration = HydrateConfiguration(reader);
                    }
                }

                if (configuration == null) return null;
                configuration.PinnedCompletionsProperties = await ReadPinnedPropertiesAsync(connection, configuration.Id, token).ConfigureAwait(false);
                return configuration;
            }
        }

        /// <inheritdoc />
        public async Task<List<ModelConfiguration>> EnumerateAsync(string tenantId, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            List<ModelConfiguration> configurations = new List<ModelConfiguration>();
            if (String.IsNullOrWhiteSpace(tenantId)) return configurations;
            Dictionary<string, ModelConfiguration> byId = new Dictionary<string, ModelConfiguration>(StringComparer.Ordinal);

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + ConfigurationColumns + " FROM model_configurations WHERE tenant_id = @tenant_id ORDER BY name ASC, id ASC;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            ModelConfiguration configuration = HydrateConfiguration(reader);
                            configurations.Add(configuration);
                            byId[configuration.Id] = configuration;
                        }
                    }
                }

                if (configurations.Count > 0)
                {
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT c.config_id, c.key_name, c.value_json FROM model_configuration_pinned_properties c " +
                            "INNER JOIN model_configurations m ON c.config_id = m.id WHERE m.tenant_id = @tenant_id " +
                            "ORDER BY c.config_id ASC, c.key_name ASC;";
                        command.Parameters.AddWithValue("@tenant_id", tenantId);
                        using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync(token).ConfigureAwait(false))
                            {
                                string configId = reader.GetString(0);
                                if (!byId.TryGetValue(configId, out ModelConfiguration? owner)) continue;
                                string keyName = reader.GetString(1);
                                object? value = JsonUtil.Deserialize<object?>(reader.GetString(2));
                                if (value != null) owner.PinnedCompletionsProperties[keyName] = value;
                            }
                        }
                    }
                }
            }

            return configurations;
        }

        /// <inheritdoc />
        public async Task<ModelConfiguration> UpdateAsync(ModelConfiguration configuration, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            if (String.IsNullOrWhiteSpace(configuration.Id)) throw new ArgumentException("Model configuration ID is required.", nameof(configuration));
            token.ThrowIfCancellationRequested();

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (DbTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertConfigurationRowAsync(connection, transaction, configuration, token).ConfigureAwait(false);
                    await ReplacePinnedPropertiesAsync(connection, transaction, configuration, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return configuration;
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
                using (DbTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    using (SqliteCommand childCommand = connection.CreateCommand())
                    {
                        childCommand.Transaction = (SqliteTransaction)transaction;
                        childCommand.CommandText = "DELETE FROM model_configuration_pinned_properties WHERE config_id = @id;";
                        childCommand.Parameters.AddWithValue("@id", id);
                        await childCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int rows;
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.Transaction = (SqliteTransaction)transaction;
                        command.CommandText = "DELETE FROM model_configurations WHERE tenant_id = @tenant_id AND id = @id;";
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

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (DbTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    using (SqliteCommand childCommand = connection.CreateCommand())
                    {
                        childCommand.Transaction = (SqliteTransaction)transaction;
                        childCommand.CommandText = "DELETE FROM model_configuration_pinned_properties WHERE config_id IN (SELECT id FROM model_configurations WHERE tenant_id = @tenant_id);";
                        childCommand.Parameters.AddWithValue("@tenant_id", tenantId);
                        await childCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int rows;
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.Transaction = (SqliteTransaction)transaction;
                        command.CommandText = "DELETE FROM model_configurations WHERE tenant_id = @tenant_id;";
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
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT 1 FROM model_configurations WHERE tenant_id = @tenant_id AND id = @id LIMIT 1;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    return value != null;
                }
            }
        }

        #endregion

        #region Private-Methods

        private const string ConfigurationColumns =
            "id, tenant_id, name, model";

        private const string PinnedColumns =
            "config_id, key_name, value_json";

        private static async Task UpsertConfigurationRowAsync(SqliteConnection connection, DbTransaction transaction, ModelConfiguration configuration, CancellationToken token)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = (SqliteTransaction)transaction;
                command.CommandText = """
INSERT INTO model_configurations (id, tenant_id, name, model)
VALUES (@id, @tenant_id, @name, @model)
ON CONFLICT(id) DO UPDATE SET
    tenant_id = excluded.tenant_id,
    name = excluded.name,
    model = excluded.model;
""";
                command.Parameters.AddWithValue("@id", configuration.Id);
                command.Parameters.AddWithValue("@tenant_id", configuration.TenantId ?? String.Empty);
                command.Parameters.AddWithValue("@name", configuration.Name ?? String.Empty);
                command.Parameters.AddWithValue("@model", (object?)configuration.Model ?? DBNull.Value);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task ReplacePinnedPropertiesAsync(SqliteConnection connection, DbTransaction transaction, ModelConfiguration configuration, CancellationToken token)
        {
            using (SqliteCommand delete = connection.CreateCommand())
            {
                delete.Transaction = (SqliteTransaction)transaction;
                delete.CommandText = "DELETE FROM model_configuration_pinned_properties WHERE config_id = @id;";
                delete.Parameters.AddWithValue("@id", configuration.Id);
                await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            if (configuration.PinnedCompletionsProperties == null || configuration.PinnedCompletionsProperties.Count == 0) return;

            foreach (KeyValuePair<string, object> kvp in configuration.PinnedCompletionsProperties)
            {
                using (SqliteCommand insert = connection.CreateCommand())
                {
                    insert.Transaction = (SqliteTransaction)transaction;
                    insert.CommandText = """
INSERT INTO model_configuration_pinned_properties (config_id, key_name, value_json)
VALUES (@config_id, @key_name, @value_json);
""";
                    insert.Parameters.AddWithValue("@config_id", configuration.Id);
                    insert.Parameters.AddWithValue("@key_name", kvp.Key ?? String.Empty);
                    insert.Parameters.AddWithValue("@value_json", JsonUtil.Serialize(kvp.Value));
                    await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        private static async Task<Dictionary<string, object>> ReadPinnedPropertiesAsync(SqliteConnection connection, string configId, CancellationToken token)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT " + PinnedColumns + " FROM model_configuration_pinned_properties WHERE config_id = @id ORDER BY key_name ASC;";
                command.Parameters.AddWithValue("@id", configId);
                using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        string keyName = reader.GetString(1);
                        object? value = JsonUtil.Deserialize<object?>(reader.GetString(2));
                        if (value != null) properties[keyName] = value;
                    }
                }
            }

            return properties;
        }

        private static ModelConfiguration HydrateConfiguration(DbDataReader reader)
        {
            return new ModelConfiguration
            {
                Id = reader.GetString(0),
                TenantId = reader.IsDBNull(1) ? String.Empty : reader.GetString(1),
                Name = reader.IsDBNull(2) ? String.Empty : reader.GetString(2),
                Model = reader.IsDBNull(3) ? null : reader.GetString(3),
                PinnedCompletionsProperties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase)
            };
        }

        private static string SchemaSql()
        {
            return """
CREATE TABLE IF NOT EXISTS model_configurations
(
    id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    name TEXT NOT NULL,
    model TEXT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_model_configurations_tenant_name ON model_configurations(tenant_id, name);

CREATE INDEX IF NOT EXISTS idx_model_configurations_tenant ON model_configurations(tenant_id);

CREATE TABLE IF NOT EXISTS model_configuration_pinned_properties
(
    config_id TEXT NOT NULL,
    key_name TEXT NOT NULL,
    value_json TEXT NOT NULL,
    PRIMARY KEY (config_id, key_name),
    FOREIGN KEY (config_id) REFERENCES model_configurations(id) ON DELETE CASCADE
);
""";
        }

        #endregion
    }
}

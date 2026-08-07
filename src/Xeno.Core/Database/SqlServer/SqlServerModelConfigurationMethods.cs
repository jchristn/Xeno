namespace Xeno.Core.Database.SqlServer
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.SqlClient;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;

    /// <summary>
    /// SQL Server model configuration data access methods. Configuration scalar fields map to columns on
    /// <c>dbo.model_configurations</c> and the <see cref="ModelConfiguration.PinnedCompletionsProperties"/>
    /// map maps to the child table <c>dbo.model_configuration_pinned_properties</c>, keyed by property name.
    /// The child <c>value_json</c> column is intentionally unmanaged JSON per the repository requirements: it
    /// stores each arbitrary property value as a serialized JSON document.
    /// </summary>
    public class SqlServerModelConfigurationMethods : IModelConfigurationMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public SqlServerModelConfigurationMethods(string connectionString)
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
        public async Task<ModelConfiguration> CreateAsync(ModelConfiguration configuration, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(configuration);
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(configuration.Id)) configuration.Id = IdGenerator.NewId("mc_");

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                ModelConfiguration? configuration = null;
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + ConfigurationColumns + " FROM dbo.model_configurations WHERE tenant_id = @tenant_id AND id = @id;";
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
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                ModelConfiguration? configuration = null;
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + ConfigurationColumns + " FROM dbo.model_configurations WHERE tenant_id = @tenant_id AND name = @name;";
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

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + ConfigurationColumns + " FROM dbo.model_configurations WHERE tenant_id = @tenant_id ORDER BY name ASC, id ASC;";
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
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText =
                            "SELECT c.config_id, c.key_name, c.value_json FROM dbo.model_configuration_pinned_properties c " +
                            "INNER JOIN dbo.model_configurations m ON c.config_id = m.id WHERE m.tenant_id = @tenant_id " +
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

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    using (SqlCommand childCommand = connection.CreateCommand())
                    {
                        childCommand.Transaction = transaction;
                        childCommand.CommandText = "DELETE FROM dbo.model_configuration_pinned_properties WHERE config_id = @id;";
                        childCommand.Parameters.AddWithValue("@id", id);
                        await childCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int rows;
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM dbo.model_configurations WHERE tenant_id = @tenant_id AND id = @id;";
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

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    using (SqlCommand childCommand = connection.CreateCommand())
                    {
                        childCommand.Transaction = transaction;
                        childCommand.CommandText = "DELETE FROM dbo.model_configuration_pinned_properties WHERE config_id IN (SELECT id FROM dbo.model_configurations WHERE tenant_id = @tenant_id);";
                        childCommand.Parameters.AddWithValue("@tenant_id", tenantId);
                        await childCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int rows;
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM dbo.model_configurations WHERE tenant_id = @tenant_id;";
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
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) 1 FROM dbo.model_configurations WHERE tenant_id = @tenant_id AND id = @id;";
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

        private static async Task UpsertConfigurationRowAsync(SqlConnection connection, SqlTransaction transaction, ModelConfiguration configuration, CancellationToken token)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
IF EXISTS (SELECT 1 FROM dbo.model_configurations WHERE id = @id)
    UPDATE dbo.model_configurations SET
        tenant_id = @tenant_id,
        name = @name,
        model = @model
    WHERE id = @id;
ELSE
    INSERT INTO dbo.model_configurations (id, tenant_id, name, model)
    VALUES (@id, @tenant_id, @name, @model);
""";
                command.Parameters.AddWithValue("@id", configuration.Id);
                command.Parameters.AddWithValue("@tenant_id", configuration.TenantId ?? String.Empty);
                command.Parameters.AddWithValue("@name", configuration.Name ?? String.Empty);
                command.Parameters.AddWithValue("@model", (object?)configuration.Model ?? DBNull.Value);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task ReplacePinnedPropertiesAsync(SqlConnection connection, SqlTransaction transaction, ModelConfiguration configuration, CancellationToken token)
        {
            using (SqlCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM dbo.model_configuration_pinned_properties WHERE config_id = @id;";
                delete.Parameters.AddWithValue("@id", configuration.Id);
                await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            if (configuration.PinnedCompletionsProperties == null || configuration.PinnedCompletionsProperties.Count == 0) return;

            foreach (KeyValuePair<string, object> kvp in configuration.PinnedCompletionsProperties)
            {
                using (SqlCommand insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = """
INSERT INTO dbo.model_configuration_pinned_properties (config_id, key_name, value_json)
VALUES (@config_id, @key_name, @value_json);
""";
                    insert.Parameters.AddWithValue("@config_id", configuration.Id);
                    insert.Parameters.AddWithValue("@key_name", kvp.Key ?? String.Empty);
                    insert.Parameters.AddWithValue("@value_json", JsonUtil.Serialize(kvp.Value));
                    await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        private static async Task<Dictionary<string, object>> ReadPinnedPropertiesAsync(SqlConnection connection, string configId, CancellationToken token)
        {
            Dictionary<string, object> properties = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT " + PinnedColumns + " FROM dbo.model_configuration_pinned_properties WHERE config_id = @id ORDER BY key_name ASC;";
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
IF OBJECT_ID(N'dbo.model_configurations', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.model_configurations
    (
        id nvarchar(64) NOT NULL PRIMARY KEY,
        tenant_id nvarchar(64) NOT NULL,
        name nvarchar(256) NOT NULL,
        model nvarchar(256) NULL
    );
END;

IF OBJECT_ID(N'dbo.model_configuration_pinned_properties', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.model_configuration_pinned_properties
    (
        config_id nvarchar(64) NOT NULL,
        key_name nvarchar(256) NOT NULL,
        value_json nvarchar(max) NOT NULL,
        CONSTRAINT pk_model_configuration_pinned_properties PRIMARY KEY (config_id, key_name),
        CONSTRAINT fk_model_configuration_pinned_properties_config FOREIGN KEY (config_id) REFERENCES dbo.model_configurations(id) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_model_configurations_tenant_name' AND object_id = OBJECT_ID(N'dbo.model_configurations'))
    CREATE UNIQUE INDEX idx_model_configurations_tenant_name ON dbo.model_configurations(tenant_id, name);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_model_configurations_tenant' AND object_id = OBJECT_ID(N'dbo.model_configurations'))
    CREATE INDEX idx_model_configurations_tenant ON dbo.model_configurations(tenant_id);
""";
        }

        #endregion
    }
}

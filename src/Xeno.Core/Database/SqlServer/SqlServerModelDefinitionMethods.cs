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

    /// <summary>
    /// SQL Server model definition data access methods. Model definition scalar fields map to columns on
    /// <c>dbo.model_definitions</c> and the <see cref="ModelDefinition.Labels"/> collection maps to the ordered
    /// child table <c>dbo.model_definition_labels</c>.
    /// </summary>
    public class SqlServerModelDefinitionMethods : IModelDefinitionMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public SqlServerModelDefinitionMethods(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));
            _ConnectionString = connectionString;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create the model definition tables and indexes if they do not exist.
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
        public async Task<ModelDefinition> CreateAsync(ModelDefinition definition, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(definition);
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(definition.Id)) definition.Id = IdGenerator.NewId("md_");

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertModelDefinitionRowAsync(connection, transaction, definition, token).ConfigureAwait(false);
                    await ReplaceLabelsAsync(connection, transaction, definition, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return definition;
        }

        /// <inheritdoc />
        public async Task<ModelDefinition?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return null;
            if (String.IsNullOrWhiteSpace(id)) return null;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                ModelDefinition? definition = null;
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + ModelDefinitionColumns + " FROM dbo.model_definitions WHERE tenant_id = @tenant_id AND id = @id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) definition = HydrateModelDefinition(reader);
                    }
                }

                if (definition == null) return null;
                definition.Labels = await ReadLabelsAsync(connection, definition.Id, token).ConfigureAwait(false);
                return definition;
            }
        }

        /// <inheritdoc />
        public async Task<ModelDefinition?> ReadByNameAsync(string tenantId, string name, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return null;
            if (String.IsNullOrWhiteSpace(name)) return null;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                ModelDefinition? definition = null;
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + ModelDefinitionColumns + " FROM dbo.model_definitions WHERE tenant_id = @tenant_id AND name = @name;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@name", name);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) definition = HydrateModelDefinition(reader);
                    }
                }

                if (definition == null) return null;
                definition.Labels = await ReadLabelsAsync(connection, definition.Id, token).ConfigureAwait(false);
                return definition;
            }
        }

        /// <inheritdoc />
        public async Task<List<ModelDefinition>> EnumerateAsync(string tenantId, CancellationToken token = default)
        {
            List<ModelDefinition> definitions = new List<ModelDefinition>();
            if (String.IsNullOrWhiteSpace(tenantId)) return definitions;
            token.ThrowIfCancellationRequested();
            Dictionary<string, ModelDefinition> byId = new Dictionary<string, ModelDefinition>(StringComparer.Ordinal);

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + ModelDefinitionColumns + " FROM dbo.model_definitions WHERE tenant_id = @tenant_id ORDER BY name ASC, id ASC;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            ModelDefinition definition = HydrateModelDefinition(reader);
                            definitions.Add(definition);
                            byId[definition.Id] = definition;
                        }
                    }
                }

                if (definitions.Count > 0)
                {
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT " + LabelColumns + " FROM dbo.model_definition_labels WHERE model_definition_id IN (SELECT id FROM dbo.model_definitions WHERE tenant_id = @tenant_id) ORDER BY model_definition_id ASC, ordinal ASC;";
                        command.Parameters.AddWithValue("@tenant_id", tenantId);
                        using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync(token).ConfigureAwait(false))
                            {
                                string modelDefinitionId = reader.GetString(0);
                                if (byId.TryGetValue(modelDefinitionId, out ModelDefinition? owner)) owner.Labels.Add(HydrateLabel(reader));
                            }
                        }
                    }
                }
            }

            return definitions;
        }

        /// <inheritdoc />
        public async Task<ModelDefinition> UpdateAsync(ModelDefinition definition, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(definition);
            if (String.IsNullOrWhiteSpace(definition.Id)) throw new ArgumentException("Model definition ID is required.", nameof(definition));
            token.ThrowIfCancellationRequested();

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertModelDefinitionRowAsync(connection, transaction, definition, token).ConfigureAwait(false);
                    await ReplaceLabelsAsync(connection, transaction, definition, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return definition;
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
                        childCommand.CommandText = "DELETE FROM dbo.model_definition_labels WHERE model_definition_id = @id;";
                        childCommand.Parameters.AddWithValue("@id", id);
                        await childCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int rows;
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM dbo.model_definitions WHERE tenant_id = @tenant_id AND id = @id;";
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
                    command.CommandText = "SELECT TOP (1) 1 FROM dbo.model_definitions WHERE tenant_id = @tenant_id AND id = @id;";
                    command.Parameters.AddWithValue("@tenant_id", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    return value != null;
                }
            }
        }

        #endregion

        #region Private-Methods

        private const string ModelDefinitionColumns =
            "id, tenant_id, name, supports_embeddings, supports_completions";

        private const string LabelColumns =
            "model_definition_id, label";

        private static async Task UpsertModelDefinitionRowAsync(SqlConnection connection, SqlTransaction transaction, ModelDefinition definition, CancellationToken token)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
IF EXISTS (SELECT 1 FROM dbo.model_definitions WHERE id = @id)
    UPDATE dbo.model_definitions SET
        tenant_id = @tenant_id,
        name = @name,
        supports_embeddings = @supports_embeddings,
        supports_completions = @supports_completions
    WHERE id = @id;
ELSE
    INSERT INTO dbo.model_definitions (id, tenant_id, name, supports_embeddings, supports_completions)
    VALUES (@id, @tenant_id, @name, @supports_embeddings, @supports_completions);
""";
                command.Parameters.AddWithValue("@id", definition.Id);
                command.Parameters.AddWithValue("@tenant_id", definition.TenantId ?? String.Empty);
                command.Parameters.AddWithValue("@name", definition.Name ?? String.Empty);
                command.Parameters.AddWithValue("@supports_embeddings", definition.SupportsEmbeddings);
                command.Parameters.AddWithValue("@supports_completions", definition.SupportsCompletions);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task ReplaceLabelsAsync(SqlConnection connection, SqlTransaction transaction, ModelDefinition definition, CancellationToken token)
        {
            using (SqlCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM dbo.model_definition_labels WHERE model_definition_id = @id;";
                delete.Parameters.AddWithValue("@id", definition.Id);
                await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            if (definition.Labels == null || definition.Labels.Count == 0) return;

            int ordinal = 0;
            foreach (string label in definition.Labels)
            {
                using (SqlCommand insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = """
INSERT INTO dbo.model_definition_labels (model_definition_id, ordinal, label)
VALUES (@model_definition_id, @ordinal, @label);
""";
                    insert.Parameters.AddWithValue("@model_definition_id", definition.Id);
                    insert.Parameters.AddWithValue("@ordinal", ordinal);
                    insert.Parameters.AddWithValue("@label", label ?? String.Empty);
                    await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    ordinal++;
                }
            }
        }

        private static async Task<List<string>> ReadLabelsAsync(SqlConnection connection, string modelDefinitionId, CancellationToken token)
        {
            List<string> labels = new List<string>();
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT " + LabelColumns + " FROM dbo.model_definition_labels WHERE model_definition_id = @id ORDER BY ordinal ASC;";
                command.Parameters.AddWithValue("@id", modelDefinitionId);
                using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false)) labels.Add(HydrateLabel(reader));
                }
            }

            return labels;
        }

        private static ModelDefinition HydrateModelDefinition(DbDataReader reader)
        {
            return new ModelDefinition
            {
                Id = reader.GetString(0),
                TenantId = reader.IsDBNull(1) ? String.Empty : reader.GetString(1),
                Name = reader.IsDBNull(2) ? String.Empty : reader.GetString(2),
                SupportsEmbeddings = reader.GetBoolean(3),
                SupportsCompletions = reader.GetBoolean(4),
                Labels = new List<string>()
            };
        }

        private static string HydrateLabel(DbDataReader reader)
        {
            return reader.IsDBNull(1) ? String.Empty : reader.GetString(1);
        }

        private static string SchemaSql()
        {
            return """
IF OBJECT_ID(N'dbo.model_definitions', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.model_definitions
    (
        id nvarchar(64) NOT NULL PRIMARY KEY,
        tenant_id nvarchar(64) NOT NULL,
        name nvarchar(256) NOT NULL,
        supports_embeddings bit NOT NULL DEFAULT 0,
        supports_completions bit NOT NULL DEFAULT 1
    );
END;

IF OBJECT_ID(N'dbo.model_definition_labels', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.model_definition_labels
    (
        model_definition_id nvarchar(64) NOT NULL,
        ordinal int NOT NULL,
        label nvarchar(256) NOT NULL,
        CONSTRAINT pk_model_definition_labels PRIMARY KEY (model_definition_id, ordinal),
        CONSTRAINT fk_model_definition_labels_model_definition FOREIGN KEY (model_definition_id) REFERENCES dbo.model_definitions(id) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_model_definitions_tenant_name' AND object_id = OBJECT_ID(N'dbo.model_definitions'))
    CREATE UNIQUE INDEX idx_model_definitions_tenant_name ON dbo.model_definitions(tenant_id, name);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_model_definitions_tenant' AND object_id = OBJECT_ID(N'dbo.model_definitions'))
    CREATE INDEX idx_model_definitions_tenant ON dbo.model_definitions(tenant_id);
""";
        }

        #endregion
    }
}

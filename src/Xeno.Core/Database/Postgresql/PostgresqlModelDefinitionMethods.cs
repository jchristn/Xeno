namespace Xeno.Core.Database.Postgresql
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;

    /// <summary>
    /// PostgreSQL model definition data access methods. Model definition scalar fields map to columns on
    /// <c>model_definitions</c> and the <see cref="ModelDefinition.Labels"/> collection maps to the ordered
    /// child table <c>model_definition_labels</c>.
    /// </summary>
    public class PostgresqlModelDefinitionMethods : IModelDefinitionMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public PostgresqlModelDefinitionMethods(string connectionString)
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
        public static async Task InitializeAsync(NpgsqlConnection connection, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(connection);
            token.ThrowIfCancellationRequested();
            using (NpgsqlCommand command = connection.CreateCommand())
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

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlTransaction transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                ModelDefinition? definition = null;
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + ModelDefinitionColumns + " FROM model_definitions WHERE tenant_id = @tenant_id AND id = @id LIMIT 1;";
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
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                ModelDefinition? definition = null;
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + ModelDefinitionColumns + " FROM model_definitions WHERE tenant_id = @tenant_id AND name = @name LIMIT 1;";
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

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + ModelDefinitionColumns + " FROM model_definitions WHERE tenant_id = @tenant_id ORDER BY name ASC, id ASC;";
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
                    using (NpgsqlCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT " + LabelColumns + " FROM model_definition_labels WHERE model_definition_id IN (SELECT id FROM model_definitions WHERE tenant_id = @tenant_id) ORDER BY model_definition_id ASC, ordinal ASC;";
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

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlTransaction transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlTransaction transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    using (NpgsqlCommand childCommand = connection.CreateCommand())
                    {
                        childCommand.Transaction = transaction;
                        childCommand.CommandText = "DELETE FROM model_definition_labels WHERE model_definition_id = @id;";
                        childCommand.Parameters.AddWithValue("@id", id);
                        await childCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int rows;
                    using (NpgsqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM model_definitions WHERE tenant_id = @tenant_id AND id = @id;";
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

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlTransaction transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    using (NpgsqlCommand childCommand = connection.CreateCommand())
                    {
                        childCommand.Transaction = transaction;
                        childCommand.CommandText = "DELETE FROM model_definition_labels WHERE model_definition_id IN (SELECT id FROM model_definitions WHERE tenant_id = @tenant_id);";
                        childCommand.Parameters.AddWithValue("@tenant_id", tenantId);
                        await childCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int rows;
                    using (NpgsqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM model_definitions WHERE tenant_id = @tenant_id;";
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
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT 1 FROM model_definitions WHERE tenant_id = @tenant_id AND id = @id LIMIT 1;";
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

        private static async Task UpsertModelDefinitionRowAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ModelDefinition definition, CancellationToken token)
        {
            using (NpgsqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
INSERT INTO model_definitions (id, tenant_id, name, supports_embeddings, supports_completions)
VALUES (@id, @tenant_id, @name, @supports_embeddings, @supports_completions)
ON CONFLICT (id) DO UPDATE SET
    tenant_id = EXCLUDED.tenant_id,
    name = EXCLUDED.name,
    supports_embeddings = EXCLUDED.supports_embeddings,
    supports_completions = EXCLUDED.supports_completions;
""";
                command.Parameters.AddWithValue("@id", definition.Id);
                command.Parameters.AddWithValue("@tenant_id", definition.TenantId ?? String.Empty);
                command.Parameters.AddWithValue("@name", definition.Name ?? String.Empty);
                command.Parameters.AddWithValue("@supports_embeddings", definition.SupportsEmbeddings ? 1 : 0);
                command.Parameters.AddWithValue("@supports_completions", definition.SupportsCompletions ? 1 : 0);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task ReplaceLabelsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, ModelDefinition definition, CancellationToken token)
        {
            using (NpgsqlCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM model_definition_labels WHERE model_definition_id = @id;";
                delete.Parameters.AddWithValue("@id", definition.Id);
                await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            if (definition.Labels == null || definition.Labels.Count == 0) return;

            int ordinal = 0;
            foreach (string label in definition.Labels)
            {
                using (NpgsqlCommand insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = """
INSERT INTO model_definition_labels (model_definition_id, ordinal, label)
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

        private static async Task<List<string>> ReadLabelsAsync(NpgsqlConnection connection, string modelDefinitionId, CancellationToken token)
        {
            List<string> labels = new List<string>();
            using (NpgsqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT " + LabelColumns + " FROM model_definition_labels WHERE model_definition_id = @id ORDER BY ordinal ASC;";
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
                SupportsEmbeddings = reader.GetInt32(3) != 0,
                SupportsCompletions = reader.GetInt32(4) != 0,
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
CREATE TABLE IF NOT EXISTS model_definitions
(
    id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    name TEXT NOT NULL,
    supports_embeddings INTEGER NOT NULL DEFAULT 0,
    supports_completions INTEGER NOT NULL DEFAULT 1
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_model_definitions_tenant_name ON model_definitions(tenant_id, name);

CREATE INDEX IF NOT EXISTS idx_model_definitions_tenant ON model_definitions(tenant_id);

CREATE TABLE IF NOT EXISTS model_definition_labels
(
    model_definition_id TEXT NOT NULL,
    ordinal INTEGER NOT NULL,
    label TEXT NOT NULL,
    PRIMARY KEY (model_definition_id, ordinal),
    FOREIGN KEY (model_definition_id) REFERENCES model_definitions(id) ON DELETE CASCADE
);
""";
        }

        #endregion
    }
}

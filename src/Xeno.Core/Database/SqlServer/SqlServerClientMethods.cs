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
    /// SQL Server client data access methods. The client scalar fields map to columns on <c>dbo.clients</c>,
    /// the <see cref="XenoClientRecord.Providers"/> collection maps to the ordered child table
    /// <c>dbo.client_providers</c>, and the <see cref="XenoClientRecord.Models"/> collection maps to the
    /// ordered child table <c>dbo.client_models</c>. No domain object is stored as a JSON document.
    /// </summary>
    public class SqlServerClientMethods : IClientMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public SqlServerClientMethods(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));
            _ConnectionString = connectionString;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create the client tables and indexes if they do not exist.
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
        public async Task<XenoClientRecord> CreateAsync(XenoClientRecord client, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(client);
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(client.Id)) client.Id = IdGenerator.NewId("xcl_");

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertClientRowAsync(connection, transaction, client, token).ConfigureAwait(false);
                    await ReplaceProvidersAsync(connection, transaction, client, token).ConfigureAwait(false);
                    await ReplaceModelsAsync(connection, transaction, client, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return client;
        }

        /// <inheritdoc />
        public async Task<XenoClientRecord?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return null;
            if (String.IsNullOrWhiteSpace(id)) return null;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                XenoClientRecord? client = null;
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + ClientColumns + " FROM dbo.clients WHERE tenant_id = @tenant AND id = @id;";
                    command.Parameters.AddWithValue("@tenant", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) client = HydrateClient(reader);
                    }
                }

                if (client == null) return null;
                client.Providers = await ReadProvidersAsync(connection, client.Id, token).ConfigureAwait(false);
                client.Models = await ReadModelsAsync(connection, client.Id, token).ConfigureAwait(false);
                return client;
            }
        }

        /// <inheritdoc />
        public async Task<XenoClientRecord?> ReadByNameAsync(string tenantId, string name, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return null;
            if (String.IsNullOrWhiteSpace(name)) return null;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                XenoClientRecord? client = null;
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + ClientColumns + " FROM dbo.clients WHERE tenant_id = @tenant AND name = @name;";
                    command.Parameters.AddWithValue("@tenant", tenantId);
                    command.Parameters.AddWithValue("@name", name);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) client = HydrateClient(reader);
                    }
                }

                if (client == null) return null;
                client.Providers = await ReadProvidersAsync(connection, client.Id, token).ConfigureAwait(false);
                client.Models = await ReadModelsAsync(connection, client.Id, token).ConfigureAwait(false);
                return client;
            }
        }

        /// <inheritdoc />
        public async Task<List<XenoClientRecord>> EnumerateAsync(string tenantId, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            List<XenoClientRecord> clients = new List<XenoClientRecord>();
            if (String.IsNullOrWhiteSpace(tenantId)) return clients;
            Dictionary<string, XenoClientRecord> byId = new Dictionary<string, XenoClientRecord>(StringComparer.Ordinal);

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + ClientColumns + " FROM dbo.clients WHERE tenant_id = @tenant ORDER BY name ASC, id ASC;";
                    command.Parameters.AddWithValue("@tenant", tenantId);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            XenoClientRecord client = HydrateClient(reader);
                            clients.Add(client);
                            byId[client.Id] = client;
                        }
                    }
                }

                await AttachChildrenAsync(connection, clients, byId, token).ConfigureAwait(false);
            }

            return clients;
        }

        /// <inheritdoc />
        public async Task<List<XenoClientRecord>> EnumerateSharedAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            List<XenoClientRecord> clients = new List<XenoClientRecord>();
            Dictionary<string, XenoClientRecord> byId = new Dictionary<string, XenoClientRecord>(StringComparer.Ordinal);

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + ClientColumns + " FROM dbo.clients WHERE shared = 1 ORDER BY tenant_id ASC, name ASC, id ASC;";
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            XenoClientRecord client = HydrateClient(reader);
                            clients.Add(client);
                            byId[client.Id] = client;
                        }
                    }
                }

                await AttachChildrenAsync(connection, clients, byId, token).ConfigureAwait(false);
            }

            return clients;
        }

        /// <inheritdoc />
        public async Task<XenoClientRecord> UpdateAsync(XenoClientRecord client, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(client);
            if (String.IsNullOrWhiteSpace(client.Id)) throw new ArgumentException("Client ID is required.", nameof(client));
            token.ThrowIfCancellationRequested();

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertClientRowAsync(connection, transaction, client, token).ConfigureAwait(false);
                    await ReplaceProvidersAsync(connection, transaction, client, token).ConfigureAwait(false);
                    await ReplaceModelsAsync(connection, transaction, client, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return client;
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
                    using (SqlCommand providerCommand = connection.CreateCommand())
                    {
                        providerCommand.Transaction = transaction;
                        providerCommand.CommandText = "DELETE FROM dbo.client_providers WHERE client_id IN (SELECT id FROM dbo.clients WHERE id = @id AND tenant_id = @tenant);";
                        providerCommand.Parameters.AddWithValue("@id", id);
                        providerCommand.Parameters.AddWithValue("@tenant", tenantId);
                        await providerCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    using (SqlCommand modelCommand = connection.CreateCommand())
                    {
                        modelCommand.Transaction = transaction;
                        modelCommand.CommandText = "DELETE FROM dbo.client_models WHERE client_id IN (SELECT id FROM dbo.clients WHERE id = @id AND tenant_id = @tenant);";
                        modelCommand.Parameters.AddWithValue("@id", id);
                        modelCommand.Parameters.AddWithValue("@tenant", tenantId);
                        await modelCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int rows;
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM dbo.clients WHERE id = @id AND tenant_id = @tenant;";
                        command.Parameters.AddWithValue("@id", id);
                        command.Parameters.AddWithValue("@tenant", tenantId);
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
                    using (SqlCommand providerCommand = connection.CreateCommand())
                    {
                        providerCommand.Transaction = transaction;
                        providerCommand.CommandText = "DELETE FROM dbo.client_providers WHERE client_id IN (SELECT id FROM dbo.clients WHERE tenant_id = @tenant);";
                        providerCommand.Parameters.AddWithValue("@tenant", tenantId);
                        await providerCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    using (SqlCommand modelCommand = connection.CreateCommand())
                    {
                        modelCommand.Transaction = transaction;
                        modelCommand.CommandText = "DELETE FROM dbo.client_models WHERE client_id IN (SELECT id FROM dbo.clients WHERE tenant_id = @tenant);";
                        modelCommand.Parameters.AddWithValue("@tenant", tenantId);
                        await modelCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int rows;
                    using (SqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = "DELETE FROM dbo.clients WHERE tenant_id = @tenant;";
                        command.Parameters.AddWithValue("@tenant", tenantId);
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
                    command.CommandText = "SELECT TOP (1) 1 FROM dbo.clients WHERE tenant_id = @tenant AND id = @id;";
                    command.Parameters.AddWithValue("@tenant", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    return value != null;
                }
            }
        }

        #endregion

        /// <inheritdoc />
        public async Task AdjustInFlightAsync(string tenantId, string id, int delta, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) return;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "UPDATE dbo.clients SET in_flight_requests = CASE WHEN in_flight_requests + @delta < 0 THEN 0 ELSE in_flight_requests + @delta END WHERE tenant_id = @tenant AND id = @id;";
                    command.Parameters.AddWithValue("@delta", delta);
                    command.Parameters.AddWithValue("@tenant", tenantId ?? String.Empty);
                    command.Parameters.AddWithValue("@id", id);
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateHeartbeatAsync(string tenantId, string id, bool connected, DateTime lastHeartbeatUtc, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) return false;
            token.ThrowIfCancellationRequested();
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "UPDATE dbo.clients SET connected = @connected, last_heartbeat_utc = @heartbeat WHERE tenant_id = @tenant AND id = @id;";
                    command.Parameters.AddWithValue("@connected", connected);
                    command.Parameters.AddWithValue("@heartbeat", FormatUtc(lastHeartbeatUtc));
                    command.Parameters.AddWithValue("@tenant", tenantId ?? String.Empty);
                    command.Parameters.AddWithValue("@id", id);
                    int rows = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    return rows > 0;
                }
            }
        }

        #region Private-Methods

        private const string ClientColumns =
            "id, tenant_id, name, owner_user_id, owner_credential_id, owner_principal_type, shared, connected, active, service_state, max_parallel_requests, weight, in_flight_requests, last_heartbeat_utc";

        private const string ProviderColumns =
            "client_id, name, api_type, model_management_allowed, last_advertised_utc";

        private const string ModelColumns =
            "client_id, name, provider, source, supports_completions, supports_embeddings, model_management_allowed, last_advertised_utc";

        private static async Task AttachChildrenAsync(SqlConnection connection, List<XenoClientRecord> clients, Dictionary<string, XenoClientRecord> byId, CancellationToken token)
        {
            if (clients.Count == 0) return;

            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT " + ProviderColumns + " FROM dbo.client_providers ORDER BY client_id ASC, ordinal ASC;";
                using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        string clientId = reader.GetString(0);
                        if (byId.TryGetValue(clientId, out XenoClientRecord? owner)) owner.Providers.Add(HydrateProvider(reader));
                    }
                }
            }

            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT " + ModelColumns + " FROM dbo.client_models ORDER BY client_id ASC, ordinal ASC;";
                using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        string clientId = reader.GetString(0);
                        if (byId.TryGetValue(clientId, out XenoClientRecord? owner)) owner.Models.Add(HydrateModel(reader));
                    }
                }
            }
        }

        private static async Task UpsertClientRowAsync(SqlConnection connection, SqlTransaction transaction, XenoClientRecord client, CancellationToken token)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
IF EXISTS (SELECT 1 FROM dbo.clients WHERE id = @id)
    UPDATE dbo.clients SET
        tenant_id = @tenant_id,
        name = @name,
        owner_user_id = @owner_user_id,
        owner_credential_id = @owner_credential_id,
        owner_principal_type = @owner_principal_type,
        shared = @shared,
        connected = @connected,
        active = @active,
        service_state = @service_state,
        max_parallel_requests = @max_parallel_requests,
        weight = @weight,
        in_flight_requests = @in_flight_requests,
        last_heartbeat_utc = @last_heartbeat_utc
    WHERE id = @id;
ELSE
    INSERT INTO dbo.clients (id, tenant_id, name, owner_user_id, owner_credential_id, owner_principal_type, shared, connected, active, service_state, max_parallel_requests, weight, in_flight_requests, last_heartbeat_utc)
    VALUES (@id, @tenant_id, @name, @owner_user_id, @owner_credential_id, @owner_principal_type, @shared, @connected, @active, @service_state, @max_parallel_requests, @weight, @in_flight_requests, @last_heartbeat_utc);
""";
                command.Parameters.AddWithValue("@id", client.Id);
                command.Parameters.AddWithValue("@tenant_id", client.TenantId ?? String.Empty);
                command.Parameters.AddWithValue("@name", client.Name ?? String.Empty);
                command.Parameters.AddWithValue("@owner_user_id", (object?)client.OwnerUserId ?? DBNull.Value);
                command.Parameters.AddWithValue("@owner_credential_id", (object?)client.OwnerCredentialId ?? DBNull.Value);
                command.Parameters.AddWithValue("@owner_principal_type", client.OwnerPrincipalType.ToString());
                command.Parameters.AddWithValue("@shared", client.Shared);
                command.Parameters.AddWithValue("@connected", client.Connected);
                command.Parameters.AddWithValue("@active", client.Active);
                command.Parameters.AddWithValue("@service_state", client.ServiceState.ToString());
                command.Parameters.AddWithValue("@max_parallel_requests", client.MaxParallelRequests);
                command.Parameters.AddWithValue("@weight", client.Weight);
                command.Parameters.AddWithValue("@in_flight_requests", client.InFlightRequests);
                command.Parameters.AddWithValue("@last_heartbeat_utc", FormatUtc(client.LastHeartbeatUtc));
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task ReplaceProvidersAsync(SqlConnection connection, SqlTransaction transaction, XenoClientRecord client, CancellationToken token)
        {
            using (SqlCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM dbo.client_providers WHERE client_id = @id;";
                delete.Parameters.AddWithValue("@id", client.Id);
                await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            if (client.Providers == null || client.Providers.Count == 0) return;

            int ordinal = 0;
            foreach (XenoClientProvider provider in client.Providers)
            {
                using (SqlCommand insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = """
INSERT INTO dbo.client_providers (client_id, ordinal, name, api_type, model_management_allowed, last_advertised_utc)
VALUES (@client_id, @ordinal, @name, @api_type, @model_management_allowed, @last_advertised_utc);
""";
                    insert.Parameters.AddWithValue("@client_id", client.Id);
                    insert.Parameters.AddWithValue("@ordinal", ordinal);
                    insert.Parameters.AddWithValue("@name", provider.Name ?? String.Empty);
                    insert.Parameters.AddWithValue("@api_type", provider.ApiType.ToString());
                    insert.Parameters.AddWithValue("@model_management_allowed", provider.ModelManagementAllowed);
                    insert.Parameters.AddWithValue("@last_advertised_utc", FormatUtc(provider.LastAdvertisedUtc));
                    await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    ordinal++;
                }
            }
        }

        private static async Task ReplaceModelsAsync(SqlConnection connection, SqlTransaction transaction, XenoClientRecord client, CancellationToken token)
        {
            using (SqlCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM dbo.client_models WHERE client_id = @id;";
                delete.Parameters.AddWithValue("@id", client.Id);
                await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            if (client.Models == null || client.Models.Count == 0) return;

            int ordinal = 0;
            foreach (XenoClientModel model in client.Models)
            {
                using (SqlCommand insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
                    insert.CommandText = """
INSERT INTO dbo.client_models (client_id, ordinal, name, provider, source, supports_completions, supports_embeddings, model_management_allowed, last_advertised_utc)
VALUES (@client_id, @ordinal, @name, @provider, @source, @supports_completions, @supports_embeddings, @model_management_allowed, @last_advertised_utc);
""";
                    insert.Parameters.AddWithValue("@client_id", client.Id);
                    insert.Parameters.AddWithValue("@ordinal", ordinal);
                    insert.Parameters.AddWithValue("@name", model.Name ?? String.Empty);
                    insert.Parameters.AddWithValue("@provider", model.Provider.ToString());
                    insert.Parameters.AddWithValue("@source", model.Source ?? String.Empty);
                    insert.Parameters.AddWithValue("@supports_completions", model.SupportsCompletions);
                    insert.Parameters.AddWithValue("@supports_embeddings", model.SupportsEmbeddings);
                    insert.Parameters.AddWithValue("@model_management_allowed", model.ModelManagementAllowed);
                    insert.Parameters.AddWithValue("@last_advertised_utc", FormatUtc(model.LastAdvertisedUtc));
                    await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    ordinal++;
                }
            }
        }

        private static async Task<List<XenoClientProvider>> ReadProvidersAsync(SqlConnection connection, string clientId, CancellationToken token)
        {
            List<XenoClientProvider> providers = new List<XenoClientProvider>();
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT " + ProviderColumns + " FROM dbo.client_providers WHERE client_id = @id ORDER BY ordinal ASC;";
                command.Parameters.AddWithValue("@id", clientId);
                using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false)) providers.Add(HydrateProvider(reader));
                }
            }

            return providers;
        }

        private static async Task<List<XenoClientModel>> ReadModelsAsync(SqlConnection connection, string clientId, CancellationToken token)
        {
            List<XenoClientModel> models = new List<XenoClientModel>();
            using (SqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT " + ModelColumns + " FROM dbo.client_models WHERE client_id = @id ORDER BY ordinal ASC;";
                command.Parameters.AddWithValue("@id", clientId);
                using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false)) models.Add(HydrateModel(reader));
                }
            }

            return models;
        }

        private static XenoClientRecord HydrateClient(DbDataReader reader)
        {
            return new XenoClientRecord
            {
                Id = reader.GetString(0),
                TenantId = reader.IsDBNull(1) ? String.Empty : reader.GetString(1),
                Name = reader.IsDBNull(2) ? String.Empty : reader.GetString(2),
                OwnerUserId = reader.IsDBNull(3) ? null : reader.GetString(3),
                OwnerCredentialId = reader.IsDBNull(4) ? null : reader.GetString(4),
                OwnerPrincipalType = ParsePrincipalType(reader, 5),
                Shared = reader.GetBoolean(6),
                Connected = reader.GetBoolean(7),
                Active = reader.GetBoolean(8),
                ServiceState = ParseServiceState(reader, 9),
                MaxParallelRequests = reader.GetInt32(10),
                Weight = reader.GetInt32(11),
                InFlightRequests = reader.GetInt32(12),
                LastHeartbeatUtc = ParseUtc(reader.GetString(13)),
                Providers = new List<XenoClientProvider>(),
                Models = new List<XenoClientModel>()
            };
        }

        private static XenoClientProvider HydrateProvider(DbDataReader reader)
        {
            return new XenoClientProvider
            {
                Name = reader.IsDBNull(1) ? String.Empty : reader.GetString(1),
                ApiType = ParseApiType(reader, 2),
                ModelManagementAllowed = reader.GetBoolean(3),
                LastAdvertisedUtc = ParseUtc(reader.GetString(4))
            };
        }

        private static XenoClientModel HydrateModel(DbDataReader reader)
        {
            return new XenoClientModel
            {
                Name = reader.IsDBNull(1) ? String.Empty : reader.GetString(1),
                Provider = ParseApiType(reader, 2),
                Source = reader.IsDBNull(3) ? String.Empty : reader.GetString(3),
                SupportsCompletions = reader.GetBoolean(4),
                SupportsEmbeddings = reader.GetBoolean(5),
                ModelManagementAllowed = reader.GetBoolean(6),
                LastAdvertisedUtc = ParseUtc(reader.GetString(7))
            };
        }

        private static PrincipalTypeEnum ParsePrincipalType(DbDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return PrincipalTypeEnum.None;
            return Enum.TryParse(reader.GetString(ordinal), out PrincipalTypeEnum parsed) ? parsed : PrincipalTypeEnum.None;
        }

        private static ClientServiceStateEnum ParseServiceState(DbDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return ClientServiceStateEnum.Normal;
            return Enum.TryParse(reader.GetString(ordinal), out ClientServiceStateEnum parsed) ? parsed : ClientServiceStateEnum.Normal;
        }

        private static ApiProviderTypeEnum ParseApiType(DbDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return ApiProviderTypeEnum.Ollama;
            return Enum.TryParse(reader.GetString(ordinal), out ApiProviderTypeEnum parsed) ? parsed : ApiProviderTypeEnum.Ollama;
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
IF OBJECT_ID(N'dbo.clients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.clients
    (
        id nvarchar(64) NOT NULL PRIMARY KEY,
        tenant_id nvarchar(64) NOT NULL,
        name nvarchar(256) NOT NULL,
        owner_user_id nvarchar(64) NULL,
        owner_credential_id nvarchar(64) NULL,
        owner_principal_type nvarchar(64) NOT NULL,
        shared bit NOT NULL DEFAULT 0,
        connected bit NOT NULL DEFAULT 1,
        active bit NOT NULL DEFAULT 1,
        service_state nvarchar(64) NOT NULL,
        max_parallel_requests int NOT NULL DEFAULT 4,
        weight int NOT NULL DEFAULT 100,
        in_flight_requests int NOT NULL DEFAULT 0,
        last_heartbeat_utc nvarchar(64) NOT NULL
    );
END;

IF OBJECT_ID(N'dbo.client_providers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.client_providers
    (
        client_id nvarchar(64) NOT NULL,
        ordinal int NOT NULL,
        name nvarchar(256) NOT NULL,
        api_type nvarchar(64) NOT NULL,
        model_management_allowed bit NOT NULL DEFAULT 0,
        last_advertised_utc nvarchar(64) NOT NULL,
        CONSTRAINT pk_client_providers PRIMARY KEY (client_id, ordinal),
        CONSTRAINT fk_client_providers_client FOREIGN KEY (client_id) REFERENCES dbo.clients(id) ON DELETE CASCADE
    );
END;

IF OBJECT_ID(N'dbo.client_models', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.client_models
    (
        client_id nvarchar(64) NOT NULL,
        ordinal int NOT NULL,
        name nvarchar(256) NOT NULL,
        provider nvarchar(64) NOT NULL,
        source nvarchar(256) NOT NULL,
        supports_completions bit NOT NULL DEFAULT 1,
        supports_embeddings bit NOT NULL DEFAULT 0,
        model_management_allowed bit NOT NULL DEFAULT 0,
        last_advertised_utc nvarchar(64) NOT NULL,
        CONSTRAINT pk_client_models PRIMARY KEY (client_id, ordinal),
        CONSTRAINT fk_client_models_client FOREIGN KEY (client_id) REFERENCES dbo.clients(id) ON DELETE CASCADE
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_clients_tenant_name' AND object_id = OBJECT_ID(N'dbo.clients'))
    CREATE UNIQUE INDEX idx_clients_tenant_name ON dbo.clients(tenant_id, name);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_clients_tenant' AND object_id = OBJECT_ID(N'dbo.clients'))
    CREATE INDEX idx_clients_tenant ON dbo.clients(tenant_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_clients_shared' AND object_id = OBJECT_ID(N'dbo.clients'))
    CREATE INDEX idx_clients_shared ON dbo.clients(shared);
""";
        }

        #endregion
    }
}

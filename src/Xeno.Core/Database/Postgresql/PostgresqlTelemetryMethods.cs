namespace Xeno.Core.Database.Postgresql
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models.Telemetry;
    using Xeno.Core.Serialization;

    /// <summary>
    /// PostgreSQL implementation of server-collected telemetry methods.
    /// </summary>
    public sealed class PostgresqlTelemetryMethods : IClientTelemetryMethods, IClientProviderTelemetryMethods, IClientTelemetryCollectionAttemptMethods
    {
        private readonly string _ConnectionString;

        /// <summary>
        /// Instantiate PostgreSQL telemetry methods.
        /// </summary>
        /// <param name="connectionString">PostgreSQL connection string.</param>
        public PostgresqlTelemetryMethods(string connectionString)
        {
            _ConnectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Initialize PostgreSQL telemetry storage.
        /// </summary>
        /// <param name="connection">Open PostgreSQL connection.</param>
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

            await EnsureColumnAsync(connection, "client_provider_telemetry_samples", "available_model_count", "INTEGER NULL", token).ConfigureAwait(false);
            await EnsureColumnAsync(connection, "client_provider_telemetry_samples", "loaded_model_count", "INTEGER NULL", token).ConfigureAwait(false);
        }

        /// <summary>
        /// Delete all server-collected telemetry rows for a tenant.
        /// </summary>
        /// <param name="connection">Open PostgreSQL connection.</param>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Deleted row count.</returns>
        public static async Task<int> PurgeTenantAsync(NpgsqlConnection connection, string tenantId, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(connection);
            if (String.IsNullOrWhiteSpace(tenantId)) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
            token.ThrowIfCancellationRequested();
            using (NpgsqlTransaction transaction = connection.BeginTransaction())
            {
                int total = 0;
                total += await ExecuteTenantDeleteAsync(connection, transaction, "client_telemetry_gpu_samples", tenantId, token).ConfigureAwait(false);
                total += await ExecuteTenantDeleteAsync(connection, transaction, "client_provider_telemetry_models", tenantId, token).ConfigureAwait(false);
                total += await ExecuteTenantDeleteAsync(connection, transaction, "client_provider_telemetry_metrics", tenantId, token).ConfigureAwait(false);
                total += await ExecuteTenantDeleteAsync(connection, transaction, "client_telemetry_collection_attempts", tenantId, token).ConfigureAwait(false);
                total += await ExecuteTenantDeleteAsync(connection, transaction, "client_telemetry_samples", tenantId, token).ConfigureAwait(false);
                total += await ExecuteTenantDeleteAsync(connection, transaction, "client_provider_telemetry_samples", tenantId, token).ConfigureAwait(false);
                transaction.Commit();
                return total;
            }
        }

        /// <inheritdoc />
        public async Task<ServerClientTelemetrySampleRecord> CreateHostSampleAsync(string tenantId, string clientId, string? clientName, string? collectionRequestId, ClientTelemetrySnapshot snapshot, string? snapshotJson, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            token.ThrowIfCancellationRequested();
            ServerClientTelemetrySampleRecord record = BuildHostRecord(tenantId, clientId, clientName, collectionRequestId, snapshot);
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlTransaction transaction = connection.BeginTransaction())
                {
                    using (NpgsqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
                        command.CommandText = InsertHostSql();
                        AddHostParameters(command, record, snapshotJson);
                        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    foreach (ClientGpuDeviceTelemetry device in snapshot.Gpu.Devices)
                    {
                        using (NpgsqlCommand command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = InsertGpuSql();
                            AddGpuParameters(command, record, device);
                            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        }
                    }

                    transaction.Commit();
                }
            }

            return record;
        }

        /// <inheritdoc />
        public async Task<ServerClientTelemetrySampleDetail?> ReadHostDetailAsync(string tenantId, string id, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM client_telemetry_samples WHERE tenant_id = @tenantId AND id = @id AND active = 1 LIMIT 1;";
                    command.Parameters.AddWithValue("@tenantId", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (!await reader.ReadAsync(token).ConfigureAwait(false)) return null;
                        ServerClientTelemetrySampleDetail detail = new ServerClientTelemetrySampleDetail
                        {
                            Sample = ReadHost(reader),
                            SnapshotJson = GetNullableString(reader, "snapshot_json")
                        };
                        reader.Close();
                        detail.Gpus = await ReadGpusAsync(connection, tenantId, id, token).ConfigureAwait(false);
                        return detail;
                    }
                }
            }
        }

        /// <inheritdoc />
        public async Task<ServerClientTelemetrySearchResult> SearchHostAsync(ServerClientTelemetrySearchRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            token.ThrowIfCancellationRequested();
            ServerClientTelemetrySearchResult result = new ServerClientTelemetrySearchResult();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM client_telemetry_samples WHERE tenant_id = @tenantId AND active = 1 ORDER BY collected_utc DESC LIMIT @limit OFFSET @offset;";
                    command.Parameters.AddWithValue("@tenantId", request.TenantId ?? string.Empty);
                    command.Parameters.AddWithValue("@limit", Math.Clamp(request.Limit, 1, 1000));
                    command.Parameters.AddWithValue("@offset", Math.Max(0, request.Offset));
                    using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            result.Items.Add(ReadHost(reader));
                        }
                    }
                }
            }

            result.Total = result.Items.Count;
            return result;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ServerClientTelemetryRollupBucket>> RollupHostAsync(ServerClientTelemetryRollupRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            token.ThrowIfCancellationRequested();
            string metricColumn = HostMetricColumn(request.Metric);
            DateTime toUtc = request.ToUtc ?? DateTime.UtcNow;
            DateTime fromUtc = request.FromUtc ?? toUtc.AddHours(-1);
            int bucketMinutes = Math.Clamp(request.BucketMinutes, 1, 1440);
            List<ServerClientTelemetryRollupBucket> buckets = new List<ServerClientTelemetryRollupBucket>();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = $@"
SELECT FLOOR((EXTRACT(EPOCH FROM collected_utc::timestamptz) - EXTRACT(EPOCH FROM CAST(@fromUtc AS timestamptz))) / (@bucketMinutes * 60))::integer AS bucket_index,
       COUNT({metricColumn}) AS sample_count,
       AVG({metricColumn}) AS average_value,
       MIN({metricColumn}) AS minimum_value,
       MAX({metricColumn}) AS maximum_value
FROM client_telemetry_samples
WHERE tenant_id = @tenantId
  AND active = 1
  AND collected_utc >= @fromUtc
  AND collected_utc < @toUtc
  AND (@clientId IS NULL OR client_id = @clientId)
  AND {metricColumn} IS NOT NULL
GROUP BY bucket_index
ORDER BY bucket_index;";
                    AddNullable(command, "@tenantId", request.TenantId ?? string.Empty);
                    AddNullable(command, "@clientId", String.IsNullOrWhiteSpace(request.ClientId) ? null : request.ClientId);
                    AddNullable(command, "@fromUtc", fromUtc.ToString("O", CultureInfo.InvariantCulture));
                    AddNullable(command, "@toUtc", toUtc.ToString("O", CultureInfo.InvariantCulture));
                    AddNullable(command, "@bucketMinutes", bucketMinutes);
                    using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            int bucketIndex = GetInt(reader, "bucket_index");
                            DateTime startUtc = fromUtc.AddMinutes(bucketIndex * bucketMinutes);
                            DateTime endUtc = startUtc.AddMinutes(bucketMinutes);
                            buckets.Add(new ServerClientTelemetryRollupBucket
                            {
                                StartUtc = startUtc,
                                EndUtc = endUtc,
                                Label = BuildRollupLabel(startUtc),
                                Count = GetInt(reader, "sample_count"),
                                Average = GetNullableDouble(reader, "average_value"),
                                Minimum = GetNullableDouble(reader, "minimum_value"),
                                Maximum = GetNullableDouble(reader, "maximum_value")
                            });
                        }
                    }
                }
            }

            return buckets;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ServerClientTelemetrySampleRecord>> LatestHostByClientAsync(string tenantId, string? clientId, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            List<ServerClientTelemetrySampleRecord> records = new List<ServerClientTelemetrySampleRecord>();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = String.IsNullOrWhiteSpace(clientId)
                        ? "SELECT * FROM client_telemetry_samples WHERE tenant_id = @tenantId AND active = 1 ORDER BY collected_utc DESC LIMIT 250;"
                        : "SELECT * FROM client_telemetry_samples WHERE tenant_id = @tenantId AND client_id = @clientId AND active = 1 ORDER BY collected_utc DESC LIMIT 1;";
                    command.Parameters.AddWithValue("@tenantId", tenantId);
                    if (!String.IsNullOrWhiteSpace(clientId)) command.Parameters.AddWithValue("@clientId", clientId);
                    using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            ServerClientTelemetrySampleRecord record = ReadHost(reader);
                            if (seen.Add(record.ClientId ?? string.Empty)) records.Add(record);
                        }
                    }
                }
            }

            return records;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteHostAsync(string tenantId, string id, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlTransaction transaction = connection.BeginTransaction())
                {
                    await DeleteHostChildrenAsync(connection, transaction, tenantId, "sample_id = @id", delegate (NpgsqlCommand command)
                    {
                        command.Parameters.AddWithValue("@id", id);
                    }, token).ConfigureAwait(false);
                    int deleted = await DeleteHostParentsAsync(connection, transaction, tenantId, "id = @id", delegate (NpgsqlCommand command)
                    {
                        command.Parameters.AddWithValue("@id", id);
                    }, token).ConfigureAwait(false);
                    transaction.Commit();
                    return deleted > 0;
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteHostBulkAsync(ServerClientTelemetryDeleteRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            token.ThrowIfCancellationRequested();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlTransaction transaction = connection.BeginTransaction())
                {
                    string predicate = "(@clientId IS NULL OR client_id = @clientId) AND (@toUtc IS NULL OR collected_utc < @toUtc)";
                    Action<NpgsqlCommand> addParameters = delegate (NpgsqlCommand command)
                    {
                        AddNullable(command, "@clientId", request.ClientId);
                        AddNullable(command, "@toUtc", request.ToUtc?.ToString("O", CultureInfo.InvariantCulture));
                    };
                    await DeleteHostChildrenAsync(connection, transaction, request.TenantId ?? string.Empty, predicate, addParameters, token).ConfigureAwait(false);
                    int deleted = await DeleteHostParentsAsync(connection, transaction, request.TenantId ?? string.Empty, predicate, addParameters, token).ConfigureAwait(false);
                    transaction.Commit();
                    return deleted;
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteOlderThanAsync(DateTime olderThanUtc, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlTransaction transaction = connection.BeginTransaction())
                {
                    int total = 0;
                    string predicate = "collected_utc < @olderThanUtc";
                    Action<NpgsqlCommand> addParameters = delegate (NpgsqlCommand command)
                    {
                        command.Parameters.AddWithValue("@olderThanUtc", olderThanUtc.ToString("O", CultureInfo.InvariantCulture));
                    };
                    total += await DeleteHostChildrenAsync(connection, transaction, null, predicate, addParameters, token).ConfigureAwait(false);
                    total += await DeleteHostParentsAsync(connection, transaction, null, predicate, addParameters, token).ConfigureAwait(false);
                    total += await DeleteProviderChildrenForParentsAsync(connection, transaction, null, predicate, addParameters, token).ConfigureAwait(false);
                    total += await DeleteProviderParentsAsync(connection, transaction, null, predicate, addParameters, token).ConfigureAwait(false);
                    total += await DeleteAttemptsAsync(connection, transaction, "started_utc < @olderThanUtc", addParameters, token).ConfigureAwait(false);

                    transaction.Commit();
                    return total;
                }
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ServerProviderTelemetrySampleRecord>> CreateProviderSnapshotAsync(string tenantId, string clientId, string? clientName, string? collectionRequestId, ClientProviderTelemetrySnapshot snapshot, bool persistRawSnapshots, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            token.ThrowIfCancellationRequested();
            List<ServerProviderTelemetrySampleRecord> records = new List<ServerProviderTelemetrySampleRecord>();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlTransaction transaction = connection.BeginTransaction())
                {
                    foreach (ClientProviderTelemetryProviderStatus provider in snapshot.Providers)
                    {
                        ServerProviderTelemetrySampleRecord record = BuildProviderRecord(tenantId, clientId, clientName, collectionRequestId, provider);
                        records.Add(record);
                        using (NpgsqlCommand command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = InsertProviderSql();
                            AddProviderParameters(command, record, persistRawSnapshots ? JsonUtil.Serialize(provider, true) : null, provider.RawMetricsTruncated);
                            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        }
                    }

                    transaction.Commit();
                }
            }

            return records;
        }

        /// <inheritdoc />
        public async Task<ServerProviderTelemetrySampleDetail?> ReadProviderDetailAsync(string tenantId, string id, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM client_provider_telemetry_samples WHERE tenant_id = @tenantId AND id = @id AND active = 1 LIMIT 1;";
                    command.Parameters.AddWithValue("@tenantId", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (!await reader.ReadAsync(token).ConfigureAwait(false)) return null;
                        return new ServerProviderTelemetrySampleDetail
                        {
                            Sample = ReadProvider(reader),
                            SnapshotJson = GetNullableString(reader, "snapshot_json")
                        };
                    }
                }
            }
        }

        /// <inheritdoc />
        public async Task<ServerProviderTelemetrySearchResult> SearchProviderAsync(ServerProviderTelemetrySearchRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            token.ThrowIfCancellationRequested();
            ServerProviderTelemetrySearchResult result = new ServerProviderTelemetrySearchResult();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT * FROM client_provider_telemetry_samples WHERE tenant_id = @tenantId AND active = 1 ORDER BY collected_utc DESC LIMIT @limit OFFSET @offset;";
                    command.Parameters.AddWithValue("@tenantId", request.TenantId ?? string.Empty);
                    command.Parameters.AddWithValue("@limit", Math.Clamp(request.Limit, 1, 1000));
                    command.Parameters.AddWithValue("@offset", Math.Max(0, request.Offset));
                    using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            result.Items.Add(ReadProvider(reader));
                        }
                    }
                }
            }

            result.Total = result.Items.Count;
            return result;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ServerProviderTelemetryRollupBucket>> RollupProviderAsync(ServerProviderTelemetryRollupRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            token.ThrowIfCancellationRequested();
            string metricColumn = ProviderMetricColumn(request.Metric);
            DateTime toUtc = request.ToUtc ?? DateTime.UtcNow;
            DateTime fromUtc = request.FromUtc ?? toUtc.AddHours(-1);
            int bucketMinutes = Math.Clamp(request.BucketMinutes, 1, 1440);
            List<ServerProviderTelemetryRollupBucket> buckets = new List<ServerProviderTelemetryRollupBucket>();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = $@"
SELECT FLOOR((EXTRACT(EPOCH FROM collected_utc::timestamptz) - EXTRACT(EPOCH FROM CAST(@fromUtc AS timestamptz))) / (@bucketMinutes * 60))::integer AS bucket_index,
       COUNT({metricColumn}) AS sample_count,
       AVG({metricColumn}) AS average_value,
       MIN({metricColumn}) AS minimum_value,
       MAX({metricColumn}) AS maximum_value
FROM client_provider_telemetry_samples
WHERE tenant_id = @tenantId
  AND active = 1
  AND collected_utc >= @fromUtc
  AND collected_utc < @toUtc
  AND (@clientId IS NULL OR client_id = @clientId)
  AND (@providerName IS NULL OR provider_name = @providerName)
  AND {metricColumn} IS NOT NULL
GROUP BY bucket_index
ORDER BY bucket_index;";
                    AddNullable(command, "@tenantId", request.TenantId ?? string.Empty);
                    AddNullable(command, "@clientId", String.IsNullOrWhiteSpace(request.ClientId) ? null : request.ClientId);
                    AddNullable(command, "@providerName", String.IsNullOrWhiteSpace(request.ProviderName) ? null : request.ProviderName);
                    AddNullable(command, "@fromUtc", fromUtc.ToString("O", CultureInfo.InvariantCulture));
                    AddNullable(command, "@toUtc", toUtc.ToString("O", CultureInfo.InvariantCulture));
                    AddNullable(command, "@bucketMinutes", bucketMinutes);
                    using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            int bucketIndex = GetInt(reader, "bucket_index");
                            DateTime startUtc = fromUtc.AddMinutes(bucketIndex * bucketMinutes);
                            DateTime endUtc = startUtc.AddMinutes(bucketMinutes);
                            buckets.Add(new ServerProviderTelemetryRollupBucket
                            {
                                StartUtc = startUtc,
                                EndUtc = endUtc,
                                Label = BuildRollupLabel(startUtc),
                                Count = GetInt(reader, "sample_count"),
                                Average = GetNullableDouble(reader, "average_value"),
                                Minimum = GetNullableDouble(reader, "minimum_value"),
                                Maximum = GetNullableDouble(reader, "maximum_value")
                            });
                        }
                    }
                }
            }

            return buckets;
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ServerProviderTelemetrySampleRecord>> LatestProviderByClientAsync(string tenantId, string? clientId, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            List<ServerProviderTelemetrySampleRecord> records = new List<ServerProviderTelemetrySampleRecord>();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = String.IsNullOrWhiteSpace(clientId)
                        ? "SELECT * FROM client_provider_telemetry_samples WHERE tenant_id = @tenantId AND active = 1 ORDER BY collected_utc DESC LIMIT 500;"
                        : "SELECT * FROM client_provider_telemetry_samples WHERE tenant_id = @tenantId AND client_id = @clientId AND active = 1 ORDER BY collected_utc DESC LIMIT 100;";
                    command.Parameters.AddWithValue("@tenantId", tenantId);
                    if (!String.IsNullOrWhiteSpace(clientId)) command.Parameters.AddWithValue("@clientId", clientId);
                    using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            ServerProviderTelemetrySampleRecord record = ReadProvider(reader);
                            string key = (record.ClientId ?? string.Empty) + "|" + record.ProviderName + "|" + record.EndpointHash;
                            if (seen.Add(key)) records.Add(record);
                        }
                    }
                }
            }

            return records;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteProviderAsync(string tenantId, string id, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlTransaction transaction = connection.BeginTransaction())
                {
                    await DeleteProviderChildrenAsync(connection, transaction, tenantId, "sample_id = @id", delegate (NpgsqlCommand command)
                    {
                        command.Parameters.AddWithValue("@id", id);
                    }, token).ConfigureAwait(false);
                    int deleted = await DeleteProviderParentsAsync(connection, transaction, tenantId, "id = @id", delegate (NpgsqlCommand command)
                    {
                        command.Parameters.AddWithValue("@id", id);
                    }, token).ConfigureAwait(false);
                    transaction.Commit();
                    return deleted > 0;
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteProviderBulkAsync(ServerClientTelemetryDeleteRequest request, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(request);
            token.ThrowIfCancellationRequested();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlTransaction transaction = connection.BeginTransaction())
                {
                    string predicate = "(@clientId IS NULL OR client_id = @clientId) AND (@toUtc IS NULL OR collected_utc < @toUtc)";
                    Action<NpgsqlCommand> addParameters = delegate (NpgsqlCommand command)
                    {
                        AddNullable(command, "@clientId", request.ClientId);
                        AddNullable(command, "@toUtc", request.ToUtc?.ToString("O", CultureInfo.InvariantCulture));
                    };
                    await DeleteProviderChildrenForParentsAsync(connection, transaction, request.TenantId ?? string.Empty, predicate, addParameters, token).ConfigureAwait(false);
                    int deleted = await DeleteProviderParentsAsync(connection, transaction, request.TenantId ?? string.Empty, predicate, addParameters, token).ConfigureAwait(false);
                    transaction.Commit();
                    return deleted;
                }
            }
        }

        /// <inheritdoc />
        public async Task<ServerClientTelemetryCollectionAttemptRecord> CreateAttemptAsync(ServerClientTelemetryCollectionAttemptRecord attempt, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(attempt);
            token.ThrowIfCancellationRequested();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = InsertAttemptSql();
                    AddAttemptParameters(command, attempt);
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }

            return attempt;
        }

        /// <inheritdoc />
        public async Task CompleteAttemptAsync(ServerClientTelemetryCollectionAttemptRecord attempt, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(attempt);
            token.ThrowIfCancellationRequested();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "UPDATE client_telemetry_collection_attempts SET completed_utc = @completedUtc, elapsed_ms = @elapsedMs, host_succeeded = @hostSucceeded, provider_succeeded = @providerSucceeded, status = @status, error_code = @errorCode, error_message = @errorMessage, host_sample_id = @hostSampleId, provider_sample_count = @providerSampleCount WHERE id = @id AND tenant_id = @tenantId;";
                    AddAttemptParameters(command, attempt);
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<IReadOnlyList<ServerClientTelemetryCollectionAttemptRecord>> SearchAttemptsAsync(string tenantId, string? clientId, int limit, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            List<ServerClientTelemetryCollectionAttemptRecord> records = new List<ServerClientTelemetryCollectionAttemptRecord>();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = String.IsNullOrWhiteSpace(clientId)
                        ? "SELECT * FROM client_telemetry_collection_attempts WHERE tenant_id = @tenantId ORDER BY started_utc DESC LIMIT @limit;"
                        : "SELECT * FROM client_telemetry_collection_attempts WHERE tenant_id = @tenantId AND client_id = @clientId ORDER BY started_utc DESC LIMIT @limit;";
                    command.Parameters.AddWithValue("@tenantId", tenantId);
                    if (!String.IsNullOrWhiteSpace(clientId)) command.Parameters.AddWithValue("@clientId", clientId);
                    command.Parameters.AddWithValue("@limit", Math.Clamp(limit, 1, 1000));
                    using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            records.Add(ReadAttempt(reader));
                        }
                    }
                }
            }

            return records;
        }

        private static ServerClientTelemetrySampleRecord BuildHostRecord(string tenantId, string clientId, string? clientName, string? collectionRequestId, ClientTelemetrySnapshot snapshot)
        {
            return new ServerClientTelemetrySampleRecord
            {
                Id = IdGenerator.NewClientTelemetrySampleId(),
                TenantId = tenantId,
                ClientId = clientId,
                ClientName = clientName,
                MachineName = snapshot.Metadata.MachineName,
                CollectedUtc = snapshot.Metadata.CollectedUtc,
                PersistedUtc = DateTime.UtcNow,
                CollectionElapsedMs = snapshot.Metadata.CollectionElapsedMs,
                OsDescription = snapshot.System.OsDescription,
                Architecture = snapshot.System.Architecture,
                ProcessorCount = snapshot.System.ProcessorCount,
                CpuUtilizationPercent = snapshot.Cpu.UtilizationPercent,
                MemoryTotalBytes = snapshot.Memory.TotalBytes,
                MemoryUsedBytes = snapshot.Memory.UsedBytes,
                MemoryAvailableBytes = snapshot.Memory.AvailableBytes,
                MemoryUtilizationPercent = snapshot.Memory.UtilizationPercent,
                DiskTotalBytes = snapshot.Disk.TotalBytes,
                DiskUsedBytes = snapshot.Disk.UsedBytes,
                DiskAvailableBytes = snapshot.Disk.AvailableBytes,
                DiskUtilizationPercent = snapshot.Disk.UtilizationPercent,
                DiskReadBytesPerSecond = snapshot.Disk.ReadBytesPerSecond,
                DiskWriteBytesPerSecond = snapshot.Disk.WriteBytesPerSecond,
                GpuProvider = snapshot.Gpu.Provider,
                GpuDeviceCount = snapshot.Gpu.DeviceCount,
                GpuAverageUtilizationPercent = snapshot.Gpu.AverageUtilizationPercent,
                GpuAverageMemoryUtilizationPercent = snapshot.Gpu.AverageMemoryUtilizationPercent,
                GpuAverageTemperatureCelsius = snapshot.Gpu.AverageTemperatureCelsius,
                GpuAveragePowerWatts = snapshot.Gpu.AveragePowerWatts,
                GpuTotalMemoryBytes = snapshot.Gpu.TotalMemoryBytes,
                GpuUsedMemoryBytes = snapshot.Gpu.UsedMemoryBytes
            };
        }

        private static ServerProviderTelemetrySampleRecord BuildProviderRecord(string tenantId, string clientId, string? clientName, string? collectionRequestId, ClientProviderTelemetryProviderStatus provider)
        {
            return new ServerProviderTelemetrySampleRecord
            {
                Id = IdGenerator.NewClientProviderTelemetrySampleId(),
                TenantId = tenantId,
                ClientId = clientId,
                ClientName = clientName,
                ProviderName = provider.Endpoint.ProviderName,
                ApiType = provider.Endpoint.ApiType,
                ProviderKind = provider.Endpoint.Kind,
                EndpointHash = provider.Endpoint.EndpointHash,
                EndpointDisplay = provider.Endpoint.EndpointDisplay,
                CollectedUtc = provider.CollectedUtc,
                PersistedUtc = DateTime.UtcNow,
                Status = provider.Status,
                HealthStatus = provider.HealthStatus,
                Version = provider.Version,
                AvailableModelCount = provider.AvailableModelCount,
                LoadedModelCount = provider.LoadedModelCount,
                ActiveRequests = provider.ActiveRequests,
                QueuedRequests = provider.QueuedRequests,
                WaitingRequests = provider.WaitingRequests,
                PromptTokensPerSecond = provider.PromptTokensPerSecond,
                GenerationTokensPerSecond = provider.GenerationTokensPerSecond,
                TotalTokensPerSecond = provider.TotalTokensPerSecond,
                RequestRatePerSecond = provider.RequestRatePerSecond,
                ErrorRatePerSecond = provider.ErrorRatePerSecond,
                LatencyP95Ms = provider.LatencyP95Ms,
                TimeToFirstTokenP95Ms = provider.TimeToFirstTokenP95Ms,
                CacheUsagePercent = provider.CacheUsagePercent,
                ProcessId = provider.ProcessId,
                ProcessName = provider.ProcessName,
                LastError = provider.LastError
            };
        }

        private static void AddHostParameters(NpgsqlCommand command, ServerClientTelemetrySampleRecord record, string? snapshotJson)
        {
            AddNullable(command, "@id", record.Id);
            AddNullable(command, "@tenantId", record.TenantId);
            AddNullable(command, "@clientId", record.ClientId);
            AddNullable(command, "@clientName", record.ClientName);
            AddNullable(command, "@machineName", record.MachineName);
            AddNullable(command, "@collectedUtc", record.CollectedUtc.ToString("O", CultureInfo.InvariantCulture));
            AddNullable(command, "@persistedUtc", record.PersistedUtc.ToString("O", CultureInfo.InvariantCulture));
            AddNullable(command, "@cpu", record.CpuUtilizationPercent);
            AddNullable(command, "@memory", record.MemoryUtilizationPercent);
            AddNullable(command, "@disk", record.DiskUtilizationPercent);
            AddNullable(command, "@gpu", record.GpuAverageUtilizationPercent);
            AddNullable(command, "@snapshotJson", snapshotJson);
            AddNullable(command, "@osDescription", record.OsDescription);
            AddNullable(command, "@architecture", record.Architecture);
            AddNullable(command, "@processorCount", record.ProcessorCount);
        }

        private static void AddGpuParameters(NpgsqlCommand command, ServerClientTelemetrySampleRecord record, ClientGpuDeviceTelemetry device)
        {
            AddNullable(command, "@id", IdGenerator.NewClientTelemetryGpuSampleId());
            AddNullable(command, "@sampleId", record.Id);
            AddNullable(command, "@tenantId", record.TenantId);
            AddNullable(command, "@clientId", record.ClientId);
            AddNullable(command, "@collectedUtc", record.CollectedUtc.ToString("O", CultureInfo.InvariantCulture));
            AddNullable(command, "@gpuUuid", device.Uuid);
            AddNullable(command, "@name", device.Name);
            AddNullable(command, "@utilization", device.UtilizationPercent);
        }

        private static void AddProviderParameters(NpgsqlCommand command, ServerProviderTelemetrySampleRecord record, string? snapshotJson, bool rawMetricsTruncated)
        {
            AddNullable(command, "@id", record.Id);
            AddNullable(command, "@tenantId", record.TenantId);
            AddNullable(command, "@clientId", record.ClientId);
            AddNullable(command, "@clientName", record.ClientName);
            AddNullable(command, "@providerName", record.ProviderName);
            AddNullable(command, "@apiType", record.ApiType);
            AddNullable(command, "@providerKind", record.ProviderKind.ToString());
            AddNullable(command, "@endpointHash", record.EndpointHash);
            AddNullable(command, "@endpointDisplay", record.EndpointDisplay);
            AddNullable(command, "@collectedUtc", record.CollectedUtc.ToString("O", CultureInfo.InvariantCulture));
            AddNullable(command, "@persistedUtc", record.PersistedUtc.ToString("O", CultureInfo.InvariantCulture));
            AddNullable(command, "@status", record.Status.ToString());
            AddNullable(command, "@healthStatus", record.HealthStatus.ToString());
            AddNullable(command, "@availableModelCount", record.AvailableModelCount);
            AddNullable(command, "@loadedModelCount", record.LoadedModelCount);
            AddNullable(command, "@activeRequests", record.ActiveRequests);
            AddNullable(command, "@queuedRequests", record.QueuedRequests);
            AddNullable(command, "@latencyP95Ms", record.LatencyP95Ms);
            AddNullable(command, "@snapshotJson", snapshotJson);
            AddNullable(command, "@rawMetricsTruncated", rawMetricsTruncated ? 1 : 0);
        }

        private static void AddAttemptParameters(NpgsqlCommand command, ServerClientTelemetryCollectionAttemptRecord attempt)
        {
            AddNullable(command, "@id", attempt.Id);
            AddNullable(command, "@tenantId", attempt.TenantId);
            AddNullable(command, "@clientId", attempt.ClientId);
            AddNullable(command, "@clientName", attempt.ClientName);
            AddNullable(command, "@startedUtc", attempt.StartedUtc.ToString("O", CultureInfo.InvariantCulture));
            AddNullable(command, "@completedUtc", attempt.CompletedUtc?.ToString("O", CultureInfo.InvariantCulture));
            AddNullable(command, "@elapsedMs", attempt.ElapsedMs);
            AddNullable(command, "@hostRequested", attempt.HostRequested ? 1 : 0);
            AddNullable(command, "@providerRequested", attempt.ProviderRequested ? 1 : 0);
            AddNullable(command, "@hostSucceeded", attempt.HostSucceeded ? 1 : 0);
            AddNullable(command, "@providerSucceeded", attempt.ProviderSucceeded ? 1 : 0);
            AddNullable(command, "@status", attempt.Status);
            AddNullable(command, "@errorCode", attempt.ErrorCode);
            AddNullable(command, "@errorMessage", attempt.ErrorMessage);
            AddNullable(command, "@hostSampleId", attempt.HostSampleId);
            AddNullable(command, "@providerSampleCount", attempt.ProviderSampleCount);
        }

        private static ServerClientTelemetrySampleRecord ReadHost(NpgsqlDataReader reader)
        {
            return new ServerClientTelemetrySampleRecord
            {
                Id = GetString(reader, "id"),
                TenantId = GetString(reader, "tenant_id"),
                ClientId = GetString(reader, "client_id"),
                ClientName = GetNullableString(reader, "client_name"),
                MachineName = GetNullableString(reader, "machine_name") ?? string.Empty,
                CollectedUtc = ReadUtc(reader, "collected_utc"),
                PersistedUtc = ReadUtc(reader, "persisted_utc"),
                OsDescription = GetNullableString(reader, "os_description") ?? string.Empty,
                Architecture = GetNullableString(reader, "architecture") ?? string.Empty,
                ProcessorCount = GetInt(reader, "processor_count"),
                CpuUtilizationPercent = GetNullableDouble(reader, "cpu_utilization_percent"),
                MemoryUtilizationPercent = GetNullableDouble(reader, "memory_utilization_percent"),
                DiskUtilizationPercent = GetNullableDouble(reader, "disk_utilization_percent"),
                GpuAverageUtilizationPercent = GetNullableDouble(reader, "gpu_average_utilization_percent")
            };
        }

        private static string HostMetricColumn(ClientTelemetryMetricEnum metric)
        {
            switch (metric)
            {
                case ClientTelemetryMetricEnum.CpuUtilizationPercent:
                    return "cpu_utilization_percent";
                case ClientTelemetryMetricEnum.MemoryUtilizationPercent:
                    return "memory_utilization_percent";
                case ClientTelemetryMetricEnum.DiskUtilizationPercent:
                    return "disk_utilization_percent";
                case ClientTelemetryMetricEnum.GpuUtilizationPercent:
                    return "gpu_average_utilization_percent";
                default:
                    throw new ArgumentException("Host telemetry metric is not stored in the server rollup table: " + metric.ToString(), nameof(metric));
            }
        }

        private static string ProviderMetricColumn(ClientProviderTelemetryMetricEnum metric)
        {
            switch (metric)
            {
                case ClientProviderTelemetryMetricEnum.ActiveRequests:
                    return "active_requests";
                case ClientProviderTelemetryMetricEnum.QueuedRequests:
                    return "queued_requests";
                case ClientProviderTelemetryMetricEnum.LatencyP95Ms:
                    return "latency_p95_ms";
                case ClientProviderTelemetryMetricEnum.LoadedModelCount:
                    return "loaded_model_count";
                case ClientProviderTelemetryMetricEnum.AvailableModelCount:
                    return "available_model_count";
                default:
                    throw new ArgumentException("Provider telemetry metric is not stored in the server rollup table: " + metric.ToString(), nameof(metric));
            }
        }

        private static string BuildRollupLabel(DateTime startUtc)
        {
            return startUtc.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        private static ServerProviderTelemetrySampleRecord ReadProvider(NpgsqlDataReader reader)
        {
            return new ServerProviderTelemetrySampleRecord
            {
                Id = GetString(reader, "id"),
                TenantId = GetString(reader, "tenant_id"),
                ClientId = GetString(reader, "client_id"),
                ClientName = GetNullableString(reader, "client_name"),
                ProviderName = GetNullableString(reader, "provider_name") ?? string.Empty,
                ApiType = GetNullableString(reader, "api_type") ?? string.Empty,
                ProviderKind = Enum.TryParse(GetString(reader, "provider_kind"), out ClientProviderTelemetryKindEnum kind) ? kind : ClientProviderTelemetryKindEnum.Unknown,
                EndpointHash = GetNullableString(reader, "endpoint_hash") ?? string.Empty,
                EndpointDisplay = GetNullableString(reader, "endpoint_display") ?? string.Empty,
                CollectedUtc = ReadUtc(reader, "collected_utc"),
                PersistedUtc = ReadUtc(reader, "persisted_utc"),
                Status = Enum.TryParse(GetString(reader, "status"), out ClientProviderTelemetryStatusEnum status) ? status : ClientProviderTelemetryStatusEnum.Unavailable,
                HealthStatus = Enum.TryParse(GetString(reader, "health_status"), out ClientProviderTelemetryHealthStatusEnum health) ? health : ClientProviderTelemetryHealthStatusEnum.Unknown,
                AvailableModelCount = GetInt(reader, "available_model_count"),
                LoadedModelCount = GetInt(reader, "loaded_model_count"),
                ActiveRequests = GetNullableDouble(reader, "active_requests"),
                QueuedRequests = GetNullableDouble(reader, "queued_requests"),
                LatencyP95Ms = GetNullableDouble(reader, "latency_p95_ms")
            };
        }

        private static ServerClientTelemetryCollectionAttemptRecord ReadAttempt(NpgsqlDataReader reader)
        {
            return new ServerClientTelemetryCollectionAttemptRecord
            {
                Id = GetString(reader, "id"),
                TenantId = GetString(reader, "tenant_id"),
                ClientId = GetString(reader, "client_id"),
                ClientName = GetNullableString(reader, "client_name"),
                StartedUtc = ReadUtc(reader, "started_utc"),
                CompletedUtc = ReadNullableUtc(reader, "completed_utc"),
                ElapsedMs = GetNullableDouble(reader, "elapsed_ms"),
                HostRequested = GetInt(reader, "host_requested") == 1,
                ProviderRequested = GetInt(reader, "provider_requested") == 1,
                HostSucceeded = GetInt(reader, "host_succeeded") == 1,
                ProviderSucceeded = GetInt(reader, "provider_succeeded") == 1,
                Status = GetNullableString(reader, "status") ?? string.Empty,
                ErrorCode = GetNullableString(reader, "error_code"),
                ErrorMessage = GetNullableString(reader, "error_message"),
                HostSampleId = GetNullableString(reader, "host_sample_id"),
                ProviderSampleCount = GetInt(reader, "provider_sample_count")
            };
        }

        private static async Task<List<ServerClientTelemetryGpuSampleRecord>> ReadGpusAsync(NpgsqlConnection connection, string tenantId, string sampleId, CancellationToken token)
        {
            List<ServerClientTelemetryGpuSampleRecord> rows = new List<ServerClientTelemetryGpuSampleRecord>();
            using (NpgsqlCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT * FROM client_telemetry_gpu_samples WHERE tenant_id = @tenantId AND sample_id = @sampleId ORDER BY id ASC;";
                command.Parameters.AddWithValue("@tenantId", tenantId);
                command.Parameters.AddWithValue("@sampleId", sampleId);
                using (NpgsqlDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        rows.Add(new ServerClientTelemetryGpuSampleRecord
                        {
                            Id = GetString(reader, "id"),
                            SampleId = GetString(reader, "sample_id"),
                            TenantId = GetString(reader, "tenant_id"),
                            Uuid = GetNullableString(reader, "gpu_uuid") ?? string.Empty,
                            Name = GetNullableString(reader, "name") ?? string.Empty,
                            UtilizationPercent = GetNullableDouble(reader, "utilization_percent")
                        });
                    }
                }
            }

            return rows;
        }

        private static string InsertHostSql()
        {
            return "INSERT INTO client_telemetry_samples (id, tenant_id, client_id, client_name, machine_name, collected_utc, received_utc, persisted_utc, os_description, architecture, processor_count, cpu_utilization_percent, memory_utilization_percent, disk_utilization_percent, gpu_average_utilization_percent, snapshot_json, active) VALUES (@id, @tenantId, @clientId, @clientName, @machineName, @collectedUtc, @persistedUtc, @persistedUtc, @osDescription, @architecture, @processorCount, @cpu, @memory, @disk, @gpu, @snapshotJson, 1);";
        }

        private static string InsertGpuSql()
        {
            return "INSERT INTO client_telemetry_gpu_samples (id, sample_id, tenant_id, client_id, collected_utc, gpu_uuid, name, utilization_percent) VALUES (@id, @sampleId, @tenantId, @clientId, @collectedUtc, @gpuUuid, @name, @utilization);";
        }

        private static string InsertProviderSql()
        {
            return "INSERT INTO client_provider_telemetry_samples (id, tenant_id, client_id, client_name, provider_name, api_type, provider_kind, endpoint_hash, endpoint_display, collected_utc, received_utc, persisted_utc, status, health_status, available_model_count, loaded_model_count, active_requests, queued_requests, latency_p95_ms, snapshot_json, raw_metrics_truncated, active) VALUES (@id, @tenantId, @clientId, @clientName, @providerName, @apiType, @providerKind, @endpointHash, @endpointDisplay, @collectedUtc, @persistedUtc, @persistedUtc, @status, @healthStatus, @availableModelCount, @loadedModelCount, @activeRequests, @queuedRequests, @latencyP95Ms, @snapshotJson, @rawMetricsTruncated, 1);";
        }

        private static string InsertAttemptSql()
        {
            return "INSERT INTO client_telemetry_collection_attempts (id, tenant_id, client_id, client_name, started_utc, completed_utc, elapsed_ms, host_requested, provider_requested, host_succeeded, provider_succeeded, status, error_code, error_message, host_sample_id, provider_sample_count) VALUES (@id, @tenantId, @clientId, @clientName, @startedUtc, @completedUtc, @elapsedMs, @hostRequested, @providerRequested, @hostSucceeded, @providerSucceeded, @status, @errorCode, @errorMessage, @hostSampleId, @providerSampleCount);";
        }

        private static async Task<int> ExecuteTenantDeleteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string tableName, string tenantId, CancellationToken token)
        {
            return await ExecuteDeleteAsync(connection, transaction, "DELETE FROM " + tableName + " WHERE tenant_id = @tenantId;", delegate (NpgsqlCommand command)
            {
                command.Parameters.AddWithValue("@tenantId", tenantId);
            }, token).ConfigureAwait(false);
        }

        private static async Task<int> DeleteHostChildrenAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string? tenantId, string predicate, Action<NpgsqlCommand> addParameters, CancellationToken token)
        {
            string tenantPredicate = String.IsNullOrWhiteSpace(tenantId) ? string.Empty : "tenant_id = @tenantId AND ";
            return await ExecuteDeleteAsync(connection, transaction, "DELETE FROM client_telemetry_gpu_samples WHERE " + tenantPredicate + predicate + ";", delegate (NpgsqlCommand command)
            {
                if (!String.IsNullOrWhiteSpace(tenantId)) command.Parameters.AddWithValue("@tenantId", tenantId);
                addParameters(command);
            }, token).ConfigureAwait(false);
        }

        private static async Task<int> DeleteHostParentsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string? tenantId, string predicate, Action<NpgsqlCommand> addParameters, CancellationToken token)
        {
            string tenantPredicate = String.IsNullOrWhiteSpace(tenantId) ? string.Empty : "tenant_id = @tenantId AND ";
            return await ExecuteDeleteAsync(connection, transaction, "DELETE FROM client_telemetry_samples WHERE " + tenantPredicate + predicate + ";", delegate (NpgsqlCommand command)
            {
                if (!String.IsNullOrWhiteSpace(tenantId)) command.Parameters.AddWithValue("@tenantId", tenantId);
                addParameters(command);
            }, token).ConfigureAwait(false);
        }

        private static async Task<int> DeleteProviderChildrenAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string? tenantId, string predicate, Action<NpgsqlCommand> addParameters, CancellationToken token)
        {
            string tenantPredicate = String.IsNullOrWhiteSpace(tenantId) ? string.Empty : "tenant_id = @tenantId AND ";
            int total = 0;
            total += await ExecuteDeleteAsync(connection, transaction, "DELETE FROM client_provider_telemetry_models WHERE " + tenantPredicate + predicate + ";", delegate (NpgsqlCommand command)
            {
                if (!String.IsNullOrWhiteSpace(tenantId)) command.Parameters.AddWithValue("@tenantId", tenantId);
                addParameters(command);
            }, token).ConfigureAwait(false);
            total += await ExecuteDeleteAsync(connection, transaction, "DELETE FROM client_provider_telemetry_metrics WHERE " + tenantPredicate + predicate + ";", delegate (NpgsqlCommand command)
            {
                if (!String.IsNullOrWhiteSpace(tenantId)) command.Parameters.AddWithValue("@tenantId", tenantId);
                addParameters(command);
            }, token).ConfigureAwait(false);
            return total;
        }

        private static async Task<int> DeleteProviderChildrenForParentsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string? tenantId, string parentPredicate, Action<NpgsqlCommand> addParameters, CancellationToken token)
        {
            string tenantPredicate = String.IsNullOrWhiteSpace(tenantId) ? string.Empty : "tenant_id = @tenantId AND ";
            string parentTenantPredicate = String.IsNullOrWhiteSpace(tenantId) ? string.Empty : "parent.tenant_id = @tenantId AND ";
            string subquery = "SELECT parent.id FROM client_provider_telemetry_samples parent WHERE " + parentTenantPredicate + parentPredicate;
            int total = 0;
            total += await ExecuteDeleteAsync(connection, transaction, "DELETE FROM client_provider_telemetry_models WHERE " + tenantPredicate + "sample_id IN (" + subquery + ");", delegate (NpgsqlCommand command)
            {
                if (!String.IsNullOrWhiteSpace(tenantId)) command.Parameters.AddWithValue("@tenantId", tenantId);
                addParameters(command);
            }, token).ConfigureAwait(false);
            total += await ExecuteDeleteAsync(connection, transaction, "DELETE FROM client_provider_telemetry_metrics WHERE " + tenantPredicate + "sample_id IN (" + subquery + ");", delegate (NpgsqlCommand command)
            {
                if (!String.IsNullOrWhiteSpace(tenantId)) command.Parameters.AddWithValue("@tenantId", tenantId);
                addParameters(command);
            }, token).ConfigureAwait(false);
            return total;
        }

        private static async Task<int> DeleteProviderParentsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string? tenantId, string predicate, Action<NpgsqlCommand> addParameters, CancellationToken token)
        {
            string tenantPredicate = String.IsNullOrWhiteSpace(tenantId) ? string.Empty : "tenant_id = @tenantId AND ";
            return await ExecuteDeleteAsync(connection, transaction, "DELETE FROM client_provider_telemetry_samples WHERE " + tenantPredicate + predicate + ";", delegate (NpgsqlCommand command)
            {
                if (!String.IsNullOrWhiteSpace(tenantId)) command.Parameters.AddWithValue("@tenantId", tenantId);
                addParameters(command);
            }, token).ConfigureAwait(false);
        }

        private static async Task<int> DeleteAttemptsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string predicate, Action<NpgsqlCommand> addParameters, CancellationToken token)
        {
            return await ExecuteDeleteAsync(connection, transaction, "DELETE FROM client_telemetry_collection_attempts WHERE " + predicate + ";", addParameters, token).ConfigureAwait(false);
        }

        private static async Task<int> ExecuteDeleteAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, Action<NpgsqlCommand> addParameters, CancellationToken token)
        {
            using (NpgsqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = sql;
                addParameters(command);
                return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static string SchemaSql()
        {
            return """
CREATE TABLE IF NOT EXISTS client_telemetry_samples
(
    id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    client_id TEXT NOT NULL,
    client_name TEXT NULL,
    machine_name TEXT NULL,
    collection_request_id TEXT NULL,
    collected_utc TEXT NOT NULL,
    received_utc TEXT NOT NULL,
    persisted_utc TEXT NOT NULL,
    collection_elapsed_ms REAL NULL,
    sample_age_ms_at_receive REAL NULL,
    os_description TEXT NULL,
    architecture TEXT NULL,
    processor_count INTEGER NULL,
    cpu_utilization_percent REAL NULL,
    memory_utilization_percent REAL NULL,
    disk_utilization_percent REAL NULL,
    gpu_average_utilization_percent REAL NULL,
    snapshot_json TEXT NULL,
    active INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS client_telemetry_gpu_samples
(
    id TEXT PRIMARY KEY,
    sample_id TEXT NOT NULL,
    tenant_id TEXT NOT NULL,
    client_id TEXT NOT NULL,
    collected_utc TEXT NOT NULL,
    gpu_uuid TEXT NULL,
    name TEXT NULL,
    utilization_percent REAL NULL
);

CREATE TABLE IF NOT EXISTS client_provider_telemetry_samples
(
    id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    client_id TEXT NOT NULL,
    client_name TEXT NULL,
    provider_name TEXT NULL,
    api_type TEXT NULL,
    provider_kind TEXT NULL,
    endpoint_hash TEXT NULL,
    endpoint_display TEXT NULL,
    collected_utc TEXT NOT NULL,
    received_utc TEXT NOT NULL,
    persisted_utc TEXT NOT NULL,
    status TEXT NULL,
    health_status TEXT NULL,
    available_model_count INTEGER NULL,
    loaded_model_count INTEGER NULL,
    active_requests REAL NULL,
    queued_requests REAL NULL,
    latency_p95_ms REAL NULL,
    snapshot_json TEXT NULL,
    raw_metrics_truncated INTEGER NOT NULL DEFAULT 0,
    active INTEGER NOT NULL DEFAULT 1
);

CREATE TABLE IF NOT EXISTS client_provider_telemetry_models
(
    id TEXT PRIMARY KEY,
    sample_id TEXT NOT NULL,
    tenant_id TEXT NOT NULL,
    client_id TEXT NOT NULL,
    provider_name TEXT NULL,
    model_name TEXT NULL
);

CREATE TABLE IF NOT EXISTS client_provider_telemetry_metrics
(
    id TEXT PRIMARY KEY,
    sample_id TEXT NOT NULL,
    tenant_id TEXT NOT NULL,
    client_id TEXT NOT NULL,
    provider_name TEXT NULL,
    model_name TEXT NULL,
    metric_name TEXT NULL,
    metric_kind TEXT NULL,
    metric_value REAL NULL,
    unit TEXT NULL,
    source TEXT NULL,
    labels_json TEXT NULL
);

CREATE TABLE IF NOT EXISTS client_telemetry_collection_attempts
(
    id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    client_id TEXT NOT NULL,
    client_name TEXT NULL,
    started_utc TEXT NOT NULL,
    completed_utc TEXT NULL,
    elapsed_ms REAL NULL,
    host_requested INTEGER NOT NULL DEFAULT 0,
    provider_requested INTEGER NOT NULL DEFAULT 0,
    host_succeeded INTEGER NOT NULL DEFAULT 0,
    provider_succeeded INTEGER NOT NULL DEFAULT 0,
    status TEXT NOT NULL,
    error_code TEXT NULL,
    error_message TEXT NULL,
    host_sample_id TEXT NULL,
    provider_sample_count INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_client_telemetry_samples_tenant_collected ON client_telemetry_samples(tenant_id, collected_utc);
CREATE INDEX IF NOT EXISTS idx_client_telemetry_samples_tenant_client_collected ON client_telemetry_samples(tenant_id, client_id, collected_utc);
CREATE INDEX IF NOT EXISTS idx_client_telemetry_gpu_samples_sample ON client_telemetry_gpu_samples(sample_id);
CREATE INDEX IF NOT EXISTS idx_client_provider_telemetry_samples_tenant_collected ON client_provider_telemetry_samples(tenant_id, collected_utc);
CREATE INDEX IF NOT EXISTS idx_client_provider_telemetry_samples_tenant_client_collected ON client_provider_telemetry_samples(tenant_id, client_id, collected_utc);
CREATE INDEX IF NOT EXISTS idx_client_provider_telemetry_models_sample ON client_provider_telemetry_models(sample_id);
CREATE INDEX IF NOT EXISTS idx_client_provider_telemetry_metrics_sample ON client_provider_telemetry_metrics(sample_id);
CREATE INDEX IF NOT EXISTS idx_client_telemetry_collection_attempts_tenant_started ON client_telemetry_collection_attempts(tenant_id, started_utc);
""";
        }

        private static void AddNullable(NpgsqlCommand command, string name, object? value)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        private static async Task EnsureColumnAsync(NpgsqlConnection connection, string tableName, string columnName, string columnDefinition, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            bool exists = false;
            using (NpgsqlCommand info = connection.CreateCommand())
            {
                info.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_schema = current_schema() AND table_name = @tableName;";
                info.Parameters.AddWithValue("@tableName", tableName);
                using (NpgsqlDataReader reader = await info.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        if (String.Equals(GetString(reader, "column_name"), columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            exists = true;
                            break;
                        }
                    }
                }
            }

            if (exists) return;

            using (NpgsqlCommand alter = connection.CreateCommand())
            {
                alter.CommandText = "ALTER TABLE " + tableName + " ADD COLUMN " + columnName + " " + columnDefinition + ";";
                await alter.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static string GetString(NpgsqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? string.Empty : Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        private static string? GetNullableString(NpgsqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? null : Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static int GetInt(NpgsqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? 0 : Convert.ToInt32(value, CultureInfo.InvariantCulture);
        }

        private static double? GetNullableDouble(NpgsqlDataReader reader, string name)
        {
            object value = reader[name];
            return value == DBNull.Value ? null : Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        private static DateTime ReadUtc(NpgsqlDataReader reader, string name)
        {
            return DateTime.Parse(GetString(reader, name), CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        private static DateTime? ReadNullableUtc(NpgsqlDataReader reader, string name)
        {
            string? value = GetNullableString(reader, name);
            return String.IsNullOrWhiteSpace(value) ? null : DateTime.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }
    }
}



namespace Xeno.DataLoader
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Xeno.Core.Database.Sqlite;
    using Xeno.Core.Enums;
    using Xeno.Core.Models;
    using Xeno.Core.Serialization;

    internal sealed class SqliteSyntheticDataStore
    {
        private const int TelemetryCommitBatchSize = 5000;
        private readonly DataLoaderOptions _Options;

        public SqliteSyntheticDataStore(DataLoaderOptions options)
        {
            _Options = options ?? throw new ArgumentNullException(nameof(options));
        }

        public async Task<SyntheticDataLoadResult> LoadAsync(List<SyntheticClientProfile> clients, List<RequestHistoryEntry> requestHistory, SyntheticTelemetryBatch telemetry, CancellationToken token)
        {
            if (clients == null) throw new ArgumentNullException(nameof(clients));
            if (requestHistory == null) throw new ArgumentNullException(nameof(requestHistory));
            if (telemetry == null) throw new ArgumentNullException(nameof(telemetry));
            token.ThrowIfCancellationRequested();

            if (_Options.DatabaseType != DatabaseTypeEnum.Sqlite)
            {
                throw new InvalidOperationException("Xeno.DataLoader currently supports full synthetic telemetry loading only for SQLite databases.");
            }

            string filename = ResolveFilename();
            string? backupPath = CreateBackup(filename);
            string connectionString = BuildConnectionString(filename);
            await using (SqliteConnection connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                await EnsureSchemaAsync(connection, token).ConfigureAwait(false);
                await using (SqliteTransaction transaction = connection.BeginTransaction())
                {
                    PlatformState state = await ReadPlatformStateAsync(connection, transaction, token).ConfigureAwait(false);
                    ApplyPlatformState(state, clients);
                    await WritePlatformStateAsync(connection, transaction, state, token).ConfigureAwait(false);

                    if (_Options.ReplaceSynthetic)
                    {
                        await DeleteSyntheticTelemetryAsync(connection, transaction, token).ConfigureAwait(false);
                    }

                    transaction.Commit();
                }

                await ApplyRequestHistoryAsync(connectionString, requestHistory, token).ConfigureAwait(false);
                await InsertTelemetryInBatchesAsync(connection, telemetry, token).ConfigureAwait(false);
            }

            return new SyntheticDataLoadResult
            {
                FromUtc = _Options.FromUtc,
                ToUtc = _Options.ToUtc,
                BackupPath = backupPath,
                ClientsCreated = clients.Count,
                RequestHistoryEntriesCreated = requestHistory.Count,
                HostTelemetrySamplesCreated = telemetry.HostSamples.Count,
                ProviderTelemetrySamplesCreated = telemetry.ProviderSamples.Count,
                CollectionAttemptsCreated = telemetry.Attempts.Count
            };
        }

        private static async Task EnsureSchemaAsync(SqliteConnection connection, CancellationToken token)
        {
            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = """
CREATE TABLE IF NOT EXISTS schema_migrations
(
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    applied_utc TEXT NOT NULL
);

CREATE TABLE IF NOT EXISTS xeno_platform_state
(
    id TEXT PRIMARY KEY,
    data TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL,
    active INTEGER NOT NULL DEFAULT 1
);

INSERT OR IGNORE INTO schema_migrations (id, name, applied_utc)
VALUES ('0001', 'initial_platform_state', strftime('%Y-%m-%dT%H:%M:%fZ', 'now'));
""";
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            await SqliteTelemetryMethods.InitializeAsync(connection, token).ConfigureAwait(false);
            await SqliteRequestHistoryMethods.InitializeAsync(connection, token).ConfigureAwait(false);
        }

        private static async Task<PlatformState> ReadPlatformStateAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken token)
        {
            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = "SELECT data FROM xeno_platform_state WHERE id = $id AND active = 1;";
                command.Parameters.AddWithValue("$id", "platform");
                object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                string? json = value?.ToString();
                if (String.IsNullOrWhiteSpace(json)) return new PlatformState();
                PlatformState? state = JsonUtil.Deserialize<PlatformState>(json);
                return state ?? new PlatformState();
            }
        }

        private static async Task WritePlatformStateAsync(SqliteConnection connection, SqliteTransaction transaction, PlatformState state, CancellationToken token)
        {
            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
INSERT INTO xeno_platform_state (id, data, created_utc, updated_utc, active)
VALUES ($id, $data, $createdUtc, $updatedUtc, 1)
ON CONFLICT(id) DO UPDATE SET
    data = excluded.data,
    updated_utc = excluded.updated_utc,
    active = 1;
""";
                string now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);
                command.Parameters.AddWithValue("$id", "platform");
                command.Parameters.AddWithValue("$data", JsonUtil.Serialize(state));
                command.Parameters.AddWithValue("$createdUtc", now);
                command.Parameters.AddWithValue("$updatedUtc", now);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private void ApplyPlatformState(PlatformState state, List<SyntheticClientProfile> clients)
        {
            if (_Options.ReplaceSynthetic)
            {
                state.Clients.RemoveAll(item => item.Id.StartsWith(SyntheticConstants.ClientIdPrefix, StringComparison.OrdinalIgnoreCase));
            }

            foreach (SyntheticClientProfile client in clients)
            {
                state.Clients.Add(CreateClientRecord(client));
            }
        }

        private async Task ApplyRequestHistoryAsync(string connectionString, List<RequestHistoryEntry> requestHistory, CancellationToken token)
        {
            if (_Options.ReplaceSynthetic)
            {
                await using (SqliteConnection connection = new SqliteConnection(connectionString))
                {
                    await connection.OpenAsync(token).ConfigureAwait(false);
                    await using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "DELETE FROM request_history WHERE id LIKE $idPrefix OR selected_client_id LIKE $clientPrefix OR entry_json LIKE $metadata;";
                        command.Parameters.AddWithValue("$idPrefix", SyntheticConstants.RequestIdPrefix + "%");
                        command.Parameters.AddWithValue("$clientPrefix", SyntheticConstants.ClientIdPrefix + "%");
                        command.Parameters.AddWithValue("$metadata", "%" + SyntheticConstants.SyntheticMetadataValue + "%");
                        await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }
                }
            }

            SqliteRequestHistoryMethods methods = new SqliteRequestHistoryMethods(connectionString);
            foreach (RequestHistoryEntry entry in requestHistory.OrderBy(item => item.CreatedUtc))
            {
                await methods.InsertAsync(entry, token).ConfigureAwait(false);
            }
        }

        private XenoClientRecord CreateClientRecord(SyntheticClientProfile profile)
        {
            XenoClientRecord record = new XenoClientRecord
            {
                Id = profile.Id,
                TenantId = _Options.TenantId,
                Name = profile.Name,
                Connected = false,
                Active = true,
                ServiceState = ClientServiceStateEnum.Normal,
                MaxParallelRequests = profile.MaxParallelRequests,
                Weight = profile.Weight,
                InFlightRequests = 0,
                LastHeartbeatUtc = _Options.ToUtc.AddMinutes(-15)
            };

            foreach (SyntheticModelProfile model in profile.Models)
            {
                record.Models.Add(new XenoClientModel
                {
                    Name = model.Name,
                    Provider = ResolveApiProvider(profile),
                    Source = profile.Source,
                    SupportsCompletions = model.SupportsCompletions,
                    SupportsEmbeddings = model.SupportsEmbeddings,
                    LastAdvertisedUtc = _Options.ToUtc.AddMinutes(-20)
                });
            }

            return record;
        }

        private static ApiProviderTypeEnum ResolveApiProvider(SyntheticClientProfile profile)
        {
            if (String.Equals(profile.ProviderKind, "Ollama", StringComparison.OrdinalIgnoreCase)) return ApiProviderTypeEnum.Ollama;
            if (String.Equals(profile.ProviderKind, "Vllm", StringComparison.OrdinalIgnoreCase)) return ApiProviderTypeEnum.vLLM;
            return ApiProviderTypeEnum.OpenAICompatible;
        }

        private async Task DeleteSyntheticTelemetryAsync(SqliteConnection connection, SqliteTransaction transaction, CancellationToken token)
        {
            string[] tables =
            {
                "client_telemetry_gpu_samples",
                "client_provider_telemetry_metrics",
                "client_provider_telemetry_models",
                "client_telemetry_samples",
                "client_provider_telemetry_samples",
                "client_telemetry_collection_attempts"
            };

            foreach (string table in tables)
            {
                await using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = "DELETE FROM " + table + " WHERE client_id LIKE $clientPrefix;";
                    command.Parameters.AddWithValue("$clientPrefix", SyntheticConstants.ClientIdPrefix + "%");
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        private async Task InsertTelemetryInBatchesAsync(SqliteConnection connection, SyntheticTelemetryBatch telemetry, CancellationToken token)
        {
            SqliteTransaction transaction = connection.BeginTransaction();
            int pending = 0;
            try
            {
                foreach (SyntheticHostTelemetrySample sample in telemetry.HostSamples)
                {
                    token.ThrowIfCancellationRequested();
                    await InsertHostSampleAsync(connection, transaction, sample, token).ConfigureAwait(false);
                    await InsertGpuSamplesAsync(connection, transaction, sample, token).ConfigureAwait(false);
                    pending++;
                    if (pending >= TelemetryCommitBatchSize)
                    {
                        transaction.Commit();
                        transaction.Dispose();
                        transaction = connection.BeginTransaction();
                        pending = 0;
                    }
                }

                foreach (SyntheticProviderTelemetrySample sample in telemetry.ProviderSamples)
                {
                    token.ThrowIfCancellationRequested();
                    await InsertProviderSampleAsync(connection, transaction, sample, token).ConfigureAwait(false);
                    await InsertProviderModelsAndMetricsAsync(connection, transaction, sample, token).ConfigureAwait(false);
                    pending++;
                    if (pending >= TelemetryCommitBatchSize)
                    {
                        transaction.Commit();
                        transaction.Dispose();
                        transaction = connection.BeginTransaction();
                        pending = 0;
                    }
                }

                foreach (SyntheticCollectionAttempt attempt in telemetry.Attempts)
                {
                    token.ThrowIfCancellationRequested();
                    await InsertAttemptAsync(connection, transaction, attempt, token).ConfigureAwait(false);
                    pending++;
                    if (pending >= TelemetryCommitBatchSize)
                    {
                        transaction.Commit();
                        transaction.Dispose();
                        transaction = connection.BeginTransaction();
                        pending = 0;
                    }
                }

                transaction.Commit();
            }
            finally
            {
                transaction.Dispose();
            }
        }

        private async Task InsertTelemetryAsync(SqliteConnection connection, SqliteTransaction transaction, SyntheticTelemetryBatch telemetry, CancellationToken token)
        {
            foreach (SyntheticHostTelemetrySample sample in telemetry.HostSamples)
            {
                token.ThrowIfCancellationRequested();
                await InsertHostSampleAsync(connection, transaction, sample, token).ConfigureAwait(false);
                await InsertGpuSamplesAsync(connection, transaction, sample, token).ConfigureAwait(false);
            }

            foreach (SyntheticProviderTelemetrySample sample in telemetry.ProviderSamples)
            {
                token.ThrowIfCancellationRequested();
                await InsertProviderSampleAsync(connection, transaction, sample, token).ConfigureAwait(false);
                await InsertProviderModelsAndMetricsAsync(connection, transaction, sample, token).ConfigureAwait(false);
            }

            foreach (SyntheticCollectionAttempt attempt in telemetry.Attempts)
            {
                token.ThrowIfCancellationRequested();
                await InsertAttemptAsync(connection, transaction, attempt, token).ConfigureAwait(false);
            }
        }

        private async Task InsertHostSampleAsync(SqliteConnection connection, SqliteTransaction transaction, SyntheticHostTelemetrySample sample, CancellationToken token)
        {
            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
INSERT OR REPLACE INTO client_telemetry_samples
(id, tenant_id, client_id, client_name, machine_name, collection_request_id, collected_utc, received_utc, persisted_utc, collection_elapsed_ms, sample_age_ms_at_receive, os_description, architecture, processor_count, cpu_utilization_percent, memory_utilization_percent, disk_utilization_percent, gpu_average_utilization_percent, snapshot_json, active)
VALUES ($id, $tenantId, $clientId, $clientName, $machineName, $collectionRequestId, $collectedUtc, $receivedUtc, $persistedUtc, $collectionElapsedMs, $sampleAgeMsAtReceive, $osDescription, $architecture, $processorCount, $cpu, $memory, $disk, $gpu, $snapshotJson, 1);
""";
                AddCommonHostParameters(command, sample);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private async Task InsertGpuSamplesAsync(SqliteConnection connection, SqliteTransaction transaction, SyntheticHostTelemetrySample sample, CancellationToken token)
        {
            for (int index = 0; index < sample.Client.GpuCount; index++)
            {
                await using (SqliteCommand command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    command.CommandText = """
INSERT OR REPLACE INTO client_telemetry_gpu_samples
(id, sample_id, tenant_id, client_id, collected_utc, gpu_uuid, name, utilization_percent)
VALUES ($id, $sampleId, $tenantId, $clientId, $collectedUtc, $gpuUuid, $name, $utilization);
""";
                    command.Parameters.AddWithValue("$id", SyntheticIdFactory.Create(SyntheticConstants.GpuTelemetryIdPrefix, sample.Id + index.ToString(CultureInfo.InvariantCulture), 32));
                    command.Parameters.AddWithValue("$sampleId", sample.Id);
                    command.Parameters.AddWithValue("$tenantId", _Options.TenantId);
                    command.Parameters.AddWithValue("$clientId", sample.Client.Id);
                    command.Parameters.AddWithValue("$collectedUtc", FormatUtc(sample.CollectedUtc));
                    command.Parameters.AddWithValue("$gpuUuid", "GPU-" + SyntheticIdFactory.CreateHashFragment(sample.Client.Id + index.ToString(CultureInfo.InvariantCulture), 16).ToUpperInvariant());
                    command.Parameters.AddWithValue("$name", sample.Client.GpuName);
                    command.Parameters.AddWithValue("$utilization", RandomSource.Clamp(sample.GpuPercent + index - sample.Client.GpuCount / 2.0, 0, 100));
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        private async Task InsertProviderSampleAsync(SqliteConnection connection, SqliteTransaction transaction, SyntheticProviderTelemetrySample sample, CancellationToken token)
        {
            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
INSERT OR REPLACE INTO client_provider_telemetry_samples
(id, tenant_id, client_id, client_name, provider_name, api_type, provider_kind, endpoint_hash, endpoint_display, collected_utc, received_utc, persisted_utc, status, health_status, active_requests, queued_requests, latency_p95_ms, snapshot_json, raw_metrics_truncated, active, available_model_count, loaded_model_count)
VALUES ($id, $tenantId, $clientId, $clientName, $providerName, $apiType, $providerKind, $endpointHash, $endpointDisplay, $collectedUtc, $receivedUtc, $persistedUtc, $status, $healthStatus, $activeRequests, $queuedRequests, $latencyP95Ms, $snapshotJson, 0, 1, $availableModelCount, $loadedModelCount);
""";
                command.Parameters.AddWithValue("$id", sample.Id);
                command.Parameters.AddWithValue("$tenantId", _Options.TenantId);
                command.Parameters.AddWithValue("$clientId", sample.Client.Id);
                command.Parameters.AddWithValue("$clientName", sample.Client.Name);
                command.Parameters.AddWithValue("$providerName", sample.Client.ProviderName);
                command.Parameters.AddWithValue("$apiType", sample.Client.ApiType);
                command.Parameters.AddWithValue("$providerKind", sample.Client.ProviderKind);
                command.Parameters.AddWithValue("$endpointHash", sample.Client.EndpointHash);
                command.Parameters.AddWithValue("$endpointDisplay", sample.Client.EndpointDisplay);
                command.Parameters.AddWithValue("$collectedUtc", FormatUtc(sample.CollectedUtc));
                command.Parameters.AddWithValue("$receivedUtc", FormatUtc(sample.ReceivedUtc));
                command.Parameters.AddWithValue("$persistedUtc", FormatUtc(sample.PersistedUtc));
                command.Parameters.AddWithValue("$status", sample.Status);
                command.Parameters.AddWithValue("$healthStatus", sample.HealthStatus);
                command.Parameters.AddWithValue("$activeRequests", sample.ActiveRequests);
                command.Parameters.AddWithValue("$queuedRequests", sample.QueuedRequests);
                command.Parameters.AddWithValue("$latencyP95Ms", sample.LatencyP95Ms);
                command.Parameters.AddWithValue("$snapshotJson", sample.SnapshotJson);
                command.Parameters.AddWithValue("$availableModelCount", sample.AvailableModelCount);
                command.Parameters.AddWithValue("$loadedModelCount", sample.LoadedModelCount);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private async Task InsertProviderModelsAndMetricsAsync(SqliteConnection connection, SqliteTransaction transaction, SyntheticProviderTelemetrySample sample, CancellationToken token)
        {
            foreach (SyntheticModelProfile model in sample.Client.Models)
            {
                await using (SqliteCommand modelCommand = connection.CreateCommand())
                {
                    modelCommand.Transaction = transaction;
                    modelCommand.CommandText = """
INSERT OR REPLACE INTO client_provider_telemetry_models
(id, sample_id, tenant_id, client_id, provider_name, model_name)
VALUES ($id, $sampleId, $tenantId, $clientId, $providerName, $modelName);
""";
                    modelCommand.Parameters.AddWithValue("$id", SyntheticIdFactory.Create(SyntheticConstants.ProviderModelIdPrefix, sample.Id + model.Name, 32));
                    modelCommand.Parameters.AddWithValue("$sampleId", sample.Id);
                    modelCommand.Parameters.AddWithValue("$tenantId", _Options.TenantId);
                    modelCommand.Parameters.AddWithValue("$clientId", sample.Client.Id);
                    modelCommand.Parameters.AddWithValue("$providerName", sample.Client.ProviderName);
                    modelCommand.Parameters.AddWithValue("$modelName", model.Name);
                    await modelCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                await using (SqliteCommand metricCommand = connection.CreateCommand())
                {
                    metricCommand.Transaction = transaction;
                    metricCommand.CommandText = """
INSERT OR REPLACE INTO client_provider_telemetry_metrics
(id, sample_id, tenant_id, client_id, provider_name, model_name, metric_name, metric_kind, metric_value, unit, source, labels_json)
VALUES ($id, $sampleId, $tenantId, $clientId, $providerName, $modelName, $metricName, $metricKind, $metricValue, $unit, $source, $labelsJson);
""";
                    double tokensPerSecond = Math.Max(1, 2400.0 / Math.Max(100, sample.LatencyP95Ms) * model.Weight);
                    metricCommand.Parameters.AddWithValue("$id", SyntheticIdFactory.Create(SyntheticConstants.ProviderMetricIdPrefix, sample.Id + model.Name + "tokens", 32));
                    metricCommand.Parameters.AddWithValue("$sampleId", sample.Id);
                    metricCommand.Parameters.AddWithValue("$tenantId", _Options.TenantId);
                    metricCommand.Parameters.AddWithValue("$clientId", sample.Client.Id);
                    metricCommand.Parameters.AddWithValue("$providerName", sample.Client.ProviderName);
                    metricCommand.Parameters.AddWithValue("$modelName", model.Name);
                    metricCommand.Parameters.AddWithValue("$metricName", "tokens_per_second");
                    metricCommand.Parameters.AddWithValue("$metricKind", "Gauge");
                    metricCommand.Parameters.AddWithValue("$metricValue", Math.Round(tokensPerSecond, 2));
                    metricCommand.Parameters.AddWithValue("$unit", "tokens/s");
                    metricCommand.Parameters.AddWithValue("$source", "Provider");
                    metricCommand.Parameters.AddWithValue("$labelsJson", JsonUtil.Serialize(new { zone = sample.Client.Zone, synthetic = SyntheticConstants.SyntheticMetadataValue }));
                    await metricCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        private async Task InsertAttemptAsync(SqliteConnection connection, SqliteTransaction transaction, SyntheticCollectionAttempt attempt, CancellationToken token)
        {
            await using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
INSERT OR REPLACE INTO client_telemetry_collection_attempts
(id, tenant_id, client_id, client_name, started_utc, completed_utc, elapsed_ms, host_requested, provider_requested, host_succeeded, provider_succeeded, status, error_code, error_message, host_sample_id, provider_sample_count)
VALUES ($id, $tenantId, $clientId, $clientName, $startedUtc, $completedUtc, $elapsedMs, 1, 1, 1, 1, 'Completed', NULL, NULL, $hostSampleId, $providerSampleCount);
""";
                command.Parameters.AddWithValue("$id", attempt.Id);
                command.Parameters.AddWithValue("$tenantId", _Options.TenantId);
                command.Parameters.AddWithValue("$clientId", attempt.Client.Id);
                command.Parameters.AddWithValue("$clientName", attempt.Client.Name);
                command.Parameters.AddWithValue("$startedUtc", FormatUtc(attempt.StartedUtc));
                command.Parameters.AddWithValue("$completedUtc", FormatUtc(attempt.CompletedUtc));
                command.Parameters.AddWithValue("$elapsedMs", attempt.ElapsedMs);
                command.Parameters.AddWithValue("$hostSampleId", attempt.HostSampleId);
                command.Parameters.AddWithValue("$providerSampleCount", attempt.ProviderSampleCount);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private void AddCommonHostParameters(SqliteCommand command, SyntheticHostTelemetrySample sample)
        {
            command.Parameters.AddWithValue("$id", sample.Id);
            command.Parameters.AddWithValue("$tenantId", _Options.TenantId);
            command.Parameters.AddWithValue("$clientId", sample.Client.Id);
            command.Parameters.AddWithValue("$clientName", sample.Client.Name);
            command.Parameters.AddWithValue("$machineName", sample.Client.MachineName);
            command.Parameters.AddWithValue("$collectionRequestId", sample.CollectionRequestId);
            command.Parameters.AddWithValue("$collectedUtc", FormatUtc(sample.CollectedUtc));
            command.Parameters.AddWithValue("$receivedUtc", FormatUtc(sample.ReceivedUtc));
            command.Parameters.AddWithValue("$persistedUtc", FormatUtc(sample.PersistedUtc));
            command.Parameters.AddWithValue("$collectionElapsedMs", sample.CollectionElapsedMs);
            command.Parameters.AddWithValue("$sampleAgeMsAtReceive", sample.SampleAgeMsAtReceive);
            command.Parameters.AddWithValue("$osDescription", "Ubuntu 24.04.2 LTS");
            command.Parameters.AddWithValue("$architecture", "X64");
            command.Parameters.AddWithValue("$processorCount", sample.Client.ProcessorCount);
            command.Parameters.AddWithValue("$cpu", sample.CpuPercent);
            command.Parameters.AddWithValue("$memory", sample.MemoryPercent);
            command.Parameters.AddWithValue("$disk", sample.DiskPercent);
            command.Parameters.AddWithValue("$gpu", sample.GpuPercent);
            command.Parameters.AddWithValue("$snapshotJson", sample.SnapshotJson);
        }

        private string ResolveFilename()
        {
            string? filename = _Options.Filename;
            if (String.IsNullOrWhiteSpace(filename)) throw new InvalidOperationException("SQLite loading requires --filename.");
            return Path.GetFullPath(filename);
        }

        private string? CreateBackup(string filename)
        {
            if (!_Options.Backup || !File.Exists(filename)) return null;
            string backup = filename + ".bak-dataloader-" + DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
            File.Copy(filename, backup, true);
            return backup;
        }

        private static string BuildConnectionString(string filename)
        {
            SqliteConnectionStringBuilder builder = new SqliteConnectionStringBuilder
            {
                DataSource = filename
            };
            return builder.ConnectionString;
        }

        private static string FormatUtc(DateTime value)
        {
            return value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }
    }
}

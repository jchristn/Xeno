namespace Xeno.Core.Database.Sqlite
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;
    using Xeno.Core.Settings;

    /// <summary>
    /// SQLite routing policy data access methods. Policy scalar fields and the owned
    /// <see cref="RoutingPolicy.Adaptive"/> composite map to columns on <c>routing_policies</c>, while the
    /// ordered <see cref="RoutingPolicy.TelemetryFilters"/> and <see cref="RoutingPolicy.TelemetryRanking"/>
    /// collections map to the child tables <c>routing_policy_filters</c> and
    /// <c>routing_policy_ranking_rules</c>. No domain object is stored as a JSON document.
    /// </summary>
    public class SqliteRoutingPolicyMethods : IRoutingPolicyMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public SqliteRoutingPolicyMethods(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));
            _ConnectionString = connectionString;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create the routing policy tables and indexes if they do not exist.
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
        public async Task<RoutingPolicy> CreateAsync(RoutingPolicy policy, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(policy);
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(policy.Id)) policy.Id = IdGenerator.NewId("rpol_");

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (DbTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertPolicyRowAsync(connection, transaction, policy, token).ConfigureAwait(false);
                    await ReplaceFiltersAsync(connection, transaction, policy, token).ConfigureAwait(false);
                    await ReplaceRankingAsync(connection, transaction, policy, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return policy;
        }

        /// <inheritdoc />
        public async Task<RoutingPolicy?> ReadAsync(string tenantId, string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return null;
            if (String.IsNullOrWhiteSpace(id)) return null;
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                RoutingPolicy? policy = null;
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + PolicyColumns + " FROM routing_policies WHERE tenant_id = @tenant AND id = @id LIMIT 1;";
                    command.Parameters.AddWithValue("@tenant", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) policy = HydratePolicy(reader);
                    }
                }

                if (policy == null) return null;
                policy.TelemetryFilters = await ReadFiltersAsync(connection, policy.Id, token).ConfigureAwait(false);
                policy.TelemetryRanking = await ReadRankingAsync(connection, policy.Id, token).ConfigureAwait(false);
                return policy;
            }
        }

        /// <inheritdoc />
        public async Task<RoutingPolicy?> ReadDefaultAsync(string tenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(tenantId)) return null;
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                RoutingPolicy? policy = null;
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + PolicyColumns + " FROM routing_policies WHERE tenant_id = @tenant AND is_default = 1 LIMIT 1;";
                    command.Parameters.AddWithValue("@tenant", tenantId);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) policy = HydratePolicy(reader);
                    }
                }

                if (policy == null) return null;
                policy.TelemetryFilters = await ReadFiltersAsync(connection, policy.Id, token).ConfigureAwait(false);
                policy.TelemetryRanking = await ReadRankingAsync(connection, policy.Id, token).ConfigureAwait(false);
                return policy;
            }
        }

        /// <inheritdoc />
        public async Task<List<RoutingPolicy>> EnumerateAsync(string tenantId, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            List<RoutingPolicy> policies = new List<RoutingPolicy>();
            if (String.IsNullOrWhiteSpace(tenantId)) return policies;
            Dictionary<string, RoutingPolicy> byId = new Dictionary<string, RoutingPolicy>(StringComparer.Ordinal);

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + PolicyColumns + " FROM routing_policies WHERE tenant_id = @tenant ORDER BY created_utc ASC, id ASC;";
                    command.Parameters.AddWithValue("@tenant", tenantId);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false))
                        {
                            RoutingPolicy policy = HydratePolicy(reader);
                            policies.Add(policy);
                            byId[policy.Id] = policy;
                        }
                    }
                }

                if (policies.Count > 0)
                {
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT " + FilterColumns + " FROM routing_policy_filters WHERE policy_id IN (SELECT id FROM routing_policies WHERE tenant_id = @tenant) ORDER BY policy_id ASC, ordinal ASC;";
                        command.Parameters.AddWithValue("@tenant", tenantId);
                        using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync(token).ConfigureAwait(false))
                            {
                                string policyId = reader.GetString(0);
                                if (byId.TryGetValue(policyId, out RoutingPolicy? owner)) owner.TelemetryFilters.Add(HydrateFilter(reader));
                            }
                        }
                    }

                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.CommandText = "SELECT " + RankingColumns + " FROM routing_policy_ranking_rules WHERE policy_id IN (SELECT id FROM routing_policies WHERE tenant_id = @tenant) ORDER BY policy_id ASC, ordinal ASC;";
                        command.Parameters.AddWithValue("@tenant", tenantId);
                        using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                        {
                            while (await reader.ReadAsync(token).ConfigureAwait(false))
                            {
                                string policyId = reader.GetString(0);
                                if (byId.TryGetValue(policyId, out RoutingPolicy? owner)) owner.TelemetryRanking.Add(HydrateRanking(reader));
                            }
                        }
                    }
                }
            }

            return policies;
        }

        /// <inheritdoc />
        public async Task<RoutingPolicy> UpdateAsync(RoutingPolicy policy, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(policy);
            if (String.IsNullOrWhiteSpace(policy.Id)) throw new ArgumentException("Routing policy ID is required.", nameof(policy));
            token.ThrowIfCancellationRequested();
            policy.LastUpdateUtc = DateTime.UtcNow;

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (DbTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertPolicyRowAsync(connection, transaction, policy, token).ConfigureAwait(false);
                    await ReplaceFiltersAsync(connection, transaction, policy, token).ConfigureAwait(false);
                    await ReplaceRankingAsync(connection, transaction, policy, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return policy;
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
                    using (SqliteCommand filterCommand = connection.CreateCommand())
                    {
                        filterCommand.Transaction = (SqliteTransaction)transaction;
                        filterCommand.CommandText = "DELETE FROM routing_policy_filters WHERE policy_id = @id;";
                        filterCommand.Parameters.AddWithValue("@id", id);
                        await filterCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    using (SqliteCommand rankingCommand = connection.CreateCommand())
                    {
                        rankingCommand.Transaction = (SqliteTransaction)transaction;
                        rankingCommand.CommandText = "DELETE FROM routing_policy_ranking_rules WHERE policy_id = @id;";
                        rankingCommand.Parameters.AddWithValue("@id", id);
                        await rankingCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int rows;
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.Transaction = (SqliteTransaction)transaction;
                        command.CommandText = "DELETE FROM routing_policies WHERE tenant_id = @tenant AND id = @id;";
                        command.Parameters.AddWithValue("@tenant", tenantId);
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
                    using (SqliteCommand filterCommand = connection.CreateCommand())
                    {
                        filterCommand.Transaction = (SqliteTransaction)transaction;
                        filterCommand.CommandText = "DELETE FROM routing_policy_filters WHERE policy_id IN (SELECT id FROM routing_policies WHERE tenant_id = @tenant_id);";
                        filterCommand.Parameters.AddWithValue("@tenant_id", tenantId);
                        await filterCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    using (SqliteCommand rankingCommand = connection.CreateCommand())
                    {
                        rankingCommand.Transaction = (SqliteTransaction)transaction;
                        rankingCommand.CommandText = "DELETE FROM routing_policy_ranking_rules WHERE policy_id IN (SELECT id FROM routing_policies WHERE tenant_id = @tenant_id);";
                        rankingCommand.Parameters.AddWithValue("@tenant_id", tenantId);
                        await rankingCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int rows;
                    using (SqliteCommand command = connection.CreateCommand())
                    {
                        command.Transaction = (SqliteTransaction)transaction;
                        command.CommandText = "DELETE FROM routing_policies WHERE tenant_id = @tenant_id;";
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
                    command.CommandText = "SELECT 1 FROM routing_policies WHERE tenant_id = @tenant AND id = @id LIMIT 1;";
                    command.Parameters.AddWithValue("@tenant", tenantId);
                    command.Parameters.AddWithValue("@id", id);
                    object? value = await command.ExecuteScalarAsync(token).ConfigureAwait(false);
                    return value != null;
                }
            }
        }

        #endregion

        #region Private-Methods

        private const string PolicyColumns =
            "id, tenant_id, name, model_name, client_name, request_timeout_ms, load_balancing_mode, max_telemetry_age_ms, telemetry_fallback_mode, is_default, active, created_utc, last_update_utc, adaptive_sample_count, adaptive_capacity_weight, adaptive_in_flight_weight, adaptive_client_weight, adaptive_heartbeat_weight";

        private const string FilterColumns =
            "policy_id, metric, operator_type, value_type, value";

        private const string RankingColumns =
            "policy_id, metric, direction, weight";

        private static async Task UpsertPolicyRowAsync(SqliteConnection connection, DbTransaction transaction, RoutingPolicy policy, CancellationToken token)
        {
            AdaptiveLoadBalancingSettings adaptive = policy.Adaptive ?? new AdaptiveLoadBalancingSettings();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = (SqliteTransaction)transaction;
                command.CommandText = """
INSERT INTO routing_policies (id, tenant_id, name, model_name, client_name, request_timeout_ms, load_balancing_mode, max_telemetry_age_ms, telemetry_fallback_mode, is_default, active, created_utc, last_update_utc, adaptive_sample_count, adaptive_capacity_weight, adaptive_in_flight_weight, adaptive_client_weight, adaptive_heartbeat_weight)
VALUES (@id, @tenant_id, @name, @model_name, @client_name, @request_timeout_ms, @load_balancing_mode, @max_telemetry_age_ms, @telemetry_fallback_mode, @is_default, @active, @created_utc, @last_update_utc, @adaptive_sample_count, @adaptive_capacity_weight, @adaptive_in_flight_weight, @adaptive_client_weight, @adaptive_heartbeat_weight)
ON CONFLICT(id) DO UPDATE SET
    tenant_id = excluded.tenant_id,
    name = excluded.name,
    model_name = excluded.model_name,
    client_name = excluded.client_name,
    request_timeout_ms = excluded.request_timeout_ms,
    load_balancing_mode = excluded.load_balancing_mode,
    max_telemetry_age_ms = excluded.max_telemetry_age_ms,
    telemetry_fallback_mode = excluded.telemetry_fallback_mode,
    is_default = excluded.is_default,
    active = excluded.active,
    last_update_utc = excluded.last_update_utc,
    adaptive_sample_count = excluded.adaptive_sample_count,
    adaptive_capacity_weight = excluded.adaptive_capacity_weight,
    adaptive_in_flight_weight = excluded.adaptive_in_flight_weight,
    adaptive_client_weight = excluded.adaptive_client_weight,
    adaptive_heartbeat_weight = excluded.adaptive_heartbeat_weight;
""";
                command.Parameters.AddWithValue("@id", policy.Id);
                command.Parameters.AddWithValue("@tenant_id", policy.TenantId ?? String.Empty);
                command.Parameters.AddWithValue("@name", policy.Name ?? String.Empty);
                command.Parameters.AddWithValue("@model_name", policy.ModelName ?? String.Empty);
                command.Parameters.AddWithValue("@client_name", policy.ClientName ?? String.Empty);
                command.Parameters.AddWithValue("@request_timeout_ms", policy.RequestTimeoutMs);
                command.Parameters.AddWithValue("@load_balancing_mode", policy.LoadBalancingMode.ToString());
                command.Parameters.AddWithValue("@max_telemetry_age_ms", policy.MaxTelemetryAgeMs);
                command.Parameters.AddWithValue("@telemetry_fallback_mode", policy.TelemetryFallbackMode.ToString());
                command.Parameters.AddWithValue("@is_default", policy.IsDefault ? 1 : 0);
                command.Parameters.AddWithValue("@active", policy.Active ? 1 : 0);
                command.Parameters.AddWithValue("@created_utc", FormatUtc(policy.CreatedUtc));
                command.Parameters.AddWithValue("@last_update_utc", FormatUtc(policy.LastUpdateUtc));
                command.Parameters.AddWithValue("@adaptive_sample_count", adaptive.SampleCount);
                command.Parameters.AddWithValue("@adaptive_capacity_weight", adaptive.CapacityWeight);
                command.Parameters.AddWithValue("@adaptive_in_flight_weight", adaptive.InFlightWeight);
                command.Parameters.AddWithValue("@adaptive_client_weight", adaptive.ClientWeight);
                command.Parameters.AddWithValue("@adaptive_heartbeat_weight", adaptive.HeartbeatWeight);
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static async Task ReplaceFiltersAsync(SqliteConnection connection, DbTransaction transaction, RoutingPolicy policy, CancellationToken token)
        {
            using (SqliteCommand delete = connection.CreateCommand())
            {
                delete.Transaction = (SqliteTransaction)transaction;
                delete.CommandText = "DELETE FROM routing_policy_filters WHERE policy_id = @id;";
                delete.Parameters.AddWithValue("@id", policy.Id);
                await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            if (policy.TelemetryFilters == null || policy.TelemetryFilters.Count == 0) return;

            int ordinal = 0;
            foreach (RoutingPolicyCondition filter in policy.TelemetryFilters)
            {
                using (SqliteCommand insert = connection.CreateCommand())
                {
                    insert.Transaction = (SqliteTransaction)transaction;
                    insert.CommandText = """
INSERT INTO routing_policy_filters (policy_id, ordinal, metric, operator_type, value_type, value)
VALUES (@policy_id, @ordinal, @metric, @operator_type, @value_type, @value);
""";
                    insert.Parameters.AddWithValue("@policy_id", policy.Id);
                    insert.Parameters.AddWithValue("@ordinal", ordinal);
                    insert.Parameters.AddWithValue("@metric", filter.Metric ?? String.Empty);
                    insert.Parameters.AddWithValue("@operator_type", filter.Operator.ToString());
                    insert.Parameters.AddWithValue("@value_type", filter.ValueType.ToString());
                    insert.Parameters.AddWithValue("@value", filter.Value ?? String.Empty);
                    await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    ordinal++;
                }
            }
        }

        private static async Task ReplaceRankingAsync(SqliteConnection connection, DbTransaction transaction, RoutingPolicy policy, CancellationToken token)
        {
            using (SqliteCommand delete = connection.CreateCommand())
            {
                delete.Transaction = (SqliteTransaction)transaction;
                delete.CommandText = "DELETE FROM routing_policy_ranking_rules WHERE policy_id = @id;";
                delete.Parameters.AddWithValue("@id", policy.Id);
                await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            if (policy.TelemetryRanking == null || policy.TelemetryRanking.Count == 0) return;

            int ordinal = 0;
            foreach (RoutingPolicyRankingRule rule in policy.TelemetryRanking)
            {
                using (SqliteCommand insert = connection.CreateCommand())
                {
                    insert.Transaction = (SqliteTransaction)transaction;
                    insert.CommandText = """
INSERT INTO routing_policy_ranking_rules (policy_id, ordinal, metric, direction, weight)
VALUES (@policy_id, @ordinal, @metric, @direction, @weight);
""";
                    insert.Parameters.AddWithValue("@policy_id", policy.Id);
                    insert.Parameters.AddWithValue("@ordinal", ordinal);
                    insert.Parameters.AddWithValue("@metric", rule.Metric ?? String.Empty);
                    insert.Parameters.AddWithValue("@direction", rule.Direction.ToString());
                    insert.Parameters.AddWithValue("@weight", rule.Weight);
                    await insert.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    ordinal++;
                }
            }
        }

        private static async Task<List<RoutingPolicyCondition>> ReadFiltersAsync(SqliteConnection connection, string policyId, CancellationToken token)
        {
            List<RoutingPolicyCondition> filters = new List<RoutingPolicyCondition>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT " + FilterColumns + " FROM routing_policy_filters WHERE policy_id = @id ORDER BY ordinal ASC;";
                command.Parameters.AddWithValue("@id", policyId);
                using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false)) filters.Add(HydrateFilter(reader));
                }
            }

            return filters;
        }

        private static async Task<List<RoutingPolicyRankingRule>> ReadRankingAsync(SqliteConnection connection, string policyId, CancellationToken token)
        {
            List<RoutingPolicyRankingRule> rules = new List<RoutingPolicyRankingRule>();
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT " + RankingColumns + " FROM routing_policy_ranking_rules WHERE policy_id = @id ORDER BY ordinal ASC;";
                command.Parameters.AddWithValue("@id", policyId);
                using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false)) rules.Add(HydrateRanking(reader));
                }
            }

            return rules;
        }

        private static RoutingPolicy HydratePolicy(DbDataReader reader)
        {
            return new RoutingPolicy
            {
                Id = reader.GetString(0),
                TenantId = reader.IsDBNull(1) ? String.Empty : reader.GetString(1),
                Name = reader.IsDBNull(2) ? String.Empty : reader.GetString(2),
                ModelName = reader.IsDBNull(3) ? String.Empty : reader.GetString(3),
                ClientName = reader.IsDBNull(4) ? String.Empty : reader.GetString(4),
                RequestTimeoutMs = reader.GetInt32(5),
                LoadBalancingMode = ParseEnum(reader, 6, LoadBalancingModeEnum.LeastRecentlyUsed),
                MaxTelemetryAgeMs = reader.GetInt32(7),
                TelemetryFallbackMode = ParseFallbackMode(reader, 8),
                IsDefault = reader.GetInt64(9) != 0,
                Active = reader.GetInt64(10) != 0,
                CreatedUtc = ParseUtc(reader.GetString(11)),
                LastUpdateUtc = ParseUtc(reader.GetString(12)),
                Adaptive = new AdaptiveLoadBalancingSettings
                {
                    SampleCount = reader.GetInt32(13),
                    CapacityWeight = reader.GetDouble(14),
                    InFlightWeight = reader.GetDouble(15),
                    ClientWeight = reader.GetDouble(16),
                    HeartbeatWeight = reader.GetDouble(17)
                },
                TelemetryFilters = new List<RoutingPolicyCondition>(),
                TelemetryRanking = new List<RoutingPolicyRankingRule>()
            };
        }

        private static RoutingPolicyCondition HydrateFilter(DbDataReader reader)
        {
            return new RoutingPolicyCondition
            {
                Metric = reader.IsDBNull(1) ? String.Empty : reader.GetString(1),
                Operator = ParseOperator(reader, 2),
                ValueType = ParseValueType(reader, 3),
                Value = reader.IsDBNull(4) ? String.Empty : reader.GetString(4)
            };
        }

        private static RoutingPolicyRankingRule HydrateRanking(DbDataReader reader)
        {
            return new RoutingPolicyRankingRule
            {
                Metric = reader.IsDBNull(1) ? String.Empty : reader.GetString(1),
                Direction = ParseDirection(reader, 2),
                Weight = reader.GetDouble(3)
            };
        }

        private static LoadBalancingModeEnum ParseEnum(DbDataReader reader, int ordinal, LoadBalancingModeEnum fallback)
        {
            if (reader.IsDBNull(ordinal)) return fallback;
            return Enum.TryParse(reader.GetString(ordinal), out LoadBalancingModeEnum parsed) ? parsed : fallback;
        }

        private static RoutingPolicyFallbackModeEnum ParseFallbackMode(DbDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return RoutingPolicyFallbackModeEnum.UseLoadBalancingMode;
            return Enum.TryParse(reader.GetString(ordinal), out RoutingPolicyFallbackModeEnum parsed) ? parsed : RoutingPolicyFallbackModeEnum.UseLoadBalancingMode;
        }

        private static RoutingPolicyOperatorEnum ParseOperator(DbDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return RoutingPolicyOperatorEnum.Equal;
            return Enum.TryParse(reader.GetString(ordinal), out RoutingPolicyOperatorEnum parsed) ? parsed : RoutingPolicyOperatorEnum.Equal;
        }

        private static RoutingPolicyMetricValueTypeEnum ParseValueType(DbDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return RoutingPolicyMetricValueTypeEnum.Number;
            return Enum.TryParse(reader.GetString(ordinal), out RoutingPolicyMetricValueTypeEnum parsed) ? parsed : RoutingPolicyMetricValueTypeEnum.Number;
        }

        private static RoutingPolicyRankingDirectionEnum ParseDirection(DbDataReader reader, int ordinal)
        {
            if (reader.IsDBNull(ordinal)) return RoutingPolicyRankingDirectionEnum.Ascending;
            return Enum.TryParse(reader.GetString(ordinal), out RoutingPolicyRankingDirectionEnum parsed) ? parsed : RoutingPolicyRankingDirectionEnum.Ascending;
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
CREATE TABLE IF NOT EXISTS routing_policies
(
    id TEXT PRIMARY KEY,
    tenant_id TEXT NOT NULL,
    name TEXT NOT NULL,
    model_name TEXT NOT NULL,
    client_name TEXT NOT NULL,
    request_timeout_ms INTEGER NOT NULL DEFAULT 0,
    load_balancing_mode TEXT NOT NULL,
    max_telemetry_age_ms INTEGER NOT NULL DEFAULT 0,
    telemetry_fallback_mode TEXT NOT NULL,
    is_default INTEGER NOT NULL DEFAULT 0,
    active INTEGER NOT NULL DEFAULT 1,
    created_utc TEXT NOT NULL,
    last_update_utc TEXT NOT NULL,
    adaptive_sample_count INTEGER NOT NULL DEFAULT 0,
    adaptive_capacity_weight REAL NOT NULL DEFAULT 0,
    adaptive_in_flight_weight REAL NOT NULL DEFAULT 0,
    adaptive_client_weight REAL NOT NULL DEFAULT 0,
    adaptive_heartbeat_weight REAL NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_routing_policies_tenant ON routing_policies(tenant_id);
CREATE INDEX IF NOT EXISTS idx_routing_policies_tenant_default ON routing_policies(tenant_id, is_default);

CREATE TABLE IF NOT EXISTS routing_policy_filters
(
    policy_id TEXT NOT NULL,
    ordinal INTEGER NOT NULL,
    metric TEXT NOT NULL,
    operator_type TEXT NOT NULL,
    value_type TEXT NOT NULL,
    value TEXT NOT NULL,
    PRIMARY KEY (policy_id, ordinal),
    FOREIGN KEY (policy_id) REFERENCES routing_policies(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS routing_policy_ranking_rules
(
    policy_id TEXT NOT NULL,
    ordinal INTEGER NOT NULL,
    metric TEXT NOT NULL,
    direction TEXT NOT NULL,
    weight REAL NOT NULL DEFAULT 0,
    PRIMARY KEY (policy_id, ordinal),
    FOREIGN KEY (policy_id) REFERENCES routing_policies(id) ON DELETE CASCADE
);
""";
        }

        #endregion
    }
}

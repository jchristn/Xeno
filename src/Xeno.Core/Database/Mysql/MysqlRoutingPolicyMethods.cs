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
    using Xeno.Core.Enums;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;
    using Xeno.Core.Settings;

    /// <summary>
    /// MySQL routing policy data access methods. Policy scalar fields and the owned
    /// <see cref="RoutingPolicy.Adaptive"/> composite map to columns on <c>routing_policies</c>, while the
    /// ordered <see cref="RoutingPolicy.TelemetryFilters"/> and <see cref="RoutingPolicy.TelemetryRanking"/>
    /// collections map to the child tables <c>routing_policy_filters</c> and
    /// <c>routing_policy_ranking_rules</c>.
    /// </summary>
    public class MysqlRoutingPolicyMethods : IRoutingPolicyMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public MysqlRoutingPolicyMethods(string connectionString)
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
        public async Task<RoutingPolicy> CreateAsync(RoutingPolicy policy, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(policy);
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(policy.Id)) policy.Id = IdGenerator.NewId("rpol_");

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlTransaction transaction = (MySqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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
            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                RoutingPolicy? policy = null;
                using (MySqlCommand command = connection.CreateCommand())
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
            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                RoutingPolicy? policy = null;
                using (MySqlCommand command = connection.CreateCommand())
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

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (MySqlCommand command = connection.CreateCommand())
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
                    using (MySqlCommand command = connection.CreateCommand())
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

                    using (MySqlCommand command = connection.CreateCommand())
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

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlTransaction transaction = (MySqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlTransaction transaction = (MySqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    using (MySqlCommand filterCommand = connection.CreateCommand())
                    {
                        filterCommand.Transaction = transaction;
                        filterCommand.CommandText = "DELETE FROM routing_policy_filters WHERE policy_id = @id;";
                        filterCommand.Parameters.AddWithValue("@id", id);
                        await filterCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    using (MySqlCommand rankingCommand = connection.CreateCommand())
                    {
                        rankingCommand.Transaction = transaction;
                        rankingCommand.CommandText = "DELETE FROM routing_policy_ranking_rules WHERE policy_id = @id;";
                        rankingCommand.Parameters.AddWithValue("@id", id);
                        await rankingCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int rows;
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
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

            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlTransaction transaction = (MySqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    using (MySqlCommand filterCommand = connection.CreateCommand())
                    {
                        filterCommand.Transaction = transaction;
                        filterCommand.CommandText = "DELETE FROM routing_policy_filters WHERE policy_id IN (SELECT id FROM (SELECT id FROM routing_policies WHERE tenant_id = @tenant_id) AS matched);";
                        filterCommand.Parameters.AddWithValue("@tenant_id", tenantId);
                        await filterCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    using (MySqlCommand rankingCommand = connection.CreateCommand())
                    {
                        rankingCommand.Transaction = transaction;
                        rankingCommand.CommandText = "DELETE FROM routing_policy_ranking_rules WHERE policy_id IN (SELECT id FROM (SELECT id FROM routing_policies WHERE tenant_id = @tenant_id) AS matched);";
                        rankingCommand.Parameters.AddWithValue("@tenant_id", tenantId);
                        await rankingCommand.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    int rows;
                    using (MySqlCommand command = connection.CreateCommand())
                    {
                        command.Transaction = transaction;
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
            using (MySqlConnection connection = new MySqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (MySqlCommand command = connection.CreateCommand())
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

        private static async Task UpsertPolicyRowAsync(MySqlConnection connection, MySqlTransaction transaction, RoutingPolicy policy, CancellationToken token)
        {
            AdaptiveLoadBalancingSettings adaptive = policy.Adaptive ?? new AdaptiveLoadBalancingSettings();
            using (MySqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
INSERT INTO routing_policies (id, tenant_id, name, model_name, client_name, request_timeout_ms, load_balancing_mode, max_telemetry_age_ms, telemetry_fallback_mode, is_default, active, created_utc, last_update_utc, adaptive_sample_count, adaptive_capacity_weight, adaptive_in_flight_weight, adaptive_client_weight, adaptive_heartbeat_weight)
VALUES (@id, @tenant_id, @name, @model_name, @client_name, @request_timeout_ms, @load_balancing_mode, @max_telemetry_age_ms, @telemetry_fallback_mode, @is_default, @active, @created_utc, @last_update_utc, @adaptive_sample_count, @adaptive_capacity_weight, @adaptive_in_flight_weight, @adaptive_client_weight, @adaptive_heartbeat_weight)
ON DUPLICATE KEY UPDATE
    tenant_id = VALUES(tenant_id),
    name = VALUES(name),
    model_name = VALUES(model_name),
    client_name = VALUES(client_name),
    request_timeout_ms = VALUES(request_timeout_ms),
    load_balancing_mode = VALUES(load_balancing_mode),
    max_telemetry_age_ms = VALUES(max_telemetry_age_ms),
    telemetry_fallback_mode = VALUES(telemetry_fallback_mode),
    is_default = VALUES(is_default),
    active = VALUES(active),
    last_update_utc = VALUES(last_update_utc),
    adaptive_sample_count = VALUES(adaptive_sample_count),
    adaptive_capacity_weight = VALUES(adaptive_capacity_weight),
    adaptive_in_flight_weight = VALUES(adaptive_in_flight_weight),
    adaptive_client_weight = VALUES(adaptive_client_weight),
    adaptive_heartbeat_weight = VALUES(adaptive_heartbeat_weight);
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

        private static async Task ReplaceFiltersAsync(MySqlConnection connection, MySqlTransaction transaction, RoutingPolicy policy, CancellationToken token)
        {
            using (MySqlCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM routing_policy_filters WHERE policy_id = @id;";
                delete.Parameters.AddWithValue("@id", policy.Id);
                await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            if (policy.TelemetryFilters == null || policy.TelemetryFilters.Count == 0) return;

            int ordinal = 0;
            foreach (RoutingPolicyCondition filter in policy.TelemetryFilters)
            {
                using (MySqlCommand insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
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

        private static async Task ReplaceRankingAsync(MySqlConnection connection, MySqlTransaction transaction, RoutingPolicy policy, CancellationToken token)
        {
            using (MySqlCommand delete = connection.CreateCommand())
            {
                delete.Transaction = transaction;
                delete.CommandText = "DELETE FROM routing_policy_ranking_rules WHERE policy_id = @id;";
                delete.Parameters.AddWithValue("@id", policy.Id);
                await delete.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }

            if (policy.TelemetryRanking == null || policy.TelemetryRanking.Count == 0) return;

            int ordinal = 0;
            foreach (RoutingPolicyRankingRule rule in policy.TelemetryRanking)
            {
                using (MySqlCommand insert = connection.CreateCommand())
                {
                    insert.Transaction = transaction;
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

        private static async Task<List<RoutingPolicyCondition>> ReadFiltersAsync(MySqlConnection connection, string policyId, CancellationToken token)
        {
            List<RoutingPolicyCondition> filters = new List<RoutingPolicyCondition>();
            using (MySqlCommand command = connection.CreateCommand())
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

        private static async Task<List<RoutingPolicyRankingRule>> ReadRankingAsync(MySqlConnection connection, string policyId, CancellationToken token)
        {
            List<RoutingPolicyRankingRule> rules = new List<RoutingPolicyRankingRule>();
            using (MySqlCommand command = connection.CreateCommand())
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
                IsDefault = reader.GetInt32(9) != 0,
                Active = reader.GetInt32(10) != 0,
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
    id varchar(64) NOT NULL PRIMARY KEY,
    tenant_id varchar(64) NOT NULL,
    name varchar(256) NOT NULL,
    model_name varchar(256) NOT NULL,
    client_name varchar(256) NOT NULL,
    request_timeout_ms INT NOT NULL DEFAULT 0,
    load_balancing_mode varchar(64) NOT NULL,
    max_telemetry_age_ms INT NOT NULL DEFAULT 0,
    telemetry_fallback_mode varchar(64) NOT NULL,
    is_default INT NOT NULL DEFAULT 0,
    active INT NOT NULL DEFAULT 1,
    created_utc varchar(64) NOT NULL,
    last_update_utc varchar(64) NOT NULL,
    adaptive_sample_count INT NOT NULL DEFAULT 0,
    adaptive_capacity_weight DOUBLE NOT NULL DEFAULT 0,
    adaptive_in_flight_weight DOUBLE NOT NULL DEFAULT 0,
    adaptive_client_weight DOUBLE NOT NULL DEFAULT 0,
    adaptive_heartbeat_weight DOUBLE NOT NULL DEFAULT 0,
    INDEX idx_routing_policies_tenant (tenant_id),
    INDEX idx_routing_policies_tenant_default (tenant_id, is_default)
);

CREATE TABLE IF NOT EXISTS routing_policy_filters
(
    policy_id varchar(64) NOT NULL,
    ordinal INT NOT NULL,
    metric varchar(256) NOT NULL,
    operator_type varchar(64) NOT NULL,
    value_type varchar(64) NOT NULL,
    value TEXT NOT NULL,
    PRIMARY KEY (policy_id, ordinal),
    CONSTRAINT fk_routing_policy_filters_policy FOREIGN KEY (policy_id) REFERENCES routing_policies(id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS routing_policy_ranking_rules
(
    policy_id varchar(64) NOT NULL,
    ordinal INT NOT NULL,
    metric varchar(256) NOT NULL,
    direction varchar(64) NOT NULL,
    weight DOUBLE NOT NULL DEFAULT 0,
    PRIMARY KEY (policy_id, ordinal),
    CONSTRAINT fk_routing_policy_ranking_rules_policy FOREIGN KEY (policy_id) REFERENCES routing_policies(id) ON DELETE CASCADE
);
""";
        }

        #endregion
    }
}

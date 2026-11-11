namespace Xeno.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Analytics;
    using Xeno.Core.Database.Interfaces;

    /// <summary>
    /// Connected-client samples for every database driver. Thread safe: every call opens its own connection.
    /// </summary>
    public sealed class DbConnectedClientSampleMethods : IConnectedClientSampleMethods
    {
        #region Private-Members

        private const string Table = "connected_client_samples";
        private const string Index = "idx_connected_client_samples_tenant";
        private const string UtcFormat = "yyyy-MM-ddTHH:mm:ss.fffffffZ";
        private readonly ChatSqlDialectEnum _Dialect;
        private readonly Func<DbConnection> _Connect;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the store.
        /// </summary>
        /// <param name="dialect">SQL dialect.</param>
        /// <param name="connect">Creates an unopened connection.</param>
        /// <exception cref="ArgumentNullException">connect is null.</exception>
        public DbConnectedClientSampleMethods(ChatSqlDialectEnum dialect, Func<DbConnection> connect)
        {
            _Dialect = dialect;
            _Connect = connect ?? throw new ArgumentNullException(nameof(connect));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create the table when missing.
        /// </summary>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Task.</returns>
        public async Task InitializeAsync(CancellationToken token)
        {
            string id = _Dialect == ChatSqlDialectEnum.Mysql ? "VARCHAR(64)" : _Dialect == ChatSqlDialectEnum.SqlServer ? "NVARCHAR(64)" : "TEXT";
            string integer = _Dialect == ChatSqlDialectEnum.Mysql || _Dialect == ChatSqlDialectEnum.SqlServer ? "INT" : "INTEGER";
            string columns = "id " + id + " NOT NULL PRIMARY KEY, tenant_id " + id + " NOT NULL, sampled_utc " + id + " NOT NULL, connected " + integer + " NOT NULL, total " + integer + " NOT NULL";
            string create;
            string index;
            switch (_Dialect)
            {
                case ChatSqlDialectEnum.SqlServer:
                    create = "IF OBJECT_ID(N'dbo." + Table + "', N'U') IS NULL CREATE TABLE dbo." + Table + " (" + columns + ")";
                    index = "IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'" + Index + "' AND object_id = OBJECT_ID(N'dbo." + Table + "')) CREATE INDEX " + Index + " ON dbo." + Table + " (tenant_id, sampled_utc)";
                    break;
                case ChatSqlDialectEnum.Mysql:
                    create = "CREATE TABLE IF NOT EXISTS " + Table + " (" + columns + ")";
                    index = "CREATE INDEX " + Index + " ON " + Table + " (tenant_id, sampled_utc)";
                    break;
                default:
                    create = "CREATE TABLE IF NOT EXISTS " + Table + " (" + columns + ")";
                    index = "CREATE INDEX IF NOT EXISTS " + Index + " ON " + Table + " (tenant_id, sampled_utc)";
                    break;
            }

            using (DbConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                await ExecuteAsync(connection, create, token).ConfigureAwait(false);
                try
                {
                    await ExecuteAsync(connection, index, token).ConfigureAwait(false);
                }
                catch (DbException ex) when (_Dialect == ChatSqlDialectEnum.Mysql && ex.Message.Contains("Duplicate key name", StringComparison.OrdinalIgnoreCase))
                {
                }
            }
        }

        /// <inheritdoc />
        public async Task InsertAsync(ConnectedClientSample sample, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(sample);
            using (DbConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                await ExecuteAsync(
                    connection,
                    "INSERT INTO " + Table + " (id, tenant_id, sampled_utc, connected, total) VALUES (@id, @tenant, @sampled, @connected, @total)",
                    token,
                    P("@id", Guid.NewGuid().ToString("N")),
                    P("@tenant", sample.TenantId),
                    P("@sampled", Format(sample.SampledUtc)),
                    P("@connected", sample.Connected),
                    P("@total", sample.Total)).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task<List<ConnectedClientSample>> ListAsync(string tenantId, DateTime fromUtc, DateTime toUtc, CancellationToken token)
        {
            List<ConnectedClientSample> samples = new List<ConnectedClientSample>();
            using (DbConnection connection = await OpenAsync(token).ConfigureAwait(false))
            using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT sampled_utc, connected, total FROM " + Table + " WHERE tenant_id = @tenant AND sampled_utc >= @from AND sampled_utc < @to ORDER BY sampled_utc";
                Bind(command, P("@tenant", tenantId), P("@from", Format(fromUtc)), P("@to", Format(toUtc)));
                using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        samples.Add(new ConnectedClientSample
                        {
                            TenantId = tenantId,
                            SampledUtc = DateTime.ParseExact(reader.GetString(0), UtcFormat, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal),
                            Connected = Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture),
                            Total = Convert.ToInt32(reader.GetValue(2), CultureInfo.InvariantCulture)
                        });
                    }
                }
            }

            return samples;
        }

        /// <inheritdoc />
        public async Task<int> DeleteOlderThanAsync(DateTime olderThanUtc, CancellationToken token)
        {
            using (DbConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                return await ExecuteAsync(connection, "DELETE FROM " + Table + " WHERE sampled_utc < @cutoff", token, P("@cutoff", Format(olderThanUtc))).ConfigureAwait(false);
            }
        }

        /// <inheritdoc />
        public async Task DeleteByTenantAsync(string tenantId, CancellationToken token)
        {
            using (DbConnection connection = await OpenAsync(token).ConfigureAwait(false))
            {
                await ExecuteAsync(connection, "DELETE FROM " + Table + " WHERE tenant_id = @tenant", token, P("@tenant", tenantId)).ConfigureAwait(false);
            }
        }

        #endregion

        #region Private-Methods

        private async Task<DbConnection> OpenAsync(CancellationToken token)
        {
            DbConnection connection = _Connect();
            try
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                return connection;
            }
            catch
            {
                connection.Dispose();
                throw;
            }
        }

        private static KeyValuePair<string, object> P(string name, object value)
        {
            return new KeyValuePair<string, object>(name, value);
        }

        private static void Bind(DbCommand command, params KeyValuePair<string, object>[] parameters)
        {
            foreach (KeyValuePair<string, object> pair in parameters)
            {
                DbParameter parameter = command.CreateParameter();
                parameter.ParameterName = pair.Key;
                parameter.Value = pair.Value;
                command.Parameters.Add(parameter);
            }
        }

        private static async Task<int> ExecuteAsync(DbConnection connection, string sql, CancellationToken token, params KeyValuePair<string, object>[] parameters)
        {
            using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = sql;
                Bind(command, parameters);
                return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static string Format(DateTime value)
        {
            return DateTime.SpecifyKind(value.ToUniversalTime(), DateTimeKind.Utc).ToString(UtcFormat, CultureInfo.InvariantCulture);
        }

        #endregion
    }
}

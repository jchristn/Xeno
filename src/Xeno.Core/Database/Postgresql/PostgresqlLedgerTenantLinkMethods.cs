namespace Xeno.Core.Database.Postgresql
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Globalization;
    using System.Threading;
    using System.Threading.Tasks;
    using Npgsql;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;

    /// <summary>
    /// PostgreSQL ledger tenant link data access methods. The link scalar fields map to columns on
    /// <c>ledger_tenant_links</c>.
    /// </summary>
    public class PostgresqlLedgerTenantLinkMethods : ILedgerTenantLinkMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public PostgresqlLedgerTenantLinkMethods(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));
            _ConnectionString = connectionString;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create the ledger tenant link table and indexes if they do not exist.
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
        public async Task<LedgerTenantLink> CreateAsync(LedgerTenantLink link, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(link);
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(link.Id)) link.Id = IdGenerator.NewLedgerTenantLinkId();

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlTransaction transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertLinkRowAsync(connection, transaction, link, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return link;
        }

        /// <inheritdoc />
        public async Task<LedgerTenantLink?> ReadAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) return null;
            token.ThrowIfCancellationRequested();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + LinkColumns + " FROM ledger_tenant_links WHERE id = @id LIMIT 1;";
                    command.Parameters.AddWithValue("@id", id);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) return HydrateLink(reader);
                    }
                }

                return null;
            }
        }

        /// <inheritdoc />
        public async Task<LedgerTenantLink?> ReadByTenantAsync(string xenoTenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(xenoTenantId)) return null;
            token.ThrowIfCancellationRequested();
            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + LinkColumns + " FROM ledger_tenant_links WHERE xeno_tenant_id = @xeno_tenant_id LIMIT 1;";
                    command.Parameters.AddWithValue("@xeno_tenant_id", xenoTenantId);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) return HydrateLink(reader);
                    }
                }

                return null;
            }
        }

        /// <inheritdoc />
        public async Task<List<LedgerTenantLink>> EnumerateAsync(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            List<LedgerTenantLink> links = new List<LedgerTenantLink>();

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + LinkColumns + " FROM ledger_tenant_links ORDER BY created_utc ASC, id ASC;";
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false)) links.Add(HydrateLink(reader));
                    }
                }
            }

            return links;
        }

        /// <inheritdoc />
        public async Task<LedgerTenantLink> UpdateAsync(LedgerTenantLink link, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(link);
            if (String.IsNullOrWhiteSpace(link.Id)) throw new ArgumentException("Ledger tenant link ID is required.", nameof(link));
            token.ThrowIfCancellationRequested();
            link.UpdatedUtc = DateTime.UtcNow;

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlTransaction transaction = (NpgsqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertLinkRowAsync(connection, transaction, link, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return link;
        }

        /// <inheritdoc />
        public async Task<bool> DeleteAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) return false;
            token.ThrowIfCancellationRequested();

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM ledger_tenant_links WHERE id = @id;";
                    command.Parameters.AddWithValue("@id", id);
                    int rows = await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    return rows > 0;
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteByTenantAsync(string xenoTenantId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(xenoTenantId)) return 0;
            token.ThrowIfCancellationRequested();

            using (NpgsqlConnection connection = new NpgsqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (NpgsqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM ledger_tenant_links WHERE xeno_tenant_id = @xeno_tenant_id;";
                    command.Parameters.AddWithValue("@xeno_tenant_id", xenoTenantId);
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        #endregion

        #region Private-Methods

        private const string LinkColumns =
            "id, xeno_tenant_id, net_ledger_tenant_id, created_utc, updated_utc";

        private static async Task UpsertLinkRowAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, LedgerTenantLink link, CancellationToken token)
        {
            using (NpgsqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
INSERT INTO ledger_tenant_links (id, xeno_tenant_id, net_ledger_tenant_id, created_utc, updated_utc)
VALUES (@id, @xeno_tenant_id, @net_ledger_tenant_id, @created_utc, @updated_utc)
ON CONFLICT (id) DO UPDATE SET
    xeno_tenant_id = EXCLUDED.xeno_tenant_id,
    net_ledger_tenant_id = EXCLUDED.net_ledger_tenant_id,
    updated_utc = EXCLUDED.updated_utc;
""";
                command.Parameters.AddWithValue("@id", link.Id);
                command.Parameters.AddWithValue("@xeno_tenant_id", link.XenoTenantId ?? String.Empty);
                command.Parameters.AddWithValue("@net_ledger_tenant_id", link.NetLedgerTenantId ?? String.Empty);
                command.Parameters.AddWithValue("@created_utc", FormatUtc(link.CreatedUtc));
                command.Parameters.AddWithValue("@updated_utc", FormatUtc(link.UpdatedUtc));
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static LedgerTenantLink HydrateLink(DbDataReader reader)
        {
            return new LedgerTenantLink
            {
                Id = reader.GetString(0),
                XenoTenantId = reader.IsDBNull(1) ? String.Empty : reader.GetString(1),
                NetLedgerTenantId = reader.IsDBNull(2) ? String.Empty : reader.GetString(2),
                CreatedUtc = ParseUtc(reader.GetString(3)),
                UpdatedUtc = ParseUtc(reader.GetString(4))
            };
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
CREATE TABLE IF NOT EXISTS ledger_tenant_links
(
    id TEXT PRIMARY KEY,
    xeno_tenant_id TEXT NOT NULL,
    net_ledger_tenant_id TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_ledger_tenant_links_xeno_tenant_id ON ledger_tenant_links(xeno_tenant_id);

CREATE INDEX IF NOT EXISTS idx_ledger_tenant_links_net_ledger_tenant_id ON ledger_tenant_links(net_ledger_tenant_id);
""";
        }

        #endregion
    }
}

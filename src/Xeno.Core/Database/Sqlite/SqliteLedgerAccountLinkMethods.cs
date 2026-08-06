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
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;

    /// <summary>
    /// SQLite ledger account link data access methods. The link scalar fields map to columns on the
    /// flat table <c>ledger_account_links</c>. No domain object is stored as a JSON document.
    /// </summary>
    public class SqliteLedgerAccountLinkMethods : ILedgerAccountLinkMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public SqliteLedgerAccountLinkMethods(string connectionString)
        {
            if (String.IsNullOrEmpty(connectionString)) throw new ArgumentNullException(nameof(connectionString));
            _ConnectionString = connectionString;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create the ledger account link tables and indexes if they do not exist.
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
        public async Task<LedgerAccountLink> CreateAsync(LedgerAccountLink link, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(link);
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(link.Id)) link.Id = IdGenerator.NewLedgerAccountLinkId();

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (DbTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    await UpsertLinkRowAsync(connection, transaction, link, token).ConfigureAwait(false);
                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }
            }

            return link;
        }

        /// <inheritdoc />
        public async Task<LedgerAccountLink?> ReadAsync(string id, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(id)) return null;
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + LinkColumns + " FROM ledger_account_links WHERE id = @id LIMIT 1;";
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
        public async Task<LedgerAccountLink?> ReadByUserAsync(string xenoTenantId, string xenoUserId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(xenoTenantId)) return null;
            if (String.IsNullOrWhiteSpace(xenoUserId)) return null;
            token.ThrowIfCancellationRequested();
            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + LinkColumns + " FROM ledger_account_links WHERE xeno_tenant_id = @xeno_tenant_id AND xeno_user_id = @xeno_user_id LIMIT 1;";
                    command.Parameters.AddWithValue("@xeno_tenant_id", xenoTenantId);
                    command.Parameters.AddWithValue("@xeno_user_id", xenoUserId);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        if (await reader.ReadAsync(token).ConfigureAwait(false)) return HydrateLink(reader);
                    }
                }

                return null;
            }
        }

        /// <inheritdoc />
        public async Task<List<LedgerAccountLink>> EnumerateByTenantAsync(string xenoTenantId, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();
            List<LedgerAccountLink> links = new List<LedgerAccountLink>();
            if (String.IsNullOrWhiteSpace(xenoTenantId)) return links;

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + LinkColumns + " FROM ledger_account_links WHERE xeno_tenant_id = @xeno_tenant_id ORDER BY created_utc ASC, id ASC;";
                    command.Parameters.AddWithValue("@xeno_tenant_id", xenoTenantId);
                    using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                    {
                        while (await reader.ReadAsync(token).ConfigureAwait(false)) links.Add(HydrateLink(reader));
                    }
                }
            }

            return links;
        }

        /// <inheritdoc />
        public async Task<LedgerAccountLink> UpdateAsync(LedgerAccountLink link, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(link);
            if (String.IsNullOrWhiteSpace(link.Id)) throw new ArgumentException("Ledger account link ID is required.", nameof(link));
            token.ThrowIfCancellationRequested();
            link.UpdatedUtc = DateTime.UtcNow;

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (DbTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM ledger_account_links WHERE id = @id;";
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

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM ledger_account_links WHERE xeno_tenant_id = @xeno_tenant_id;";
                    command.Parameters.AddWithValue("@xeno_tenant_id", xenoTenantId);
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc />
        public async Task<int> DeleteByUserAsync(string xenoTenantId, string xenoUserId, CancellationToken token = default)
        {
            if (String.IsNullOrWhiteSpace(xenoTenantId)) return 0;
            if (String.IsNullOrWhiteSpace(xenoUserId)) return 0;
            token.ThrowIfCancellationRequested();

            using (SqliteConnection connection = new SqliteConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM ledger_account_links WHERE xeno_tenant_id = @xeno_tenant_id AND xeno_user_id = @xeno_user_id;";
                    command.Parameters.AddWithValue("@xeno_tenant_id", xenoTenantId);
                    command.Parameters.AddWithValue("@xeno_user_id", xenoUserId);
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        #endregion

        #region Private-Methods

        private const string LinkColumns =
            "id, xeno_tenant_id, xeno_user_id, net_ledger_tenant_id, net_ledger_account_id, created_utc, updated_utc";

        private static async Task UpsertLinkRowAsync(SqliteConnection connection, DbTransaction transaction, LedgerAccountLink link, CancellationToken token)
        {
            using (SqliteCommand command = connection.CreateCommand())
            {
                command.Transaction = (SqliteTransaction)transaction;
                command.CommandText = """
INSERT INTO ledger_account_links (id, xeno_tenant_id, xeno_user_id, net_ledger_tenant_id, net_ledger_account_id, created_utc, updated_utc)
VALUES (@id, @xeno_tenant_id, @xeno_user_id, @net_ledger_tenant_id, @net_ledger_account_id, @created_utc, @updated_utc)
ON CONFLICT(id) DO UPDATE SET
    xeno_tenant_id = excluded.xeno_tenant_id,
    xeno_user_id = excluded.xeno_user_id,
    net_ledger_tenant_id = excluded.net_ledger_tenant_id,
    net_ledger_account_id = excluded.net_ledger_account_id,
    updated_utc = excluded.updated_utc;
""";
                command.Parameters.AddWithValue("@id", link.Id);
                command.Parameters.AddWithValue("@xeno_tenant_id", link.XenoTenantId ?? String.Empty);
                command.Parameters.AddWithValue("@xeno_user_id", link.XenoUserId ?? String.Empty);
                command.Parameters.AddWithValue("@net_ledger_tenant_id", link.NetLedgerTenantId ?? String.Empty);
                command.Parameters.AddWithValue("@net_ledger_account_id", link.NetLedgerAccountId ?? String.Empty);
                command.Parameters.AddWithValue("@created_utc", FormatUtc(link.CreatedUtc));
                command.Parameters.AddWithValue("@updated_utc", FormatUtc(link.UpdatedUtc));
                await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
            }
        }

        private static LedgerAccountLink HydrateLink(DbDataReader reader)
        {
            return new LedgerAccountLink
            {
                Id = reader.GetString(0),
                XenoTenantId = reader.IsDBNull(1) ? String.Empty : reader.GetString(1),
                XenoUserId = reader.IsDBNull(2) ? String.Empty : reader.GetString(2),
                NetLedgerTenantId = reader.IsDBNull(3) ? String.Empty : reader.GetString(3),
                NetLedgerAccountId = reader.IsDBNull(4) ? String.Empty : reader.GetString(4),
                CreatedUtc = ParseUtc(reader.GetString(5)),
                UpdatedUtc = ParseUtc(reader.GetString(6))
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
CREATE TABLE IF NOT EXISTS ledger_account_links
(
    id TEXT PRIMARY KEY,
    xeno_tenant_id TEXT NOT NULL,
    xeno_user_id TEXT NOT NULL,
    net_ledger_tenant_id TEXT NOT NULL,
    net_ledger_account_id TEXT NOT NULL,
    created_utc TEXT NOT NULL,
    updated_utc TEXT NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS idx_ledger_account_links_user ON ledger_account_links(xeno_tenant_id, xeno_user_id);

CREATE INDEX IF NOT EXISTS idx_ledger_account_links_tenant ON ledger_account_links(xeno_tenant_id);

CREATE INDEX IF NOT EXISTS idx_ledger_account_links_account ON ledger_account_links(net_ledger_account_id);
""";
        }

        #endregion
    }
}

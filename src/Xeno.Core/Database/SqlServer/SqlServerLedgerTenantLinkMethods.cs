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
    using Xeno.Core.Helpers;
    using Xeno.Core.Models;

    /// <summary>
    /// SQL Server ledger tenant link data access methods. The link scalar fields map to columns on
    /// <c>dbo.ledger_tenant_links</c>.
    /// </summary>
    public class SqlServerLedgerTenantLinkMethods : ILedgerTenantLinkMethods
    {
        #region Private-Members

        private readonly string _ConnectionString;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        public SqlServerLedgerTenantLinkMethods(string connectionString)
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
        public async Task<LedgerTenantLink> CreateAsync(LedgerTenantLink link, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(link);
            token.ThrowIfCancellationRequested();
            if (String.IsNullOrWhiteSpace(link.Id)) link.Id = IdGenerator.NewLedgerTenantLinkId();

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + LinkColumns + " FROM dbo.ledger_tenant_links WHERE id = @id;";
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
            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT TOP (1) " + LinkColumns + " FROM dbo.ledger_tenant_links WHERE xeno_tenant_id = @xeno_tenant_id;";
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

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);

                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "SELECT " + LinkColumns + " FROM dbo.ledger_tenant_links ORDER BY created_utc ASC, id ASC;";
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

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlTransaction transaction = (SqlTransaction)await connection.BeginTransactionAsync(token).ConfigureAwait(false))
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

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM dbo.ledger_tenant_links WHERE id = @id;";
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

            using (SqlConnection connection = new SqlConnection(_ConnectionString))
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqlCommand command = connection.CreateCommand())
                {
                    command.CommandText = "DELETE FROM dbo.ledger_tenant_links WHERE xeno_tenant_id = @xeno_tenant_id;";
                    command.Parameters.AddWithValue("@xeno_tenant_id", xenoTenantId);
                    return await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }
            }
        }

        #endregion

        #region Private-Methods

        private const string LinkColumns =
            "id, xeno_tenant_id, net_ledger_tenant_id, created_utc, updated_utc";

        private static async Task UpsertLinkRowAsync(SqlConnection connection, SqlTransaction transaction, LedgerTenantLink link, CancellationToken token)
        {
            using (SqlCommand command = connection.CreateCommand())
            {
                command.Transaction = transaction;
                command.CommandText = """
IF EXISTS (SELECT 1 FROM dbo.ledger_tenant_links WHERE id = @id)
    UPDATE dbo.ledger_tenant_links SET
        xeno_tenant_id = @xeno_tenant_id,
        net_ledger_tenant_id = @net_ledger_tenant_id,
        updated_utc = @updated_utc
    WHERE id = @id;
ELSE
    INSERT INTO dbo.ledger_tenant_links (id, xeno_tenant_id, net_ledger_tenant_id, created_utc, updated_utc)
    VALUES (@id, @xeno_tenant_id, @net_ledger_tenant_id, @created_utc, @updated_utc);
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
IF OBJECT_ID(N'dbo.ledger_tenant_links', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ledger_tenant_links
    (
        id nvarchar(64) NOT NULL PRIMARY KEY,
        xeno_tenant_id nvarchar(64) NOT NULL,
        net_ledger_tenant_id nvarchar(64) NOT NULL,
        created_utc nvarchar(64) NOT NULL,
        updated_utc nvarchar(64) NOT NULL
    );
END;

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_ledger_tenant_links_xeno_tenant_id' AND object_id = OBJECT_ID(N'dbo.ledger_tenant_links'))
    CREATE UNIQUE INDEX idx_ledger_tenant_links_xeno_tenant_id ON dbo.ledger_tenant_links(xeno_tenant_id);

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'idx_ledger_tenant_links_net_ledger_tenant_id' AND object_id = OBJECT_ID(N'dbo.ledger_tenant_links'))
    CREATE INDEX idx_ledger_tenant_links_net_ledger_tenant_id ON dbo.ledger_tenant_links(net_ledger_tenant_id);
""";
        }

        #endregion
    }
}

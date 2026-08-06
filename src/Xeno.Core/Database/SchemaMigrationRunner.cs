namespace Xeno.Core.Database
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Applies versioned schema migrations against the <c>schema_migrations</c> tracking table. The runner
    /// is provider-neutral (it operates on <see cref="DbConnection"/>/<see cref="DbCommand"/>); the SQL in
    /// each <see cref="SchemaMigration"/> and the current-timestamp expression are supplied by the calling
    /// provider driver. Application is tracked and idempotent: a migration whose version identifier already
    /// exists in <c>schema_migrations</c> is skipped, and each applied migration runs inside a transaction
    /// so a failure leaves the version unrecorded and re-runnable.
    /// </summary>
    public static class SchemaMigrationRunner
    {
        /// <summary>
        /// Apply all not-yet-recorded migrations in ascending version order.
        /// </summary>
        /// <param name="connection">Open provider connection.</param>
        /// <param name="nowSqlExpression">Provider SQL expression yielding the current UTC time for the
        /// <c>applied_utc</c> column (for example <c>now()</c>, <c>SYSUTCDATETIME()</c>, or
        /// <c>strftime('%Y-%m-%dT%H:%M:%fZ','now')</c>).</param>
        /// <param name="migrations">Forward migrations (version &gt; 1) to apply.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>The number of migrations applied.</returns>
        public static async Task<int> ApplyAsync(DbConnection connection, string nowSqlExpression, IEnumerable<SchemaMigration> migrations, CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(connection);
            if (String.IsNullOrWhiteSpace(nowSqlExpression)) throw new ArgumentException("A current-timestamp SQL expression is required.", nameof(nowSqlExpression));
            if (migrations == null) return 0;
            token.ThrowIfCancellationRequested();

            List<SchemaMigration> ordered = migrations
                .Where(item => item != null)
                .OrderBy(item => item.Version)
                .ToList();
            if (ordered.Count == 0) return 0;

            HashSet<string> applied = await ReadAppliedIdsAsync(connection, token).ConfigureAwait(false);

            int count = 0;
            foreach (SchemaMigration migration in ordered)
            {
                if (applied.Contains(migration.Id)) continue;

                using (DbTransaction transaction = await connection.BeginTransactionAsync(token).ConfigureAwait(false))
                {
                    foreach (string statement in migration.Statements)
                    {
                        if (String.IsNullOrWhiteSpace(statement)) continue;
                        using (DbCommand command = connection.CreateCommand())
                        {
                            command.Transaction = transaction;
                            command.CommandText = statement;
                            await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                        }
                    }

                    using (DbCommand record = connection.CreateCommand())
                    {
                        record.Transaction = transaction;
                        record.CommandText = "INSERT INTO schema_migrations (id, name, applied_utc) VALUES (@id, @name, " + nowSqlExpression + ");";
                        AddParameter(record, "@id", migration.Id);
                        AddParameter(record, "@name", migration.Description);
                        await record.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                    }

                    await transaction.CommitAsync(token).ConfigureAwait(false);
                }

                applied.Add(migration.Id);
                count++;
            }

            return count;
        }

        private static async Task<HashSet<string>> ReadAppliedIdsAsync(DbConnection connection, CancellationToken token)
        {
            HashSet<string> applied = new HashSet<string>(StringComparer.Ordinal);
            using (DbCommand command = connection.CreateCommand())
            {
                command.CommandText = "SELECT id FROM schema_migrations;";
                using (DbDataReader reader = await command.ExecuteReaderAsync(token).ConfigureAwait(false))
                {
                    while (await reader.ReadAsync(token).ConfigureAwait(false))
                    {
                        if (!reader.IsDBNull(0)) applied.Add(reader.GetString(0));
                    }
                }
            }

            return applied;
        }

        private static void AddParameter(DbCommand command, string name, object value)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}

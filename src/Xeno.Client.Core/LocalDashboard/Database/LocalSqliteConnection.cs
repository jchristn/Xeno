namespace Xeno.Client.LocalDashboard.Database
{
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Data.Sqlite;

    /// <summary>
    /// Opens connections to the local dashboard SQLite database with the settings that let the client worker, the
    /// desktop app, and the tray agent share the one file without "database is locked" failures. WAL journaling lets
    /// readers and the single writer work at the same time (so reading in the desktop app no longer blocks the worker's
    /// writes, which is what used to hang the window), and a busy timeout makes the remaining writer-versus-writer
    /// collisions wait briefly instead of failing immediately.
    /// </summary>
    internal static class LocalSqliteConnection
    {
        /// <summary>
        /// Busy timeout applied to every connection, in milliseconds.
        /// </summary>
        internal const int BusyTimeoutMilliseconds = 5000;

        /// <summary>
        /// Open a connection and apply the shared pragmas. <c>journal_mode=WAL</c> persists on the database file, so
        /// setting it here also enables WAL for every other connection and process; <c>busy_timeout</c> and
        /// <c>synchronous</c> are per-connection and so are applied on every open.
        /// </summary>
        /// <param name="connectionString">Connection string.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>An open connection.</returns>
        internal static async Task<SqliteConnection> OpenAsync(string connectionString, CancellationToken token)
        {
            SqliteConnection connection = new SqliteConnection(connectionString);
            try
            {
                await connection.OpenAsync(token).ConfigureAwait(false);
                using (SqliteCommand command = connection.CreateCommand())
                {
                    command.CommandText = "PRAGMA busy_timeout=" + BusyTimeoutMilliseconds + "; PRAGMA journal_mode=WAL; PRAGMA synchronous=NORMAL;";
                    await command.ExecuteNonQueryAsync(token).ConfigureAwait(false);
                }

                return connection;
            }
            catch
            {
                await connection.DisposeAsync().ConfigureAwait(false);
                throw;
            }
        }
    }
}

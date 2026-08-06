namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Data.Common;
    using System.Globalization;
    using System.IO;
    using System.Text;
    using System.Threading.Tasks;
    using Xeno.Core.Enums;
    using Xeno.Core.Settings;

    /// <summary>
    /// Central test-database configuration for the shared suites. Resolves a single backing provider
    /// (from discrete connection properties supplied on the command line or environment) and hands out
    /// an isolated database per test so the suites can run unchanged against SQLite (the default) or a
    /// real Postgres/MySQL/SQL Server instance.
    ///
    /// SQLite isolates per test with a unique file. Server providers isolate per test with a uniquely
    /// named catalog derived from the caller's <c>dbFile</c> key; the catalog is created on first use
    /// (idempotently, so a test that reuses one key across multiple server restarts shares one catalog)
    /// and dropped by <see cref="CleanupAsync"/> or <see cref="DropAllAsync"/>.
    ///
    /// All resolved configuration is read as an atomic snapshot under <see cref="_Lock"/> so the helper
    /// is safe even if a runner executes tests concurrently.
    /// </summary>
    public static class TestDatabase
    {
        private static readonly object _Lock = new object();
        private static bool _Resolved;
        private static DatabaseTypeEnum _Provider = DatabaseTypeEnum.Sqlite;
        private static string _Host = "127.0.0.1";
        private static int _Port;
        private static string _MaintenanceDatabase = String.Empty;
        private static string _Username = String.Empty;
        private static string _Password = String.Empty;
        private static readonly HashSet<string> _CreatedCatalogs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The provider all shared suites are running against for this process.
        /// </summary>
        public static DatabaseTypeEnum Provider
        {
            get { return Resolve().Provider; }
        }

        /// <summary>
        /// Whether the configured provider is a networked server (as opposed to embedded SQLite).
        /// </summary>
        public static bool IsServerProvider
        {
            get { return Resolve().Provider != DatabaseTypeEnum.Sqlite; }
        }

        /// <summary>
        /// Configure the backing database explicitly. Runners that accept discrete command-line
        /// properties call this before executing the suites; when it is not called the configuration is
        /// resolved lazily from environment variables (defaulting to SQLite).
        /// </summary>
        /// <param name="provider">Provider name (sqlite, postgresql, mysql, sqlserver).</param>
        /// <param name="host">Server host.</param>
        /// <param name="port">Server port (0 uses the provider default).</param>
        /// <param name="database">Maintenance database used to create per-test catalogs.</param>
        /// <param name="username">Server username.</param>
        /// <param name="password">Server password.</param>
        public static void Configure(string? provider, string? host, int port, string? database, string? username, string? password)
        {
            lock (_Lock)
            {
                _Provider = ParseProvider(provider);
                if (!String.IsNullOrWhiteSpace(host)) _Host = host!;
                _Port = port;
                _MaintenanceDatabase = database ?? String.Empty;
                _Username = username ?? String.Empty;
                _Password = password ?? String.Empty;
                _Resolved = true;
            }
        }

        /// <summary>
        /// Build isolated database settings for a test. <paramref name="dbFile"/> is the per-test key the
        /// suites already generate (a unique temp file path); its stem names the catalog for server
        /// providers. For server providers the catalog is created synchronously on first use.
        /// </summary>
        /// <param name="dbFile">Per-test unique key (temp file path).</param>
        /// <returns>Database settings pointing at an isolated database.</returns>
        public static DatabaseSettings Settings(string dbFile)
        {
            if (String.IsNullOrWhiteSpace(dbFile)) throw new ArgumentException("A per-test database key is required.", nameof(dbFile));
            Snapshot config = Resolve();

            if (config.Provider == DatabaseTypeEnum.Sqlite)
            {
                return new DatabaseSettings { Type = DatabaseTypeEnum.Sqlite, Filename = dbFile };
            }

            string catalog = CatalogName(dbFile);
            EnsureCatalog(config, catalog);
            return new DatabaseSettings
            {
                Type = config.Provider,
                Server = config.Host,
                Port = config.Port,
                DatabaseName = catalog,
                Username = config.Username,
                Password = config.Password
            };
        }

        /// <summary>
        /// Open a raw ADO connection to a test's isolated database, for suites that verify low-level table
        /// contents directly. The caller owns the returned connection. Uses portable <c>@name</c> parameter
        /// markers across all providers (Microsoft.Data.Sqlite accepts them too).
        /// </summary>
        /// <param name="dbFile">The per-test key previously passed to <see cref="Settings"/>.</param>
        /// <returns>An open connection to the test's isolated database.</returns>
        public static async Task<DbConnection> OpenConnectionAsync(string dbFile)
        {
            if (String.IsNullOrWhiteSpace(dbFile)) throw new ArgumentException("A per-test database key is required.", nameof(dbFile));
            DbConnection connection = CreateConnection(Resolve(), dbFile);
            await connection.OpenAsync().ConfigureAwait(false);
            return connection;
        }

        /// <summary>
        /// Release the isolated database for a test: delete the SQLite file or drop the server catalog.
        /// </summary>
        /// <param name="dbFile">The per-test key previously passed to <see cref="Settings"/>.</param>
        public static async Task CleanupAsync(string dbFile)
        {
            if (String.IsNullOrWhiteSpace(dbFile)) return;
            Snapshot config = Resolve();

            if (config.Provider == DatabaseTypeEnum.Sqlite)
            {
                await DeleteSqliteFileAsync(dbFile).ConfigureAwait(false);
                return;
            }

            string catalog = CatalogName(dbFile);
            await DropCatalogAsync(config, catalog).ConfigureAwait(false);
            lock (_Lock) _CreatedCatalogs.Remove(catalog);
        }

        /// <summary>
        /// Drop every server catalog created during this run that has not already been cleaned up. Called
        /// once by the runner after the suites complete; a no-op for SQLite.
        /// </summary>
        public static async Task DropAllAsync()
        {
            Snapshot config = Resolve();
            if (config.Provider == DatabaseTypeEnum.Sqlite) return;

            List<string> remaining;
            lock (_Lock)
            {
                remaining = new List<string>(_CreatedCatalogs);
                _CreatedCatalogs.Clear();
            }

            foreach (string catalog in remaining)
            {
                await DropCatalogAsync(config, catalog).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// An atomic snapshot of the resolved configuration, taken under the lock so concurrent callers
        /// never observe partially-initialized fields.
        /// </summary>
        private readonly struct Snapshot
        {
            public Snapshot(DatabaseTypeEnum provider, string host, int port, string maintenanceDatabase, string username, string password)
            {
                Provider = provider;
                Host = host;
                Port = port;
                MaintenanceDatabase = maintenanceDatabase;
                Username = username;
                Password = password;
            }

            public DatabaseTypeEnum Provider { get; }

            public string Host { get; }

            public int Port { get; }

            public string MaintenanceDatabase { get; }

            public string Username { get; }

            public string Password { get; }

            public int PortOrDefault
            {
                get
                {
                    if (Port > 0) return Port;
                    switch (Provider)
                    {
                        case DatabaseTypeEnum.Postgresql: return 5432;
                        case DatabaseTypeEnum.Mysql: return 3306;
                        case DatabaseTypeEnum.SqlServer: return 1433;
                        default: return 0;
                    }
                }
            }
        }

        private static Snapshot Resolve()
        {
            lock (_Lock)
            {
                if (!_Resolved)
                {
                    _Provider = ParseProvider(Environment.GetEnvironmentVariable("XENO_TEST_DB_PROVIDER"));
                    string? host = Environment.GetEnvironmentVariable("XENO_TEST_DB_HOST");
                    if (!String.IsNullOrWhiteSpace(host)) _Host = host;
                    string? port = Environment.GetEnvironmentVariable("XENO_TEST_DB_PORT");
                    _Port = Int32.TryParse(port, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsedPort) ? parsedPort : 0;
                    _MaintenanceDatabase = Environment.GetEnvironmentVariable("XENO_TEST_DB_NAME") ?? String.Empty;
                    _Username = Environment.GetEnvironmentVariable("XENO_TEST_DB_USER") ?? String.Empty;
                    _Password = Environment.GetEnvironmentVariable("XENO_TEST_DB_PASSWORD") ?? String.Empty;
                    _Resolved = true;
                }

                Snapshot resolved = new Snapshot(_Provider, _Host, _Port, _MaintenanceDatabase, _Username, _Password);
                return new Snapshot(resolved.Provider, resolved.Host, resolved.PortOrDefault, resolved.MaintenanceDatabase, resolved.Username, resolved.Password);
            }
        }

        private static DatabaseTypeEnum ParseProvider(string? provider)
        {
            if (String.IsNullOrWhiteSpace(provider)) return DatabaseTypeEnum.Sqlite;
            switch (provider.Trim().ToLowerInvariant())
            {
                case "sqlite": return DatabaseTypeEnum.Sqlite;
                case "postgres":
                case "postgresql": return DatabaseTypeEnum.Postgresql;
                case "mysql":
                case "mariadb": return DatabaseTypeEnum.Mysql;
                case "sqlserver":
                case "mssql": return DatabaseTypeEnum.SqlServer;
                default: throw new ArgumentException("Unknown test database provider '" + provider + "'. Use sqlite, postgresql, mysql, or sqlserver.");
            }
        }

        private static string CatalogName(string dbFile)
        {
            string stem = Path.GetFileNameWithoutExtension(dbFile);
            StringBuilder builder = new StringBuilder(stem.Length + 3);
            foreach (char c in stem.ToLowerInvariant())
            {
                builder.Append((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') ? c : '_');
            }

            string sanitized = builder.ToString();
            if (String.IsNullOrEmpty(sanitized) || !((sanitized[0] >= 'a' && sanitized[0] <= 'z') || sanitized[0] == '_'))
            {
                sanitized = "t_" + sanitized;
            }

            // Postgres caps identifiers at 63 bytes; keep the guid-bearing tail to preserve uniqueness.
            const int maxLength = 60;
            if (sanitized.Length > maxLength) sanitized = "t_" + sanitized.Substring(sanitized.Length - (maxLength - 2));
            return sanitized;
        }

        private static void EnsureCatalog(Snapshot config, string catalog)
        {
            lock (_Lock)
            {
                if (_CreatedCatalogs.Contains(catalog)) return;
            }

            CreateCatalog(config, catalog);
            lock (_Lock)
            {
                _CreatedCatalogs.Add(catalog);
            }
        }

        private static DbConnection CreateConnection(Snapshot config, string dbFile)
        {
            switch (config.Provider)
            {
                case DatabaseTypeEnum.Sqlite:
                    return new Microsoft.Data.Sqlite.SqliteConnection("Data Source=" + dbFile + ";Pooling=False;");

                case DatabaseTypeEnum.Postgresql:
                    return new Npgsql.NpgsqlConnection(new Npgsql.NpgsqlConnectionStringBuilder
                    {
                        Host = config.Host,
                        Port = config.Port,
                        Database = CatalogName(dbFile),
                        Username = config.Username,
                        Password = config.Password,
                        Pooling = false
                    }.ConnectionString);

                case DatabaseTypeEnum.Mysql:
                    return new MySql.Data.MySqlClient.MySqlConnection(new MySql.Data.MySqlClient.MySqlConnectionStringBuilder
                    {
                        Server = config.Host,
                        Port = (uint)config.Port,
                        Database = CatalogName(dbFile),
                        UserID = config.Username,
                        Password = config.Password,
                        Pooling = false
                    }.ConnectionString);

                case DatabaseTypeEnum.SqlServer:
                    return new Microsoft.Data.SqlClient.SqlConnection(new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
                    {
                        DataSource = config.Port > 0 ? config.Host + "," + config.Port.ToString(CultureInfo.InvariantCulture) : config.Host,
                        InitialCatalog = CatalogName(dbFile),
                        UserID = config.Username,
                        Password = config.Password,
                        TrustServerCertificate = true,
                        Pooling = false
                    }.ConnectionString);

                default:
                    throw new InvalidOperationException("Unsupported provider " + config.Provider + ".");
            }
        }

        private static void CreateCatalog(Snapshot config, string catalog)
        {
            switch (config.Provider)
            {
                case DatabaseTypeEnum.Postgresql:
                {
                    Npgsql.NpgsqlConnectionStringBuilder builder = new Npgsql.NpgsqlConnectionStringBuilder
                    {
                        Host = config.Host,
                        Port = config.Port,
                        Database = String.IsNullOrWhiteSpace(config.MaintenanceDatabase) ? "postgres" : config.MaintenanceDatabase,
                        Username = config.Username,
                        Password = config.Password,
                        Pooling = false
                    };
                    using (Npgsql.NpgsqlConnection connection = new Npgsql.NpgsqlConnection(builder.ConnectionString))
                    {
                        connection.Open();
                        // CREATE DATABASE cannot run inside a transaction; ignore the "already exists" race.
                        try
                        {
                            using (Npgsql.NpgsqlCommand command = new Npgsql.NpgsqlCommand("CREATE DATABASE \"" + catalog + "\"", connection))
                            {
                                command.ExecuteNonQuery();
                            }
                        }
                        catch (Npgsql.PostgresException ex) when (ex.SqlState == "42P04")
                        {
                        }
                    }

                    break;
                }

                case DatabaseTypeEnum.Mysql:
                {
                    MySql.Data.MySqlClient.MySqlConnectionStringBuilder builder = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder
                    {
                        Server = config.Host,
                        Port = (uint)config.Port,
                        UserID = config.Username,
                        Password = config.Password,
                        Pooling = false
                    };
                    if (!String.IsNullOrWhiteSpace(config.MaintenanceDatabase)) builder.Database = config.MaintenanceDatabase;
                    using (MySql.Data.MySqlClient.MySqlConnection connection = new MySql.Data.MySqlClient.MySqlConnection(builder.ConnectionString))
                    {
                        connection.Open();
                        using (MySql.Data.MySqlClient.MySqlCommand command = new MySql.Data.MySqlClient.MySqlCommand("CREATE DATABASE IF NOT EXISTS `" + catalog + "`", connection))
                        {
                            command.ExecuteNonQuery();
                        }
                    }

                    break;
                }

                case DatabaseTypeEnum.SqlServer:
                {
                    Microsoft.Data.SqlClient.SqlConnectionStringBuilder builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
                    {
                        DataSource = config.Port > 0 ? config.Host + "," + config.Port.ToString(CultureInfo.InvariantCulture) : config.Host,
                        InitialCatalog = String.IsNullOrWhiteSpace(config.MaintenanceDatabase) ? "master" : config.MaintenanceDatabase,
                        UserID = config.Username,
                        Password = config.Password,
                        TrustServerCertificate = true,
                        Pooling = false
                    };
                    using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString))
                    {
                        connection.Open();
                        using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand("IF DB_ID(N'" + catalog + "') IS NULL CREATE DATABASE [" + catalog + "];", connection))
                        {
                            command.ExecuteNonQuery();
                        }
                    }

                    break;
                }

                default:
                    throw new InvalidOperationException("Catalog creation is not supported for provider " + config.Provider + ".");
            }
        }

        private static async Task DropCatalogAsync(Snapshot config, string catalog)
        {
            try
            {
                switch (config.Provider)
                {
                    case DatabaseTypeEnum.Postgresql:
                    {
                        Npgsql.NpgsqlConnectionStringBuilder builder = new Npgsql.NpgsqlConnectionStringBuilder
                        {
                            Host = config.Host,
                            Port = config.Port,
                            Database = String.IsNullOrWhiteSpace(config.MaintenanceDatabase) ? "postgres" : config.MaintenanceDatabase,
                            Username = config.Username,
                            Password = config.Password,
                            Pooling = false
                        };
                        using (Npgsql.NpgsqlConnection connection = new Npgsql.NpgsqlConnection(builder.ConnectionString))
                        {
                            await connection.OpenAsync().ConfigureAwait(false);
                            using (Npgsql.NpgsqlCommand command = new Npgsql.NpgsqlCommand("DROP DATABASE IF EXISTS \"" + catalog + "\" WITH (FORCE)", connection))
                            {
                                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                            }
                        }

                        break;
                    }

                    case DatabaseTypeEnum.Mysql:
                    {
                        MySql.Data.MySqlClient.MySqlConnectionStringBuilder builder = new MySql.Data.MySqlClient.MySqlConnectionStringBuilder
                        {
                            Server = config.Host,
                            Port = (uint)config.Port,
                            UserID = config.Username,
                            Password = config.Password,
                            Pooling = false
                        };
                        using (MySql.Data.MySqlClient.MySqlConnection connection = new MySql.Data.MySqlClient.MySqlConnection(builder.ConnectionString))
                        {
                            await connection.OpenAsync().ConfigureAwait(false);
                            using (MySql.Data.MySqlClient.MySqlCommand command = new MySql.Data.MySqlClient.MySqlCommand("DROP DATABASE IF EXISTS `" + catalog + "`", connection))
                            {
                                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                            }
                        }

                        break;
                    }

                    case DatabaseTypeEnum.SqlServer:
                    {
                        Microsoft.Data.SqlClient.SqlConnectionStringBuilder builder = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder
                        {
                            DataSource = config.Port > 0 ? config.Host + "," + config.Port.ToString(CultureInfo.InvariantCulture) : config.Host,
                            InitialCatalog = String.IsNullOrWhiteSpace(config.MaintenanceDatabase) ? "master" : config.MaintenanceDatabase,
                            UserID = config.Username,
                            Password = config.Password,
                            TrustServerCertificate = true,
                            Pooling = false
                        };
                        using (Microsoft.Data.SqlClient.SqlConnection connection = new Microsoft.Data.SqlClient.SqlConnection(builder.ConnectionString))
                        {
                            await connection.OpenAsync().ConfigureAwait(false);
                            string sql = "IF DB_ID(N'" + catalog + "') IS NOT NULL BEGIN ALTER DATABASE [" + catalog + "] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [" + catalog + "]; END";
                            using (Microsoft.Data.SqlClient.SqlCommand command = new Microsoft.Data.SqlClient.SqlCommand(sql, connection))
                            {
                                await command.ExecuteNonQueryAsync().ConfigureAwait(false);
                            }
                        }

                        break;
                    }
                }
            }
            catch
            {
                // Best-effort teardown; a leaked catalog on an ephemeral docker instance is harmless.
            }
        }

        private static async Task DeleteSqliteFileAsync(string dbFile)
        {
            for (int attempt = 0; attempt < 10; attempt++)
            {
                try
                {
                    if (File.Exists(dbFile)) File.Delete(dbFile);
                    return;
                }
                catch (IOException)
                {
                    await Task.Delay(100).ConfigureAwait(false);
                }
                catch (UnauthorizedAccessException)
                {
                    await Task.Delay(100).ConfigureAwait(false);
                }
            }
        }
    }
}

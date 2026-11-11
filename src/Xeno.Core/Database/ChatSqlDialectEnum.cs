namespace Xeno.Core.Database
{
    /// <summary>
    /// SQL dialect for the shared chat store.
    /// </summary>
    public enum ChatSqlDialectEnum
    {
        /// <summary>SQLite.</summary>
        Sqlite,

        /// <summary>PostgreSQL.</summary>
        Postgresql,

        /// <summary>MySQL.</summary>
        Mysql,

        /// <summary>SQL Server.</summary>
        SqlServer
    }
}

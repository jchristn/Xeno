namespace Xeno.Core.Database.Postgresql
{
    using Xeno.Core.Database;
    using Xeno.Core.Enums;

    /// <summary>
    /// PostgreSQL database driver.
    /// </summary>
    public class PostgresqlDatabaseDriver : DatabaseDriverBase
    {
        /// <summary>
        /// Database provider type.
        /// </summary>
        public override DatabaseTypeEnum DatabaseType { get { return DatabaseTypeEnum.Postgresql; } }
    }
}

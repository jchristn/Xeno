namespace Xeno.Core.Database.SqlServer
{
    using Xeno.Core.Database;
    using Xeno.Core.Enums;

    /// <summary>
    /// SQL Server database driver.
    /// </summary>
    public class SqlServerDatabaseDriver : DatabaseDriverBase
    {
        /// <summary>
        /// Database provider type.
        /// </summary>
        public override DatabaseTypeEnum DatabaseType { get { return DatabaseTypeEnum.SqlServer; } }
    }
}

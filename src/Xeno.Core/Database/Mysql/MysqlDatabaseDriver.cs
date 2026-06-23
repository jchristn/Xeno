namespace Xeno.Core.Database.Mysql
{
    using Xeno.Core.Database;
    using Xeno.Core.Enums;

    /// <summary>
    /// MySQL database driver.
    /// </summary>
    public class MysqlDatabaseDriver : DatabaseDriverBase
    {
        /// <summary>
        /// Database provider type.
        /// </summary>
        public override DatabaseTypeEnum DatabaseType { get { return DatabaseTypeEnum.Mysql; } }
    }
}

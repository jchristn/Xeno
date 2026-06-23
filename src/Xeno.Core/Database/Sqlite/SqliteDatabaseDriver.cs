namespace Xeno.Core.Database.Sqlite
{
    using Xeno.Core.Database;
    using Xeno.Core.Enums;

    /// <summary>
    /// SQLite database driver.
    /// </summary>
    public class SqliteDatabaseDriver : DatabaseDriverBase
    {
        /// <summary>
        /// Database provider type.
        /// </summary>
        public override DatabaseTypeEnum DatabaseType { get { return DatabaseTypeEnum.Sqlite; } }
    }
}

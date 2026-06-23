namespace Xeno.Core.Settings
{
    using System;
    using System.Collections.Generic;
    using Xeno.Core.Enums;

    /// <summary>
    /// Database settings.
    /// </summary>
    public class DatabaseSettings
    {
        /// <summary>
        /// Database provider.
        /// </summary>
        public DatabaseTypeEnum Type { get; set; } = DatabaseTypeEnum.Sqlite;

        /// <summary>
        /// SQLite filename.
        /// </summary>
        public string Filename { get; set; } = "data/xeno.db";

        /// <summary>
        /// Database server.
        /// </summary>
        public string? Server { get; set; } = null;

        /// <summary>
        /// Database port.
        /// </summary>
        public int Port { get; set; } = 0;

        /// <summary>
        /// Database name.
        /// </summary>
        public string? DatabaseName { get; set; } = null;

        /// <summary>
        /// Username.
        /// </summary>
        public string? Username { get; set; } = null;

        /// <summary>
        /// Password.
        /// </summary>
        public string? Password { get; set; } = null;
    }
}

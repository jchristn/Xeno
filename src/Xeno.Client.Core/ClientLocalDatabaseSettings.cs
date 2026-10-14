namespace Xeno.Client
{
    using System;
    using System.IO;

    /// <summary>
    /// Local dashboard SQLite database settings.
    /// </summary>
    public class ClientLocalDatabaseSettings
    {
        /// <summary>
        /// SQLite database filename. Default is ~/.xeno/local-dashboard.db.
        /// </summary>
        public string Filename { get; set; } = Path.Combine(ClientSettings.SettingsDirectory, "local-dashboard.db");

        /// <summary>
        /// Optional SQLite connection string. When supplied, this overrides Filename.
        /// </summary>
        public string? ConnectionString { get; set; } = null;

        /// <summary>
        /// Resolve the configured filename to an absolute path.
        /// </summary>
        /// <returns>Absolute filename.</returns>
        public string ResolveFilename()
        {
            string filename = String.IsNullOrWhiteSpace(Filename)
                ? Path.Combine(ClientSettings.SettingsDirectory, "local-dashboard.db")
                : Filename;
            if (filename.StartsWith("~/", StringComparison.Ordinal) || filename.StartsWith("~\\", StringComparison.Ordinal))
            {
                filename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), filename.Substring(2));
            }

            return Path.GetFullPath(Environment.ExpandEnvironmentVariables(filename));
        }
    }
}

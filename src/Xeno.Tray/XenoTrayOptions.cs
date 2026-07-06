namespace Xeno.Tray
{
    using System;
    using System.IO;

    /// <summary>
    /// Xeno tray runtime options.
    /// </summary>
    public sealed class XenoTrayOptions
    {
        /// <summary>
        /// Xeno client executable path or command name. Defaults to a sibling xeno executable when present, otherwise xeno from PATH.
        /// </summary>
        public string ClientExecutablePath { get; set; } = ResolveDefaultClientExecutablePath();

        /// <summary>
        /// Start command timeout in milliseconds. Default is 30000.
        /// </summary>
        public int StartTimeoutMs { get; set; } = 30000;

        /// <summary>
        /// Stop command timeout in milliseconds. Default is 10000.
        /// </summary>
        public int StopTimeoutMs { get; set; } = 10000;

        /// <summary>
        /// Resolve the default client executable path.
        /// </summary>
        /// <returns>Executable path or command name.</returns>
        public static string ResolveDefaultClientExecutablePath()
        {
            string executable = OperatingSystem.IsWindows() ? "xeno.exe" : "xeno";
            string sibling = Path.Combine(AppContext.BaseDirectory, executable);
            return File.Exists(sibling) ? sibling : "xeno";
        }
    }
}

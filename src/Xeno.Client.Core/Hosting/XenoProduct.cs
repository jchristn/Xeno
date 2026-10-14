namespace Xeno.Client.Hosting
{
    using System;
    using System.Reflection;

    /// <summary>
    /// Product identity shared by the CLI, desktop app, terminal app, and tray agent: display names, links, and version.
    /// </summary>
    public static class XenoProduct
    {
        /// <summary>
        /// Product name shown in window titles and the macOS menu bar.
        /// </summary>
        public const string Name = "Xeno";

        /// <summary>
        /// Display name of the desktop application.
        /// </summary>
        public const string DesktopName = "Xeno";

        /// <summary>
        /// Display name of the tray agent.
        /// </summary>
        public const string TrayName = "Xeno Agent";

        /// <summary>
        /// Display name of the terminal application.
        /// </summary>
        public const string TerminalName = "Xeno Terminal";

        /// <summary>
        /// One-line product description.
        /// </summary>
        public const string Tagline = "Serve your models to the Xeno network, and use the network's models from this machine.";

        /// <summary>
        /// Documentation and source repository.
        /// </summary>
        public const string DocumentationUrl = "https://github.com/jchristn/Xeno";

        /// <summary>
        /// Issue tracker.
        /// </summary>
        public const string IssuesUrl = "https://github.com/jchristn/Xeno/issues";

        /// <summary>
        /// Community Discord.
        /// </summary>
        public const string CommunityUrl = "https://discord.gg/tRAN8HgvK5";

        /// <summary>
        /// Product version taken from the shared client library's informational version, without build metadata.
        /// </summary>
        public static string Version
        {
            get
            {
                Assembly assembly = typeof(XenoProduct).Assembly;
                string? informational = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!String.IsNullOrWhiteSpace(informational))
                {
                    int plus = informational.IndexOf('+');
                    return plus > 0 ? informational.Substring(0, plus) : informational;
                }

                return assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            }
        }
    }
}

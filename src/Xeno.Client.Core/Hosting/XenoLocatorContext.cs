namespace Xeno.Client.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    /// <summary>
    /// Inputs for locating a Xeno executable. Production code uses <see cref="FromEnvironment"/>; tests supply their own
    /// values so the search is deterministic.
    /// </summary>
    public class XenoLocatorContext
    {
        /// <summary>
        /// Environment variable values by name (XENO_CLI, XENO_DESKTOP, XENO_TRAY).
        /// </summary>
        public Dictionary<string, string> EnvironmentOverrides { get; set; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Value of the PATH environment variable.
        /// </summary>
        public string PathVariable { get; set; } = String.Empty;

        /// <summary>
        /// User home directory (for ~/.dotnet/tools).
        /// </summary>
        public string HomeDirectory { get; set; } = String.Empty;

        /// <summary>
        /// Directories searched for sibling executables, normally the running program's directory.
        /// </summary>
        public List<string> SiblingDirectories { get; set; } = new List<string>();

        /// <summary>
        /// Directories from which to walk upward looking for a source checkout (src/Xeno.*/bin).
        /// </summary>
        public List<string> CheckoutSearchRoots { get; set; } = new List<string>();

        /// <summary>
        /// Install directories searched before a source checkout: /Applications/Xeno.app/Contents/MacOS and
        /// ~/Applications/Xeno.app/Contents/MacOS on macOS, %LOCALAPPDATA%\Programs\Xeno on Windows, and
        /// ~/.local/share/xeno/app (or $XDG_DATA_HOME/xeno/app) on Linux. The install scripts put the apps there.
        /// </summary>
        public List<string> ApplicationDirectories { get; set; } = new List<string>();

        /// <summary>
        /// True when locating Windows executables (.exe).
        /// </summary>
        public bool IsWindows { get; set; } = OperatingSystem.IsWindows();

        /// <summary>
        /// Maximum number of parent directories walked from each checkout search root. Default 8.
        /// </summary>
        public int MaxCheckoutDepth { get; set; } = 8;

        /// <summary>
        /// Build a context from the current process environment.
        /// </summary>
        /// <returns>Context.</returns>
        public static XenoLocatorContext FromEnvironment()
        {
            XenoLocatorContext context = new XenoLocatorContext
            {
                PathVariable = Environment.GetEnvironmentVariable("PATH") ?? String.Empty,
                HomeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                IsWindows = OperatingSystem.IsWindows()
            };

            foreach (string name in new[] { XenoExecutableLocator.CliEnvironmentVariable, XenoExecutableLocator.DesktopEnvironmentVariable, XenoExecutableLocator.TrayEnvironmentVariable })
            {
                string? value = Environment.GetEnvironmentVariable(name);
                if (!String.IsNullOrWhiteSpace(value)) context.EnvironmentOverrides[name] = value;
            }

            string baseDirectory = AppContext.BaseDirectory;
            if (!String.IsNullOrWhiteSpace(baseDirectory)) context.SiblingDirectories.Add(baseDirectory);
            if (!String.IsNullOrWhiteSpace(baseDirectory)) context.CheckoutSearchRoots.Add(baseDirectory);
            context.CheckoutSearchRoots.Add(Environment.CurrentDirectory);
            if (OperatingSystem.IsMacOS())
            {
                context.ApplicationDirectories.Add(Path.Combine("/Applications", "Xeno.app", "Contents", "MacOS"));
                context.ApplicationDirectories.Add(Path.Combine(context.HomeDirectory, "Applications", "Xeno.app", "Contents", "MacOS"));
            }
            else if (OperatingSystem.IsWindows())
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (!String.IsNullOrWhiteSpace(localAppData)) context.ApplicationDirectories.Add(Path.Combine(localAppData, "Programs", "Xeno"));
            }
            else
            {
                string? dataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
                if (String.IsNullOrWhiteSpace(dataHome)) dataHome = Path.Combine(context.HomeDirectory, ".local", "share");
                context.ApplicationDirectories.Add(Path.Combine(dataHome, "xeno", "app"));
            }

            return context;
        }
    }
}

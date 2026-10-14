namespace Xeno.Client.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    /// <summary>
    /// Finds the xeno, xeno-desktop, and xeno-tray executables so each front end can launch the others.
    /// Search order: environment override (XENO_CLI, XENO_DESKTOP, XENO_TRAY), sibling of the running program, PATH
    /// and ~/.dotnet/tools (CLI only), installed application directories, then the newest build in a source checkout.
    /// A framework-dependent assembly without an apphost is run through the dotnet host. Thread-safe.
    /// </summary>
    public static class XenoExecutableLocator
    {
        #region Public-Members

        /// <summary>
        /// Environment variable that overrides the CLI location (a file or a directory).
        /// </summary>
        public const string CliEnvironmentVariable = "XENO_CLI";

        /// <summary>
        /// Environment variable that overrides the desktop app location (a file or a directory).
        /// </summary>
        public const string DesktopEnvironmentVariable = "XENO_DESKTOP";

        /// <summary>
        /// Environment variable that overrides the tray agent location (a file or a directory).
        /// </summary>
        public const string TrayEnvironmentVariable = "XENO_TRAY";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Executable base name (without extension) for an application.
        /// </summary>
        /// <param name="application">Application.</param>
        /// <returns>Base name: xeno, xeno-desktop, or xeno-tray.</returns>
        public static string BaseName(XenoApplicationEnum application)
        {
            switch (application)
            {
                case XenoApplicationEnum.Desktop: return "xeno-desktop";
                case XenoApplicationEnum.Tray: return "xeno-tray";
                default: return "xeno";
            }
        }

        /// <summary>
        /// Source project directory name for an application, used when searching a checkout.
        /// </summary>
        /// <param name="application">Application.</param>
        /// <returns>Project directory name.</returns>
        public static string ProjectName(XenoApplicationEnum application)
        {
            switch (application)
            {
                case XenoApplicationEnum.Desktop: return "Xeno.Desktop";
                case XenoApplicationEnum.Tray: return "Xeno.Tray";
                default: return "Xeno.Client";
            }
        }

        /// <summary>
        /// Environment variable that overrides an application's location.
        /// </summary>
        /// <param name="application">Application.</param>
        /// <returns>Variable name.</returns>
        public static string EnvironmentVariable(XenoApplicationEnum application)
        {
            switch (application)
            {
                case XenoApplicationEnum.Desktop: return DesktopEnvironmentVariable;
                case XenoApplicationEnum.Tray: return TrayEnvironmentVariable;
                default: return CliEnvironmentVariable;
            }
        }

        /// <summary>
        /// Locate an application using the current process environment.
        /// </summary>
        /// <param name="application">Application.</param>
        /// <returns>Location, or null when not found.</returns>
        public static XenoExecutableLocation? Locate(XenoApplicationEnum application)
        {
            return Locate(application, XenoLocatorContext.FromEnvironment());
        }

        /// <summary>
        /// Locate an application using explicit inputs.
        /// </summary>
        /// <param name="application">Application.</param>
        /// <param name="context">Search inputs.</param>
        /// <returns>Location, or null when not found.</returns>
        /// <exception cref="ArgumentNullException">Context is null.</exception>
        public static XenoExecutableLocation? Locate(XenoApplicationEnum application, XenoLocatorContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            string baseName = BaseName(application);

            if (context.EnvironmentOverrides.TryGetValue(EnvironmentVariable(application), out string? overrideValue) && !String.IsNullOrWhiteSpace(overrideValue))
            {
                XenoExecutableLocation? fromOverride = Directory.Exists(overrideValue)
                    ? FromDirectory(overrideValue, baseName, context.IsWindows, "environment")
                    : FromFile(overrideValue, "environment");
                if (fromOverride != null) return fromOverride;
            }

            foreach (string directory in context.SiblingDirectories)
            {
                XenoExecutableLocation? sibling = FromDirectory(directory, baseName, context.IsWindows, "sibling");
                if (sibling != null) return sibling;
            }

            if (application == XenoApplicationEnum.Cli)
            {
                foreach (string directory in SplitPath(context.PathVariable, context.IsWindows))
                {
                    string candidate = Path.Combine(directory, ExecutableName(baseName, context.IsWindows));
                    if (File.Exists(candidate)) return new XenoExecutableLocation { Executable = candidate, Source = "path" };
                }

                if (!String.IsNullOrWhiteSpace(context.HomeDirectory))
                {
                    string tool = Path.Combine(context.HomeDirectory, ".dotnet", "tools", ExecutableName(baseName, context.IsWindows));
                    if (File.Exists(tool)) return new XenoExecutableLocation { Executable = tool, Source = "dotnet-tools" };
                }
            }

            foreach (string directory in context.ApplicationDirectories)
            {
                XenoExecutableLocation? installed = FromDirectory(directory, baseName, context.IsWindows, "installed");
                if (installed != null) return installed;
            }

            return FromCheckout(application, context);
        }

        /// <summary>
        /// Describe how to re-run the current process, used by the CLI to start its own background worker. When the
        /// process is the dotnet host, the entry assembly path is passed as a leading argument.
        /// </summary>
        /// <param name="entryAssembly">Entry assembly, or null to use <see cref="Assembly.GetEntryAssembly"/>.</param>
        /// <returns>Location of the current process.</returns>
        /// <exception cref="InvalidOperationException">The current executable path cannot be determined.</exception>
        public static XenoExecutableLocation CurrentProcess(Assembly? entryAssembly)
        {
            string? processPath = Environment.ProcessPath;
            if (String.IsNullOrWhiteSpace(processPath))
            {
                using (System.Diagnostics.Process process = System.Diagnostics.Process.GetCurrentProcess())
                {
                    processPath = process.MainModule?.FileName;
                }
            }

            if (String.IsNullOrWhiteSpace(processPath)) throw new InvalidOperationException("Unable to determine the current executable path.");
            XenoExecutableLocation location = new XenoExecutableLocation { Executable = processPath, Source = "current-process" };
            if (String.Equals(Path.GetFileNameWithoutExtension(processPath), "dotnet", StringComparison.OrdinalIgnoreCase))
            {
                Assembly? assembly = entryAssembly ?? Assembly.GetEntryAssembly();
                if (assembly != null && !String.IsNullOrWhiteSpace(assembly.Location)) location.LeadingArguments.Add(assembly.Location);
            }

            return location;
        }

        /// <summary>
        /// Executable file name for a base name on the target platform.
        /// </summary>
        /// <param name="baseName">Base name.</param>
        /// <param name="isWindows">True for Windows.</param>
        /// <returns>File name.</returns>
        public static string ExecutableName(string baseName, bool isWindows)
        {
            return isWindows ? baseName + ".exe" : baseName;
        }

        #endregion

        #region Private-Methods

        private static XenoExecutableLocation? FromFile(string path, string source)
        {
            if (!File.Exists(path)) return null;
            if (path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return new XenoExecutableLocation { Executable = DotnetHost(), LeadingArguments = new List<string> { path }, Source = source };
            }

            return new XenoExecutableLocation { Executable = path, Source = source };
        }

        private static XenoExecutableLocation? FromDirectory(string directory, string baseName, bool isWindows, string source)
        {
            if (String.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory)) return null;
            string apphost = Path.Combine(directory, ExecutableName(baseName, isWindows));
            if (File.Exists(apphost)) return new XenoExecutableLocation { Executable = apphost, Source = source };
            string dll = Path.Combine(directory, baseName + ".dll");
            if (File.Exists(dll)) return new XenoExecutableLocation { Executable = DotnetHost(), LeadingArguments = new List<string> { dll }, Source = source };
            return null;
        }

        private static XenoExecutableLocation? FromCheckout(XenoApplicationEnum application, XenoLocatorContext context)
        {
            string baseName = BaseName(application);
            string projectBin = Path.Combine("src", ProjectName(application), "bin");
            HashSet<string> visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in context.CheckoutSearchRoots)
            {
                if (String.IsNullOrWhiteSpace(root)) continue;
                DirectoryInfo? current;
                try
                {
                    current = new DirectoryInfo(root);
                }
                catch (ArgumentException)
                {
                    continue;
                }

                for (int depth = 0; current != null && depth <= context.MaxCheckoutDepth; depth++, current = current.Parent)
                {
                    if (!visited.Add(current.FullName)) continue;
                    string bin = Path.Combine(current.FullName, projectBin);
                    if (!Directory.Exists(bin)) continue;
                    FileInfo? newest = NewestBuild(bin, baseName, context.IsWindows);
                    if (newest == null) continue;
                    if (newest.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                    {
                        return new XenoExecutableLocation { Executable = DotnetHost(), LeadingArguments = new List<string> { newest.FullName }, Source = "checkout" };
                    }

                    return new XenoExecutableLocation { Executable = newest.FullName, Source = "checkout" };
                }
            }

            return null;
        }

        private static FileInfo? NewestBuild(string bin, string baseName, bool isWindows)
        {
            string apphostName = ExecutableName(baseName, isWindows);
            List<FileInfo> candidates = new List<FileInfo>();
            try
            {
                foreach (string file in Directory.EnumerateFiles(bin, apphostName, SearchOption.AllDirectories)) candidates.Add(new FileInfo(file));
                if (candidates.Count == 0)
                {
                    foreach (string file in Directory.EnumerateFiles(bin, baseName + ".dll", SearchOption.AllDirectories)) candidates.Add(new FileInfo(file));
                }
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }

            return candidates
                .Where(item => !item.FullName.Contains(Path.DirectorySeparatorChar + "ref" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(item => item.LastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static IEnumerable<string> SplitPath(string pathVariable, bool isWindows)
        {
            if (String.IsNullOrWhiteSpace(pathVariable)) yield break;
            char separator = isWindows ? ';' : ':';
            foreach (string part in pathVariable.Split(separator, StringSplitOptions.RemoveEmptyEntries))
            {
                string trimmed = part.Trim().Trim('"');
                if (!String.IsNullOrWhiteSpace(trimmed)) yield return trimmed;
            }
        }

        private static string DotnetHost()
        {
            string? root = Environment.GetEnvironmentVariable("DOTNET_ROOT");
            if (!String.IsNullOrWhiteSpace(root))
            {
                string candidate = Path.Combine(root, OperatingSystem.IsWindows() ? "dotnet.exe" : "dotnet");
                if (File.Exists(candidate)) return candidate;
            }

            return "dotnet";
        }

        #endregion
    }
}

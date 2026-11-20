namespace Xeno.Publisher.Manifest
{
    using System.Collections.Generic;

    /// <summary>
    /// One published payload and how it is installed: which executables it contains, which command goes on PATH,
    /// what starts at login, and which shortcuts the menus get.
    /// </summary>
    public sealed class ArtifactSettings
    {
        /// <summary>
        /// Short id that channels reference, for example "client".
        /// </summary>
        public string Id { get; set; } = "";

        /// <summary>
        /// Project to publish, relative to the repository root.
        /// </summary>
        public string Csproj { get; set; } = "";

        /// <summary>
        /// True to publish a single-file executable. False publishes a self-contained folder, which lets several
        /// executables share one copy of the .NET runtime. Default false.
        /// </summary>
        public bool SingleFile { get; set; } = false;

        /// <summary>
        /// Executable (without extension) that opens the product: the macOS bundle's main executable and the
        /// installed-app icon.
        /// </summary>
        public string MainExecutable { get; set; } = "";

        /// <summary>
        /// Every executable (without extension) the payload must contain. Publishing fails when one is missing.
        /// </summary>
        public List<string> Executables { get; set; } = new List<string>();

        /// <summary>
        /// Executables that installers put on PATH.
        /// </summary>
        public List<string> PathCommands { get; set; } = new List<string>();

        /// <summary>
        /// The program started when the user signs in, or null for none.
        /// </summary>
        public LoginItemSettings? LoginItem { get; set; } = null;

        /// <summary>
        /// Start menu and application menu shortcuts.
        /// </summary>
        public List<ShortcutSettings> Shortcuts { get; set; } = new List<ShortcutSettings>();

        /// <summary>
        /// Command that stops the running product before an upgrade or uninstall, or null for none.
        /// </summary>
        public StopCommandSettings? StopCommand { get; set; } = null;

        /// <summary>
        /// Process names (without extension) to end before files are replaced, after <see cref="StopCommand"/> runs.
        /// </summary>
        public List<string> Processes { get; set; } = new List<string>();

        /// <summary>
        /// Steps that run once before the first publish, for example building an embedded web dashboard.
        /// </summary>
        public List<PreBuildStep> PreBuild { get; set; } = new List<PreBuildStep>();

        /// <summary>
        /// Native libraries the payload needs on Linux, by package format.
        /// </summary>
        public LinuxDependencySettings LinuxDependencies { get; set; } = new LinuxDependencySettings();
    }
}

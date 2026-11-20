namespace Xeno.Publisher.Manifest
{
    /// <summary>
    /// A program that starts when the user signs in: a Run key on Windows, a LaunchAgent on macOS, and an XDG
    /// autostart entry on Linux.
    /// </summary>
    public sealed class LoginItemSettings
    {
        /// <summary>
        /// Executable (without extension) to start.
        /// </summary>
        public string Executable { get; set; } = "";

        /// <summary>
        /// Name shown in startup settings, for example "Xeno Agent".
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// One-line description for the autostart entry.
        /// </summary>
        public string Comment { get; set; } = "";

        /// <summary>
        /// Suffix appended to the project's bundle id to form the LaunchAgent label, for example "agent".
        /// </summary>
        public string LabelSuffix { get; set; } = "agent";
    }
}

namespace Xeno.Publisher.Manifest
{
    /// <summary>
    /// A Start menu (Windows) or application menu (Linux) entry.
    /// </summary>
    public sealed class ShortcutSettings
    {
        /// <summary>
        /// Name shown in the menu.
        /// </summary>
        public string DisplayName { get; set; } = "";

        /// <summary>
        /// Executable (without extension) the shortcut runs.
        /// </summary>
        public string Executable { get; set; } = "";

        /// <summary>
        /// Arguments passed to the executable, or empty.
        /// </summary>
        public string Arguments { get; set; } = "";

        /// <summary>
        /// One-line description.
        /// </summary>
        public string Comment { get; set; } = "";

        /// <summary>
        /// True when the program runs in a terminal window (Linux desktop entries set Terminal=true).
        /// </summary>
        public bool Terminal { get; set; } = false;

        /// <summary>
        /// Linux desktop entry file name without extension, for example "xeno-agent".
        /// </summary>
        public string DesktopId { get; set; } = "";
    }
}

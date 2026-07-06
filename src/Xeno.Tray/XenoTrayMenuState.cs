namespace Xeno.Tray
{
    /// <summary>
    /// Computed tray menu state.
    /// </summary>
    public sealed class XenoTrayMenuState
    {
        /// <summary>
        /// Status menu header.
        /// </summary>
        public string StatusHeader { get; set; } = "Status: Unknown";

        /// <summary>
        /// Tooltip text.
        /// </summary>
        public string ToolTip { get; set; } = "Xeno Client";

        /// <summary>
        /// Whether start is available.
        /// </summary>
        public bool StartEnabled { get; set; } = true;

        /// <summary>
        /// Whether dashboard opening is available.
        /// </summary>
        public bool OpenDashboardEnabled { get; set; } = false;

        /// <summary>
        /// Whether exiting the client is available.
        /// </summary>
        public bool ExitClientEnabled { get; set; } = false;
    }
}

namespace Xeno.Client.Presentation
{
    using System.Collections.Generic;

    /// <summary>
    /// Every color a front end needs, worked out from a <see cref="XenoColorSet"/> by <see cref="XenoColorSchemes.Resolve"/>.
    /// Colors are #rrggbb.
    /// </summary>
    public sealed class XenoResolvedColors
    {
        #region Public-Members

        /// <summary>
        /// True for the dark appearance.
        /// </summary>
        public bool IsDark { get; set; } = false;

        /// <summary>
        /// Window background.
        /// </summary>
        public string Background { get; set; } = "#ffffff";

        /// <summary>
        /// Cards and panels.
        /// </summary>
        public string Panel { get; set; } = "#ffffff";

        /// <summary>
        /// Soft panel background.
        /// </summary>
        public string PanelSoft { get; set; } = "#ffffff";

        /// <summary>
        /// Primary text.
        /// </summary>
        public string Text { get; set; } = "#000000";

        /// <summary>
        /// Secondary text.
        /// </summary>
        public string Muted { get; set; } = "#666666";

        /// <summary>
        /// Borders.
        /// </summary>
        public string Border { get; set; } = "#dddddd";

        /// <summary>
        /// Accent for text and highlights.
        /// </summary>
        public string Accent { get; set; } = "#0969da";

        /// <summary>
        /// Accent button fill.
        /// </summary>
        public string AccentFill { get; set; } = "#0969da";

        /// <summary>
        /// Accent button text, or null to leave it to the toolkit theme.
        /// </summary>
        public string? AccentText { get; set; } = null;

        /// <summary>
        /// Success.
        /// </summary>
        public string Success { get; set; } = "#1a7f37";

        /// <summary>
        /// Success background.
        /// </summary>
        public string SuccessSoft { get; set; } = "#e6f4ea";

        /// <summary>
        /// Warning.
        /// </summary>
        public string Warning { get; set; } = "#9a6700";

        /// <summary>
        /// Warning background.
        /// </summary>
        public string WarningSoft { get; set; } = "#fff4df";

        /// <summary>
        /// Danger.
        /// </summary>
        public string Danger { get; set; } = "#c62828";

        /// <summary>
        /// Danger background.
        /// </summary>
        public string DangerSoft { get; set; } = "#fdecec";

        /// <summary>
        /// Information.
        /// </summary>
        public string Info { get; set; } = "#0969da";

        /// <summary>
        /// Information background.
        /// </summary>
        public string InfoSoft { get; set; } = "#e8f0fe";

        /// <summary>
        /// Sidebar background.
        /// </summary>
        public string Sidebar { get; set; } = "#24292f";

        /// <summary>
        /// Sidebar text.
        /// </summary>
        public string SidebarText { get; set; } = "#ffffff";

        /// <summary>
        /// Sidebar section headings.
        /// </summary>
        public string SidebarMuted { get; set; } = "#aaaaaa";

        /// <summary>
        /// Selected sidebar item background.
        /// </summary>
        public string SidebarActive { get; set; } = "#0969da";

        /// <summary>
        /// Selected sidebar item text.
        /// </summary>
        public string SidebarActiveText { get; set; } = "#ffffff";

        /// <summary>
        /// Chart series colors.
        /// </summary>
        public List<string> Series { get; set; } = new List<string>();

        #endregion
    }
}

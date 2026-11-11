namespace Xeno.Client.Presentation
{
    using System.Collections.Generic;

    /// <summary>
    /// The colors of one appearance (light or dark) of a color scheme. Six base colors are required; every other color
    /// is optional and, when left null, is worked out from the base colors with readable contrast. Colors are #rrggbb.
    /// </summary>
    public sealed class XenoColorSet
    {
        #region Public-Members

        /// <summary>
        /// Window background.
        /// </summary>
        public string Background { get; set; } = "#f5f6f8";

        /// <summary>
        /// Cards and panels.
        /// </summary>
        public string Panel { get; set; } = "#ffffff";

        /// <summary>
        /// Primary text.
        /// </summary>
        public string Text { get; set; } = "#1f2328";

        /// <summary>
        /// Accent: buttons, links, and highlights.
        /// </summary>
        public string Accent { get; set; } = "#0969da";

        /// <summary>
        /// Navigation sidebar background.
        /// </summary>
        public string Sidebar { get; set; } = "#24292f";

        /// <summary>
        /// Selected sidebar item background.
        /// </summary>
        public string SidebarActive { get; set; } = "#0969da";

        /// <summary>
        /// Soft panel background for headers and stripes, or null to derive.
        /// </summary>
        public string? PanelSoft { get; set; } = null;

        /// <summary>
        /// Secondary text, or null to derive.
        /// </summary>
        public string? Muted { get; set; } = null;

        /// <summary>
        /// Borders, or null to derive.
        /// </summary>
        public string? Border { get; set; } = null;

        /// <summary>
        /// Fill of accent buttons when it differs from <see cref="Accent"/>, or null to use the accent.
        /// </summary>
        public string? AccentFill { get; set; } = null;

        /// <summary>
        /// Text on accent buttons, or null to pick black or white for contrast.
        /// </summary>
        public string? AccentText { get; set; } = null;

        /// <summary>
        /// True to leave accent button text to the toolkit theme (the original Xeno scheme does this).
        /// </summary>
        public bool ThemeAccentText { get; set; } = false;

        /// <summary>
        /// Sidebar text, or null to derive.
        /// </summary>
        public string? SidebarText { get; set; } = null;

        /// <summary>
        /// Sidebar section headings, or null to derive.
        /// </summary>
        public string? SidebarMuted { get; set; } = null;

        /// <summary>
        /// Selected sidebar item text, or null to derive.
        /// </summary>
        public string? SidebarActiveText { get; set; } = null;

        /// <summary>
        /// Success, or null for the standard green.
        /// </summary>
        public string? Success { get; set; } = null;

        /// <summary>
        /// Success background, or null to derive.
        /// </summary>
        public string? SuccessSoft { get; set; } = null;

        /// <summary>
        /// Warning, or null for the standard amber.
        /// </summary>
        public string? Warning { get; set; } = null;

        /// <summary>
        /// Warning background, or null to derive.
        /// </summary>
        public string? WarningSoft { get; set; } = null;

        /// <summary>
        /// Danger, or null for the standard red.
        /// </summary>
        public string? Danger { get; set; } = null;

        /// <summary>
        /// Danger background, or null to derive.
        /// </summary>
        public string? DangerSoft { get; set; } = null;

        /// <summary>
        /// Information, or null to use the accent.
        /// </summary>
        public string? Info { get; set; } = null;

        /// <summary>
        /// Information background, or null to derive.
        /// </summary>
        public string? InfoSoft { get; set; } = null;

        /// <summary>
        /// Chart series colors, or null to derive.
        /// </summary>
        public List<string>? Series { get; set; } = null;

        #endregion

        #region Public-Methods

        /// <summary>
        /// A copy holding only the six base colors.
        /// </summary>
        /// <returns>Copy.</returns>
        public XenoColorSet BaseCopy()
        {
            return new XenoColorSet { Background = Background, Panel = Panel, Text = Text, Accent = Accent, Sidebar = Sidebar, SidebarActive = SidebarActive };
        }

        #endregion
    }
}

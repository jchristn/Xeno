namespace Xeno.Tui
{
    using System;
    using System.Globalization;
    using TUIKit;
    using TUIKit.Theming;
    using Xeno.Client.Presentation;

    /// <summary>
    /// Terminal themes in the Xeno logo colors (<see cref="XenoBrand"/>): green accents and focus, and purple for the
    /// header, selections, and borders. Text keeps the terminal's own background so the app sits naturally in any
    /// terminal; the dark and light variants pick foregrounds readable on dark and light backgrounds.
    /// </summary>
    public static class XenoTuiTheme
    {
        #region Public-Members

        /// <summary>
        /// Theme for dark terminals.
        /// </summary>
        public static Theme Dark { get; } = Build(false);

        /// <summary>
        /// Theme for light terminals.
        /// </summary>
        public static Theme Light { get; } = Build(true);

        /// <summary>
        /// Named style for the menu bar row: logo-purple tints behind the menu names.
        /// </summary>
        public const string MenuBarRole = "xeno.menubar";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Convert a #rrggbb string to a terminal color.
        /// </summary>
        /// <param name="hex">Color such as #42dc7d.</param>
        /// <returns>Truecolor value.</returns>
        /// <exception cref="ArgumentException">The value is not #rrggbb.</exception>
        public static Color ToColor(string hex)
        {
            if (String.IsNullOrEmpty(hex) || hex.Length != 7 || hex[0] != '#') throw new ArgumentException("Expected a #rrggbb color.", nameof(hex));
            return Color.FromRgb(Int32.Parse(hex.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
        }

        #endregion

        #region Private-Methods

        private static Theme Build(bool light)
        {
            Color none = Color.Default;
            Color accent = ToColor(light ? XenoBrand.GreenDeep : XenoBrand.Green);
            Color border = ToColor(light ? "#b9a6c1" : "#6b4a78");
            Color muted = ToColor(light ? "#6e5f75" : "#a48fad");
            Color text = ToColor(light ? "#2a1530" : "#f3ecf5");
            Color purple = ToColor(XenoBrand.Purple);
            Color white = ToColor("#ffffff");

            Theme theme = new Theme(
                light ? "Xeno Light" : "Xeno Dark",
                new CellStyle(text, none, CellAttributes.None, 0),
                new CellStyle(accent, none, CellAttributes.None, 0),
                new CellStyle(border, none, CellAttributes.None, 0),
                new CellStyle(muted, none, CellAttributes.None, 0),
                false,
                new CellStyle(accent, none, CellAttributes.None, 0),
                new CellStyle(ToColor(light ? "#a15d00" : "#fbbf24"), none, CellAttributes.None, 0),
                new CellStyle(ToColor(light ? "#c73737" : "#f87171"), none, CellAttributes.None, 0),
                new CellStyle(ToColor(light ? XenoBrand.Purple : XenoBrand.Lavender), none, CellAttributes.None, 0),
                new CellStyle(white, purple, CellAttributes.None, 0),
                new CellStyle(muted, none, CellAttributes.Dim, 0));

            CellStyle focus = new CellStyle(accent, none, CellAttributes.Bold, 0);
            theme.SetStyle(Theme.FocusBorderRole, focus);
            theme.SetStyle(Theme.FocusTitleRole, focus);
            theme.SetStyle(Theme.ButtonFocusedRole, new CellStyle(ToColor(XenoBrand.PurpleInk), ToColor(XenoBrand.Green), CellAttributes.Bold, 0));
            theme.SetStyle(MenuBarRole, new CellStyle(ToColor(light ? XenoBrand.PurpleInk : "#efe6f2"), ToColor(light ? "#efe6f2" : XenoBrand.PurpleDeep), CellAttributes.None, 0));
            theme.SetStyle(Theme.TabFocusedRole, new CellStyle(white, purple, CellAttributes.Bold | CellAttributes.Underline, 0));
            return theme;
        }

        #endregion
    }
}

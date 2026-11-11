namespace Xeno.Desktop.Common
{
    using System.Linq;
    using Avalonia.Media;
    using Xeno.Client.Presentation;

    /// <summary>
    /// Brushes for the Xeno desktop app and tray agent, built from a color scheme in <see cref="XenoColorSchemes"/>.
    /// </summary>
    public sealed class XenoPalette
    {
        #region Public-Members

        /// <summary>
        /// True for the dark palette.
        /// </summary>
        public bool IsDark { get; private set; }

        /// <summary>
        /// Window background.
        /// </summary>
        public IBrush Background { get; private set; } = Brushes.White;

        /// <summary>
        /// Card and panel background.
        /// </summary>
        public IBrush Panel { get; private set; } = Brushes.White;

        /// <summary>
        /// Soft panel background for headers and stripes.
        /// </summary>
        public IBrush PanelSoft { get; private set; } = Brushes.White;

        /// <summary>
        /// Primary text.
        /// </summary>
        public IBrush Text { get; private set; } = Brushes.Black;

        /// <summary>
        /// Secondary text.
        /// </summary>
        public IBrush Muted { get; private set; } = Brushes.Gray;

        /// <summary>
        /// Borders.
        /// </summary>
        public IBrush Border { get; private set; } = Brushes.LightGray;

        /// <summary>
        /// Brand accent.
        /// </summary>
        public IBrush Accent { get; private set; } = Brushes.Teal;

        /// <summary>
        /// Brand accent color.
        /// </summary>
        public Color AccentColor { get; private set; } = Colors.Teal;

        /// <summary>
        /// Fill of accent buttons and the user's chat bubbles.
        /// </summary>
        public IBrush AccentFill { get; private set; } = Brushes.Teal;

        /// <summary>
        /// Text on <see cref="AccentFill"/>.
        /// </summary>
        public IBrush AccentText { get; private set; } = Brushes.White;

        /// <summary>
        /// Text on accent buttons, or null when the toolkit theme chooses it.
        /// </summary>
        public Color? AccentTextColor { get; private set; } = null;

        /// <summary>
        /// Success.
        /// </summary>
        public IBrush Success { get; private set; } = Brushes.Green;

        /// <summary>
        /// Success background.
        /// </summary>
        public IBrush SuccessSoft { get; private set; } = Brushes.Honeydew;

        /// <summary>
        /// Warning.
        /// </summary>
        public IBrush Warning { get; private set; } = Brushes.Orange;

        /// <summary>
        /// Warning background.
        /// </summary>
        public IBrush WarningSoft { get; private set; } = Brushes.LightYellow;

        /// <summary>
        /// Danger.
        /// </summary>
        public IBrush Danger { get; private set; } = Brushes.Red;

        /// <summary>
        /// Danger background.
        /// </summary>
        public IBrush DangerSoft { get; private set; } = Brushes.MistyRose;

        /// <summary>
        /// Information.
        /// </summary>
        public IBrush Info { get; private set; } = Brushes.SteelBlue;

        /// <summary>
        /// Information background.
        /// </summary>
        public IBrush InfoSoft { get; private set; } = Brushes.AliceBlue;

        /// <summary>
        /// Sidebar background.
        /// </summary>
        public IBrush Sidebar { get; private set; } = Brushes.DarkSlateGray;

        /// <summary>
        /// Sidebar text.
        /// </summary>
        public IBrush SidebarText { get; private set; } = Brushes.Gainsboro;

        /// <summary>
        /// Sidebar section headings.
        /// </summary>
        public IBrush SidebarMuted { get; private set; } = Brushes.Gray;

        /// <summary>
        /// Selected sidebar item background.
        /// </summary>
        public IBrush SidebarActive { get; private set; } = Brushes.Teal;

        /// <summary>
        /// Selected sidebar item text.
        /// </summary>
        public IBrush SidebarActiveText { get; private set; } = Brushes.White;

        /// <summary>
        /// Chart series colors.
        /// </summary>
        public IBrush[] Series { get; private set; } = new IBrush[0];

        #endregion

        #region Public-Methods

        /// <summary>
        /// Light palette of the default Xeno scheme.
        /// </summary>
        /// <returns>Palette.</returns>
        public static XenoPalette Light()
        {
            return From(XenoColorSchemes.Resolve(XenoColorSchemes.Find(XenoColorSchemes.DefaultId), false));
        }

        /// <summary>
        /// Dark palette of the default Xeno scheme.
        /// </summary>
        /// <returns>Palette.</returns>
        public static XenoPalette Dark()
        {
            return From(XenoColorSchemes.Resolve(XenoColorSchemes.Find(XenoColorSchemes.DefaultId), true));
        }

        /// <summary>
        /// Palette for resolved scheme colors.
        /// </summary>
        /// <param name="colors">Colors from <see cref="XenoColorSchemes.Resolve"/>.</param>
        /// <returns>Palette.</returns>
        /// <exception cref="System.ArgumentNullException">colors is null.</exception>
        public static XenoPalette From(XenoResolvedColors colors)
        {
            System.ArgumentNullException.ThrowIfNull(colors);
            return new XenoPalette
            {
                IsDark = colors.IsDark,
                Background = Brush(colors.Background),
                Panel = Brush(colors.Panel),
                PanelSoft = Brush(colors.PanelSoft),
                Text = Brush(colors.Text),
                Muted = Brush(colors.Muted),
                Border = Brush(colors.Border),
                Accent = Brush(colors.Accent),
                AccentColor = Color.Parse(colors.AccentFill),
                AccentFill = Brush(colors.AccentFill),
                AccentText = colors.AccentText != null ? Brush(colors.AccentText) : Brushes.White,
                AccentTextColor = colors.AccentText != null ? Color.Parse(colors.AccentText) : null,
                Success = Brush(colors.Success),
                SuccessSoft = Brush(colors.SuccessSoft),
                Warning = Brush(colors.Warning),
                WarningSoft = Brush(colors.WarningSoft),
                Danger = Brush(colors.Danger),
                DangerSoft = Brush(colors.DangerSoft),
                Info = Brush(colors.Info),
                InfoSoft = Brush(colors.InfoSoft),
                Sidebar = Brush(colors.Sidebar),
                SidebarText = Brush(colors.SidebarText),
                SidebarMuted = Brush(colors.SidebarMuted),
                SidebarActive = Brush(colors.SidebarActive),
                SidebarActiveText = Brush(colors.SidebarActiveText),
                Series = colors.Series.Select(Brush).ToArray()
            };
        }

        #endregion

        #region Private-Methods

        private static IBrush Brush(string hex)
        {
            return new SolidColorBrush(Color.Parse(hex));
        }

        #endregion
    }
}

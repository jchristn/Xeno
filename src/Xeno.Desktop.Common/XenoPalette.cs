namespace Xeno.Desktop.Common
{
    using Avalonia.Media;
    using Xeno.Client.Presentation;

    /// <summary>
    /// Colors for the Xeno desktop app and tray agent in light and dark, built from the logo colors in <see cref="XenoBrand"/>.
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
        /// Light palette.
        /// </summary>
        /// <returns>Palette.</returns>
        public static XenoPalette Light()
        {
            return new XenoPalette
            {
                IsDark = false,
                Background = Brush("#f7f5f9"),
                Panel = Brush("#ffffff"),
                PanelSoft = Brush("#f4f0f6"),
                Text = Brush("#2a1530"),
                Muted = Brush("#6e5f75"),
                Border = Brush("#e3dbe8"),
                Accent = Brush(XenoBrand.GreenDeep),
                AccentColor = Color.Parse(XenoBrand.GreenDeep),
                Success = Brush(XenoBrand.GreenDeep),
                SuccessSoft = Brush("#e3f8eb"),
                Warning = Brush("#a15d00"),
                WarningSoft = Brush("#fff4df"),
                Danger = Brush("#c73737"),
                DangerSoft = Brush("#fdecec"),
                Info = Brush(XenoBrand.Purple),
                InfoSoft = Brush("#f1e8f4"),
                Sidebar = Brush(XenoBrand.Purple),
                SidebarText = Brush("#efe6f2"),
                SidebarMuted = Brush("#b9a6c1"),
                SidebarActive = Brush(XenoBrand.Green),
                SidebarActiveText = Brush(XenoBrand.PurpleInk),
                Series = new IBrush[] { Brush(XenoBrand.GreenShade), Brush(XenoBrand.Purple), Brush("#a15d00"), Brush("#0f6e8f"), Brush("#c73737"), Brush(XenoBrand.Green) }
            };
        }

        /// <summary>
        /// Dark palette.
        /// </summary>
        /// <returns>Palette.</returns>
        public static XenoPalette Dark()
        {
            return new XenoPalette
            {
                IsDark = true,
                Background = Brush("#150b18"),
                Panel = Brush("#211327"),
                PanelSoft = Brush("#2b1933"),
                Text = Brush("#f3ecf5"),
                Muted = Brush("#b4a3bb"),
                Border = Brush("#3d2846"),
                Accent = Brush(XenoBrand.Green),
                AccentColor = Color.Parse(XenoBrand.GreenDeep),
                Success = Brush(XenoBrand.Green),
                SuccessSoft = Brush("#123a22"),
                Warning = Brush("#fbbf24"),
                WarningSoft = Brush("#412f15"),
                Danger = Brush("#f87171"),
                DangerSoft = Brush("#442121"),
                Info = Brush(XenoBrand.Lavender),
                InfoSoft = Brush("#352040"),
                Sidebar = Brush(XenoBrand.PurpleDeep),
                SidebarText = Brush("#e6dbea"),
                SidebarMuted = Brush("#a48fad"),
                SidebarActive = Brush(XenoBrand.Green),
                SidebarActiveText = Brush(XenoBrand.PurpleInk),
                Series = new IBrush[] { Brush(XenoBrand.Green), Brush(XenoBrand.Lavender), Brush("#fbbf24"), Brush("#38bdf8"), Brush("#f87171"), Brush(XenoBrand.GreenShade) }
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

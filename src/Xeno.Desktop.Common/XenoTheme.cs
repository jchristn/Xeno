namespace Xeno.Desktop.Common
{
    using System;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.Presenters;
    using Avalonia.Media;
    using Avalonia.Styling;
    using Xeno.Client.Presentation;

    /// <summary>
    /// Applies the Xeno appearance (system, light, dark) and color scheme to an Avalonia application and exposes the
    /// active palette.
    /// Call from the UI thread.
    /// </summary>
    public static class XenoTheme
    {
        #region Public-Members

        /// <summary>
        /// Active palette.
        /// </summary>
        public static XenoPalette Current { get; private set; } = XenoPalette.Light();

        /// <summary>
        /// Selected appearance.
        /// </summary>
        public static XenoAppearanceEnum Appearance { get; private set; } = XenoAppearanceEnum.System;

        /// <summary>
        /// Selected color scheme.
        /// </summary>
        public static XenoColorScheme Scheme { get; private set; } = XenoColorSchemes.Find(XenoColorSchemes.DefaultId);

        /// <summary>
        /// Raised after the palette changes.
        /// </summary>
        public static event EventHandler? Changed;

        #endregion

        #region Private-Members

        private static Application? _Application = null;
        private static readonly Styles _AccentStyles = new Styles();
        private static bool _ForceChanged = false;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Apply an appearance to the application and follow system changes while the appearance is System.
        /// </summary>
        /// <param name="application">Application.</param>
        /// <param name="appearance">Appearance.</param>
        /// <exception cref="ArgumentNullException">Application is null.</exception>
        public static void Apply(Application application, XenoAppearanceEnum appearance)
        {
            ArgumentNullException.ThrowIfNull(application);
            if (!Object.ReferenceEquals(_Application, application))
            {
                _Application = application;
                application.Styles.Add(_AccentStyles);
                application.ActualThemeVariantChanged += (sender, args) => Refresh();
            }

            Appearance = appearance;
            switch (appearance)
            {
                case XenoAppearanceEnum.Light:
                    application.RequestedThemeVariant = ThemeVariant.Light;
                    break;
                case XenoAppearanceEnum.Dark:
                    application.RequestedThemeVariant = ThemeVariant.Dark;
                    break;
                default:
                    application.RequestedThemeVariant = ThemeVariant.Default;
                    break;
            }

            Refresh();
        }

        /// <summary>
        /// Apply an appearance and a color scheme.
        /// </summary>
        /// <param name="application">Application.</param>
        /// <param name="appearance">Appearance.</param>
        /// <param name="scheme">Color scheme.</param>
        /// <exception cref="ArgumentNullException">Application or scheme is null.</exception>
        public static void Apply(Application application, XenoAppearanceEnum appearance, XenoColorScheme scheme)
        {
            ArgumentNullException.ThrowIfNull(scheme);
            Scheme = scheme;
            _ForceChanged = true;
            Apply(application, appearance);
        }

        /// <summary>
        /// Switch to another color scheme, or apply edits to the current one, and raise <see cref="Changed"/>.
        /// </summary>
        /// <param name="scheme">Color scheme.</param>
        /// <exception cref="ArgumentNullException">scheme is null.</exception>
        public static void SetScheme(XenoColorScheme scheme)
        {
            ArgumentNullException.ThrowIfNull(scheme);
            Scheme = scheme;
            _ForceChanged = true;
            Refresh();
        }

        #endregion

        #region Private-Methods

        private static void Refresh()
        {
            Application? application = _Application;
            if (application == null) return;
            bool dark = application.ActualThemeVariant == ThemeVariant.Dark;
            XenoPalette palette = XenoPalette.From(XenoColorSchemes.Resolve(Scheme, dark));
            Color accent = palette.AccentColor;
            application.Resources["SystemAccentColor"] = accent;
            application.Resources["SystemAccentColorDark1"] = Shade(accent, 0.88);
            application.Resources["SystemAccentColorDark2"] = Shade(accent, 0.76);
            application.Resources["SystemAccentColorDark3"] = Shade(accent, 0.64);
            application.Resources["SystemAccentColorLight1"] = Shade(accent, 1.12);
            application.Resources["SystemAccentColorLight2"] = Shade(accent, 1.24);
            application.Resources["SystemAccentColorLight3"] = Shade(accent, 1.36);
            application.Resources["ControlCornerRadius"] = new CornerRadius(6);
            application.Resources["OverlayCornerRadius"] = new CornerRadius(8);
            ApplyAccentText(palette.AccentTextColor);
            bool changed = _ForceChanged || Current.IsDark != palette.IsDark || Current.Series.Length == 0;
            _ForceChanged = false;
            Current = palette;
            if (changed) Changed?.Invoke(null, EventArgs.Empty);
        }

        private static void ApplyAccentText(Color? color)
        {
            // Accent buttons take their text color from the scheme, except in the original scheme, which leaves it
            // to the Fluent theme. Styles added after the theme win.
            _AccentStyles.Clear();
            if (color == null) return;
            SolidColorBrush brush = new SolidColorBrush(color.Value);
            foreach (string state in new[] { String.Empty, ":pointerover", ":pressed" })
            {
                Style style = new Style(selector =>
                {
                    Selector button = selector.OfType<Button>().Class("accent");
                    if (state.Length > 0) button = button.Class(state);
                    return button.Template().OfType<ContentPresenter>().Name("PART_ContentPresenter");
                });
                style.Setters.Add(new Setter(ContentPresenter.ForegroundProperty, brush));
                _AccentStyles.Add(style);
            }
        }

        private static Color Shade(Color color, double factor)
        {
            byte Scale(byte channel)
            {
                return (byte)Math.Clamp((int)Math.Round(channel * factor), 0, 255);
            }

            return Color.FromArgb(color.A, Scale(color.R), Scale(color.G), Scale(color.B));
        }

        #endregion
    }
}

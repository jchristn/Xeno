namespace Xeno.Desktop.Common
{
    using System;
    using Avalonia;
    using Avalonia.Media;
    using Avalonia.Styling;
    using Xeno.Client.Presentation;

    /// <summary>
    /// Applies the Xeno appearance (system, light, dark) to an Avalonia application and exposes the active palette.
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
        /// Raised after the palette changes.
        /// </summary>
        public static event EventHandler? Changed;

        #endregion

        #region Private-Members

        private static Application? _Application = null;

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

        #endregion

        #region Private-Methods

        private static void Refresh()
        {
            Application? application = _Application;
            if (application == null) return;
            bool dark = application.ActualThemeVariant == ThemeVariant.Dark;
            XenoPalette palette = dark ? XenoPalette.Dark() : XenoPalette.Light();
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
            bool changed = Current.IsDark != palette.IsDark || Current.Series.Length == 0;
            Current = palette;
            if (changed) Changed?.Invoke(null, EventArgs.Empty);
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

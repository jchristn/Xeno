namespace Xeno.Client.Presentation
{
    using System;
    using System.IO;
    using Xeno.Core.Serialization;

    /// <summary>
    /// Per-user preferences for the desktop app and the terminal app (appearance and last page), stored in
    /// frontend.json in the client home directory. Load failures fall back to defaults.
    /// </summary>
    public class XenoFrontEndPreferences
    {
        #region Public-Members

        /// <summary>
        /// Preferences file path.
        /// </summary>
        public static string PreferencesFile
        {
            get { return Path.Combine(ClientSettings.SettingsDirectory, "frontend.json"); }
        }

        /// <summary>
        /// Seconds between chart refreshes: 0 (off), 15, or 60, the same choices as the web dashboards. Default 15. Other
        /// values round to the nearest choice.
        /// </summary>
        public int AutoRefreshSeconds
        {
            get { return _AutoRefreshSeconds; }
            set { _AutoRefreshSeconds = value <= 0 ? 0 : value <= 30 ? 15 : 60; }
        }

        /// <summary>
        /// Appearance for the desktop app.
        /// </summary>
        public XenoAppearanceEnum DesktopAppearance { get; set; } = XenoAppearanceEnum.System;

        /// <summary>
        /// Color scheme for the desktop app and the tray agent: a <see cref="XenoColorSchemes.BuiltIn"/> identifier, or
        /// <see cref="XenoColorSchemes.CustomId"/> for <see cref="DesktopCustomColors"/>. Default "xeno".
        /// </summary>
        public string DesktopColorScheme { get; set; } = XenoColorSchemes.DefaultId;

        /// <summary>
        /// Color scheme for the terminal app: a <see cref="XenoColorSchemes.BuiltIn"/> identifier, or
        /// <see cref="XenoColorSchemes.CustomId"/> for <see cref="DesktopCustomColors"/>. Default "xeno".
        /// </summary>
        public string TerminalColorScheme { get; set; } = XenoColorSchemes.DefaultId;

        /// <summary>
        /// The user's own colors, used when <see cref="DesktopColorScheme"/> or <see cref="TerminalColorScheme"/> is
        /// "custom". Shared by the desktop and terminal apps. Null until the user makes one.
        /// </summary>
        public XenoColorScheme? DesktopCustomColors { get; set; } = null;

        /// <summary>
        /// Appearance for the terminal app.
        /// </summary>
        public XenoAppearanceEnum TerminalAppearance { get; set; } = XenoAppearanceEnum.System;

        /// <summary>
        /// Last page shown in the desktop app.
        /// </summary>
        public XenoPageEnum DesktopLastPage { get; set; } = XenoPageEnum.Overview;

        /// <summary>
        /// Last page shown in the terminal app.
        /// </summary>
        public XenoPageEnum TerminalLastPage { get; set; } = XenoPageEnum.Overview;

        /// <summary>
        /// Status refresh interval in seconds for the desktop app and the terminal app. Minimum 2, maximum 300, default 5.
        /// </summary>
        public int RefreshSeconds
        {
            get { return _RefreshSeconds; }
            set { _RefreshSeconds = Math.Clamp(value, 2, 300); }
        }

        #endregion

        #region Private-Members

        private int _RefreshSeconds = 5;
        private int _AutoRefreshSeconds = 15;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Load preferences from a file, or defaults when the file is missing or unreadable.
        /// </summary>
        /// <param name="path">File path, or null for <see cref="PreferencesFile"/>.</param>
        /// <returns>Preferences.</returns>
        public static XenoFrontEndPreferences Load(string? path = null)
        {
            string file = String.IsNullOrWhiteSpace(path) ? PreferencesFile : path;
            try
            {
                if (!File.Exists(file)) return new XenoFrontEndPreferences();
                XenoFrontEndPreferences? loaded = JsonUtil.Deserialize<XenoFrontEndPreferences>(File.ReadAllText(file));
                return loaded ?? new XenoFrontEndPreferences();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is InvalidDataException || ex is System.Text.Json.JsonException)
            {
                return new XenoFrontEndPreferences();
            }
        }

        /// <summary>
        /// Save preferences. Failures are ignored because preferences are a convenience.
        /// </summary>
        /// <param name="path">File path, or null for <see cref="PreferencesFile"/>.</param>
        /// <returns>True when saved.</returns>
        public bool Save(string? path = null)
        {
            string file = String.IsNullOrWhiteSpace(path) ? PreferencesFile : path;
            try
            {
                string? directory = Path.GetDirectoryName(file);
                if (!String.IsNullOrWhiteSpace(directory)) Directory.CreateDirectory(directory);
                File.WriteAllText(file, JsonUtil.Serialize(this, true));
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return false;
            }
        }

        #endregion
    }
}

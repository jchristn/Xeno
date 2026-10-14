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
        /// Appearance for the desktop app.
        /// </summary>
        public XenoAppearanceEnum DesktopAppearance { get; set; } = XenoAppearanceEnum.System;

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

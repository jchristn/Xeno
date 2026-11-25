namespace Xeno.Desktop.Common
{
    using System;
    using SkiaSharp;
    using Xeno.Client.Presentation;

    /// <summary>
    /// Font families for the desktop app and the tray agent: the operating system's own UI and code fonts, chosen from
    /// <see cref="XenoTypography"/> by asking Skia (Avalonia's renderer) which are installed. Falls back to the bundled
    /// Inter for text and the platform's generic monospace for code.
    /// </summary>
    public static class XenoFonts
    {
        #region Public-Members

        /// <summary>
        /// Bundled Inter, used for text only when none of the system candidates is installed.
        /// </summary>
        public const string BundledSans = "avares://Xeno.Desktop.Common/Assets/Fonts/Inter.ttf#Inter";

        /// <summary>
        /// Text font family name, for <c>FontManagerOptions.DefaultFamilyName</c>.
        /// </summary>
        public static string SansFamilyName
        {
            get
            {
                return _Sans.Value;
            }
        }

        /// <summary>
        /// Monospace font family name, for identifiers, URLs, and logs.
        /// </summary>
        public static string MonoFamilyName
        {
            get
            {
                return _Mono.Value;
            }
        }

        #endregion

        #region Private-Members

        private static readonly Lazy<string> _Sans = new Lazy<string>(() => XenoTypography.Resolve(XenoTypography.SansCandidates(XenoTypography.CurrentPlatform()), IsInstalled, BundledSans));
        private static readonly Lazy<string> _Mono = new Lazy<string>(() => XenoTypography.Resolve(XenoTypography.MonoCandidates(XenoTypography.CurrentPlatform()), IsInstalled, "monospace"));

        #endregion

        #region Public-Methods

        /// <summary>
        /// True when Skia finds the family itself, not a substitute (fontconfig answers every name with something).
        /// </summary>
        /// <param name="familyName">Family name.</param>
        /// <returns>True when installed.</returns>
        public static bool IsInstalled(string familyName)
        {
            if (String.IsNullOrWhiteSpace(familyName)) return false;
            using (SKTypeface? typeface = SKFontManager.Default.MatchFamily(familyName))
            {
                return typeface != null && String.Equals(typeface.FamilyName, familyName, StringComparison.OrdinalIgnoreCase);
            }
        }

        #endregion
    }
}

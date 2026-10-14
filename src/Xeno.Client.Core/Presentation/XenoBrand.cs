namespace Xeno.Client.Presentation
{
    /// <summary>
    /// Brand colors taken from the Xeno logo (assets/logo.png), as #rrggbb strings. The desktop palette and the
    /// terminal theme are both built from these, so the apps match the logo and each other.
    /// </summary>
    public static class XenoBrand
    {
        /// <summary>
        /// Logo face green.
        /// </summary>
        public const string Green = "#42dc7d";

        /// <summary>
        /// Logo shadow green.
        /// </summary>
        public const string GreenShade = "#21964e";

        /// <summary>
        /// Deeper green with enough contrast for white text and for text on white (buttons, links).
        /// </summary>
        public const string GreenDeep = "#1b7f41";

        /// <summary>
        /// Logo outline and eye purple.
        /// </summary>
        public const string Purple = "#4a2054";

        /// <summary>
        /// Darker purple for dark-mode chrome.
        /// </summary>
        public const string PurpleDeep = "#2a1131";

        /// <summary>
        /// Near-black purple used for text drawn on the logo green.
        /// </summary>
        public const string PurpleInk = "#2a0f30";

        /// <summary>
        /// Light lavender derived from the logo purple, readable on dark backgrounds.
        /// </summary>
        public const string Lavender = "#c9a6d4";
    }
}

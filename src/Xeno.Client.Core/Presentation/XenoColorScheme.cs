namespace Xeno.Client.Presentation
{
    /// <summary>
    /// A named color scheme for the desktop app and the tray agent, with a light and a dark set of colors.
    /// </summary>
    public sealed class XenoColorScheme
    {
        #region Public-Members

        /// <summary>
        /// Stable identifier stored in the preferences, for example "xeno" or "custom".
        /// </summary>
        public string Id { get; set; } = XenoColorSchemes.DefaultId;

        /// <summary>
        /// Display name.
        /// </summary>
        public string Name { get; set; } = "Xeno";

        /// <summary>
        /// One-line description.
        /// </summary>
        public string Description { get; set; } = "";

        /// <summary>
        /// Colors in light appearance.
        /// </summary>
        public XenoColorSet Light { get; set; } = new XenoColorSet();

        /// <summary>
        /// Colors in dark appearance.
        /// </summary>
        public XenoColorSet Dark { get; set; } = new XenoColorSet();

        #endregion
    }
}

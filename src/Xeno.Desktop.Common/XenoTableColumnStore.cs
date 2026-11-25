namespace Xeno.Desktop.Common
{
    using System;
    using Xeno.Client.Presentation;

    /// <summary>
    /// Where every <see cref="XenoTable{TRow}"/> keeps its visible-column choice: the app's front-end preferences, saved
    /// through the app so nothing else overwrites them. The desktop app sets both members at startup; without them,
    /// tables still let users choose columns for the session but do not remember the choice.
    /// </summary>
    public static class XenoTableColumnStore
    {
        #region Public-Members

        /// <summary>
        /// Preferences holding the choices, or null to keep them for the session only.
        /// </summary>
        public static XenoFrontEndPreferences? Preferences { get; set; } = null;

        /// <summary>
        /// Saves <see cref="Preferences"/>, or null.
        /// </summary>
        public static Action? Save { get; set; } = null;

        #endregion
    }
}

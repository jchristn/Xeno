namespace Xeno.Tui.Pages
{
    using TUIKit.Input;

    /// <summary>
    /// Helpers for single-key page actions.
    /// </summary>
    internal static class PageKeys
    {
        /// <summary>
        /// Determine whether a key is an unmodified character (case-insensitive).
        /// </summary>
        /// <param name="key">Key.</param>
        /// <param name="character">Character.</param>
        /// <returns>True when the key is the character with no Ctrl or Alt.</returns>
        public static bool Is(KeyEvent key, char character)
        {
            if (key.Code != KeyCode.Character) return false;
            if ((key.Modifiers & (KeyModifiers.Ctrl | KeyModifiers.Alt)) != 0) return false;
            return char.ToLowerInvariant((char)key.Rune) == char.ToLowerInvariant(character);
        }
    }
}

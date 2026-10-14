namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// A platform-neutral menu entry. The desktop app turns entries into native menus, the tray agent into a tray
    /// menu, and the terminal app into its menu bar.
    /// </summary>
    public class XenoMenuEntry
    {
        #region Public-Members

        /// <summary>
        /// Kind.
        /// </summary>
        public XenoMenuEntryKindEnum Kind { get; set; } = XenoMenuEntryKindEnum.Item;

        /// <summary>
        /// Display text.
        /// </summary>
        public string Header { get; set; } = String.Empty;

        /// <summary>
        /// Command for items and radio items.
        /// </summary>
        public XenoCommandEnum Command { get; set; } = XenoCommandEnum.About;

        /// <summary>
        /// Shortcut key used with Cmd (macOS) or Ctrl: a letter, a digit, "," or a function key such as "F5".
        /// Empty for no shortcut.
        /// </summary>
        public string Key { get; set; } = String.Empty;

        /// <summary>
        /// True when the shortcut also requires Shift.
        /// </summary>
        public bool Shift { get; set; } = false;

        /// <summary>
        /// Children of a submenu.
        /// </summary>
        public List<XenoMenuEntry> Children { get; set; } = new List<XenoMenuEntry>();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Create a command item.
        /// </summary>
        /// <param name="header">Display text.</param>
        /// <param name="command">Command.</param>
        /// <param name="key">Shortcut key, or empty.</param>
        /// <param name="shift">True when the shortcut requires Shift.</param>
        /// <returns>Entry.</returns>
        public static XenoMenuEntry Item(string header, XenoCommandEnum command, string key = "", bool shift = false)
        {
            return new XenoMenuEntry { Kind = XenoMenuEntryKindEnum.Item, Header = header, Command = command, Key = key ?? String.Empty, Shift = shift };
        }

        /// <summary>
        /// Create a radio item.
        /// </summary>
        /// <param name="header">Display text.</param>
        /// <param name="command">Command.</param>
        /// <returns>Entry.</returns>
        public static XenoMenuEntry Radio(string header, XenoCommandEnum command)
        {
            return new XenoMenuEntry { Kind = XenoMenuEntryKindEnum.Radio, Header = header, Command = command };
        }

        /// <summary>
        /// Create a submenu.
        /// </summary>
        /// <param name="header">Display text.</param>
        /// <param name="children">Children; null entries are skipped.</param>
        /// <returns>Entry.</returns>
        public static XenoMenuEntry Submenu(string header, params XenoMenuEntry?[] children)
        {
            return new XenoMenuEntry
            {
                Kind = XenoMenuEntryKindEnum.Submenu,
                Header = header,
                Children = children.Where(item => item != null).Select(item => item!).ToList()
            };
        }

        /// <summary>
        /// Create a separator.
        /// </summary>
        /// <returns>Entry.</returns>
        public static XenoMenuEntry Separator()
        {
            return new XenoMenuEntry { Kind = XenoMenuEntryKindEnum.Separator };
        }

        /// <summary>
        /// Create a status line.
        /// </summary>
        /// <returns>Entry.</returns>
        public static XenoMenuEntry StatusLine()
        {
            return new XenoMenuEntry { Kind = XenoMenuEntryKindEnum.StatusLine };
        }

        /// <summary>
        /// Enumerate this entry and every descendant.
        /// </summary>
        /// <returns>Entries, depth first.</returns>
        public IEnumerable<XenoMenuEntry> Flatten()
        {
            yield return this;
            foreach (XenoMenuEntry child in Children)
            {
                foreach (XenoMenuEntry descendant in child.Flatten()) yield return descendant;
            }
        }

        #endregion
    }
}

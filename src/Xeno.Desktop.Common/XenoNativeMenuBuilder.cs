namespace Xeno.Desktop.Common
{
    using System;
    using System.Collections.Generic;
    using Avalonia.Controls;
    using Avalonia.Input;
    using Xeno.Client.Presentation;

    /// <summary>
    /// Turns the shared <see cref="XenoMenuLayout"/> entries into Avalonia native menus (the macOS menu bar, the
    /// in-window menu on Windows and Linux, and tray menus) and keeps their enabled, checked, and status text in sync
    /// with an <see cref="IXenoMenuHost"/>. Build a fresh menu for each window: a native item belongs to one menu.
    /// Call from the UI thread.
    /// </summary>
    public sealed class XenoNativeMenuBuilder
    {
        #region Private-Members

        private readonly IXenoMenuHost _Host;
        private readonly Dictionary<NativeMenuItem, XenoMenuEntry> _Items = new Dictionary<NativeMenuItem, XenoMenuEntry>();
        private readonly List<NativeMenuItem> _StatusItems = new List<NativeMenuItem>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a builder.
        /// </summary>
        /// <param name="host">Command host.</param>
        /// <exception cref="ArgumentNullException">Host is null.</exception>
        public XenoNativeMenuBuilder(IXenoMenuHost host)
        {
            _Host = host ?? throw new ArgumentNullException(nameof(host));
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Build a native menu from entries.
        /// </summary>
        /// <param name="entries">Entries.</param>
        /// <param name="withShortcuts">True to attach keyboard shortcuts (false for tray menus).</param>
        /// <returns>Menu.</returns>
        /// <exception cref="ArgumentNullException">Entries is null.</exception>
        public NativeMenu Build(IEnumerable<XenoMenuEntry> entries, bool withShortcuts)
        {
            ArgumentNullException.ThrowIfNull(entries);
            NativeMenu menu = new NativeMenu();
            foreach (XenoMenuEntry entry in entries)
            {
                menu.Items.Add(BuildItem(entry, withShortcuts));
            }

            Refresh();
            return menu;
        }

        /// <summary>
        /// Re-read enabled, checked, and status text for every item built by this builder.
        /// </summary>
        public void Refresh()
        {
            foreach (KeyValuePair<NativeMenuItem, XenoMenuEntry> pair in _Items)
            {
                pair.Key.IsEnabled = _Host.CanExecute(pair.Value.Command);
                if (pair.Value.Kind == XenoMenuEntryKindEnum.Radio)
                {
                    pair.Key.IsChecked = _Host.IsChecked(pair.Value.Command);
                }
            }

            string status = _Host.StatusLine;
            foreach (NativeMenuItem item in _StatusItems)
            {
                item.Header = status;
            }
        }

        /// <summary>
        /// Stop tracking the items of a menu, for example when its window closes.
        /// </summary>
        /// <param name="menu">Menu.</param>
        public void Release(NativeMenu? menu)
        {
            if (menu == null) return;
            foreach (NativeMenuItemBase item in menu.Items)
            {
                if (item is NativeMenuItem nativeItem)
                {
                    _Items.Remove(nativeItem);
                    _StatusItems.Remove(nativeItem);
                    Release(nativeItem.Menu);
                }
            }
        }

        /// <summary>
        /// Convert a shared shortcut key into an Avalonia key gesture using Cmd on macOS and Ctrl elsewhere.
        /// </summary>
        /// <param name="key">Shortcut key: a letter, a digit, ",", "/", or a function key such as "F5".</param>
        /// <param name="shift">True when Shift is also required.</param>
        /// <returns>Gesture, or null when the key is empty or unknown.</returns>
        public static KeyGesture? ToGesture(string key, bool shift)
        {
            Key? parsed = ParseKey(key);
            if (parsed == null) return null;
            KeyModifiers modifiers = OperatingSystem.IsMacOS() ? KeyModifiers.Meta : KeyModifiers.Control;
            if (key.StartsWith("F", StringComparison.Ordinal) && key.Length > 1 && Char.IsDigit(key[1])) modifiers = KeyModifiers.None;
            if (shift) modifiers |= KeyModifiers.Shift;
            return new KeyGesture(parsed.Value, modifiers);
        }

        #endregion

        #region Private-Methods

        private NativeMenuItemBase BuildItem(XenoMenuEntry entry, bool withShortcuts)
        {
            switch (entry.Kind)
            {
                case XenoMenuEntryKindEnum.Separator:
                    return new NativeMenuItemSeparator();
                case XenoMenuEntryKindEnum.StatusLine:
                    NativeMenuItem status = new NativeMenuItem { Header = _Host.StatusLine, IsEnabled = false };
                    _StatusItems.Add(status);
                    return status;
                case XenoMenuEntryKindEnum.Submenu:
                    NativeMenu submenu = new NativeMenu();
                    foreach (XenoMenuEntry child in entry.Children) submenu.Items.Add(BuildItem(child, withShortcuts));
                    return new NativeMenuItem { Header = entry.Header, Menu = submenu };
                default:
                    NativeMenuItem item = new NativeMenuItem { Header = entry.Header };
                    if (entry.Kind == XenoMenuEntryKindEnum.Radio)
                    {
                        item.ToggleType = MenuItemToggleType.Radio;
                    }

                    if (withShortcuts && !String.IsNullOrEmpty(entry.Key))
                    {
                        KeyGesture? gesture = ToGesture(entry.Key, entry.Shift);
                        if (gesture != null) item.Gesture = gesture;
                    }

                    XenoCommandEnum command = entry.Command;
                    item.Click += (sender, args) =>
                    {
                        if (_Host.CanExecute(command)) _Host.Execute(command);
                    };
                    _Items[item] = entry;
                    return item;
            }
        }

        private static Key? ParseKey(string key)
        {
            if (String.IsNullOrEmpty(key)) return null;
            if (key == ",") return Key.OemComma;
            if (key == "/") return Key.OemQuestion;
            if (key.Length == 1 && Char.IsLetter(key[0]) && Enum.TryParse(key.ToUpperInvariant(), out Key letter)) return letter;
            if (key.Length == 1 && Char.IsDigit(key[0]) && Enum.TryParse("D" + key, out Key digit)) return digit;
            if (Enum.TryParse(key, true, out Key named)) return named;
            return null;
        }

        #endregion
    }
}

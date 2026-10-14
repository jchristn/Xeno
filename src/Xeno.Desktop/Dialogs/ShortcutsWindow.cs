namespace Xeno.Desktop.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Layout;
    using Xeno.Client.Presentation;
    using Xeno.Desktop.Common;

    /// <summary>
    /// Lists every keyboard shortcut from the shared menu layout.
    /// </summary>
    public sealed class ShortcutsWindow : XenoDialogWindow
    {
        /// <summary>
        /// Create the window.
        /// </summary>
        public ShortcutsWindow()
        {
            Title = "Keyboard Shortcuts";
            Width = 520;
            Height = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = XenoAssets.LoadWindowIcon();
            Background = XenoTheme.Current.Background;
            string modifier = OperatingSystem.IsMacOS() ? "Cmd" : "Ctrl";
            List<KeyValuePair<string, string>> rows = new List<KeyValuePair<string, string>>();
            foreach (XenoMenuEntry menu in XenoMenuLayout.MenuBar(XenoSurfaceEnum.Desktop, OperatingSystem.IsMacOS()))
            {
                foreach (XenoMenuEntry entry in menu.Flatten().Where(item => item.Kind == XenoMenuEntryKindEnum.Item && !String.IsNullOrEmpty(item.Key)))
                {
                    string key = entry.Key.StartsWith("F", StringComparison.Ordinal) && entry.Key.Length > 1 ? entry.Key : modifier + "+" + (entry.Shift ? "Shift+" : String.Empty) + entry.Key;
                    rows.Add(new KeyValuePair<string, string>(key, menu.Header + " > " + entry.Header.TrimEnd('.')));
                }
            }

            rows.Add(new KeyValuePair<string, string>("Enter / double-click", "Open the selected row"));
            rows.Add(new KeyValuePair<string, string>("Right-click", "Row actions"));
            Button close = Ui.Primary("Close", () => CloseWith(null));
            close.IsCancel = true;
            close.IsDefault = true;
            close.HorizontalAlignment = HorizontalAlignment.Right;
            DockPanel root = new DockPanel { Margin = new Thickness(20) };
            DockPanel.SetDock(close, Dock.Bottom);
            root.Children.Add(close);
            root.Children.Add(new ScrollViewer { Content = Ui.Details(rows, 170) });
            Content = root;
        }
    }
}

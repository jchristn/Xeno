namespace Xeno.Desktop.Dialogs
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Layout;
    using Avalonia.Media;
    using Xeno.Core.Chat;
    using Xeno.Desktop.Common;

    /// <summary>
    /// The tools a chat model can use, grouped by where they come from (Xeno, the working folder, the web, connected MCP
    /// servers), each with whether it runs right away or asks first, and a filter.
    /// </summary>
    public sealed class ChatToolsWindow : XenoDialogWindow
    {
        #region Private-Members

        private readonly IReadOnlyList<ChatToolInfo> _Tools;
        private readonly StackPanel _List = new StackPanel { Spacing = 4 };
        private readonly TextBlock _Count = new TextBlock();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the window.
        /// </summary>
        /// <param name="tools">Tools.</param>
        /// <exception cref="ArgumentNullException">tools is null.</exception>
        public ChatToolsWindow(IReadOnlyList<ChatToolInfo> tools)
        {
            _Tools = tools ?? throw new ArgumentNullException(nameof(tools));
            XenoPalette palette = XenoTheme.Current;
            Title = "Chat Tools";
            Width = 780;
            Height = 660;
            MinWidth = 560;
            MinHeight = 400;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Icon = XenoAssets.LoadWindowIcon();
            Background = palette.Background;

            TextBlock heading = new TextBlock { Text = "Tools the model can use", FontSize = 18, FontWeight = FontWeight.SemiBold, Foreground = palette.Text };
            TextBlock intro = new TextBlock
            {
                Text = "Turn on Tools in a chat to let the model call these. Read-only tools run right away; tools that change something ask you first.",
                TextWrapping = TextWrapping.Wrap,
                Foreground = palette.Muted
            };
            TextBox filter = new TextBox { PlaceholderText = "Filter by name or description", MinWidth = 280 };
            filter.TextChanged += (sender, args) => Render(filter.Text);
            _Count.Foreground = palette.Muted;
            _Count.VerticalAlignment = VerticalAlignment.Center;
            _Count.Margin = new Thickness(12, 0, 0, 0);
            DockPanel filterRow = new DockPanel();
            DockPanel.SetDock(_Count, Dock.Right);
            filterRow.Children.Add(_Count);
            filterRow.Children.Add(filter);
            StackPanel top = Ui.Stack(10, heading, intro, filterRow);
            top.Margin = new Thickness(0, 0, 0, 12);

            Button close = Ui.Primary("Close", () => CloseWith(null));
            close.IsCancel = true;
            close.IsDefault = true;
            close.HorizontalAlignment = HorizontalAlignment.Right;
            close.Margin = new Thickness(0, 12, 0, 0);

            Border listFrame = new Border
            {
                Background = palette.Panel,
                BorderBrush = palette.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Child = new ScrollViewer { Content = _List, Padding = new Thickness(0, 0, 12, 0) }
            };

            DockPanel root = new DockPanel { Margin = new Thickness(20) };
            DockPanel.SetDock(top, Dock.Top);
            DockPanel.SetDock(close, Dock.Bottom);
            root.Children.Add(top);
            root.Children.Add(close);
            root.Children.Add(listFrame);
            Content = root;
            Render(null);
        }

        #endregion

        #region Private-Methods

        private void Render(string? filter)
        {
            XenoPalette palette = XenoTheme.Current;
            string text = (filter ?? String.Empty).Trim();
            List<ChatToolInfo> shown = _Tools.Where(tool => text.Length == 0
                || tool.Name.Contains(text, StringComparison.OrdinalIgnoreCase)
                || tool.Description.Contains(text, StringComparison.OrdinalIgnoreCase)
                || tool.Group.Contains(text, StringComparison.OrdinalIgnoreCase)).ToList();
            int asking = shown.Count(tool => tool.Mutating);
            _Count.Text = shown.Count + " of " + _Tools.Count + " tools, " + asking + " ask first";
            _List.Children.Clear();
            if (shown.Count == 0)
            {
                _List.Children.Add(new TextBlock { Text = _Tools.Count == 0 ? "No tools are available." : "No tools match the filter.", Foreground = palette.Muted, Margin = new Thickness(16) });
                return;
            }

            // Groups in the order the client lists them; within a group, read-only tools first, then by name.
            foreach (IGrouping<string, ChatToolInfo> group in shown.GroupBy(tool => String.IsNullOrWhiteSpace(tool.Group) ? "Other" : tool.Group))
            {
                Border header = new Border
                {
                    Background = palette.PanelSoft,
                    BorderBrush = palette.Border,
                    BorderThickness = new Thickness(0, 0, 0, 1),
                    Padding = new Thickness(16, 8),
                    Child = new TextBlock
                    {
                        Text = group.Key.ToUpperInvariant() + "  ·  " + group.Count(),
                        FontSize = 11.5,
                        FontWeight = FontWeight.SemiBold,
                        Foreground = palette.Muted
                    }
                };
                _List.Children.Add(header);
                foreach (ChatToolInfo tool in group.OrderBy(item => item.Mutating).ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
                {
                    _List.Children.Add(Row(tool, palette));
                }
            }
        }

        private static Control Row(ChatToolInfo tool, XenoPalette palette)
        {
            Border badge = Ui.Badge(tool.Mutating ? "Asks first" : "Read-only", tool.Mutating ? XenoToneEnum.Warning : XenoToneEnum.Success);
            badge.VerticalAlignment = VerticalAlignment.Center;
            DockPanel title = new DockPanel();
            DockPanel.SetDock(badge, Dock.Right);
            title.Children.Add(badge);
            title.Children.Add(new SelectableTextBlock { Text = tool.Name, FontFamily = Ui.Mono, FontWeight = FontWeight.SemiBold, Foreground = palette.Text, VerticalAlignment = VerticalAlignment.Center });
            StackPanel body = new StackPanel { Spacing = 4 };
            body.Children.Add(title);
            if (!String.IsNullOrWhiteSpace(tool.Description))
            {
                body.Children.Add(new SelectableTextBlock { Text = tool.Description.Trim(), TextWrapping = TextWrapping.Wrap, Foreground = palette.Muted, LineHeight = 19 });
            }

            return new Border
            {
                BorderBrush = palette.Border,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(16, 10),
                Child = body
            };
        }

        #endregion
    }
}

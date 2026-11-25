namespace Xeno.Desktop.Common
{
    using System;
    using System.Collections.Generic;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Layout;
    using Avalonia.Media;

    /// <summary>
    /// Layout helpers for code-built Avalonia views, styled with the active <see cref="XenoTheme"/> palette.
    /// Views rebuild themselves when the palette changes, so these helpers read the palette at call time.
    /// </summary>
    public static class Ui
    {
        #region Public-Members

        /// <summary>
        /// Monospace font family used for identifiers, URLs, and logs.
        /// </summary>
        public static readonly FontFamily Mono = new FontFamily(XenoFonts.MonoFamilyName);

        #endregion

        #region Public-Methods

        /// <summary>
        /// Page title.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <returns>Text block.</returns>
        public static TextBlock Title(string text)
        {
            return new TextBlock { Text = text, FontSize = 22, FontWeight = FontWeight.SemiBold, Foreground = XenoTheme.Current.Text };
        }

        /// <summary>
        /// Section heading.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <returns>Text block.</returns>
        public static TextBlock Heading(string text)
        {
            return new TextBlock { Text = text, FontSize = 15, FontWeight = FontWeight.SemiBold, Foreground = XenoTheme.Current.Text };
        }

        /// <summary>
        /// Body text that wraps.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <returns>Text block.</returns>
        public static TextBlock Body(string text)
        {
            return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = XenoTheme.Current.Text };
        }

        /// <summary>
        /// Secondary text that wraps.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <returns>Text block.</returns>
        public static TextBlock Muted(string text)
        {
            return new TextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = XenoTheme.Current.Muted, FontSize = 12.5 };
        }

        /// <summary>
        /// Selectable monospace text, for identifiers and URLs.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <returns>Selectable text block.</returns>
        public static SelectableTextBlock Code(string text)
        {
            return new SelectableTextBlock { Text = text, FontFamily = Mono, FontSize = 12.5, Foreground = XenoTheme.Current.Text, TextWrapping = TextWrapping.Wrap };
        }

        /// <summary>
        /// A card: rounded panel with an optional title, header actions, and body.
        /// </summary>
        /// <param name="title">Title, or null.</param>
        /// <param name="body">Body.</param>
        /// <param name="actions">Controls placed at the right of the title.</param>
        /// <returns>Card.</returns>
        public static Border Card(string? title, Control body, params Control[] actions)
        {
            XenoPalette palette = XenoTheme.Current;
            StackPanel content = new StackPanel { Spacing = 10 };
            if (!String.IsNullOrWhiteSpace(title) || actions.Length > 0)
            {
                DockPanel header = new DockPanel { LastChildFill = true };
                StackPanel actionPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                foreach (Control action in actions) actionPanel.Children.Add(Adopt(action));
                DockPanel.SetDock(actionPanel, Dock.Right);
                header.Children.Add(actionPanel);
                header.Children.Add(Heading(title ?? String.Empty));
                content.Children.Add(header);
            }

            content.Children.Add(Adopt(body));
            return new Border
            {
                Background = palette.Panel,
                BorderBrush = palette.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16),
                Child = content
            };
        }

        /// <summary>
        /// A metric tile: label, large value, and caption.
        /// </summary>
        /// <param name="label">Label.</param>
        /// <param name="value">Value.</param>
        /// <param name="caption">Caption, or null.</param>
        /// <param name="valueBrush">Value color, or null for the text color.</param>
        /// <returns>Tile.</returns>
        public static Border Metric(string label, string value, string? caption, IBrush? valueBrush = null)
        {
            XenoPalette palette = XenoTheme.Current;
            StackPanel stack = new StackPanel { Spacing = 4 };
            stack.Children.Add(new TextBlock { Text = label.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeight.SemiBold, Foreground = palette.Muted });
            stack.Children.Add(new TextBlock { Text = value, FontSize = 24, FontWeight = FontWeight.SemiBold, Foreground = valueBrush ?? palette.Text });
            if (!String.IsNullOrWhiteSpace(caption)) stack.Children.Add(Muted(caption));
            return new Border
            {
                Background = palette.Panel,
                BorderBrush = palette.Border,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 12),
                MinWidth = 160,
                Child = stack
            };
        }

        /// <summary>
        /// A rounded badge.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="tone">Tone.</param>
        /// <returns>Badge.</returns>
        public static Border Badge(string text, XenoToneEnum tone)
        {
            XenoPalette palette = XenoTheme.Current;
            IBrush foreground = palette.Muted;
            IBrush background = palette.PanelSoft;
            switch (tone)
            {
                case XenoToneEnum.Success: foreground = palette.Success; background = palette.SuccessSoft; break;
                case XenoToneEnum.Warning: foreground = palette.Warning; background = palette.WarningSoft; break;
                case XenoToneEnum.Danger: foreground = palette.Danger; background = palette.DangerSoft; break;
                case XenoToneEnum.Info: foreground = palette.Info; background = palette.InfoSoft; break;
            }

            return new Border
            {
                Background = background,
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(8, 2),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Child = new TextBlock { Text = text, FontSize = 11.5, FontWeight = FontWeight.SemiBold, Foreground = foreground }
            };
        }

        /// <summary>
        /// A two-column label and value grid.
        /// </summary>
        /// <param name="rows">Label and value pairs. Values are selectable.</param>
        /// <param name="labelWidth">Label column width.</param>
        /// <returns>Grid.</returns>
        public static Grid Details(IEnumerable<KeyValuePair<string, string>> rows, double labelWidth = 170)
        {
            Grid grid = new Grid { ColumnDefinitions = new ColumnDefinitions(labelWidth.ToString(System.Globalization.CultureInfo.InvariantCulture) + ",*") };
            int row = 0;
            foreach (KeyValuePair<string, string> pair in rows)
            {
                grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                TextBlock label = new TextBlock { Text = pair.Key, Foreground = XenoTheme.Current.Muted, Margin = new Thickness(0, 3, 12, 3), VerticalAlignment = VerticalAlignment.Top };
                SelectableTextBlock value = new SelectableTextBlock { Text = pair.Value, Foreground = XenoTheme.Current.Text, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 3) };
                Grid.SetRow(label, row);
                Grid.SetRow(value, row);
                Grid.SetColumn(value, 1);
                grid.Children.Add(label);
                grid.Children.Add(value);
                row++;
            }

            return grid;
        }

        /// <summary>
        /// A standard button.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="onClick">Click handler.</param>
        /// <param name="tip">Tooltip, or null.</param>
        /// <returns>Button.</returns>
        public static Button Button(string text, Action onClick, string? tip = null)
        {
            Button button = new Button { Content = text, Padding = new Thickness(12, 5), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            button.Click += (sender, args) => onClick();
            if (!String.IsNullOrWhiteSpace(tip)) ToolTip.SetTip(button, tip);
            return button;
        }

        /// <summary>
        /// Take a control out of its current parent so it can be placed again. Pages keep some controls (charts, lists,
        /// inputs) across rebuilds, and a page is rebuilt each time it is shown and when the theme changes; a control
        /// still attached to the previous layout cannot be added to the new one.
        /// </summary>
        /// <typeparam name="T">Control type.</typeparam>
        /// <param name="control">Control.</param>
        /// <returns>The same control, without a parent.</returns>
        public static T Adopt<T>(T control) where T : Control
        {
            ArgumentNullException.ThrowIfNull(control);
            switch (control.Parent)
            {
                case Panel panel:
                    panel.Children.Remove(control);
                    break;
                case Decorator decorator when Object.ReferenceEquals(decorator.Child, control):
                    decorator.Child = null;
                    break;
                case ContentControl content when Object.ReferenceEquals(content.Content, control):
                    content.Content = null;
                    break;
            }

            return control;
        }

        /// <summary>
        /// Outline of the copy icon: two overlapping rounded rectangles, drawn in a 24 x 24 box.
        /// </summary>
        public const string CopyIconPath = "M9 9 H19 A2 2 0 0 1 21 11 V19 A2 2 0 0 1 19 21 H11 A2 2 0 0 1 9 19 Z M15 9 V5 A2 2 0 0 0 13 3 H5 A2 2 0 0 0 3 5 V13 A2 2 0 0 0 5 15 H9";

        /// <summary>
        /// A button that shows only an outline icon. The tooltip doubles as the accessible name.
        /// </summary>
        /// <param name="pathData">Icon outline in a 24 x 24 box, as path markup.</param>
        /// <param name="tip">Tooltip and accessible name, for example "Copy answer".</param>
        /// <param name="onClick">Click handler.</param>
        /// <returns>Button.</returns>
        public static Button IconButton(string pathData, string tip, Action onClick)
        {
            Avalonia.Controls.Shapes.Path icon = new Avalonia.Controls.Shapes.Path
            {
                Data = Geometry.Parse(pathData),
                Stroke = XenoTheme.Current.Text,
                StrokeThickness = 1.8,
                StrokeLineCap = PenLineCap.Round,
                StrokeJoin = PenLineJoin.Round,
                Stretch = Stretch.Uniform,
                Width = 15,
                Height = 15
            };
            Button button = new Button { Content = icon, Padding = new Thickness(8, 6), HorizontalContentAlignment = HorizontalAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            button.Click += (sender, args) => onClick();
            ToolTip.SetTip(button, tip);
            Avalonia.Automation.AutomationProperties.SetName(button, tip);
            return button;
        }

        /// <summary>
        /// A copy button: the copy icon only, with a tooltip saying what it copies.
        /// </summary>
        /// <param name="tip">Tooltip and accessible name, for example "Copy answer".</param>
        /// <param name="onClick">Click handler.</param>
        /// <returns>Button.</returns>
        public static Button CopyButton(string tip, Action onClick)
        {
            return IconButton(CopyIconPath, tip, onClick);
        }

        /// <summary>
        /// A primary (accent) button.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="onClick">Click handler.</param>
        /// <param name="tip">Tooltip, or null.</param>
        /// <returns>Button.</returns>
        public static Button Primary(string text, Action onClick, string? tip = null)
        {
            Button button = Button(text, onClick, tip);
            button.Classes.Add("accent");
            return button;
        }

        /// <summary>
        /// A horizontal row of controls.
        /// </summary>
        /// <param name="controls">Controls.</param>
        /// <returns>Panel.</returns>
        public static StackPanel Row(params Control[] controls)
        {
            StackPanel panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
            foreach (Control control in controls) panel.Children.Add(Adopt(control));
            return panel;
        }

        /// <summary>
        /// A wrapping row of controls.
        /// </summary>
        /// <param name="controls">Controls.</param>
        /// <returns>Panel.</returns>
        public static WrapPanel Wrap(params Control[] controls)
        {
            WrapPanel panel = new WrapPanel { Orientation = Orientation.Horizontal };
            foreach (Control control in controls)
            {
                control.Margin = new Thickness(0, 0, 12, 12);
                panel.Children.Add(Adopt(control));
            }

            return panel;
        }

        /// <summary>
        /// A vertical stack of controls.
        /// </summary>
        /// <param name="spacing">Spacing.</param>
        /// <param name="controls">Controls.</param>
        /// <returns>Panel.</returns>
        public static StackPanel Stack(double spacing, params Control[] controls)
        {
            StackPanel panel = new StackPanel { Spacing = spacing };
            foreach (Control control in controls) panel.Children.Add(Adopt(control));
            return panel;
        }

        /// <summary>
        /// A labeled form field.
        /// </summary>
        /// <param name="label">Label.</param>
        /// <param name="input">Input control.</param>
        /// <param name="help">Help text, or null.</param>
        /// <returns>Panel.</returns>
        public static StackPanel Field(string label, Control input, string? help = null)
        {
            StackPanel panel = new StackPanel { Spacing = 4 };
            panel.Children.Add(new TextBlock { Text = label, FontWeight = FontWeight.SemiBold, Foreground = XenoTheme.Current.Text, FontSize = 12.5 });
            panel.Children.Add(Adopt(input));
            if (!String.IsNullOrWhiteSpace(help)) panel.Children.Add(Muted(help));
            return panel;
        }

        /// <summary>
        /// A notice strip.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="tone">Tone.</param>
        /// <returns>Notice.</returns>
        public static Border Notice(string text, XenoToneEnum tone)
        {
            XenoPalette palette = XenoTheme.Current;
            IBrush foreground = palette.Info;
            IBrush background = palette.InfoSoft;
            if (tone == XenoToneEnum.Success) { foreground = palette.Success; background = palette.SuccessSoft; }
            if (tone == XenoToneEnum.Warning) { foreground = palette.Warning; background = palette.WarningSoft; }
            if (tone == XenoToneEnum.Danger) { foreground = palette.Danger; background = palette.DangerSoft; }
            return new Border
            {
                Background = background,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8),
                Child = new SelectableTextBlock { Text = text, TextWrapping = TextWrapping.Wrap, Foreground = foreground }
            };
        }

        #endregion
    }
}

namespace Xeno.Desktop.Controls
{
    using System;
    using System.Collections.Generic;
    using Avalonia;
    using Avalonia.Automation;
    using Avalonia.Controls;
    using Avalonia.Input;
    using Avalonia.Layout;
    using Avalonia.Media;
    using Xeno.Client.Presentation;
    using Xeno.Desktop.Common;

    /// <summary>
    /// A color input: a swatch that opens a grid of colors to pick from, and a hex box for an exact value. Calls back
    /// with a #rrggbb value when a swatch is picked, or when the hex box is committed with Enter or by leaving it.
    /// </summary>
    public sealed class ColorField : StackPanel
    {
        #region Public-Members

        /// <summary>
        /// Current value, #rrggbb.
        /// </summary>
        public string Value { get; private set; }

        /// <summary>
        /// The hex box, for tests and focus.
        /// </summary>
        public TextBox HexBox { get; }

        #endregion

        #region Private-Members

        private readonly Action<string> _Commit;
        private readonly Border _Swatch;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the field.
        /// </summary>
        /// <param name="value">Starting color.</param>
        /// <param name="label">Accessible name, for example "Light background".</param>
        /// <param name="commit">Called with the new color.</param>
        /// <exception cref="ArgumentNullException">commit is null.</exception>
        public ColorField(string value, string label, Action<string> commit)
        {
            _Commit = commit ?? throw new ArgumentNullException(nameof(commit));
            Value = XenoRgb.TryParse(value)?.ToHex() ?? "#000000";
            Orientation = Orientation.Horizontal;
            Spacing = 6;
            XenoPalette palette = XenoTheme.Current;
            _Swatch = new Border { Width = 22, Height = 22, CornerRadius = new CornerRadius(4), BorderBrush = palette.Border, BorderThickness = new Thickness(1), Background = new SolidColorBrush(Color.Parse(Value)) };
            Button pick = new Button { Content = _Swatch, Padding = new Thickness(3) };
            AutomationProperties.SetName(pick, "Pick " + label);
            ToolTip.SetTip(pick, "Pick a color");
            Flyout flyout = new Flyout();
            flyout.Content = BuildGrid(hex =>
            {
                flyout.Hide();
                Set(hex);
            });
            pick.Flyout = flyout;

            HexBox = new TextBox { Text = Value, Width = 92, FontFamily = Ui.Mono };
            AutomationProperties.SetName(HexBox, label + " hex value");
            HexBox.KeyDown += (sender, args) =>
            {
                if (args.Key != Key.Enter) return;
                args.Handled = true;
                CommitText();
            };
            HexBox.LostFocus += (sender, args) => CommitText();
            Children.Add(pick);
            Children.Add(HexBox);
        }

        #endregion

        #region Private-Methods

        private void CommitText()
        {
            XenoRgb? parsed = XenoRgb.TryParse(HexBox.Text);
            if (parsed == null)
            {
                HexBox.BorderBrush = XenoTheme.Current.Danger;
                ToolTip.SetTip(HexBox, "Use a hex color such as #4a2054.");
                return;
            }

            HexBox.ClearValue(TextBox.BorderBrushProperty);
            ToolTip.SetTip(HexBox, null);
            Set(parsed.ToHex());
        }

        private void Set(string hex)
        {
            HexBox.Text = hex;
            _Swatch.Background = new SolidColorBrush(Color.Parse(hex));
            if (String.Equals(hex, Value, StringComparison.OrdinalIgnoreCase)) return;
            Value = hex;
            _Commit(hex);
        }

        private static Control BuildGrid(Action<string> picked)
        {
            StackPanel rows = new StackPanel { Spacing = 3 };
            foreach (List<string> row in Swatches())
            {
                StackPanel line = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };
                foreach (string hex in row)
                {
                    string value = hex;
                    Button button = new Button
                    {
                        Padding = new Thickness(0),
                        Width = 22,
                        Height = 22,
                        MinWidth = 0,
                        MinHeight = 0,
                        Background = new SolidColorBrush(Color.Parse(hex)),
                        BorderBrush = XenoTheme.Current.Border,
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(3)
                    };
                    ToolTip.SetTip(button, hex);
                    AutomationProperties.SetName(button, hex);
                    button.Click += (sender, args) => picked(value);
                    line.Children.Add(button);
                }

                rows.Children.Add(line);
            }

            return new Border { Padding = new Thickness(4), Child = rows };
        }

        private static List<List<string>> Swatches()
        {
            // Twelve hues at seven lightness levels, then a row of grays.
            List<List<string>> rows = new List<List<string>>();
            foreach (double lightness in new[] { 0.92, 0.82, 0.68, 0.54, 0.42, 0.3, 0.18 })
            {
                List<string> row = new List<string>();
                for (int hue = 0; hue < 360; hue += 30) row.Add(Hsl(hue, lightness > 0.85 ? 0.6 : 0.68, lightness));
                rows.Add(row);
            }

            List<string> grays = new List<string>();
            for (int i = 0; i < 12; i++) grays.Add(Hsl(0, 0, 1.0 - i / 11.0));
            rows.Add(grays);
            return rows;
        }

        private static string Hsl(double hue, double saturation, double lightness)
        {
            double c = (1 - Math.Abs(2 * lightness - 1)) * saturation;
            double h = hue / 60.0;
            double x = c * (1 - Math.Abs(h % 2 - 1));
            double r = 0;
            double g = 0;
            double b = 0;
            if (h < 1) { r = c; g = x; }
            else if (h < 2) { r = x; g = c; }
            else if (h < 3) { g = c; b = x; }
            else if (h < 4) { g = x; b = c; }
            else if (h < 5) { r = x; b = c; }
            else { r = c; b = x; }
            double m = lightness - c / 2;
            return new XenoRgb((int)Math.Round((r + m) * 255), (int)Math.Round((g + m) * 255), (int)Math.Round((b + m) * 255)).ToHex();
        }

        #endregion
    }
}

namespace Xeno.Tray
{
    using Avalonia.Controls;
    using Avalonia.Layout;
    using Avalonia.Media;

    /// <summary>
    /// Simple statistics window opened from the tray.
    /// </summary>
    public sealed class StatisticsWindow : Window
    {
        private readonly TextBlock _StatisticsText;

        /// <summary>
        /// Instantiate the statistics window.
        /// </summary>
        public StatisticsWindow()
        {
            Title = "Xeno Client Statistics";
            Width = 520;
            Height = 420;
            MinWidth = 420;
            MinHeight = 320;
            _StatisticsText = new TextBlock
            {
                FontFamily = FontFamily.Parse("Consolas, Cascadia Mono, Menlo, monospace"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            Button closeButton = new Button
            {
                Content = "Close",
                HorizontalAlignment = HorizontalAlignment.Right
            };
            closeButton.Click += delegate
            {
                Close();
            };
            Content = new DockPanel
            {
                Margin = new Avalonia.Thickness(16),
                Children =
                {
                    new ScrollViewer { Content = _StatisticsText },
                    closeButton
                }
            };
            DockPanel.SetDock(closeButton, Dock.Bottom);
        }

        /// <summary>
        /// Set statistics display text.
        /// </summary>
        /// <param name="text">Display text.</param>
        public void SetStatistics(string text)
        {
            _StatisticsText.Text = text;
        }
    }
}

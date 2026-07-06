namespace Xeno.Tray
{
    using Avalonia.Controls;
    using Avalonia.Controls.Primitives;
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
            SizeToContent = SizeToContent.WidthAndHeight;
            MinWidth = 320;
            MinHeight = 180;
            MaxWidth = 760;
            MaxHeight = 560;
            _StatisticsText = new TextBlock
            {
                FontFamily = FontFamily.Parse("Consolas, Cascadia Mono, Menlo, monospace"),
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 728
            };

            Content = new ScrollViewer
            {
                Margin = new Avalonia.Thickness(16),
                Content = _StatisticsText,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                MaxHeight = 528,
                MaxWidth = 728,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            };
        }

        /// <summary>
        /// Set statistics display text.
        /// </summary>
        /// <param name="text">Display text.</param>
        public void SetStatistics(string text)
        {
            _StatisticsText.Text = text;
            SizeToContent = SizeToContent.WidthAndHeight;
        }
    }
}

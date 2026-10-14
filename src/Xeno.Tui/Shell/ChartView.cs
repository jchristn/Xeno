namespace Xeno.Tui.Shell
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TUIKit;
    using TUIKit.Theming;
    using TUIKit.Widgets;
    using Xeno.Client.Presentation;

    /// <summary>
    /// Draws a shared <see cref="XenoChart"/>: a heading with the range and a legend on the first row, then the columns
    /// with their scale and time labels, or the chart's empty-state text when there is no data. Mutate on the loop
    /// thread.
    /// </summary>
    internal sealed class ChartView : IWidget, IThemeable
    {
        #region Public-Members

        /// <summary>
        /// Chart shown, or null before the first load.
        /// </summary>
        public XenoChart? Chart
        {
            get { return _Chart; }
            set
            {
                _Chart = value;
                _RenderedWidth = -1;
            }
        }

        /// <summary>
        /// Text shown before the first load.
        /// </summary>
        public string LoadingText { get; set; } = "Loading...";

        #endregion

        #region Private-Members

        private readonly ColumnChart _Columns = new ColumnChart { ShowLegend = false, ShowScale = true, Stacked = true };
        private XenoChart? _Chart = null;
        private XenoChart? _Fitted = null;
        private Theme _Theme = XenoTuiTheme.Dark;
        private int _RenderedWidth = -1;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Measure: the whole available area.
        /// </summary>
        /// <param name="available">Available size.</param>
        /// <returns>Desired size.</returns>
        public Size Measure(Size available)
        {
            return available;
        }

        /// <summary>
        /// Render the heading, legend, and columns, or the empty state.
        /// </summary>
        /// <param name="surface">Surface.</param>
        public void Render(ISurface surface)
        {
            int width = surface.Size.Width;
            int height = surface.Size.Height;
            if (width <= 0 || height <= 0) return;
            XenoChart? chart = _Chart;
            if (chart == null)
            {
                surface.DrawText(0, 0, Fit(LoadingText, width), _Theme.Muted);
                return;
            }

            if (_RenderedWidth != width) Bind(chart, width);
            RenderHeading(surface, _Fitted ?? chart, width);
            if (height < 2) return;
            if (!chart.HasData)
            {
                int row = 1 + Math.Max(0, (height - 1) / 2 - 1);
                foreach (string line in Wrap(chart.EmptyText, width))
                {
                    if (row >= height) break;
                    surface.DrawText(0, row++, line, _Theme.Muted);
                }

                return;
            }

            _Columns.Render(new SurfaceView(surface, new Rect(0, 1, width, height - 1)));
        }

        /// <summary>
        /// Apply a theme.
        /// </summary>
        /// <param name="theme">Theme.</param>
        public void ApplyTheme(Theme theme)
        {
            _Theme = theme ?? XenoTuiTheme.Dark;
            _Columns.LabelStyle = _Theme.Muted;
            _RenderedWidth = -1;
        }

        /// <summary>
        /// Plain text of what the chart shows, for tests: the heading, then the empty text or the bucket count.
        /// </summary>
        /// <returns>Description.</returns>
        public string Describe()
        {
            XenoChart? chart = _Chart;
            if (chart == null) return LoadingText;
            return chart.Heading + ": " + (chart.HasData ? chart.BucketCount + " buckets, peak " + chart.FormatValue(chart.Maximum) : chart.EmptyText);
        }

        /// <summary>
        /// Terminal color for a chart tone.
        /// </summary>
        /// <param name="theme">Theme.</param>
        /// <param name="tone">Tone.</param>
        /// <returns>Color.</returns>
        public static Color ToneColor(Theme theme, XenoChartToneEnum tone)
        {
            ArgumentNullException.ThrowIfNull(theme);
            switch (tone)
            {
                case XenoChartToneEnum.Success: return theme.Success.Foreground;
                case XenoChartToneEnum.Danger: return theme.Error.Foreground;
                case XenoChartToneEnum.Info: return theme.Info.Foreground;
                case XenoChartToneEnum.Warning: return theme.Warning.Foreground;
                default: return theme.Accent.Foreground;
            }
        }

        #endregion

        #region Private-Methods

        private void RenderHeading(ISurface surface, XenoChart chart, int width)
        {
            string heading = chart.Heading;
            surface.DrawText(0, 0, Fit(heading, width), _Theme.Text.WithAttribute(CellAttributes.Bold, true));
            List<KeyValuePair<string, CellStyle>> parts = new List<KeyValuePair<string, CellStyle>>();
            if (chart.HasData) parts.Add(new KeyValuePair<string, CellStyle>("peak " + chart.FormatValue(chart.Maximum) + "   ", _Theme.Muted));
            foreach (XenoChartSeries series in chart.Series)
            {
                parts.Add(new KeyValuePair<string, CellStyle>("■ ", _Theme.Text.WithForeground(ToneColor(_Theme, series.Tone))));
                parts.Add(new KeyValuePair<string, CellStyle>(series.Name + "  ", _Theme.Muted));
            }

            int legendWidth = parts.Sum(item => item.Key.Length) - 2;
            int x = width - legendWidth;
            if (x <= heading.Length + 2) return;
            foreach (KeyValuePair<string, CellStyle> part in parts)
            {
                surface.DrawText(x, 0, part.Key, part.Value);
                x += part.Key.Length;
            }
        }

        private void Bind(XenoChart chart, int width)
        {
            XenoChart fitted = chart.Downsample(Math.Max(1, (width - 8) / 2));
            _Fitted = fitted;
            _Columns.Clear();
            _Columns.ValueFormatter = value => fitted.FormatValue(value);
            _Columns.LabelStyle = _Theme.Muted;
            foreach (XenoChartSeries series in fitted.Series)
            {
                _Columns.AddSeries(series.Name, series.Values, ToneColor(_Theme, series.Tone), null);
            }

            _Columns.Labels.AddRange(fitted.Labels);
            _RenderedWidth = width;
        }

        private static string Fit(string text, int width)
        {
            if (width <= 0) return String.Empty;
            if (text.Length <= width) return text;
            return width <= 1 ? text.Substring(0, width) : text.Substring(0, width - 1) + "…";
        }

        private static List<string> Wrap(string text, int width)
        {
            List<string> lines = new List<string>();
            if (String.IsNullOrWhiteSpace(text) || width <= 0) return lines;
            string current = String.Empty;
            foreach (string word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                if (current.Length > 0 && current.Length + 1 + word.Length > width)
                {
                    lines.Add(current);
                    current = word;
                }
                else
                {
                    current = current.Length == 0 ? word : current + " " + word;
                }
            }

            if (current.Length > 0) lines.Add(Fit(current, width));
            return lines;
        }

        #endregion
    }
}

namespace Xeno.Tui.Shell
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Theming;
    using TUIKit.Widgets;
    using Xeno.Client.Presentation;

    /// <summary>
    /// Draws any catalog <see cref="XenoChart"/> in character cells: stacked columns, lines (one marker per series),
    /// horizontal bars, heatmaps (shaded cells), and status strips (one glyph per status). Time charts have a cursor,
    /// moved with the left and right arrow keys or a click; the bottom row shows that bucket's values, and Enter or a
    /// double click raises <see cref="BucketActivated"/> to drill down. Mutate on the loop thread.
    /// </summary>
    internal sealed class UsageChartView : IWidget, IFocusable, IFocusAware, IMouseAware, IKeyHintSource, IThemeable
    {
        #region Public-Members

        /// <summary>
        /// Marker per line series, in series order (median, 95th, 99th, slowest).
        /// </summary>
        public static readonly IReadOnlyList<char> LineMarkers = new List<char> { '●', '○', '◆', '×' };

        /// <summary>
        /// Shades for heatmap cells, from lightest to darkest.
        /// </summary>
        public static readonly IReadOnlyList<char> Shades = new List<char> { '░', '▒', '▓', '█' };

        /// <summary>
        /// Chart shown, or null before the first load.
        /// </summary>
        public XenoChart? Chart
        {
            get { return _Chart; }
            set
            {
                _Chart = value;
                _Fitted = null;
                _RenderedWidth = -1;
                if (_Cursor.HasValue && value != null && _Cursor.Value >= value.BucketCount) _Cursor = null;
            }
        }

        /// <summary>
        /// Text shown before the first load.
        /// </summary>
        public string LoadingText { get; set; } = "Loading...";

        /// <summary>
        /// Raised when the user drills into a bucket: its window and, for group charts, the series under the cursor.
        /// </summary>
        public event Action<XenoChartBucket, string?>? BucketActivated;

        /// <summary>
        /// Cursor bucket in the drawn (possibly combined) chart, or null.
        /// </summary>
        public int? Cursor
        {
            get { return _Cursor; }
        }

        #endregion

        #region Private-Members

        private const int ScaleWidth = 8;
        private XenoChart? _Chart = null;
        private XenoChart? _Fitted = null;
        private Theme _Theme = XenoTuiTheme.Dark;
        private int _RenderedWidth = -1;
        private int? _Cursor = null;
        private bool _Focused = false;
        private int _PlotLeft = ScaleWidth;
        private int _PlotTop = 1;
        private int _PlotHeight = 0;
        private int _ColumnWidth = 1;
        private readonly HeatMap _HeatMap = new HeatMap
        {
            IsSelectable = true,
            LevelCount = 5,
            LevelGlyphs = new List<string> { "·", "░", "▒", "▓", "█" },
            ShowLegend = true,
            ShowSelectionDescription = true,
            UseThemeStyles = true
        };
        private XenoChart? _HeatSource = null;
        private int _HeatSkip = -1;
        private int _HeatTop = 1;
        private readonly List<List<KeyValuePair<string, int>>> _Segments = new List<List<KeyValuePair<string, int>>>();

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
        /// Render.
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

            XenoChart fitted = Fitted(chart, width);
            RenderHeading(surface, fitted, width);
            int row = 1;
            if (fitted.Notice != null && height > 3)
            {
                surface.DrawText(0, row++, Fit("! " + fitted.Notice, width), _Theme.Warning);
            }

            if (!fitted.HasData)
            {
                int line = row + Math.Max(0, (height - row) / 2 - 1);
                foreach (string text in Wrap(fitted.EmptyText, width))
                {
                    if (line >= height) break;
                    surface.DrawText(0, line++, text, _Theme.Muted);
                }

                return;
            }

            int available = height - row;
            switch (fitted.Kind)
            {
                case XenoChartKindEnum.HorizontalBars:
                    RenderBars(surface, fitted, row, available, width);
                    break;
                case XenoChartKindEnum.Heatmap:
                    RenderHeatMap(surface, fitted, row, available, width);
                    break;
                case XenoChartKindEnum.StatusStrip:
                    RenderGrid(surface, fitted, row, available, width, true);
                    break;
                default:
                    RenderTime(surface, fitted, row, available, width);
                    break;
            }
        }

        /// <summary>
        /// Plain text of what the chart shows, for tests.
        /// </summary>
        /// <returns>Description.</returns>
        public string Describe()
        {
            XenoChart? chart = _Chart;
            if (chart == null) return LoadingText;
            return chart.Kind + " " + chart.Heading + ": " + (chart.HasData ? chart.BucketCount + " buckets" : chart.EmptyText);
        }

        /// <summary>
        /// Text for the bucket under the cursor, for example "14:15  Median 1.1 s  95th percentile 3.9 s".
        /// </summary>
        /// <returns>Text, or empty without a cursor.</returns>
        public string CursorText()
        {
            XenoChart? chart = _Fitted ?? _Chart;
            if (chart == null || !_Cursor.HasValue || _Cursor.Value >= chart.BucketCount) return String.Empty;
            int index = _Cursor.Value;
            List<string> parts = new List<string> { index < chart.Labels.Count ? chart.Labels[index] : String.Empty };
            foreach (XenoChartSeries series in chart.Series)
            {
                if (index < series.Values.Count) parts.Add(series.Name + " " + chart.FormatValue(series.Values[index]));
            }

            return String.Join("  ", parts.Where(item => item.Length > 0));
        }

        /// <summary>
        /// Handle keys: left and right move the cursor, Home and End jump, Enter drills down.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True when handled.</returns>
        public bool HandleKey(KeyEvent key)
        {
            if (IsHeatMap()) return _HeatMap.HandleKey(key);
            XenoChart? chart = _Fitted ?? _Chart;
            if (chart == null || !IsTimeChart(chart) || chart.BucketCount == 0) return false;
            int last = chart.BucketCount - 1;
            switch (key.Code)
            {
                case KeyCode.Left:
                    _Cursor = Math.Max(0, (_Cursor ?? last + 1) - 1);
                    return true;
                case KeyCode.Right:
                    _Cursor = Math.Min(last, (_Cursor ?? -1) + 1);
                    return true;
                case KeyCode.Home:
                    _Cursor = 0;
                    return true;
                case KeyCode.End:
                    _Cursor = last;
                    return true;
                case KeyCode.Enter:
                    if (!_Cursor.HasValue) _Cursor = last;
                    Activate(chart, _Cursor.Value, null);
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Handle a click: move the cursor to the clicked bucket; a double click (or a click on the selected bucket)
        /// drills down.
        /// </summary>
        /// <param name="mouse">Mouse event.</param>
        /// <returns>True when handled.</returns>
        public bool HandleMouse(MouseEvent mouse)
        {
            if (IsHeatMap())
            {
                if (mouse.Y < _HeatTop) return false;
                return _HeatMap.HandleMouse(new MouseEvent(mouse.Kind, mouse.Button, mouse.X, mouse.Y - _HeatTop, mouse.Modifiers, mouse.ClickCount));
            }

            if (mouse.Kind != MouseEventKind.Press || mouse.Button != MouseButton.Left) return false;
            XenoChart? chart = _Fitted;
            if (chart == null || !IsTimeChart(chart) || mouse.X < _PlotLeft) return false;
            int index = (mouse.X - _PlotLeft) / Math.Max(1, _ColumnWidth);
            if (index < 0 || index >= chart.BucketCount) return false;
            bool again = _Cursor == index;
            _Cursor = index;
            if (mouse.ClickCount >= 2 || again) Activate(chart, index, SeriesAt(index, mouse.Y));
            return true;
        }

        /// <summary>
        /// Track focus.
        /// </summary>
        /// <param name="focused">True when focused.</param>
        public void OnFocusChanged(bool focused)
        {
            _Focused = focused;
            _HeatMap.OnFocusChanged(focused);
        }

        /// <summary>
        /// Key hints.
        /// </summary>
        /// <returns>Hints.</returns>
        public IReadOnlyList<KeyHint>? GetKeyHints()
        {
            if (IsHeatMap()) return _HeatMap.GetKeyHints();
            XenoChart? chart = _Fitted ?? _Chart;
            if (chart == null || !IsTimeChart(chart)) return null;
            return new List<KeyHint> { new KeyHint("←→", "Bucket"), new KeyHint("Enter", "Show requests") };
        }

        /// <summary>
        /// Apply a theme.
        /// </summary>
        /// <param name="theme">Theme.</param>
        public void ApplyTheme(Theme theme)
        {
            _Theme = theme ?? XenoTuiTheme.Dark;
            _HeatMap.ApplyTheme(_Theme);
            _HeatMap.Palette = XenoTuiTheme.HeatPalette(_Theme);
        }

        /// <summary>
        /// Terminal color for a chart tone.
        /// </summary>
        /// <param name="theme">Theme.</param>
        /// <param name="tone">Tone.</param>
        /// <returns>Color.</returns>
        public static Color ToneColor(Theme theme, XenoChartToneEnum tone)
        {
            string? hex = XenoChartPalette.Hex(tone);
            if (hex != null)
            {
                byte[] rgb = XenoChartPalette.Rgb(hex);
                return Color.FromRgb(rgb[0], rgb[1], rgb[2]);
            }

            return ChartView.ToneColor(theme, tone);
        }

        #endregion

        #region Private-Methods

        private static bool IsTimeChart(XenoChart chart)
        {
            return (chart.Kind == XenoChartKindEnum.StackedColumns || chart.Kind == XenoChartKindEnum.Lines) && chart.Buckets.Count > 0;
        }

        private XenoChart Fitted(XenoChart chart, int width)
        {
            if (_Fitted != null && _RenderedWidth == width) return _Fitted;
            int plot = Math.Max(1, width - ScaleWidth);
            XenoChart fitted = chart.Kind == XenoChartKindEnum.StatusStrip ? chart.Downsample(Math.Max(1, width - 24)) : chart.Downsample(plot);
            _Fitted = fitted;
            _RenderedWidth = width;
            if (_Cursor.HasValue && _Cursor.Value >= fitted.BucketCount) _Cursor = fitted.BucketCount - 1;
            return fitted;
        }

        private void Activate(XenoChart chart, int index, string? series)
        {
            if (index < 0 || index >= chart.Buckets.Count) return;
            BucketActivated?.Invoke(chart.Buckets[index], series);
        }

        private string? SeriesAt(int index, int y)
        {
            if (index < 0 || index >= _Segments.Count) return null;
            foreach (KeyValuePair<string, int> segment in _Segments[index])
            {
                if (y <= segment.Value) return segment.Key;
            }

            return null;
        }

        private void RenderHeading(ISurface surface, XenoChart chart, int width)
        {
            string heading = chart.Heading;
            surface.DrawText(0, 0, Fit(heading, width), _Theme.Text.WithAttribute(CellAttributes.Bold, true));
            List<KeyValuePair<string, CellStyle>> parts = new List<KeyValuePair<string, CellStyle>>();
            for (int i = 0; i < chart.Series.Count; i++)
            {
                XenoChartSeries series = chart.Series[i];
                string mark = chart.Kind == XenoChartKindEnum.Lines ? LineMarkers[i % LineMarkers.Count] + " "
                    : chart.Kind == XenoChartKindEnum.StatusStrip ? StatusGlyph(i + 1 > 3 ? 0 : i + 1) + " "
                    : "■ ";
                parts.Add(new KeyValuePair<string, CellStyle>(mark, _Theme.Text.WithForeground(ToneColor(_Theme, series.Tone))));
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

        private void RenderTime(ISurface surface, XenoChart chart, int top, int available, int width)
        {
            int plotHeight = Math.Max(1, available - 1);
            int plotWidth = Math.Max(1, width - ScaleWidth);
            int count = Math.Max(1, chart.BucketCount);
            int columnWidth = Math.Max(1, plotWidth / count);
            _PlotLeft = ScaleWidth;
            _PlotTop = top;
            _PlotHeight = plotHeight;
            _ColumnWidth = columnWidth;
            _Segments.Clear();
            double maximum = chart.Maximum;
            if (maximum <= 0) maximum = 1;
            surface.DrawText(0, top, Fit(chart.FormatValue(maximum), ScaleWidth - 1).PadLeft(ScaleWidth - 1), _Theme.Muted);
            surface.DrawText(0, top + plotHeight - 1, Fit(chart.FormatValue(0), ScaleWidth - 1).PadLeft(ScaleWidth - 1), _Theme.Muted);
            for (int bucket = 0; bucket < chart.BucketCount; bucket++)
            {
                int x = _PlotLeft + bucket * columnWidth;
                if (x >= width) break;
                bool selected = _Cursor == bucket;
                List<KeyValuePair<string, int>> segments = new List<KeyValuePair<string, int>>();
                if (selected)
                {
                    for (int y = 0; y < plotHeight; y++) surface.DrawText(x, top + y, new string(' ', Math.Min(columnWidth, width - x)), _Theme.Selection);
                }

                if (chart.Kind == XenoChartKindEnum.Lines)
                {
                    for (int s = chart.Series.Count - 1; s >= 0; s--)
                    {
                        XenoChartSeries series = chart.Series[s];
                        if (bucket >= series.Values.Count || Double.IsNaN(series.Values[bucket])) continue;
                        int y = top + plotHeight - 1 - (int)Math.Round(Math.Min(1, series.Values[bucket] / maximum) * (plotHeight - 1));
                        surface.DrawText(x, y, LineMarkers[s % LineMarkers.Count].ToString(), _Theme.Text.WithForeground(ToneColor(_Theme, series.Tone)));
                    }
                }
                else
                {
                    double running = 0;
                    int drawnBottom = top + plotHeight;
                    foreach (XenoChartSeries series in chart.Series)
                    {
                        double value = bucket < series.Values.Count && !Double.IsNaN(series.Values[bucket]) ? Math.Max(0, series.Values[bucket]) : 0;
                        if (value <= 0) continue;
                        running += value;
                        int topRow = top + plotHeight - (int)Math.Round(Math.Min(1, running / maximum) * plotHeight);
                        if (topRow >= drawnBottom) topRow = drawnBottom - 1;
                        string block = new string('█', Math.Max(1, Math.Min(columnWidth > 2 ? columnWidth - 1 : columnWidth, width - x)));
                        for (int y = topRow; y < drawnBottom; y++) surface.DrawText(x, y, block, _Theme.Text.WithForeground(ToneColor(_Theme, series.Tone)));
                        segments.Insert(0, new KeyValuePair<string, int>(series.Name, drawnBottom - 1));
                        drawnBottom = topRow;
                    }
                }

                _Segments.Add(segments.OrderBy(item => item.Value).ToList());
            }

            int labelRow = top + plotHeight;
            if (_Cursor.HasValue)
            {
                surface.DrawText(0, labelRow, Fit(CursorText(), width), _Theme.Accent);
                return;
            }

            int every = Math.Max(1, (int)Math.Ceiling(12.0 / columnWidth));
            for (int bucket = 0; bucket < chart.Labels.Count; bucket += every)
            {
                int x = _PlotLeft + bucket * columnWidth;
                string label = chart.Labels[bucket];
                if (x + label.Length > width) break;
                surface.DrawText(x, labelRow, label, _Theme.Muted);
            }
        }

        private void RenderBars(ISurface surface, XenoChart chart, int top, int available, int width)
        {
            int labelWidth = Math.Min(28, Math.Max(6, chart.Labels.Count == 0 ? 6 : chart.Labels.Max(item => item.Length) + 1));
            int valueWidth = 18;
            int barWidth = Math.Max(4, width - labelWidth - valueWidth);
            double maximum = chart.Maximum <= 0 ? 1 : chart.Maximum;
            for (int row = 0; row < chart.Labels.Count && row < available; row++)
            {
                int y = top + row;
                surface.DrawText(0, y, Fit(chart.Labels[row], labelWidth - 1), _Theme.Text);
                int x = labelWidth;
                if (chart.Stacked)
                {
                    double total = 0;
                    foreach (XenoChartSeries series in chart.Series)
                    {
                        double value = row < series.Values.Count ? Math.Max(0, series.Values[row]) : 0;
                        int cells = (int)Math.Round(value / maximum * barWidth);
                        if (cells > 0) surface.DrawText(x, y, new string('█', cells), _Theme.Text.WithForeground(ToneColor(_Theme, series.Tone)));
                        x += cells;
                        total += value;
                    }

                    surface.DrawText(labelWidth + barWidth + 1, y, Fit(chart.FormatValue(total), valueWidth - 1), _Theme.Muted);
                }
                else
                {
                    // Side by side in one row: the longest value lightly shaded, each shorter value drawn over it solid.
                    List<int> order = Enumerable.Range(0, chart.Series.Count).OrderByDescending(index => row < chart.Series[index].Values.Count ? chart.Series[index].Values[row] : 0).ToList();
                    bool first = true;
                    foreach (int index in order)
                    {
                        XenoChartSeries series = chart.Series[index];
                        double value = row < series.Values.Count ? Math.Max(0, series.Values[row]) : 0;
                        int cells = (int)Math.Round(value / maximum * barWidth);
                        if (cells > 0) surface.DrawText(labelWidth, y, new string(first ? '░' : '█', cells), _Theme.Text.WithForeground(ToneColor(_Theme, series.Tone)));
                        first = false;
                    }

                    string values = String.Join(" / ", chart.Series.Select(series => chart.FormatValue(row < series.Values.Count ? series.Values[row] : 0)));
                    surface.DrawText(labelWidth + barWidth + 1, y, Fit(values, valueWidth - 1), _Theme.Muted);
                }
            }
        }

        private bool IsHeatMap()
        {
            return (_Fitted ?? _Chart)?.Kind == XenoChartKindEnum.Heatmap;
        }

        private void RenderHeatMap(ISurface surface, XenoChart chart, int top, int available, int width)
        {
            // TUIKit's HeatMap draws the cells, labels, legend, and the selected cell's description. Leave a row each for
            // the column labels, the legend, and the description; a taller grid shows its most recent rows.
            int columns = Math.Max(1, chart.Labels.Count);
            int fit = Math.Max(1, available - 3);
            int skip = Math.Max(0, chart.RowLabels.Count - fit);
            if (!Object.ReferenceEquals(chart, _HeatSource) || skip != _HeatSkip)
            {
                int rows = Math.Max(0, chart.RowLabels.Count - skip);
                double[,] cells = new double[rows, columns];
                for (int r = 0; r < rows; r++)
                {
                    IReadOnlyList<double> source = skip + r < chart.Grid.Count ? chart.Grid[skip + r] : new List<double>();
                    for (int c = 0; c < columns; c++)
                    {
                        double value = c < source.Count ? source[c] : 0;
                        cells[r, c] = Double.IsNaN(value) || value < 0 ? 0 : value;
                    }
                }

                _HeatMap.SetCells(cells);
                _HeatMap.SetRowLabels(chart.RowLabels.Skip(skip));
                _HeatMap.SetColumnLabels(chart.Labels);
                _HeatMap.CellDescription = (row, column) =>
                {
                    string rowLabel = skip + row < chart.RowLabels.Count ? chart.RowLabels[skip + row] : String.Empty;
                    string columnLabel = column < chart.Labels.Count ? chart.Labels[column] : String.Empty;
                    return (rowLabel + " " + columnLabel).Trim() + ": " + chart.FormatValue(cells[row, column]);
                };
                _HeatSource = chart;
                _HeatSkip = skip;
            }

            // One blank column between cells (TUIKit's CellGap) when there is room for cells at least two columns wide;
            // otherwise single-column cells edge to edge.
            int labelWidth = chart.RowLabels.Count == 0 ? 0 : chart.RowLabels.Max(item => item.Length) + 1;
            int stride = Math.Max(1, (width - labelWidth + 1) / columns);
            int gap = stride >= 3 ? 1 : 0;
            _HeatMap.CellGap = gap;
            _HeatMap.CellWidth = Math.Clamp(stride - gap, 1, 4);
            _HeatTop = top;
            _HeatMap.Render(new OffsetSurface(surface, 0, top, width, available));
        }

        private void RenderGrid(ISurface surface, XenoChart chart, int top, int available, int width, bool status)
        {
            int labelWidth = Math.Min(24, Math.Max(5, chart.RowLabels.Count == 0 ? 5 : chart.RowLabels.Max(item => item.Length) + 1));
            int columns = Math.Max(1, chart.Labels.Count);
            int cellWidth = Math.Max(1, (width - labelWidth) / columns);
            double maximum = chart.Maximum <= 0 ? 1 : chart.Maximum;
            int rows = Math.Min(chart.RowLabels.Count, Math.Max(0, available - 1));
            int skip = status ? 0 : Math.Max(0, chart.RowLabels.Count - rows);
            for (int line = 0; line < rows; line++)
            {
                // A heatmap taller than the space shows its most recent rows.
                int row = skip + line;
                int y = top + line;
                surface.DrawText(0, y, Fit(chart.RowLabels[row], labelWidth - 1), _Theme.Muted);
                IReadOnlyList<double> cells = row < chart.Grid.Count ? chart.Grid[row] : new List<double>();
                for (int column = 0; column < cells.Count && column < columns; column++)
                {
                    int x = labelWidth + column * cellWidth;
                    if (x >= width) break;
                    double value = cells[column];
                    string glyph;
                    CellStyle style;
                    if (status)
                    {
                        int code = (int)value;
                        glyph = StatusGlyph(code).ToString();
                        style = _Theme.Text.WithForeground(ToneColor(_Theme, StatusTone(code)));
                    }
                    else if (value <= 0 || Double.IsNaN(value))
                    {
                        glyph = "·";
                        style = _Theme.Muted;
                    }
                    else
                    {
                        int shade = Math.Clamp((int)Math.Ceiling(value / maximum * Shades.Count) - 1, 0, Shades.Count - 1);
                        glyph = Shades[shade].ToString();
                        style = _Theme.Text.WithForeground(ToneColor(_Theme, XenoChartToneEnum.Accent));
                    }

                    surface.DrawText(x, y, new string(glyph[0], Math.Min(cellWidth, width - x)), style);
                }
            }

            int labelRow = top + rows;
            if (labelRow >= top + available) return;
            int every = Math.Max(1, (int)Math.Ceiling(6.0 / cellWidth));
            for (int column = 0; column < chart.Labels.Count; column += every)
            {
                int x = labelWidth + column * cellWidth;
                string label = chart.Labels[column];
                if (x + label.Length > width) break;
                surface.DrawText(x, labelRow, label, _Theme.Muted);
            }
        }

        private static char StatusGlyph(int code)
        {
            switch (code)
            {
                case (int)XenoChartStatusEnum.Healthy: return '█';
                case (int)XenoChartStatusEnum.Degraded: return '▒';
                case (int)XenoChartStatusEnum.Unhealthy: return '×';
                default: return '·';
            }
        }

        private static XenoChartToneEnum StatusTone(int code)
        {
            switch (code)
            {
                case (int)XenoChartStatusEnum.Healthy: return XenoChartToneEnum.Success;
                case (int)XenoChartStatusEnum.Degraded: return XenoChartToneEnum.Warning;
                case (int)XenoChartStatusEnum.Unhealthy: return XenoChartToneEnum.Danger;
                default: return XenoChartToneEnum.Neutral;
            }
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

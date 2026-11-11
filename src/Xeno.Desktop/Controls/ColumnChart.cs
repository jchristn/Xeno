namespace Xeno.Desktop.Controls
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Input;
    using Avalonia.Media;
    using Xeno.Client.Presentation;
    using Xeno.Desktop.Common;

    /// <summary>
    /// Draws a shared <see cref="XenoChart"/> as stacked columns: the heading and a legend on top, a value scale with
    /// grid lines, time labels underneath, and a tooltip per bucket. Shows the chart's empty-state text when it has no
    /// data. Used for requests over time and telemetry.
    /// </summary>
    public sealed class ColumnChart : Control
    {
        #region Public-Members

        /// <summary>
        /// Chart shown, or null before the first load.
        /// </summary>
        public XenoChart? Chart
        {
            get { return _Chart; }
        }

        /// <summary>
        /// Raised when the user drills into a time bucket (click, or Enter on the keyboard cursor): its window and, for
        /// group charts, the series clicked.
        /// </summary>
        public event Action<XenoChartBucket, string?>? BucketActivated;

        /// <summary>
        /// Keyboard cursor bucket, or null.
        /// </summary>
        public int? CursorBucket
        {
            get { return _Cursor; }
        }

        #endregion

        #region Private-Members

        private const double HeaderHeight = 22;
        private const double AxisWidth = 52;
        private const double LabelHeight = 18;
        private XenoChart? _Chart = null;
        private string _LoadingText = "Loading...";
        private int? _Cursor = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a chart.
        /// </summary>
        public ColumnChart()
        {
            Height = 180;
            ClipToBounds = true;
            Focusable = true;
            PointerMoved += (sender, args) => UpdateTip(args.GetPosition(this));
            PointerExited += (sender, args) => ToolTip.SetTip(this, null);
            PointerPressed += (sender, args) => OnPressed(args.GetPosition(this));
            KeyDown += (sender, args) => args.Handled = OnKey(args.Key);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Show a chart.
        /// </summary>
        /// <param name="chart">Chart.</param>
        public void SetChart(XenoChart? chart)
        {
            _Chart = chart;
            if (_Cursor.HasValue && (chart == null || _Cursor.Value >= chart.BucketCount)) _Cursor = null;
            InvalidateVisual();
        }

        /// <summary>
        /// Drill into a bucket as a click would, for keyboard use and tests.
        /// </summary>
        /// <param name="index">Bucket index.</param>
        /// <param name="series">Series name, or null.</param>
        public void Activate(int index, string? series)
        {
            XenoChart? chart = _Chart;
            if (chart == null || index < 0 || index >= chart.Buckets.Count) return;
            _Cursor = index;
            BucketActivated?.Invoke(chart.Buckets[index], series);
            InvalidateVisual();
        }

        /// <summary>
        /// Set the text shown before the first load.
        /// </summary>
        /// <param name="text">Text.</param>
        public void SetLoadingText(string text)
        {
            _LoadingText = text ?? String.Empty;
            InvalidateVisual();
        }

        /// <summary>
        /// Brush for a chart tone.
        /// </summary>
        /// <param name="palette">Palette.</param>
        /// <param name="tone">Tone.</param>
        /// <returns>Brush.</returns>
        public static IBrush ToneBrush(XenoPalette palette, XenoChartToneEnum tone)
        {
            ArgumentNullException.ThrowIfNull(palette);
            string? hex = XenoChartPalette.Hex(tone);
            if (hex != null) return new SolidColorBrush(Color.Parse(hex));
            switch (tone)
            {
                case XenoChartToneEnum.Success: return palette.Success;
                case XenoChartToneEnum.Danger: return palette.Danger;
                case XenoChartToneEnum.Info: return palette.Info;
                case XenoChartToneEnum.Warning: return palette.Warning;
                default: return palette.Accent;
            }
        }

        /// <summary>
        /// Render the chart.
        /// </summary>
        /// <param name="context">Drawing context.</param>
        public override void Render(DrawingContext context)
        {
            // An error while drawing happens outside any event handler and would end the app, so log it and skip the frame.
            try
            {
                RenderChart(context);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is NullReferenceException || ex is IndexOutOfRangeException || ex is OverflowException)
            {
                XenoErrorLog.Write("Render", ex);
            }
        }

        private void RenderChart(DrawingContext context)
        {
            XenoPalette palette = XenoTheme.Current;
            Rect bounds = new Rect(Bounds.Size);
            XenoChart? chart = _Chart;
            if (chart == null)
            {
                DrawText(context, _LoadingText, new Point(0, 0), palette.Muted, 12, FontWeight.Normal);
                return;
            }

            DrawText(context, chart.Heading, new Point(0, 0), palette.Text, 13, FontWeight.SemiBold);
            DrawLegend(context, palette, chart, bounds.Width);
            Rect plot = PlotRect(bounds);
            Pen axis = new Pen(palette.Border, 1);
            if (!chart.HasData)
            {
                context.DrawLine(axis, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));
                FormattedText empty = Format(chart.EmptyText, palette.Muted, 12, FontWeight.Normal);
                empty.MaxTextWidth = Math.Max(40, plot.Width - 16);
                context.DrawText(empty, new Point(plot.Left + 8, plot.Top + Math.Max(0, (plot.Height - empty.Height) / 2)));
                return;
            }

            if (chart.Notice != null)
            {
                FormattedText notice = Format("! " + chart.Notice, palette.Warning, 11, FontWeight.Normal);
                notice.MaxTextWidth = Math.Max(40, bounds.Width);
                context.DrawText(notice, new Point(0, bounds.Height - notice.Height));
            }

            if (chart.Kind == XenoChartKindEnum.HorizontalBars)
            {
                RenderBars(context, palette, chart, bounds);
                return;
            }

            if (chart.Kind == XenoChartKindEnum.Heatmap || chart.Kind == XenoChartKindEnum.StatusStrip)
            {
                RenderGrid(context, palette, chart, bounds);
                return;
            }

            double maximum = NiceMaximum(chart.Maximum);
            Pen grid = new Pen(palette.Border, 1, new DashStyle(new double[] { 3, 4 }, 0));
            foreach (double ratio in new[] { 1.0, 0.5 })
            {
                double y = plot.Bottom - plot.Height * ratio;
                context.DrawLine(grid, new Point(plot.Left, y), new Point(plot.Right, y));
                FormattedText tick = Format(chart.FormatValue(maximum * ratio), palette.Muted, 11, FontWeight.Normal);
                context.DrawText(tick, new Point(Math.Max(0, plot.Left - tick.Width - 6), y - tick.Height / 2));
            }

            context.DrawLine(axis, new Point(plot.Left, plot.Bottom), new Point(plot.Right, plot.Bottom));
            int count = chart.BucketCount;
            double slot = plot.Width / Math.Max(1, count);
            double barWidth = Math.Max(1, slot * 0.72);
            if (_Cursor.HasValue && _Cursor.Value < count) context.FillRectangle(palette.Border, new Rect(plot.Left + _Cursor.Value * slot, plot.Top, slot, plot.Height));
            if (chart.Kind == XenoChartKindEnum.Lines)
            {
                for (int s = chart.Series.Count - 1; s >= 0; s--)
                {
                    // Buckets with no readings are skipped: the line joins the readings on either side with a dashed segment,
                    // so sparse data reads as one line with visible gaps instead of scattered fragments. Every reading gets a dot.
                    XenoChartSeries series = chart.Series[s];
                    IBrush brush = ToneBrush(palette, series.Tone);
                    double thickness = s == 0 ? 2.5 : 1.5;
                    Pen pen = new Pen(brush, thickness);
                    Pen bridge = new Pen(brush, Math.Max(1, thickness - 0.5), new DashStyle(new double[] { 2, 3 }, 0));
                    double dot = s == 0 ? 2.6 : 2;
                    Point? previous = null;
                    int previousIndex = -1;
                    for (int i = 0; i < count; i++)
                    {
                        if (i >= series.Values.Count || Double.IsNaN(series.Values[i])) continue;
                        Point point = new Point(plot.Left + i * slot + slot / 2, plot.Bottom - plot.Height * Math.Min(1, series.Values[i] / maximum));
                        if (previous.HasValue) context.DrawLine(i - previousIndex > 1 ? bridge : pen, previous.Value, point);
                        context.DrawEllipse(brush, null, point, dot, dot);
                        previous = point;
                        previousIndex = i;
                    }
                }
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    double x = plot.Left + i * slot + (slot - barWidth) / 2;
                    double stacked = 0;
                    foreach (XenoChartSeries series in chart.Series)
                    {
                        double value = i < series.Values.Count && !Double.IsNaN(series.Values[i]) ? Math.Max(0, series.Values[i]) : 0;
                        double height = plot.Height * value / maximum;
                        if (height > 0) context.FillRectangle(ToneBrush(palette, series.Tone), new Rect(x, plot.Bottom - stacked - height, barWidth, height));
                        stacked += height;
                    }
                }
            }

            DrawTimeLabels(context, palette, chart, plot);
        }

        #endregion

        #region Private-Methods

        private void RenderBars(DrawingContext context, XenoPalette palette, XenoChart chart, Rect bounds)
        {
            double top = HeaderHeight + 8;
            double rowHeight = Math.Max(14, Math.Min(24, (bounds.Height - top - 16) / Math.Max(1, chart.Labels.Count)));
            double labelWidth = Math.Min(200, Math.Max(60, chart.Labels.Count == 0 ? 60 : chart.Labels.Max(item => Format(item, palette.Text, 12, FontWeight.Normal).Width) + 10));
            double valueWidth = 120;
            double barWidth = Math.Max(20, bounds.Width - labelWidth - valueWidth);
            double maximum = chart.Maximum <= 0 ? 1 : chart.Maximum;
            for (int row = 0; row < chart.Labels.Count; row++)
            {
                double y = top + row * rowHeight;
                if (y + rowHeight > bounds.Height) break;
                context.DrawText(Format(chart.Labels[row], palette.Text, 12, FontWeight.Normal), new Point(0, y + 2));
                if (chart.Stacked)
                {
                    double x = labelWidth;
                    double total = 0;
                    foreach (XenoChartSeries series in chart.Series)
                    {
                        double value = row < series.Values.Count ? Math.Max(0, series.Values[row]) : 0;
                        double width = barWidth * value / maximum;
                        if (width > 0) context.FillRectangle(ToneBrush(palette, series.Tone), new Rect(x, y + 3, width, rowHeight - 6), 2);
                        x += width;
                        total += value;
                    }

                    context.DrawText(Format(chart.FormatValue(total), palette.Muted, 11, FontWeight.Normal), new Point(labelWidth + barWidth + 6, y + 3));
                }
                else
                {
                    double band = (rowHeight - 6) / Math.Max(1, chart.Series.Count);
                    for (int s = 0; s < chart.Series.Count; s++)
                    {
                        XenoChartSeries series = chart.Series[s];
                        double value = row < series.Values.Count ? Math.Max(0, series.Values[row]) : 0;
                        double width = barWidth * value / maximum;
                        if (width > 0) context.FillRectangle(ToneBrush(palette, series.Tone), new Rect(labelWidth, y + 3 + s * band, width, Math.Max(2, band - 1)), 1);
                    }

                    string values = String.Join(" / ", chart.Series.Select(series => chart.FormatValue(row < series.Values.Count ? series.Values[row] : 0)));
                    context.DrawText(Format(values, palette.Muted, 11, FontWeight.Normal), new Point(labelWidth + barWidth + 6, y + 3));
                }
            }
        }

        private void RenderGrid(DrawingContext context, XenoPalette palette, XenoChart chart, Rect bounds)
        {
            bool status = chart.Kind == XenoChartKindEnum.StatusStrip;
            double top = HeaderHeight + 8;
            double labelWidth = Math.Min(220, Math.Max(40, chart.RowLabels.Count == 0 ? 40 : chart.RowLabels.Max(item => Format(item, palette.Muted, 11, FontWeight.Normal).Width) + 8));
            int columns = Math.Max(1, chart.Labels.Count);
            double cellWidth = Math.Max(2, (bounds.Width - labelWidth) / columns);
            double cellHeight = Math.Max(8, Math.Min(22, (bounds.Height - top - 22) / Math.Max(1, chart.RowLabels.Count)));
            double maximum = chart.Maximum <= 0 ? 1 : chart.Maximum;
            IBrush accent = ToneBrush(palette, XenoChartToneEnum.Accent);
            for (int row = 0; row < chart.RowLabels.Count; row++)
            {
                double y = top + row * cellHeight;
                context.DrawText(Format(chart.RowLabels[row], palette.Muted, 11, FontWeight.Normal), new Point(0, y + 1));
                IReadOnlyList<double> cells = row < chart.Grid.Count ? chart.Grid[row] : new List<double>();
                for (int column = 0; column < cells.Count && column < columns; column++)
                {
                    Rect cell = new Rect(labelWidth + column * cellWidth, y + 1, Math.Max(1, cellWidth - 1), Math.Max(1, cellHeight - 2));
                    double value = cells[column];
                    if (status)
                    {
                        int code = (int)value;
                        XenoChartToneEnum tone = code == (int)XenoChartStatusEnum.Healthy ? XenoChartToneEnum.Success : code == (int)XenoChartStatusEnum.Degraded ? XenoChartToneEnum.Warning : code == (int)XenoChartStatusEnum.Unhealthy ? XenoChartToneEnum.Danger : XenoChartToneEnum.Neutral;
                        context.FillRectangle(ToneBrush(palette, tone), cell, 1);
                        if (code == (int)XenoChartStatusEnum.Unhealthy) context.DrawLine(new Pen(palette.Text, 1), cell.TopLeft, cell.BottomRight);
                        if (code == (int)XenoChartStatusEnum.Degraded) context.DrawLine(new Pen(palette.Text, 1), cell.BottomLeft, cell.TopRight);
                    }
                    else if (value > 0 && !Double.IsNaN(value))
                    {
                        using (context.PushOpacity(0.15 + 0.85 * Math.Min(1, value / maximum)))
                        {
                            context.FillRectangle(accent, cell, 2);
                        }
                    }
                    else
                    {
                        context.FillRectangle(palette.Border, cell, 2);
                    }
                }
            }

            int every = Math.Max(1, (int)Math.Ceiling(40 / cellWidth));
            double labelY = top + chart.RowLabels.Count * cellHeight + 2;
            for (int column = 0; column < chart.Labels.Count; column += every)
            {
                context.DrawText(Format(chart.Labels[column], palette.Muted, 10, FontWeight.Normal), new Point(labelWidth + column * cellWidth, labelY));
            }
        }

        private int? BucketAt(Point position)
        {
            XenoChart? chart = _Chart;
            if (chart == null || chart.Buckets.Count == 0 || (chart.Kind != XenoChartKindEnum.StackedColumns && chart.Kind != XenoChartKindEnum.Lines)) return null;
            Rect plot = PlotRect(new Rect(Bounds.Size));
            if (position.X < plot.Left || position.X > plot.Right) return null;
            return Math.Clamp((int)((position.X - plot.Left) / (plot.Width / Math.Max(1, chart.BucketCount))), 0, Math.Max(0, chart.BucketCount - 1));
        }

        private void OnPressed(Point position)
        {
            Focus();
            int? index = BucketAt(position);
            if (!index.HasValue || _Chart == null) return;
            string? series = null;
            if (_Chart.Kind == XenoChartKindEnum.StackedColumns && _Chart.Stacked)
            {
                Rect plot = PlotRect(new Rect(Bounds.Size));
                double maximum = NiceMaximum(_Chart.Maximum);
                double stacked = 0;
                foreach (XenoChartSeries item in _Chart.Series)
                {
                    double value = index.Value < item.Values.Count && !Double.IsNaN(item.Values[index.Value]) ? Math.Max(0, item.Values[index.Value]) : 0;
                    double height = plot.Height * value / maximum;
                    if (position.Y <= plot.Bottom - stacked && position.Y >= plot.Bottom - stacked - height) series = item.Name;
                    stacked += height;
                }
            }

            Activate(index.Value, series);
        }

        private bool OnKey(Key key)
        {
            XenoChart? chart = _Chart;
            if (chart == null || chart.Buckets.Count == 0) return false;
            int last = chart.BucketCount - 1;
            switch (key)
            {
                case Key.Left:
                    _Cursor = Math.Max(0, (_Cursor ?? last + 1) - 1);
                    break;
                case Key.Right:
                    _Cursor = Math.Min(last, (_Cursor ?? -1) + 1);
                    break;
                case Key.Enter:
                    Activate(_Cursor ?? last, null);
                    return true;
                default:
                    return false;
            }

            InvalidateVisual();
            return true;
        }

        private static Rect PlotRect(Rect bounds)
        {
            return new Rect(AxisWidth, HeaderHeight + 6, Math.Max(10, bounds.Width - AxisWidth), Math.Max(10, bounds.Height - HeaderHeight - LabelHeight - 10));
        }

        private static double NiceMaximum(double maximum)
        {
            if (maximum <= 0) return 1;
            double magnitude = Math.Pow(10, Math.Floor(Math.Log10(maximum)));
            foreach (double step in new[] { 1.0, 2.0, 2.5, 5.0, 10.0 })
            {
                if (step * magnitude >= maximum) return step * magnitude;
            }

            return 10 * magnitude;
        }

        private static void DrawLegend(DrawingContext context, XenoPalette palette, XenoChart chart, double width)
        {
            double x = width;
            for (int i = chart.Series.Count - 1; i >= 0; i--)
            {
                XenoChartSeries series = chart.Series[i];
                FormattedText name = Format(series.Name, palette.Muted, 12, FontWeight.Normal);
                x -= name.Width;
                context.DrawText(name, new Point(x, 2));
                x -= 16;
                context.FillRectangle(ToneBrush(palette, series.Tone), new Rect(x, 5, 10, 10), 2);
                x -= 14;
            }

            if (chart.HasData)
            {
                FormattedText peak = Format("peak " + chart.FormatValue(chart.Maximum), palette.Muted, 12, FontWeight.Normal);
                context.DrawText(peak, new Point(x - peak.Width - 4, 2));
            }
        }

        private static void DrawTimeLabels(DrawingContext context, XenoPalette palette, XenoChart chart, Rect plot)
        {
            if (chart.Labels.Count == 0) return;
            List<int> indexes = new List<int> { 0, chart.Labels.Count / 2, chart.Labels.Count - 1 }.Distinct().ToList();
            double slot = plot.Width / Math.Max(1, chart.BucketCount);
            foreach (int index in indexes)
            {
                FormattedText label = Format(chart.Labels[index], palette.Muted, 11, FontWeight.Normal);
                double center = plot.Left + index * slot + slot / 2;
                // A label wider than the plot starts at the left edge; Math.Clamp would throw when the bounds cross.
                double x = Math.Max(plot.Left, Math.Min(center - label.Width / 2, plot.Right - label.Width));
                context.DrawText(label, new Point(x, plot.Bottom + 4));
            }
        }

        private void UpdateTip(Point position)
        {
            XenoChart? chart = _Chart;
            if (chart == null || !chart.HasData)
            {
                ToolTip.SetTip(this, null);
                return;
            }

            Rect plot = PlotRect(new Rect(Bounds.Size));
            if (position.X < plot.Left || position.X > plot.Right)
            {
                ToolTip.SetTip(this, null);
                return;
            }

            int index = Math.Clamp((int)((position.X - plot.Left) / (plot.Width / Math.Max(1, chart.BucketCount))), 0, Math.Max(0, chart.BucketCount - 1));
            List<string> lines = new List<string>();
            if (index < chart.Labels.Count) lines.Add(chart.Labels[index]);
            foreach (XenoChartSeries series in chart.Series)
            {
                double value = index < series.Values.Count ? series.Values[index] : 0;
                lines.Add(series.Name + ": " + chart.FormatValue(value));
            }

            ToolTip.SetTip(this, String.Join(Environment.NewLine, lines));
            ToolTip.SetIsOpen(this, true);
        }

        private static void DrawText(DrawingContext context, string text, Point origin, IBrush brush, double size, FontWeight weight)
        {
            context.DrawText(Format(text, brush, size, weight), origin);
        }

        private static FormattedText Format(string text, IBrush brush, double size, FontWeight weight)
        {
            return new FormattedText(text ?? String.Empty, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, new Typeface(FontFamily.Default, FontStyle.Normal, weight), size, brush);
        }

        #endregion
    }
}

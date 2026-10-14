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

        #endregion

        #region Private-Members

        private const double HeaderHeight = 22;
        private const double AxisWidth = 52;
        private const double LabelHeight = 18;
        private XenoChart? _Chart = null;
        private string _LoadingText = "Loading...";

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a chart.
        /// </summary>
        public ColumnChart()
        {
            Height = 180;
            ClipToBounds = true;
            PointerMoved += (sender, args) => UpdateTip(args.GetPosition(this));
            PointerExited += (sender, args) => ToolTip.SetTip(this, null);
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

            DrawTimeLabels(context, palette, chart, plot);
        }

        #endregion

        #region Private-Methods

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
                double x = Math.Clamp(center - label.Width / 2, plot.Left, plot.Right - label.Width);
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

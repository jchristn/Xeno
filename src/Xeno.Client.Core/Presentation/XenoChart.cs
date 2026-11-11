namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// A chart's content, built once by <see cref="XenoCharts"/> or <see cref="XenoUsageCharts"/> and drawn by the
    /// desktop and terminal apps: a title, the range it covers, its kind, labels, series, a unit, what to say when there
    /// is no data, and (for time charts) the window each column covers. The web dashboards draw the same charts from
    /// the same catalog (<see cref="XenoChartCatalog"/>). Immutable.
    /// </summary>
    public sealed class XenoChart
    {
        #region Public-Members

        /// <summary>
        /// Title, for example "Requests" or "CPU".
        /// </summary>
        public string Title { get; }

        /// <summary>
        /// The range the chart covers, for example "last hour".
        /// </summary>
        public string RangeText { get; }

        /// <summary>
        /// Unit appended to values: "%", " req", " C", or empty.
        /// </summary>
        public string Unit { get; }

        /// <summary>
        /// Bucket labels (local time), one per bucket.
        /// </summary>
        public IReadOnlyList<string> Labels { get; }

        /// <summary>
        /// Series, stacked in order.
        /// </summary>
        public IReadOnlyList<XenoChartSeries> Series { get; }

        /// <summary>
        /// Text shown instead of the chart when it has no data.
        /// </summary>
        public string EmptyText { get; }

        /// <summary>
        /// True when values are counts that add up when buckets are combined (requests); false for averages
        /// (utilization, rates).
        /// </summary>
        public bool Additive { get; }

        /// <summary>
        /// True when any bucket has data.
        /// </summary>
        public bool HasData { get; }

        /// <summary>
        /// Catalog id, for example "usage.latency", or empty for older charts.
        /// </summary>
        public string Id { get; } = String.Empty;

        /// <summary>
        /// How the chart is drawn. Default stacked columns.
        /// </summary>
        public XenoChartKindEnum Kind { get; } = XenoChartKindEnum.StackedColumns;

        /// <summary>
        /// True when column and bar series stack; false draws them side by side. Lines never stack.
        /// </summary>
        public bool Stacked { get; } = true;

        /// <summary>
        /// Row labels for heatmaps and status strips.
        /// </summary>
        public IReadOnlyList<string> RowLabels { get; } = new List<string>();

        /// <summary>
        /// Cell values for heatmaps (shading) and status strips (<see cref="XenoChartStatusEnum"/>), row by row, one value
        /// per label.
        /// </summary>
        public IReadOnlyList<IReadOnlyList<double>> Grid { get; } = new List<IReadOnlyList<double>>();

        /// <summary>
        /// The window each column covers, for drill-down; empty when the chart has no time buckets.
        /// </summary>
        public IReadOnlyList<XenoChartBucket> Buckets { get; } = new List<XenoChartBucket>();

        /// <summary>
        /// A note shown with the chart, for example that the range was not read in full; null for none.
        /// </summary>
        public string? Notice { get; } = null;

        /// <summary>
        /// Number of buckets.
        /// </summary>
        public int BucketCount
        {
            get { return Math.Max(Labels.Count, Series.Count == 0 ? 0 : Series.Max(item => item.Values.Count)); }
        }

        /// <summary>
        /// Largest stacked value across buckets, or zero.
        /// </summary>
        public double Maximum
        {
            get
            {
                double maximum = 0;
                if (Kind == XenoChartKindEnum.Heatmap)
                {
                    foreach (IReadOnlyList<double> row in Grid) foreach (double value in row) if (!Double.IsNaN(value)) maximum = Math.Max(maximum, value);
                    return maximum;
                }

                if (Kind == XenoChartKindEnum.StatusStrip) return 3;
                for (int i = 0; i < BucketCount; i++)
                {
                    if (Stacked) maximum = Math.Max(maximum, Total(i));
                    else foreach (XenoChartSeries series in Series) if (i < series.Values.Count && !Double.IsNaN(series.Values[i])) maximum = Math.Max(maximum, series.Values[i]);
                }

                return maximum;
            }
        }

        /// <summary>
        /// Heading combining the title and range, for example "Requests, last hour".
        /// </summary>
        public string Heading
        {
            get { return String.IsNullOrEmpty(RangeText) ? Title : Title + ", " + RangeText; }
        }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a chart.
        /// </summary>
        /// <param name="title">Title.</param>
        /// <param name="rangeText">Range description.</param>
        /// <param name="unit">Unit.</param>
        /// <param name="labels">Bucket labels.</param>
        /// <param name="series">Series.</param>
        /// <param name="emptyText">Empty-state text.</param>
        /// <param name="additive">True for counts.</param>
        /// <param name="hasData">True when any bucket has data.</param>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public XenoChart(string title, string rangeText, string unit, IReadOnlyList<string> labels, IReadOnlyList<XenoChartSeries> series, string emptyText, bool additive, bool hasData)
        {
            Title = title ?? throw new ArgumentNullException(nameof(title));
            RangeText = rangeText ?? throw new ArgumentNullException(nameof(rangeText));
            Unit = unit ?? String.Empty;
            Labels = labels ?? throw new ArgumentNullException(nameof(labels));
            Series = series ?? throw new ArgumentNullException(nameof(series));
            EmptyText = emptyText ?? String.Empty;
            Additive = additive;
            HasData = hasData;
        }

        /// <summary>
        /// Create a chart of any kind.
        /// </summary>
        /// <param name="id">Catalog id.</param>
        /// <param name="kind">Kind.</param>
        /// <param name="title">Title.</param>
        /// <param name="rangeText">Range description.</param>
        /// <param name="unit">Unit.</param>
        /// <param name="labels">Labels: bucket labels, bar labels, or heatmap and strip columns.</param>
        /// <param name="series">Series.</param>
        /// <param name="emptyText">Empty-state text.</param>
        /// <param name="additive">True for counts.</param>
        /// <param name="hasData">True when anything has data.</param>
        /// <param name="stacked">True when series stack.</param>
        /// <param name="rowLabels">Row labels (heatmap and strip), or null.</param>
        /// <param name="grid">Cells (heatmap and strip), or null.</param>
        /// <param name="buckets">Window per column, or null.</param>
        /// <param name="notice">Note, or null.</param>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public XenoChart(string id, XenoChartKindEnum kind, string title, string rangeText, string unit, IReadOnlyList<string> labels, IReadOnlyList<XenoChartSeries> series, string emptyText, bool additive, bool hasData, bool stacked, IReadOnlyList<string>? rowLabels, IReadOnlyList<IReadOnlyList<double>>? grid, IReadOnlyList<XenoChartBucket>? buckets, string? notice)
            : this(title, rangeText, unit, labels, series, emptyText, additive, hasData)
        {
            Id = id ?? String.Empty;
            Kind = kind;
            Stacked = stacked && kind != XenoChartKindEnum.Lines;
            RowLabels = rowLabels ?? new List<string>();
            Grid = grid ?? new List<IReadOnlyList<double>>();
            Buckets = buckets ?? new List<XenoChartBucket>();
            Notice = String.IsNullOrWhiteSpace(notice) ? null : notice;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Stacked total of a bucket.
        /// </summary>
        /// <param name="bucket">Bucket index.</param>
        /// <returns>Total, ignoring missing and negative values.</returns>
        public double Total(int bucket)
        {
            double total = 0;
            foreach (XenoChartSeries series in Series)
            {
                if (bucket < series.Values.Count && !Double.IsNaN(series.Values[bucket])) total += Math.Max(0, series.Values[bucket]);
            }

            return total;
        }

        /// <summary>
        /// Format a value with the chart's unit, for scales and tooltips.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>Text, for example "42%", "12 req", or "3.5".</returns>
        public string FormatValue(double value)
        {
            if (Double.IsNaN(value)) return "-";
            if (Unit == " ms") return FormatMilliseconds(value);
            string number = value >= 100 || (Additive && value == Math.Floor(value))
                ? value.ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("0.#", CultureInfo.InvariantCulture);
            return number + Unit;
        }

        /// <summary>
        /// Format milliseconds as "850 ms", "1.2 s", or "3.4 min".
        /// </summary>
        /// <param name="milliseconds">Milliseconds.</param>
        /// <returns>Text.</returns>
        public static string FormatMilliseconds(double milliseconds)
        {
            if (Double.IsNaN(milliseconds)) return "-";
            if (milliseconds < 1000) return Math.Round(milliseconds).ToString("0", CultureInfo.InvariantCulture) + " ms";
            if (milliseconds < 60000) return (milliseconds / 1000).ToString("0.0", CultureInfo.InvariantCulture) + " s";
            return (milliseconds / 60000).ToString("0.0", CultureInfo.InvariantCulture) + " min";
        }

        /// <summary>
        /// Combine adjacent buckets so the chart has at most a given number of buckets, for narrow displays. Counts are
        /// summed and averages averaged; each combined bucket keeps the label of its first bucket.
        /// </summary>
        /// <param name="maximum">Maximum buckets (at least 1).</param>
        /// <returns>The chart itself when it already fits, otherwise a combined copy.</returns>
        public XenoChart Downsample(int maximum)
        {
            int max = Math.Max(1, maximum);
            int count = BucketCount;
            if (count <= max || Kind == XenoChartKindEnum.HorizontalBars || Kind == XenoChartKindEnum.Heatmap) return this;
            int group = (int)Math.Ceiling(count / (double)max);
            List<string> labels = new List<string>();
            for (int i = 0; i < count; i += group) labels.Add(i < Labels.Count ? Labels[i] : String.Empty);
            List<XenoChartSeries> series = new List<XenoChartSeries>();
            foreach (XenoChartSeries item in Series)
            {
                List<double> values = new List<double>();
                for (int i = 0; i < count; i += group)
                {
                    List<double> slice = item.Values.Skip(i).Take(group).ToList();
                    List<double> known = slice.Where(value => !Double.IsNaN(value)).ToList();
                    if (Kind == XenoChartKindEnum.Lines) values.Add(known.Count == 0 ? Double.NaN : Additive ? known.Sum() : item.Name == "Average" ? known.Average() : item.Name == "Minimum" ? known.Min() : known.Max());
                    else values.Add(known.Count == 0 ? 0 : Additive ? known.Sum() : known.Average());
                }

                series.Add(new XenoChartSeries(item.Name, item.Tone, values));
            }

            List<IReadOnlyList<double>> grid = new List<IReadOnlyList<double>>();
            foreach (IReadOnlyList<double> row in Grid)
            {
                List<double> cells = new List<double>();
                for (int i = 0; i < row.Count; i += group) cells.Add(row.Skip(i).Take(group).Max());
                grid.Add(cells);
            }

            List<XenoChartBucket> buckets = new List<XenoChartBucket>();
            for (int i = 0; i < Buckets.Count; i += group) buckets.Add(new XenoChartBucket(Buckets[i].StartUtc, Buckets[Math.Min(Buckets.Count - 1, i + group - 1)].EndUtc));
            return new XenoChart(Id, Kind, Title, RangeText, Unit, labels, series, EmptyText, Additive, HasData, Stacked, RowLabels, grid, buckets, Notice);
        }

        #endregion
    }
}

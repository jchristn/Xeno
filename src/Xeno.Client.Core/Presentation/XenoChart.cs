namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;

    /// <summary>
    /// A column chart's content, built once by <see cref="XenoCharts"/> and drawn by the desktop and terminal apps: a
    /// title, the range it covers, bucket labels, one or more series, a unit, and what to say when there is no data.
    /// Series are stacked. Immutable.
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
                for (int i = 0; i < BucketCount; i++) maximum = Math.Max(maximum, Total(i));
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
            string number = value >= 100 || (Additive && value == Math.Floor(value))
                ? value.ToString("0", CultureInfo.InvariantCulture)
                : value.ToString("0.#", CultureInfo.InvariantCulture);
            return number + Unit;
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
            if (count <= max) return this;
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
                    values.Add(slice.Count == 0 ? 0 : Additive ? slice.Sum() : slice.Average());
                }

                series.Add(new XenoChartSeries(item.Name, item.Tone, values));
            }

            return new XenoChart(Title, RangeText, Unit, labels, series, EmptyText, Additive, HasData);
        }

        #endregion
    }
}

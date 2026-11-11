namespace Xeno.Client.Presentation
{
    /// <summary>
    /// How a <see cref="XenoChart"/> is drawn. Every front end draws every kind.
    /// </summary>
    public enum XenoChartKindEnum
    {
        /// <summary>
        /// Columns per bucket, series stacked (or side by side when not stacked).
        /// </summary>
        StackedColumns,

        /// <summary>
        /// One line (or, in the terminal, one marker) per series across buckets; series are not stacked.
        /// </summary>
        Lines,

        /// <summary>
        /// One horizontal bar per label, series stacked (or side by side when not stacked).
        /// </summary>
        HorizontalBars,

        /// <summary>
        /// A grid of cells shaded by value; rows are <see cref="XenoChart.RowLabels"/>, columns are labels.
        /// </summary>
        Heatmap,

        /// <summary>
        /// One row of status cells per item (for example a provider); cell values are <see cref="XenoChartStatusEnum"/>.
        /// </summary>
        StatusStrip
    }
}

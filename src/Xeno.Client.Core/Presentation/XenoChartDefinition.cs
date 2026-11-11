namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One chart in the shared catalog.
    /// </summary>
    public sealed class XenoChartDefinition
    {
        /// <summary>
        /// Catalog id, for example usage.latency.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// How the chart is drawn.
        /// </summary>
        public XenoChartKindEnum Kind { get; set; } = XenoChartKindEnum.StackedColumns;

        /// <summary>
        /// English title (desktop and terminal).
        /// </summary>
        public string Title { get; set; } = String.Empty;

        /// <summary>
        /// i18n key for the title (web).
        /// </summary>
        public string TitleKey { get; set; } = String.Empty;

        /// <summary>
        /// Unit appended to values.
        /// </summary>
        public string Unit { get; set; } = String.Empty;

        /// <summary>
        /// True for counts.
        /// </summary>
        public bool Additive { get; set; } = true;

        /// <summary>
        /// True when series stack.
        /// </summary>
        public bool Stacked { get; set; } = true;

        /// <summary>
        /// True when series come from the data (group keys or stages) rather than from Series.
        /// </summary>
        public bool DynamicSeries { get; set; } = false;

        /// <summary>
        /// True when the chart applies only to the chat dataset.
        /// </summary>
        public bool ChatOnly { get; set; } = false;

        /// <summary>
        /// Fixed series, in stacking and legend order.
        /// </summary>
        public List<XenoChartSeriesDefinition> Series { get; set; } = new List<XenoChartSeriesDefinition>();
    }
}

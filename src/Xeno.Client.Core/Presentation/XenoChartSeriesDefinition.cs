namespace Xeno.Client.Presentation
{
    using System;

    /// <summary>
    /// One fixed series of a catalog chart.
    /// </summary>
    public sealed class XenoChartSeriesDefinition
    {
        /// <summary>
        /// Stable key, for example p95.
        /// </summary>
        public string Key { get; set; } = String.Empty;

        /// <summary>
        /// English label (desktop and terminal).
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// i18n key for the label (web).
        /// </summary>
        public string LabelKey { get; set; } = String.Empty;

        /// <summary>
        /// Color role.
        /// </summary>
        public XenoChartToneEnum Tone { get; set; } = XenoChartToneEnum.Accent;
    }
}

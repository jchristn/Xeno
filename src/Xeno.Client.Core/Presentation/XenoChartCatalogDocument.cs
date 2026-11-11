namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// The serialized chart catalog shared with the web dashboards.
    /// </summary>
    public sealed class XenoChartCatalogDocument
    {
        /// <summary>
        /// Charts.
        /// </summary>
        public List<XenoChartDefinition> Charts { get; set; } = new List<XenoChartDefinition>();

        /// <summary>
        /// Categorical tones for dynamic series, in order.
        /// </summary>
        public List<XenoChartToneEnum> CategoryTones { get; set; } = new List<XenoChartToneEnum>();

        /// <summary>
        /// Hex colors for the categorical tones, in order.
        /// </summary>
        public List<string> CategoryColors { get; set; } = new List<string>();

        /// <summary>
        /// Hex color for the neutral tone.
        /// </summary>
        public string NeutralColor { get; set; } = String.Empty;
    }
}

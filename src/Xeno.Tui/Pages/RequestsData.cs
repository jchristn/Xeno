namespace Xeno.Tui.Pages
{
    using System.Collections.Generic;
    using Xeno.Client.Presentation;

    /// <summary>
    /// Data loaded for the Requests page.
    /// </summary>
    internal sealed class RequestsData
    {
        /// <summary>
        /// Requests over time.
        /// </summary>
        public XenoChart Chart { get; }

        /// <summary>
        /// Request rows.
        /// </summary>
        public List<RequestRow> Rows { get; }

        /// <summary>
        /// Create the data.
        /// </summary>
        /// <param name="chart">Requests over time.</param>
        /// <param name="rows">Rows.</param>
        public RequestsData(XenoChart chart, List<RequestRow> rows)
        {
            Chart = chart;
            Rows = rows;
        }
    }
}

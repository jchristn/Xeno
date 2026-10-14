namespace Xeno.Tui.Pages
{
    using System.Collections.Generic;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;

    /// <summary>
    /// Data loaded for the Overview page.
    /// </summary>
    internal sealed class OverviewData
    {
        /// <summary>
        /// Request statistics.
        /// </summary>
        public LocalDashboardStatisticsResponse Statistics { get; }

        /// <summary>
        /// Requests over time.
        /// </summary>
        public XenoChart Chart { get; }

        /// <summary>
        /// Recent requests.
        /// </summary>
        public List<RequestRow> Recent { get; }

        /// <summary>
        /// Model catalog.
        /// </summary>
        public ModelCatalog Catalog { get; }

        /// <summary>
        /// Create the data.
        /// </summary>
        /// <param name="statistics">Statistics.</param>
        /// <param name="chart">Requests over time.</param>
        /// <param name="recent">Recent requests.</param>
        /// <param name="catalog">Catalog.</param>
        public OverviewData(LocalDashboardStatisticsResponse statistics, XenoChart chart, List<RequestRow> recent, ModelCatalog catalog)
        {
            Statistics = statistics;
            Chart = chart;
            Recent = recent;
            Catalog = catalog;
        }
    }
}

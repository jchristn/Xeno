namespace Xeno.Tui.Pages
{
    using System.Collections.Generic;
    using Xeno.Client.Presentation;
    using Xeno.Client.Routing;

    /// <summary>
    /// What the terminal Local endpoint page loads in one pass.
    /// </summary>
    internal sealed class LocalEndpointData
    {
        /// <summary>
        /// Routing view.
        /// </summary>
        public LocalInferenceRoutingView View { get; }

        /// <summary>
        /// Traffic chart.
        /// </summary>
        public XenoChart Chart { get; }

        /// <summary>
        /// Rule rows.
        /// </summary>
        public List<RouteRuleRow> Rules { get; }

        /// <summary>
        /// Recent request rows.
        /// </summary>
        public List<LocalInferenceRequestRow> Requests { get; }

        /// <summary>
        /// Create the data.
        /// </summary>
        /// <param name="view">Routing view.</param>
        /// <param name="chart">Traffic chart.</param>
        /// <param name="rules">Rule rows.</param>
        /// <param name="requests">Recent request rows.</param>
        public LocalEndpointData(LocalInferenceRoutingView view, XenoChart chart, List<RouteRuleRow> rules, List<LocalInferenceRequestRow> requests)
        {
            View = view;
            Chart = chart;
            Rules = rules;
            Requests = requests;
        }
    }
}

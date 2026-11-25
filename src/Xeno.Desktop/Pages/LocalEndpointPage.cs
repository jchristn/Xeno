namespace Xeno.Desktop.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia.Controls;
    using Xeno.Client.Presentation;
    using Xeno.Client.Routing;
    using Xeno.Client.Services;
    using Xeno.Desktop.Common;
    using Xeno.Desktop.Controls;
    using Xeno.Desktop.Dialogs;

    /// <summary>
    /// Local endpoint: the URL apps on this machine call, where each model goes, provider health and load, and recent requests.
    /// </summary>
    public sealed class LocalEndpointPage : DesktopPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override bool AutoRefreshes
        {
            get { return !_DrillActive; }
        }

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.LocalEndpoint; }
        }

        #endregion

        #region Private-Members

        private bool _DrillActive = false;
        private readonly StackPanel _Summary = new StackPanel();
        private readonly ColumnChart _Chart = new ColumnChart();
        private XenoTable<LocalBackendRow>? _Backends;
        private XenoTable<RouteRuleRow>? _Rules;
        private XenoTable<LocalInferenceRequestRow>? _Requests;
        private LocalInferenceRoutingView? _View;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="session">Session.</param>
        /// <param name="window">Main window.</param>
        public LocalEndpointPage(DesktopSession session, MainWindow window) : base(session, window)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override IReadOnlyList<Control> Actions()
        {
            return new List<Control>
            {
                Ui.Button("Refresh", () => _ = RefreshAsync()),
                Ui.CopyButton("Copy the endpoint URL", () => _ = XenoClipboard.CopyAsync(Window, _View?.Url ?? String.Empty)),
                Ui.Button("Test a Route...", () => _ = ClientDialogs.TestRouteAsync(Window, Session, String.Empty)),
                Ui.Button("Check Providers", () => _ = CheckAsync()),
                Ui.Primary("Add Rule...", () => _ = ClientDialogs.RouteRuleAsync(Window, Session, null, null, () => _ = RefreshAsync()))
            };
        }

        /// <inheritdoc />
        public override Control Build()
        {
            _Backends = new XenoTable<LocalBackendRow>(new List<XenoTableColumn<LocalBackendRow>>
            {
                new XenoTableColumn<LocalBackendRow>("Provider", row => row.Provider).Star(2),
                new XenoTableColumn<LocalBackendRow>("API type", row => row.ApiType).Fixed(130),
                new XenoTableColumn<LocalBackendRow>("Health", row => row.Health).Fixed(130),
                new XenoTableColumn<LocalBackendRow>("Latency", row => row.Latency).Fixed(90),
                new XenoTableColumn<LocalBackendRow>("In use", row => row.Load).Star(2),
                new XenoTableColumn<LocalBackendRow>("Shared", row => row.Shared).Fixed(100).Hidden(),
                new XenoTableColumn<LocalBackendRow>("Last error", row => row.LastError).Star(3)
            }, BackendActions, "No providers. Add one on the Providers page; its models are then answered on this machine.");
            _Backends.Height = 200;
            _Backends.RowActivated += (sender, row) => Window.Navigate(XenoPageEnum.Providers);

            _Rules = new XenoTable<RouteRuleRow>(new List<XenoTableColumn<RouteRuleRow>>
            {
                new XenoTableColumn<RouteRuleRow>("Model", row => row.Model) { Monospace = true }.Star(3),
                new XenoTableColumn<RouteRuleRow>("Send to", row => row.Target).Star(2),
                new XenoTableColumn<RouteRuleRow>("Fallback", row => row.Fallback).Fixed(90),
                new XenoTableColumn<RouteRuleRow>("Goes to now", row => row.RoutesTo).Star(2)
            }, RuleActions, "No route rules. Models served on this machine are answered here; everything else goes to the Xeno network.");
            _Rules.Height = 200;
            _Rules.RowActivated += (sender, row) => _ = ClientDialogs.RouteRuleAsync(Window, Session, row.Source, null, () => _ = RefreshAsync());

            _Requests = new XenoTable<LocalInferenceRequestRow>(new List<XenoTableColumn<LocalInferenceRequestRow>>
            {
                new XenoTableColumn<LocalInferenceRequestRow>("When", row => row.When) { SortKey = row => row.Source.ReceivedUtc }.Fixed(130),
                new XenoTableColumn<LocalInferenceRequestRow>("Model", row => row.Model) { Monospace = true }.Star(3),
                new XenoTableColumn<LocalInferenceRequestRow>("Went to", row => row.Route).Star(2),
                new XenoTableColumn<LocalInferenceRequestRow>("Status", row => row.Status).Fixed(70),
                new XenoTableColumn<LocalInferenceRequestRow>("Latency", row => row.Latency) { SortKey = row => row.Source.LatencyMs }.Fixed(90),
                new XenoTableColumn<LocalInferenceRequestRow>("Tokens in / out", row => row.Tokens).Fixed(130)
            }, RequestActions, "No requests yet. Point an app at the local endpoint URL above.");
            _Requests.Height = 260;

            return Ui.Stack(16,
                Ui.Card("Local endpoint", _Summary),
                Ui.Card("Traffic", _Chart),
                Ui.Card("Providers on this machine", _Backends, Ui.Button(XenoModelManagement.ActionText + "...", () =>
                {
                    LocalBackendRow? selected = _Backends?.SelectedRow;
                    if (selected == null) Report("Select a provider to manage its models.");
                    else _ = ClientDialogs.ManageModelsAsync(Window, Session, selected.Provider, () => _ = RefreshAsync());
                }, "See the selected provider's live models, pull new ones, and delete ones you no longer need.")),
                Ui.Card("Route rules", _Rules, Ui.Button("Add Rule...", () => _ = ClientDialogs.RouteRuleAsync(Window, Session, null, null, () => _ = RefreshAsync()))),
                Ui.Card("Recent requests", _Requests));
        }

        /// <inheritdoc />
        public override async Task RefreshAsync()
        {
            await GuardAsync("Local endpoint", async () =>
            {
                _View = await Workspace.GetLocalInferenceRoutingAsync(None).ConfigureAwait(true);
                List<LocalInferenceRequestRecord> records = await Workspace.GetLocalInferenceRequestsAsync(0, 500, None).ConfigureAwait(true);
                XenoDrillDownRequest? window = XenoDrillDown.Take(Page);
                _DrillActive = window != null;
                if (window != null) Report("Showing " + XenoDrillDown.Describe(window) + " from the Usage page. Refresh to see every recent request.");
                RenderSummary(_View);
                _Chart.SetChart(XenoCharts.LocalEndpointTraffic(records, DateTime.UtcNow));
                _Backends?.SetRows(_View.Backends.Select(LocalBackendRow.From).ToList(), (left, right) => String.Equals(left.Provider, right.Provider, StringComparison.OrdinalIgnoreCase));
                List<RouteRuleRow> rules = new List<RouteRuleRow>();
                foreach (Xeno.Client.LocalInferenceRouteRule rule in _View.Rules)
                {
                    LocalInferenceRouteDecision decision = await Workspace.ResolveRouteAsync(rule.Model, None).ConfigureAwait(true);
                    rules.Add(RouteRuleRow.From(rule, decision));
                }

                _Rules?.SetRows(rules, (left, right) => String.Equals(left.Model, right.Model, StringComparison.OrdinalIgnoreCase));
                _Requests?.SetRows((window == null ? records : records.Where(item => window.Matches(item.ReceivedUtc, item.Model)).ToList()).Take(100).Select(LocalInferenceRequestRow.From).ToList());
            }).ConfigureAwait(true);
        }

        #endregion

        #region Private-Methods

        private void RenderSummary(LocalInferenceRoutingView view)
        {
            _Summary.Children.Clear();
            string status = !view.Enabled
                ? "disabled (turn it on in Settings)"
                : view.RuntimeAvailable ? "listening" : "enabled, client not running";
            _Summary.Children.Add(Ui.Details(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("URL", view.Url),
                new KeyValuePair<string, string>("Status", status),
                new KeyValuePair<string, string>("Routing", view.RoutingMode),
                new KeyValuePair<string, string>("Providers", view.RuntimeAvailable ? view.HealthyBackends.ToString(CultureInfo.InvariantCulture) + " of " + view.RoutableBackends.ToString(CultureInfo.InvariantCulture) + " healthy" : view.RoutableBackends.ToString(CultureInfo.InvariantCulture) + " used for local requests"),
                new KeyValuePair<string, string>("Since start", DisplayFormat.Number(view.LocalRequests) + " here, " + DisplayFormat.Number(view.NetworkRequests) + " network, " + DisplayFormat.Number(view.Fallbacks) + " fallbacks, " + DisplayFormat.Number(view.Errors) + " errors"),
                new KeyValuePair<string, string>("Default limit", view.DefaultMaxConcurrentRequests.ToString(CultureInfo.InvariantCulture) + " concurrent requests per provider")
            }, 130));
        }

        private async Task CheckAsync()
        {
            await GuardAsync("Check providers", async () =>
            {
                IReadOnlyCollection<LocalBackendStatus> statuses = await Workspace.CheckBackendsAsync(None).ConfigureAwait(true);
                int healthy = statuses.Count(item => item.Healthy == true);
                Report("Checked " + statuses.Count.ToString(CultureInfo.InvariantCulture) + " provider" + (statuses.Count == 1 ? String.Empty : "s") + ": " + healthy.ToString(CultureInfo.InvariantCulture) + " healthy.");
                await RefreshAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        private IReadOnlyList<XenoRowAction<LocalBackendRow>> BackendActions(LocalBackendRow row)
        {
            return new List<XenoRowAction<LocalBackendRow>>
            {
                new XenoRowAction<LocalBackendRow>(XenoModelManagement.ActionText + "...", item => _ = ClientDialogs.ManageModelsAsync(Window, Session, item.Provider, () => _ = RefreshAsync())),
                new XenoRowAction<LocalBackendRow>("Open Providers", item => Window.Navigate(XenoPageEnum.Providers)),
                new XenoRowAction<LocalBackendRow>("Copy Endpoint", item => _ = XenoClipboard.CopyAsync(Window, item.Source.Endpoint))
            };
        }

        private IReadOnlyList<XenoRowAction<RouteRuleRow>> RuleActions(RouteRuleRow row)
        {
            return new List<XenoRowAction<RouteRuleRow>>
            {
                new XenoRowAction<RouteRuleRow>("Edit...", item => _ = ClientDialogs.RouteRuleAsync(Window, Session, item.Source, null, () => _ = RefreshAsync())),
                new XenoRowAction<RouteRuleRow>("Test", item => _ = ClientDialogs.TestRouteAsync(Window, Session, item.Model)),
                new XenoRowAction<RouteRuleRow>("Delete...", item => _ = ClientDialogs.DeleteRouteRuleAsync(Window, Session, item.Model, () => _ = RefreshAsync())) { Destructive = true }
            };
        }

        private IReadOnlyList<XenoRowAction<LocalInferenceRequestRow>> RequestActions(LocalInferenceRequestRow row)
        {
            return new List<XenoRowAction<LocalInferenceRequestRow>>
            {
                new XenoRowAction<LocalInferenceRequestRow>("Test This Model's Route", item => _ = ClientDialogs.TestRouteAsync(Window, Session, item.Source.Model)),
                new XenoRowAction<LocalInferenceRequestRow>("Add Rule for This Model...", item => _ = ClientDialogs.RouteRuleAsync(Window, Session, null, item.Source.Model, () => _ = RefreshAsync()))
            };
        }

        #endregion
    }
}

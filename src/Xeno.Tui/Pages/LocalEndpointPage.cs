namespace Xeno.Tui.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Widgets;
    using Xeno.Client;
    using Xeno.Client.Presentation;
    using Xeno.Client.Routing;
    using Xeno.Client.Services;
    using Xeno.Tui.Shell;

    /// <summary>
    /// Local endpoint: the URL apps on this machine call, where each model goes, provider health and load, and recent requests.
    /// </summary>
    internal sealed class LocalEndpointPage : TuiPage
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

        /// <inheritdoc />
        public override IWidget Root
        {
            get { return _Root; }
        }

        /// <inheritdoc />
        public override IReadOnlyList<KeyHint> Hints
        {
            get { return new List<KeyHint> { new KeyHint("a", "Add rule"), new KeyHint("e", "Edit"), new KeyHint("d", "Delete"), new KeyHint("t", "Test route"), new KeyHint("c", "Check providers"), new KeyHint("p", "Manage models") }; }
        }

        #endregion

        #region Private-Members

        private bool _DrillActive = false;
        private readonly FramedStack _Root = new FramedStack(SplitOrientation.Vertical);
        private readonly DetailsView _Summary = new DetailsView();
        private readonly ButtonRow _Actions = new ButtonRow();
        private readonly ChartView _Chart = new ChartView();
        private readonly DataTable<LocalBackendRow> _Backends;
        private readonly DataTable<RouteRuleRow> _Rules;
        private readonly DataTable<LocalInferenceRequestRow> _Requests;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="context">Context.</param>
        public LocalEndpointPage(TuiContext context) : base(context)
        {
            _Backends = Table<LocalBackendRow>("No providers. Add one on the Providers page; its models are then answered on this machine.", row => row.Provider);
            Column(_Backends, "Provider", row => row.Provider, 0);
            Column(_Backends, "API type", row => row.ApiType, 17);
            Column(_Backends, "Health", row => row.Health, 17);
            Column(_Backends, "Latency", row => row.Latency, 8);
            Column(_Backends, "In use", row => row.Load, 24);
            Column(_Backends, "Shared", row => row.Shared, 10);
            _Backends.RowActivated += index => Context.Navigate(XenoPageEnum.Providers);

            _Rules = Table<RouteRuleRow>("No route rules. Press a to add one; models served here are answered here and the rest go to the network.", row => row.Model);
            Column(_Rules, "Model", row => row.Model, 0);
            Column(_Rules, "Send to", row => row.Target, 22);
            Column(_Rules, "Fallback", row => row.Fallback, 9);
            Column(_Rules, "Goes to now", row => row.RoutesTo, 24);
            _Rules.RowActivated += index => EditRule();

            _Requests = Table<LocalInferenceRequestRow>("No requests yet. Point an app at the local endpoint URL.", row => row.Source.Id);
            Column(_Requests, "When", row => row.When, 15);
            Column(_Requests, "Model", row => row.Model, 0);
            Column(_Requests, "Went to", row => row.Route, 24);
            Column(_Requests, "Status", row => row.Status, 7);
            Column(_Requests, "Latency", row => row.Latency, 9);
            _Requests.RowActivated += index => _ = TestRouteAsync(_Requests.SelectedRow?.Source.Model);

            _Actions.Add("Add Rule", () => _ = AddRuleAsync());
            _Actions.Add("Test a Route", () => _ = TestRouteAsync(null));
            _Actions.Add("Check Providers", () => _ = CheckAsync());
            _Actions.Add(XenoModelManagement.ActionText, ManageModels);
            _Actions.Add("Refresh", () => _ = RefreshAsync());

            Frame(_Root, _Summary, StackSize.Fixed(4), "Local endpoint");
            Frame(_Root, _Actions, StackSize.Fixed(1), "Actions");
            Frame(_Root, _Chart, StackSize.Fixed(7));
            Frame(_Root, _Backends, StackSize.Weighted(1, 4), "Providers on this machine");
            Frame(_Root, _Rules, StackSize.Weighted(1, 4), "Route rules");
            Frame(_Root, _Requests, StackSize.Weighted(1, 4), "Recent requests");
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override Task RefreshAsync()
        {
            XenoDrillDownRequest? window = XenoDrillDown.Take(Page);
            _DrillActive = window != null;
            return LoadAsync(async token =>
            {
                LocalInferenceRoutingView view = await Workspace.GetLocalInferenceRoutingAsync(token).ConfigureAwait(false);
                List<LocalInferenceRequestRecord> records = await Workspace.GetLocalInferenceRequestsAsync(0, 500, token).ConfigureAwait(false);
                List<RouteRuleRow> rules = new List<RouteRuleRow>();
                foreach (LocalInferenceRouteRule rule in view.Rules)
                {
                    LocalInferenceRouteDecision decision = await Workspace.ResolveRouteAsync(rule.Model, token).ConfigureAwait(false);
                    rules.Add(RouteRuleRow.From(rule, decision));
                }

                return new LocalEndpointData(view, XenoCharts.LocalEndpointTraffic(records, DateTime.UtcNow), rules, (window == null ? records : records.Where(item => window.Matches(item.ReceivedUtc, item.Model)).ToList()).Take(100).Select(LocalInferenceRequestRow.From).ToList());
            }, data =>
            {
                RenderSummary(data.View);
                _Chart.Chart = data.Chart;
                _Backends.Bind(data.View.Backends.Select(LocalBackendRow.From).ToList());
                _Rules.Bind(data.Rules);
                _Requests.Bind(data.Requests);
            });
        }

        /// <inheritdoc />
        public override bool HandleKey(KeyEvent key)
        {
            if (PageKeys.Is(key, 'a'))
            {
                _ = AddRuleAsync();
                return true;
            }

            if (PageKeys.Is(key, 'e'))
            {
                EditRule();
                return true;
            }

            if (PageKeys.Is(key, 'd'))
            {
                _ = DeleteRuleAsync();
                return true;
            }

            if (PageKeys.Is(key, 't'))
            {
                _ = TestRouteAsync(_Rules.SelectedRow?.Model);
                return true;
            }

            if (PageKeys.Is(key, 'c'))
            {
                _ = CheckAsync();
                return true;
            }

            if (PageKeys.Is(key, 'p'))
            {
                ManageModels();
                return true;
            }

            return false;
        }

        #endregion

        #region Private-Methods

        private void RenderSummary(LocalInferenceRoutingView view)
        {
            _Summary.Clear();
            string status = !view.Enabled ? "disabled (turn it on in Settings)" : view.RuntimeAvailable ? "listening" : "enabled, client not running";
            _Summary.Set("URL", view.Url + "  " + status + "  routing " + view.RoutingMode);
            _Summary.Set("Providers", view.RuntimeAvailable
                ? view.HealthyBackends.ToString(CultureInfo.InvariantCulture) + " of " + view.RoutableBackends.ToString(CultureInfo.InvariantCulture) + " healthy, default limit " + view.DefaultMaxConcurrentRequests.ToString(CultureInfo.InvariantCulture) + " each"
                : view.RoutableBackends.ToString(CultureInfo.InvariantCulture) + " used for local requests, default limit " + view.DefaultMaxConcurrentRequests.ToString(CultureInfo.InvariantCulture) + " each");
            _Summary.Set("Since start", DisplayFormat.Number(view.LocalRequests) + " here, " + DisplayFormat.Number(view.NetworkRequests) + " network, " + DisplayFormat.Number(view.Fallbacks) + " fallbacks, " + DisplayFormat.Number(view.Errors) + " errors");
        }

        private void ManageModels()
        {
            LocalBackendRow? row = _Backends.SelectedRow;
            if (row == null) Context.Notify("Select a provider on this machine to manage its models.", NotificationSeverity.Info);
            else _ = Context.ManageModelsAsync(row.Provider, () => _ = RefreshAsync());
        }

        private Task AddRuleAsync()
        {
            return Context.FormAsync(XenoForms.RouteRule(Workspace, null, null), () => _ = RefreshAsync());
        }

        private void EditRule()
        {
            RouteRuleRow? row = _Rules.SelectedRow;
            if (row != null) _ = Context.FormAsync(XenoForms.RouteRule(Workspace, row.Source, null), () => _ = RefreshAsync());
        }

        private async Task DeleteRuleAsync()
        {
            RouteRuleRow? row = _Rules.SelectedRow;
            if (row == null) return;
            bool confirmed = await Context.ConfirmAsync("Delete Route Rule", "Delete the route rule for '" + row.Model + "'? The model goes back to the default route.", "Delete").ConfigureAwait(false);
            if (!confirmed) return;
            await Context.RunAsync(async token =>
            {
                int removed = await Workspace.DeleteRouteRuleAsync(row.Model, token).ConfigureAwait(false);
                return removed > 0 ? "Deleted the route rule for '" + row.Model + "'." : "No route rule for '" + row.Model + "'.";
            }, () => _ = RefreshAsync()).ConfigureAwait(false);
        }

        private async Task TestRouteAsync(string? initial)
        {
            Dictionary<string, string>? values = await Context.FormAsync(
                "Test a Route",
                "Where would the local endpoint send this model?",
                new List<XenoFormFieldSpec> { new XenoFormFieldSpec { Key = "model", Label = "Model", Required = true, Value = initial ?? String.Empty, Placeholder = "llama3.1:8b" } },
                null,
                "Test",
                "Resolving...").ConfigureAwait(false);
            if (values == null || !values.TryGetValue("model", out string? model) || String.IsNullOrWhiteSpace(model)) return;
            LocalInferenceRouteDecision decision = await Workspace.ResolveRouteAsync(model, System.Threading.CancellationToken.None).ConfigureAwait(false);
            await Context.ShowMarkdownAsync("Where " + model + " goes", LocalInferenceRoutingService.ExplainMarkdown(decision)).ConfigureAwait(false);
        }

        private async Task CheckAsync()
        {
            Context.Progress("Checking providers...");
            await Context.LoadAsync(token => Workspace.CheckBackendsAsync(token), statuses =>
            {
                int healthy = statuses.Count(item => item.Healthy == true);
                Context.Notify("Checked " + statuses.Count.ToString(CultureInfo.InvariantCulture) + " provider" + (statuses.Count == 1 ? String.Empty : "s") + ": " + healthy.ToString(CultureInfo.InvariantCulture) + " healthy.", healthy == statuses.Count ? NotificationSeverity.Success : NotificationSeverity.Warning);
                _ = RefreshAsync();
            }).ConfigureAwait(false);
        }

        #endregion
    }
}

namespace Xeno.Tui.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using TUIKit;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Widgets;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Tui.Shell;

    /// <summary>
    /// Overview: status and actions, requests over time, local and network model counts, and recent requests.
    /// </summary>
    internal sealed class OverviewPage : TuiPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.Overview; }
        }

        /// <inheritdoc />
        public override IWidget Root
        {
            get { return _Root; }
        }

        /// <inheritdoc />
        public override bool RefreshWithStatus
        {
            get { return true; }
        }

        /// <inheritdoc />
        public override IReadOnlyList<KeyHint> Hints
        {
            get { return new List<KeyHint> { new KeyHint("s", "Start/Stop"), new KeyHint("r", "Restart"), new KeyHint("w", "Web dashboard") }; }
        }

        #endregion

        #region Private-Members

        private readonly FramedStack _Root = new FramedStack(SplitOrientation.Vertical);
        private readonly DetailsView _Status = new DetailsView();
        private readonly ButtonRow _Actions = new ButtonRow();
        private readonly ChartView _Chart = new ChartView();
        private readonly Dropdown<string> _Range = new Dropdown<string>(HistoryRanges.Labels);
        private readonly DataTable<RequestRow> _Recent;
        private LocalDashboardStatisticsResponse? _Statistics;
        private ModelCatalog? _Catalog;
        private string _ActionKey = String.Empty;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="context">Context.</param>
        public OverviewPage(TuiContext context) : base(context)
        {
            _Recent = Table<RequestRow>("No requests yet. Requests appear when the Xeno network routes work here.", row => row.Id);
            Column(_Recent, "Received", row => row.Received, 20);
            Column(_Recent, "Model", row => row.Model, 0);
            Column(_Recent, "Status", row => row.Status, 7);
            Column(_Recent, "Duration", row => row.Duration, 9);
            _Recent.RowActivated += index => Context.Navigate(XenoPageEnum.Requests);
            _Range.SelectedIndex = 0;
            _Range.SelectionChanged += (sender, args) => _ = RefreshAsync();
            Frame(_Root, _Status, StackSize.Fixed(9), "Status");
            Frame(_Root, _Actions, StackSize.Fixed(1), "Actions");
            Frame(_Root, _Range, StackSize.Fixed(1), "Chart range");
            Frame(_Root, _Chart, StackSize.Fixed(9));
            Frame(_Root, _Recent, StackSize.Weighted(1, 3), "Recent requests");
            RenderStatus();
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override Task RefreshAsync()
        {
            HistoryRangeEnum range = HistoryRanges.FromIndex(_Range.SelectedIndex);
            return LoadAsync(async token =>
            {
                LocalDashboardStatisticsResponse statistics = await Workspace.GetStatisticsAsync(token).ConfigureAwait(false);
                List<LocalRequestHistorySummaryBucket> buckets = await Workspace.SummarizeRequestsAsync(range, token).ConfigureAwait(false);
                List<RequestRow> recent = await Workspace.ListRequestsAsync(10, token).ConfigureAwait(false);
                ModelCatalog catalog = await Workspace.GetModelCatalogAsync(true, token).ConfigureAwait(false);
                return new OverviewData(statistics, XenoCharts.Requests(buckets, range), recent, catalog);
            }, data =>
            {
                _Statistics = data.Statistics;
                _Catalog = data.Catalog;
                _Chart.Chart = data.Chart;
                _Recent.Bind(data.Recent);
                RenderStatus();
            });
        }

        /// <inheritdoc />
        public override void OnStatusChanged(ClientStatusSnapshot status)
        {
            RenderStatus();
        }

        /// <inheritdoc />
        public override bool HandleKey(KeyEvent key)
        {
            ClientStatusSnapshot? status = Context.Status;
            if (PageKeys.Is(key, 's'))
            {
                Context.Execute(status != null && status.Running ? XenoCommandEnum.StopClient : status?.Status == ClientStatusEnum.NotSignedIn ? XenoCommandEnum.SignIn : XenoCommandEnum.StartClient);
                return true;
            }

            if (PageKeys.Is(key, 'r'))
            {
                Context.Execute(XenoCommandEnum.RestartClient);
                return true;
            }

            if (PageKeys.Is(key, 'w'))
            {
                Context.Execute(XenoCommandEnum.OpenWebDashboard);
                return true;
            }

            return false;
        }

        #endregion

        #region Private-Methods

        private void RenderStatus()
        {
            ClientStatusSnapshot? status = Context.Status;
            _Status.Clear();
            if (status == null)
            {
                _Status.Set("Status", "checking...");

                // Show the actions that do not depend on status, so Actions is never empty and hidden: the page takes
                // focus before the first status arrives, and an empty Actions would send it to the range picker.
                if (_Actions.FocusedLeaf == null) AddCommonActions();
                return;
            }

            _Status.Set("Status", (Context.Busy ? "working... " : String.Empty) + status.StatusText + " - " + status.StatusDetail);
            _Status.Set("Client", DisplayFormat.OrDash(status.Name) + " (" + DisplayFormat.OrDash(status.ClientId) + ")");
            _Status.Set("Xeno server", DisplayFormat.OrDash(status.RestBaseUrl) + (status.HasCredential ? "  signed in" : "  not signed in"));
            _Status.Set("Served here", (_Catalog?.LocalCount ?? status.ModelCount).ToString(CultureInfo.InvariantCulture) + " models on " + status.ProviderCount + " provider" + (status.ProviderCount == 1 ? String.Empty : "s"));
            _Status.Set("On the network", _Catalog == null ? "checking..." : _Catalog.NetworkAvailable ? _Catalog.NetworkCount + " models available to you" : "unavailable (" + _Catalog.NetworkMessage + ")");
            _Status.Set("Requests", _Statistics == null ? DisplayFormat.Dash : DisplayFormat.Number(_Statistics.Requests) + " served, " + DisplayFormat.Number(_Statistics.Failures) + " failed, average " + DisplayFormat.Milliseconds(_Statistics.AverageLatencyMs));
            _Status.Set("Web dashboard", !status.LocalDashboardEnabled ? "disabled" : status.LocalDashboardListening ? status.LocalDashboardUrl : "not running");
            _Status.Set("Local inference", !status.LocalInferenceEnabled ? "disabled" : status.LocalInferenceListening ? status.LocalInferenceUrl : "enabled, not running");
            _Status.Set("Last heartbeat", DisplayFormat.Ago(status.LastHeartbeatUtc, DateTime.UtcNow));

            string actionKey = status.Status + "|" + status.Running + "|" + status.CanOpenLocalDashboard;
            if (actionKey == _ActionKey) return;
            _ActionKey = actionKey;
            _Actions.Clear();
            if (status.Status == ClientStatusEnum.NotSignedIn) _Actions.Add("Sign In", () => Context.Execute(XenoCommandEnum.SignIn));
            else if (!status.Running) _Actions.Add("Start Client", () => Context.Execute(XenoCommandEnum.StartClient));
            else
            {
                _Actions.Add("Stop", () => Context.Execute(XenoCommandEnum.StopClient));
                _Actions.Add("Restart", () => Context.Execute(XenoCommandEnum.RestartClient));
            }

            Button web = _Actions.Add("Web Dashboard", () => Context.Execute(XenoCommandEnum.OpenWebDashboard));
            web.IsEnabled = status.CanOpenLocalDashboard;
            AddCommonActions();
        }

        private void AddCommonActions()
        {
            _Actions.Add("Add Provider", () => Context.Execute(XenoCommandEnum.NewProvider));
            _Actions.Add("Models", () => Context.Navigate(XenoPageEnum.Models));
        }

        #endregion
    }
}

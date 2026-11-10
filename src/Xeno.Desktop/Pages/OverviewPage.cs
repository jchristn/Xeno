namespace Xeno.Desktop.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia.Controls;
    using Xeno.Client;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Desktop.Common;
    using Xeno.Desktop.Controls;

    /// <summary>
    /// Overview: client status and actions, request statistics, the models this machine serves and uses from the
    /// network, local services, and recent requests.
    /// </summary>
    public sealed class OverviewPage : DesktopPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.Overview; }
        }

        #endregion

        #region Private-Members

        private readonly App _App;
        private StackPanel _Hero = new StackPanel();
        private WrapPanel _Metrics = new WrapPanel();
        private StackPanel _Connection = new StackPanel();
        private StackPanel _Services = new StackPanel();
        private ColumnChart _Chart = new ColumnChart();
        private ComboBox _Range = new ComboBox();
        private XenoTable<RequestRow>? _Recent;
        private LocalDashboardStatisticsResponse? _Statistics;
        private string _PortNotice = String.Empty;
        private ModelCatalog? _Catalog;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="session">Session.</param>
        /// <param name="window">Main window.</param>
        /// <param name="app">Application, used to run commands.</param>
        public OverviewPage(DesktopSession session, MainWindow window, App app) : base(session, window)
        {
            _App = app ?? throw new ArgumentNullException(nameof(app));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override IReadOnlyList<Control> Actions()
        {
            return new List<Control> { Ui.Button("Refresh", () => _ = RefreshAsync()) };
        }

        /// <inheritdoc />
        public override Control Build()
        {
            _Hero = new StackPanel { Spacing = 12 };
            _Metrics = new WrapPanel();
            _Connection = new StackPanel();
            _Services = new StackPanel();
            _Chart = new ColumnChart();
            _Range = new ComboBox { ItemsSource = HistoryRanges.Labels, SelectedIndex = 0, MinWidth = 140 };
            _Range.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Recent = new XenoTable<RequestRow>(new List<XenoTableColumn<RequestRow>>
            {
                new XenoTableColumn<RequestRow>("Received", row => row.Received) { SortKey = row => row.ReceivedUtc }.Fixed(160),
                new XenoTableColumn<RequestRow>("Model", row => row.Model).Star(2),
                new XenoTableColumn<RequestRow>("Path", row => row.Path).Star(2),
                new XenoTableColumn<RequestRow>("Status", row => row.Status).Fixed(70),
                new XenoTableColumn<RequestRow>("Duration", row => row.Duration) { SortKey = row => row.DurationMs }.Fixed(90)
            }, null, "No requests yet. Requests appear here when the Xeno network routes work to this machine.");
            _Recent.Height = 260;
            _Recent.RowActivated += (sender, row) => Window.Navigate(XenoPageEnum.Requests);

            Grid columns = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*") };
            Border connection = Ui.Card("Connection", _Connection);
            Border services = Ui.Card("Local services", _Services);
            connection.Margin = new Avalonia.Thickness(0, 0, 8, 0);
            services.Margin = new Avalonia.Thickness(8, 0, 0, 0);
            Grid.SetColumn(services, 1);
            columns.Children.Add(connection);
            columns.Children.Add(services);

            Render();
            return Ui.Stack(16,
                Ui.Card(null, _Hero),
                _Metrics,
                Ui.Card("Requests over time", _Chart, _Range),
                columns,
                Ui.Card("Recent requests", _Recent, Ui.Button("View all", () => Window.Navigate(XenoPageEnum.Requests))));
        }

        /// <inheritdoc />
        public override async Task RefreshAsync()
        {
            await GuardAsync("Overview", async () =>
            {
                _Statistics = await Workspace.GetStatisticsAsync(None).ConfigureAwait(true);
                HistoryRangeEnum range = HistoryRanges.FromIndex(_Range.SelectedIndex);
                List<LocalRequestHistorySummaryBucket> buckets = await Workspace.SummarizeRequestsAsync(range, None).ConfigureAwait(true);
                _Chart.SetChart(XenoCharts.Requests(buckets, range));
                _Recent?.SetRows(await Workspace.ListRequestsAsync(8, None).ConfigureAwait(true));
                _Catalog = await Workspace.GetModelCatalogAsync(true, None).ConfigureAwait(true);
                Render();
            }).ConfigureAwait(true);
        }

        /// <inheritdoc />
        public override void OnStatusChanged()
        {
            Render();
        }

        #endregion

        #region Private-Methods

        private void Render()
        {
            ClientStatusSnapshot? status = Session.Status;
            _Hero.Children.Clear();
            if (status == null)
            {
                _Hero.Children.Add(Ui.Muted("Loading client status..."));
                return;
            }

            XenoToneEnum tone = status.Status == ClientStatusEnum.Connected ? XenoToneEnum.Success
                : status.Status == ClientStatusEnum.NotSignedIn ? XenoToneEnum.Danger
                : status.Running ? XenoToneEnum.Warning : XenoToneEnum.Neutral;
            _Hero.Children.Add(Ui.Row(Ui.Heading(DisplayFormat.OrDash(status.Name)), Ui.Badge(status.StatusText, tone)));
            _Hero.Children.Add(Ui.Body(status.StatusDetail));
            List<Control> buttons = new List<Control>();
            if (status.Status == ClientStatusEnum.NotSignedIn) buttons.Add(Ui.Primary("Sign In...", () => _App.Execute(XenoCommandEnum.SignIn)));
            else if (!status.Running) buttons.Add(Ui.Primary("Start Client", () => _App.Execute(XenoCommandEnum.StartClient)));
            else
            {
                buttons.Add(Ui.Button("Restart", () => _App.Execute(XenoCommandEnum.RestartClient)));
                buttons.Add(Ui.Button("Stop", () => _App.Execute(XenoCommandEnum.StopClient)));
            }

            Button dashboard = Ui.Button("Open Web Dashboard", () => _App.Execute(XenoCommandEnum.OpenWebDashboard), XenoCommandAvailability.DisabledReason(XenoCommandEnum.OpenWebDashboard, status));
            dashboard.IsEnabled = status.CanOpenLocalDashboard;
            buttons.Add(dashboard);
            buttons.Add(Ui.Button("Open Xeno Terminal", () => _App.Execute(XenoCommandEnum.OpenTerminalApp)));
            buttons.Add(Ui.Button("Add Provider...", () => _App.Execute(XenoCommandEnum.NewProvider)));
            foreach (Control button in buttons) button.IsEnabled = button.IsEnabled && !Session.Busy;
            _Hero.Children.Add(Ui.Row(buttons.ToArray()));

            _Metrics.Children.Clear();
            Control[] metrics = new Control[]
            {
                Ui.Metric("Requests served", DisplayFormat.Number(_Statistics?.Requests), "Since history began"),
                Ui.Metric("Failures", DisplayFormat.Number(_Statistics?.Failures), null, (_Statistics?.Failures ?? 0) > 0 ? XenoTheme.Current.Danger : null),
                Ui.Metric("Average latency", DisplayFormat.Milliseconds(_Statistics?.AverageLatencyMs), null),
                Ui.Metric("Served here", (_Catalog?.LocalCount ?? status.ModelCount).ToString(System.Globalization.CultureInfo.InvariantCulture), "models on " + status.ProviderCount + " provider" + (status.ProviderCount == 1 ? String.Empty : "s")),
                Ui.Metric("On the network", _Catalog == null || !_Catalog.NetworkAvailable ? DisplayFormat.Dash : _Catalog.NetworkCount.ToString(System.Globalization.CultureInfo.InvariantCulture), _Catalog == null ? "loading" : _Catalog.NetworkAvailable ? "models available to you" : "unavailable")
            };
            foreach (Control metric in metrics)
            {
                metric.Margin = new Avalonia.Thickness(0, 0, 12, 0);
                _Metrics.Children.Add(metric);
            }

            _Connection.Children.Clear();
            _Connection.Children.Add(Ui.Details(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Xeno server", DisplayFormat.OrDash(status.RestBaseUrl)),
                new KeyValuePair<string, string>("Client ID", DisplayFormat.OrDash(status.ClientId)),
                new KeyValuePair<string, string>("Tenant", DisplayFormat.OrDash(status.TenantId)),
                new KeyValuePair<string, string>("Signed in", status.HasCredential ? "yes" : "no"),
                new KeyValuePair<string, string>("Registered", DisplayFormat.Ago(status.RegisteredUtc, DateTime.UtcNow)),
                new KeyValuePair<string, string>("Last heartbeat", DisplayFormat.Ago(status.LastHeartbeatUtc, DateTime.UtcNow)),
                new KeyValuePair<string, string>("Last disconnect", DisplayFormat.OrDash(status.LastDisconnectReason))
            }, 130));

            _Services.Children.Clear();
            _Services.Children.Add(Ui.Details(new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Web dashboard", !status.LocalDashboardEnabled ? "disabled" : status.LocalDashboardListening ? status.LocalDashboardUrl : "not running"),
                new KeyValuePair<string, string>("Local inference", !status.LocalInferenceEnabled ? "disabled" : status.LocalInferenceListening ? status.LocalInferenceUrl : "enabled, not running"),
                new KeyValuePair<string, string>("Routing", DisplayFormat.OrDash(status.LocalInferenceRoutingMode)),
                new KeyValuePair<string, string>("Inventory update", status.InventoryUpdatePending ? "pending" : "up to date"),
                new KeyValuePair<string, string>("Worker process", status.ProcessId == null ? DisplayFormat.Dash : status.ProcessId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)),
                new KeyValuePair<string, string>("Settings file", status.SettingsFile)
            }, 130));
            if (!String.IsNullOrWhiteSpace(status.LocalInferencePortNotice) && String.IsNullOrEmpty(_PortNotice))
            {
                _PortNotice = status.LocalInferencePortNotice;
                ClientConnectionState.AcknowledgeLocalInferencePortMigration();
            }

            if (!String.IsNullOrEmpty(_PortNotice)) _Services.Children.Add(Ui.Notice(_PortNotice, XenoToneEnum.Neutral));
            if (!String.IsNullOrWhiteSpace(status.LocalInferenceLastError)) _Services.Children.Add(Ui.Notice("Local inference: " + status.LocalInferenceLastError, XenoToneEnum.Warning));
            if (!String.IsNullOrWhiteSpace(status.LocalDashboardLastError)) _Services.Children.Add(Ui.Notice("Web dashboard: " + status.LocalDashboardLastError, XenoToneEnum.Warning));
        }

        #endregion
    }
}

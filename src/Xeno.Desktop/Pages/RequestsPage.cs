namespace Xeno.Desktop.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Desktop.Common;
    using Xeno.Desktop.Controls;
    using Xeno.Desktop.Dialogs;

    /// <summary>
    /// Requests: what this machine served for the Xeno network, with a volume chart, filters, and request details.
    /// </summary>
    public sealed class RequestsPage : DesktopPage
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
            get { return XenoPageEnum.Requests; }
        }

        /// <inheritdoc />
        public override bool Scrolls
        {
            get { return false; }
        }

        #endregion

        #region Private-Members

        private bool _DrillActive = false;
        private static readonly List<string> Outcomes = new List<string> { "All outcomes", "Succeeded", "Failed" };
        private XenoTable<RequestRow>? _Table;
        private ColumnChart _Chart = new ColumnChart();
        private ComboBox _Range = new ComboBox();
        private ComboBox _Outcome = new ComboBox();
        private TextBox _Search = new TextBox();
        private TextBlock _Summary = new TextBlock();
        private List<RequestRow> _Rows = new List<RequestRow>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="session">Session.</param>
        /// <param name="window">Main window.</param>
        public RequestsPage(DesktopSession session, MainWindow window) : base(session, window)
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
                Ui.Button("Clear History...", () => _ = ClearAsync())
            };
        }

        /// <inheritdoc />
        public override Control Build()
        {
            _Range = new ComboBox { ItemsSource = HistoryRanges.Labels, SelectedIndex = 1, MinWidth = 140 };
            _Range.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Outcome = new ComboBox { ItemsSource = Outcomes, SelectedIndex = 0, MinWidth = 140 };
            _Outcome.SelectionChanged += (sender, args) => Apply();
            _Search = new TextBox { PlaceholderText = "Filter by model, provider, or path", MinWidth = 280 };
            _Search.TextChanged += (sender, args) => Apply();
            _Summary = Ui.Muted(String.Empty);
            _Chart = new ColumnChart { Height = 255 };
            _Table = new XenoTable<RequestRow>(new List<XenoTableColumn<RequestRow>>
            {
                new XenoTableColumn<RequestRow>("Received", row => row.Received) { SortKey = row => row.ReceivedUtc }.Fixed(160),
                new XenoTableColumn<RequestRow>("Method", row => row.Method).Fixed(70).Hidden(),
                new XenoTableColumn<RequestRow>("Path", row => row.Path).Star(2),
                new XenoTableColumn<RequestRow>("Model", row => row.Model).Star(2),
                new XenoTableColumn<RequestRow>("Provider", row => row.Provider).Star(1).Hidden(),
                new XenoTableColumn<RequestRow>("Status", row => row.Status).Fixed(65),
                new XenoTableColumn<RequestRow>("Outcome", row => row.Outcome) { Cell = OutcomeCell }.Fixed(100),
                new XenoTableColumn<RequestRow>("Duration", row => row.Duration) { SortKey = row => row.DurationMs }.Fixed(85),
                new XenoTableColumn<RequestRow>("Tokens", row => row.Tokens).Fixed(70).Hidden(),
                new XenoTableColumn<RequestRow>("Streamed", row => row.Streamed).Fixed(80).Hidden()
            }, RowActions, "No requests in history.");
            _Table.RowActivated += (sender, row) => _ = ShowDetailAsync(row);

            DockPanel root = new DockPanel();
            StackPanel top = Ui.Stack(10, Ui.Row(_Range, _Outcome, _Search), Ui.Card(null, _Chart), _Summary);
            top.Margin = new Thickness(0, 0, 0, 10);
            DockPanel.SetDock(top, Dock.Top);
            root.Children.Add(top);
            root.Children.Add(Ui.Adopt(_Table));
            return root;
        }

        /// <inheritdoc />
        public override async Task RefreshAsync()
        {
            await GuardAsync("Requests", async () =>
            {
                HistoryRangeEnum range = HistoryRanges.FromIndex(_Range.SelectedIndex);
                List<LocalRequestHistorySummaryBucket> buckets = await Workspace.SummarizeRequestsAsync(range, None).ConfigureAwait(true);
                _Chart.SetChart(XenoCharts.Requests(buckets, range));
                DateTime from = HistoryRanges.StartUtc(range, DateTime.UtcNow);
                XenoDrillDownRequest? window = XenoDrillDown.Take(Page);
                _DrillActive = window != null;
                List<RequestRow> all = await Workspace.ListRequestsAsync(0, None).ConfigureAwait(true);
                _Rows = window == null ? all.Where(item => item.ReceivedUtc >= from).ToList() : all.Where(item => window.Matches(item.ReceivedUtc, item.Model)).ToList();
                if (window != null) Report("Showing " + XenoDrillDown.Describe(window) + " from the Usage page. Refresh to see the whole range.");
                Apply();
            }).ConfigureAwait(true);
        }

        #endregion

        #region Private-Methods

        private void Apply()
        {
            if (_Table == null) return;
            IEnumerable<RequestRow> rows = _Rows;
            if (_Outcome.SelectedIndex == 1) rows = rows.Where(item => !item.Failed);
            if (_Outcome.SelectedIndex == 2) rows = rows.Where(item => item.Failed);
            string search = (_Search.Text ?? String.Empty).Trim();
            if (search.Length > 0)
            {
                rows = rows.Where(item => item.Model.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || item.Provider.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || item.Path.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            List<RequestRow> list = rows.ToList();
            _Table.SetRows(list, (left, right) => left.Id == right.Id);
            int failed = list.Count(item => item.Failed);
            _Summary.Text = list.Count + " request" + (list.Count == 1 ? String.Empty : "s") + " shown, " + failed + " failed. Showing up to " + Workspace.RequestPageSize + " most recent.";
        }

        private Control OutcomeCell(RequestRow row)
        {
            return Ui.Badge(row.Outcome, row.Failed ? XenoToneEnum.Danger : row.Outcome == "Processing" ? XenoToneEnum.Info : XenoToneEnum.Success);
        }

        private IReadOnlyList<XenoRowAction<RequestRow>> RowActions(RequestRow row)
        {
            return new List<XenoRowAction<RequestRow>>
            {
                new XenoRowAction<RequestRow>("Details...", item => _ = ShowDetailAsync(item)),
                new XenoRowAction<RequestRow>("Copy Request ID", item => _ = XenoClipboard.CopyAsync(Window, item.Id)),
                new XenoRowAction<RequestRow>("Delete", item => _ = DeleteAsync(item)) { Destructive = true }
            };
        }

        private async Task ShowDetailAsync(RequestRow row)
        {
            await GuardAsync("Request", async () =>
            {
                LocalRequestHistoryEntry? entry = await Workspace.GetRequestAsync(row.Id, None).ConfigureAwait(true);
                if (entry == null)
                {
                    Report("The request is no longer in history.");
                    return;
                }

                await XenoDialogs.ShowModalAsync<object>(new RequestDetailWindow(entry), Window).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        private async Task DeleteAsync(RequestRow row)
        {
            await GuardAsync("Delete Request", async () =>
            {
                bool removed = await Workspace.DeleteRequestAsync(row.Id, None).ConfigureAwait(true);
                Report(removed ? "Deleted request " + row.Id + "." : "The request was already gone.");
                await RefreshAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        private async Task ClearAsync()
        {
            bool confirmed = await XenoDialogs.ConfirmAsync(Window, "Clear Request History", "Delete every request from this machine's local history? This cannot be undone.", "Clear History").ConfigureAwait(true);
            if (!confirmed) return;
            await GuardAsync("Clear History", async () =>
            {
                int removed = await Workspace.ClearRequestsAsync(None).ConfigureAwait(true);
                Report("Deleted " + removed + " request" + (removed == 1 ? String.Empty : "s") + " from history.");
                await RefreshAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        #endregion
    }
}

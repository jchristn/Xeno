namespace Xeno.Tui.Pages
{
    using System;
    using System.Collections.Generic;
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
    /// Requests: what this machine served for the Xeno network, with volume over a range and request details.
    /// </summary>
    internal sealed class RequestsPage : TuiPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override bool AutoRefreshes
        {
            get { return _Window == null; }
        }

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.Requests; }
        }

        /// <inheritdoc />
        public override IWidget Root
        {
            get { return _Root; }
        }

        /// <inheritdoc />
        public override IReadOnlyList<KeyHint> Hints
        {
            get { return new List<KeyHint> { new KeyHint("Enter", "Details"), new KeyHint("d", "Delete"), new KeyHint("x", "Clear all") }; }
        }

        #endregion

        #region Private-Members

        private static readonly List<string> Outcomes = new List<string> { "All outcomes", "Succeeded", "Failed" };
        private readonly FramedStack _Root = new FramedStack(SplitOrientation.Vertical);
        private readonly Dropdown<string> _Range = new Dropdown<string>(HistoryRanges.Labels);
        private readonly Dropdown<string> _Outcome = new Dropdown<string>(Outcomes);
        private readonly TextField _Search = new TextField { Placeholder = "model, provider, or path" };
        private readonly ChartView _Chart = new ChartView();
        private readonly DataTable<RequestRow> _Table;
        private List<RequestRow> _Rows = new List<RequestRow>();
        private XenoDrillDownRequest? _Window = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="context">Context.</param>
        public RequestsPage(TuiContext context) : base(context)
        {
            _Range.SelectedIndex = 1;
            _Outcome.SelectedIndex = 0;
            _Range.SelectionChanged += (sender, args) =>
            {
                _Window = null;
                _ = RefreshAsync();
            };
            _Outcome.SelectionChanged += (sender, args) => Apply();
            _Search.ValueChanged += (sender, args) => Apply();
            _Table = Table<RequestRow>("No requests in this range.", row => row.Id);
            Column(_Table, "Received", row => row.Received, 20);
            Column(_Table, "Path", row => row.Path, 0);
            Column(_Table, "Model", row => row.Model, 22);
            Column(_Table, "Provider", row => row.Provider, 14, false);
            Column(_Table, "Status", row => row.Status, 7);
            Column(_Table, "Outcome", row => row.Outcome, 10);
            Column(_Table, "Duration", row => row.Duration, 9);
            Column(_Table, "Tokens", row => row.Tokens, 7, false);
            _Table.RowStyle = row => row.Failed ? Context.Theme.Error : (CellStyle?)null;
            _Table.RowActivated += index => _ = ShowDetailAsync();
            FramedStack filters = new FramedStack(SplitOrientation.Horizontal);
            Frame(filters, _Range, StackSize.Weighted(1, 14), "Range");
            Frame(filters, _Outcome, StackSize.Weighted(1, 16), "Outcome");
            Frame(filters, _Search, StackSize.Weighted(2, 16), "Filter");
            Frame(_Root, filters, StackSize.Fixed(1));
            Frame(_Root, _Chart, StackSize.Fixed(9));
            Frame(_Root, _Table, StackSize.Weighted(1, 4), "Requests");
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override Task RefreshAsync()
        {
            HistoryRangeEnum range = HistoryRanges.FromIndex(_Range.SelectedIndex);
            XenoDrillDownRequest? taken = XenoDrillDown.Take(Page);
            if (taken != null) _Window = taken;
            XenoDrillDownRequest? window = _Window;
            return LoadAsync(async token =>
            {
                List<LocalRequestHistorySummaryBucket> buckets = await Workspace.SummarizeRequestsAsync(range, token).ConfigureAwait(false);
                DateTime from = HistoryRanges.StartUtc(range, DateTime.UtcNow);
                List<RequestRow> all = await Workspace.ListRequestsAsync(0, token).ConfigureAwait(false);
                // A window from the Usage page shows only that bucket until the range changes.
                List<RequestRow> rows = window == null ? all.Where(item => item.ReceivedUtc >= from).ToList() : all.Where(item => window.Matches(item.ReceivedUtc, item.Model)).ToList();
                return new RequestsData(XenoCharts.Requests(buckets, range), rows);
            }, data =>
            {
                _Chart.Chart = data.Chart;
                _Rows = data.Rows;
                Apply();
            });
        }

        /// <inheritdoc />
        public override bool HandleKey(KeyEvent key)
        {
            if (PageKeys.Is(key, 'd'))
            {
                _ = DeleteAsync();
                return true;
            }

            if (PageKeys.Is(key, 'x'))
            {
                _ = ClearAsync();
                return true;
            }

            return false;
        }

        #endregion

        #region Private-Methods

        private void Apply()
        {
            IEnumerable<RequestRow> rows = _Rows;
            if (_Outcome.SelectedIndex == 1) rows = rows.Where(item => !item.Failed);
            if (_Outcome.SelectedIndex == 2) rows = rows.Where(item => item.Failed);
            string search = _Search.Value.Trim();
            if (search.Length > 0)
            {
                rows = rows.Where(item => item.Model.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || item.Provider.Contains(search, StringComparison.OrdinalIgnoreCase)
                    || item.Path.Contains(search, StringComparison.OrdinalIgnoreCase));
            }

            _Table.Bind(rows.ToList());
        }

        private async Task ShowDetailAsync()
        {
            RequestRow? row = _Table.SelectedRow;
            if (row == null) return;
            LocalRequestHistoryEntry? entry = await Workspace.GetRequestAsync(row.Id, System.Threading.CancellationToken.None).ConfigureAwait(false);
            if (entry == null) return;
            string markdown = XenoMarkdown.Join(new[] { RequestDetailFormatter.ToMarkdown(entry, true), XenoUsageCharts.TimingMarkdown(XenoUsageCharts.StagesFor(entry)) });
            Context.Post(() => _ = Context.ShowMarkdownAsync("Request " + entry.Id, markdown));
        }

        private async Task DeleteAsync()
        {
            RequestRow? row = _Table.SelectedRow;
            if (row == null) return;
            await Context.RunAsync(async token =>
            {
                bool removed = await Workspace.DeleteRequestAsync(row.Id, token).ConfigureAwait(false);
                return removed ? "Deleted request " + row.Id + "." : "The request was already gone.";
            }, () => _ = RefreshAsync()).ConfigureAwait(false);
        }

        private async Task ClearAsync()
        {
            bool confirmed = await Context.ConfirmAsync("Clear Request History", "Delete every request from this machine's local history?", "Clear").ConfigureAwait(false);
            if (!confirmed) return;
            await Context.RunAsync(async token =>
            {
                int removed = await Workspace.ClearRequestsAsync(token).ConfigureAwait(false);
                return "Deleted " + removed + " request" + (removed == 1 ? String.Empty : "s") + " from history.";
            }, () => _ = RefreshAsync()).ConfigureAwait(false);
        }

        #endregion
    }
}

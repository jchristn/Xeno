namespace Xeno.Desktop.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Layout;
    using Xeno.Client.Analytics;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Core.Analytics;
    using Xeno.Desktop.Common;
    using Xeno.Desktop.Controls;

    /// <summary>
    /// Usage in the desktop app: KPIs, every catalog chart for the chosen dataset in a two-column grid, and totals by
    /// group. A click (or Enter) on a bucket opens the matching list filtered to that window.
    /// </summary>
    public sealed class UsagePage : DesktopPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override bool AutoRefreshes
        {
            get { return true; }
        }

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.Usage; }
        }

        /// <summary>
        /// Charts on the page by catalog id, for tests.
        /// </summary>
        public IReadOnlyDictionary<string, ColumnChart> Charts
        {
            get { return _Charts; }
        }

        #endregion

        #region Private-Members

        private static readonly IReadOnlyList<UsageDatasetEnum> Datasets = new List<UsageDatasetEnum> { UsageDatasetEnum.ServedRequests, UsageDatasetEnum.LocalEndpoint, UsageDatasetEnum.Chat };
        private static readonly IReadOnlyList<UsageGroupByEnum> Groupings = new List<UsageGroupByEnum> { UsageGroupByEnum.Model, UsageGroupByEnum.Provider, UsageGroupByEnum.Route };
        private readonly Dictionary<string, ColumnChart> _Charts = new Dictionary<string, ColumnChart>(StringComparer.Ordinal);
        private readonly WrapPanel _Metrics = new WrapPanel();
        private readonly StackPanel _Notice = new StackPanel();
        private readonly TabControl _Tabs = new TabControl();
        private readonly Dictionary<string, TabItem> _TabItems = new Dictionary<string, TabItem>(StringComparer.Ordinal);
        private ComboBox _Range = new ComboBox();
        private ComboBox _Dataset = new ComboBox();
        private ComboBox _GroupBy = new ComboBox();
        private ComboBox _Metric = new ComboBox();
        private XenoTable<UsageGroupTotal>? _Groups;
        private UsageAnalytics? _Analytics = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="session">Session.</param>
        /// <param name="window">Main window.</param>
        public UsagePage(DesktopSession session, MainWindow window) : base(session, window)
        {
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
            _Range = Combo(HistoryRanges.Labels.ToList(), 1);
            _Dataset = Combo(Datasets.Select(XenoUsageCharts.DatasetLabel).ToList(), 0);
            _GroupBy = Combo(Groupings.Select(XenoUsageCharts.GroupByLabel).ToList(), 0);
            _Metric = Combo(new List<string> { "Requests", "Tokens" }, 0);
            _Groups = new XenoTable<UsageGroupTotal>(new List<XenoTableColumn<UsageGroupTotal>>
            {
                new XenoTableColumn<UsageGroupTotal>("Group", row => XenoUsageCharts.KeyLabel(row.Key)) { Monospace = true }.Star(3),
                new XenoTableColumn<UsageGroupTotal>("Requests", row => row.Requests.ToString("N0", CultureInfo.CurrentCulture)) { SortKey = row => row.Requests }.Fixed(100),
                new XenoTableColumn<UsageGroupTotal>("Failed", row => row.Failed.ToString("N0", CultureInfo.CurrentCulture)).Fixed(80),
                new XenoTableColumn<UsageGroupTotal>("Tokens in", row => row.PromptTokens.ToString("N0", CultureInfo.CurrentCulture)).Fixed(110),
                new XenoTableColumn<UsageGroupTotal>("Tokens out", row => row.CompletionTokens.ToString("N0", CultureInfo.CurrentCulture)).Fixed(110),
                new XenoTableColumn<UsageGroupTotal>("Median", row => Ms(row.Latency.P50)).Fixed(90),
                new XenoTableColumn<UsageGroupTotal>("95th", row => Ms(row.Latency.P95)).Fixed(90)
            }, null, "No requests in this range.");
            _Groups.Height = 220;
            return Ui.Stack(16,
                _Notice,
                Ui.Row(_Range, _Dataset, _GroupBy, _Metric),
                _Metrics,
                Ui.Card(null, _Tabs),
                Ui.Card("By group, whole range", _Groups));
        }

        /// <inheritdoc />
        public override async Task RefreshAsync()
        {
            _Notice.Children.Clear();
            if (!Workspace.Usage.IsAvailable)
            {
                _Notice.Children.Add(Ui.Notice(XenoUsageClient.NotRunningMessage, XenoToneEnum.Warning));
                return;
            }

            await GuardAsync("Usage", async () =>
            {
                UsageDatasetEnum dataset = SelectedDataset;
                UsageAnalytics analytics = await Workspace.Usage.GetUsageAsync(dataset, Query(), None).ConfigureAwait(true);
                _Analytics = analytics;
                if (analytics.Truncated && analytics.TruncatedReason != null) _Notice.Children.Add(Ui.Notice(analytics.TruncatedReason, XenoToneEnum.Warning));
                RenderMetrics(analytics);
                RenderCharts(analytics);
                _Groups?.SetRows(analytics.GroupTotals, (left, right) => left.Key == right.Key);
            }).ConfigureAwait(true);
        }

        #endregion

        #region Private-Methods

        private UsageDatasetEnum SelectedDataset
        {
            get { return Datasets[Math.Clamp(_Dataset.SelectedIndex, 0, Datasets.Count - 1)]; }
        }

        private UsageAnalyticsQuery Query()
        {
            HistoryRangeEnum range = HistoryRanges.FromIndex(_Range.SelectedIndex);
            DateTime to = DateTime.UtcNow;
            return new UsageAnalyticsQuery
            {
                FromUtc = HistoryRanges.StartUtc(range, to),
                ToUtc = to,
                BucketMinutes = HistoryRanges.DefaultBucketMinutes(range),
                GroupBy = Groupings[Math.Clamp(_GroupBy.SelectedIndex, 0, Groupings.Count - 1)],
                GroupMetric = _Metric.SelectedIndex == 1 ? UsageGroupMetricEnum.Tokens : UsageGroupMetricEnum.Requests,
                UtcOffsetMinutes = (int)TimeZoneInfo.Local.GetUtcOffset(to).TotalMinutes
            };
        }

        private ComboBox Combo(List<string> items, int selected)
        {
            ComboBox combo = new ComboBox { ItemsSource = items, SelectedIndex = selected, MinWidth = 150 };
            combo.SelectionChanged += (sender, args) => _ = RefreshAsync();
            return combo;
        }

        private void RenderMetrics(UsageAnalytics analytics)
        {
            UsageTotals totals = analytics.Totals;
            _Metrics.Children.Clear();
            Add(Ui.Metric(SelectedDataset == UsageDatasetEnum.Chat ? "Turns" : "Requests", totals.Requests.ToString("N0", CultureInfo.CurrentCulture), totals.Failed.ToString("N0", CultureInfo.CurrentCulture) + " failed" + (totals.Fallbacks.HasValue ? ", " + totals.Fallbacks.Value.ToString("N0", CultureInfo.CurrentCulture) + " fallbacks" : String.Empty), totals.Failed > 0 ? XenoTheme.Current.Danger : null));
            Add(Ui.Metric("Error rate", totals.ErrorRate.HasValue ? Math.Round(totals.ErrorRate.Value * 100, 1).ToString(CultureInfo.CurrentCulture) + "%" : "-", "Of all requests"));
            Add(Ui.Metric("Median time", Ms(totals.Latency.P50), "95th percentile " + Ms(totals.Latency.P95)));
            Add(Ui.Metric("First token", Ms(totals.TimeToFirstToken.P50), "Median"));
            Add(Ui.Metric("Tokens", (totals.PromptTokens + totals.CompletionTokens).ToString("N0", CultureInfo.CurrentCulture), totals.PromptTokens.ToString("N0", CultureInfo.CurrentCulture) + " in, " + totals.CompletionTokens.ToString("N0", CultureInfo.CurrentCulture) + " out"));
            Add(Ui.Metric("Throughput", totals.Throughput.P50.HasValue ? totals.Throughput.P50.Value.ToString("0.#", CultureInfo.CurrentCulture) + " tok/s" : "-", "Median"));
        }

        private void RenderCharts(UsageAnalytics analytics)
        {
            string range = HistoryRanges.Describe(HistoryRanges.FromIndex(_Range.SelectedIndex));
            // One tab per chart, each the full width of the page. Tabs are kept across refreshes so the chosen tab stays put;
            // they are only rebuilt when the dataset offers a different set of charts.
            List<string> ids = XenoUsageCharts.ChartsFor(SelectedDataset).ToList();
            bool sameTabs = ids.SequenceEqual(_TabItems.Keys);
            if (!sameTabs)
            {
                string? selected = (_Tabs.SelectedItem as TabItem)?.Tag as string;
                foreach (string stale in _Charts.Keys.Where(key => !ids.Contains(key)).ToList()) _Charts.Remove(stale);
                _Tabs.Items.Clear();
                _TabItems.Clear();
                foreach (string id in ids)
                {
                    if (!_Charts.TryGetValue(id, out ColumnChart? created))
                    {
                        created = new ColumnChart { Height = 420 };
                        string target = id;
                        created.BucketActivated += (bucket, series) => DrillDown(target, bucket, series);
                        _Charts[id] = created;
                    }

                    TabItem tab = new TabItem { Header = XenoChartCatalog.Find(id).Title, Tag = id, Content = Ui.Adopt(created), FontSize = 13 };
                    _TabItems[id] = tab;
                    _Tabs.Items.Add(tab);
                }

                _Tabs.SelectedItem = selected != null && _TabItems.TryGetValue(selected, out TabItem? keep) ? keep : _TabItems.Values.FirstOrDefault();
            }

            foreach (string id in ids) _Charts[id].SetChart(XenoUsageCharts.Build(id, analytics, range));
        }

        private void DrillDown(string chartId, XenoChartBucket bucket, string? series)
        {
            UsageAnalytics? analytics = _Analytics;
            string? model = null;
            if (analytics != null && chartId == XenoChartCatalog.Groups && analytics.GroupBy == UsageGroupByEnum.Model && series != null)
            {
                model = analytics.GroupKeys.FirstOrDefault(key => XenoUsageCharts.KeyLabel(key) == series && key != UsageAnalyticsBuilder.OtherKey && key != UsageAnalyticsBuilder.UnknownKey);
            }

            XenoPageEnum page = XenoDrillDown.PageFor(SelectedDataset);
            XenoDrillDownRequest request = XenoDrillDown.Request(page, bucket.StartUtc, bucket.EndUtc, model);
            Report("Showing " + XenoDrillDown.Describe(request) + ".");
            Window.Navigate(page);
        }

        private void Add(Control metric)
        {
            metric.Margin = new Thickness(0, 0, 12, 12);
            metric.Width = 220;
            _Metrics.Children.Add(metric);
        }

        private static string Ms(double? value)
        {
            return value.HasValue ? XenoChart.FormatMilliseconds(value.Value) : "-";
        }

        #endregion
    }
}

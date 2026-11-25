namespace Xeno.Tui.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using TUIKit.Layout;
    using TUIKit.Modals;
    using TUIKit.Widgets;
    using Xeno.Client.Analytics;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Core.Analytics;
    using Xeno.Tui.Shell;

    /// <summary>
    /// Usage in the terminal app: one chart at a time from the shared catalog (outcome, latency, time to first token,
    /// streaming, throughput, tokens, groups, heatmap, stages, and for chat feedback and tools) for served requests,
    /// the local endpoint, or chat, with the KPIs and a totals-by-group table. Enter or a click on a bucket opens the
    /// matching list filtered to that window.
    /// </summary>
    internal sealed class UsagePage : TuiPage
    {
        #region Public-Members

        /// <summary>
        /// Datasets this machine can chart, in picker order.
        /// </summary>
        public static readonly IReadOnlyList<UsageDatasetEnum> Datasets = new List<UsageDatasetEnum> { UsageDatasetEnum.ServedRequests, UsageDatasetEnum.LocalEndpoint, UsageDatasetEnum.Chat };

        /// <summary>
        /// Groupings this machine can chart, in picker order.
        /// </summary>
        public static readonly IReadOnlyList<UsageGroupByEnum> Groupings = new List<UsageGroupByEnum> { UsageGroupByEnum.Model, UsageGroupByEnum.Provider, UsageGroupByEnum.Route };

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

        /// <inheritdoc />
        public override IWidget Root
        {
            get { return _Root; }
        }

        /// <summary>
        /// The chart widget, for tests.
        /// </summary>
        internal UsageChartView ChartWidget
        {
            get { return _Chart; }
        }

        /// <summary>
        /// The chart picker, for tests.
        /// </summary>
        internal Dropdown<string> ChartPicker
        {
            get { return _ChartPicker; }
        }

        /// <summary>
        /// The dataset picker, for tests.
        /// </summary>
        internal Dropdown<string> DatasetPicker
        {
            get { return _Dataset; }
        }

        #endregion

        #region Private-Members

        private static readonly List<string> Metrics = new List<string> { "Requests", "Tokens" };
        private readonly FramedStack _Root = new FramedStack(SplitOrientation.Vertical);
        private readonly Dropdown<string> _Range = new Dropdown<string>(HistoryRanges.Labels);
        private readonly Dropdown<string> _Dataset = new Dropdown<string>(Datasets.Select(XenoUsageCharts.DatasetLabel).ToList());
        private readonly Dropdown<string> _ChartPicker = new Dropdown<string>(new List<string>());
        private readonly Dropdown<string> _GroupBy = new Dropdown<string>(Groupings.Select(XenoUsageCharts.GroupByLabel).ToList());
        private readonly Dropdown<string> _Metric = new Dropdown<string>(Metrics);
        private readonly DetailsView _Summary = new DetailsView();
        private readonly UsageChartView _Chart = new UsageChartView();
        private readonly DataTable<UsageGroupRow> _Groups;
        private List<string> _ChartIds = new List<string>();
        private UsageAnalytics? _Analytics = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="context">Context.</param>
        public UsagePage(TuiContext context) : base(context)
        {
            _Range.SelectedIndex = 1;
            _Dataset.SelectedIndex = 0;
            _GroupBy.SelectedIndex = 0;
            _Metric.SelectedIndex = 0;
            BindChartPicker();
            _Range.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Dataset.SelectionChanged += (sender, args) =>
            {
                BindChartPicker();
                _ = RefreshAsync();
            };
            _GroupBy.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Metric.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _ChartPicker.SelectionChanged += (sender, args) => ShowChart();
            _Chart.BucketActivated += (bucket, series) => DrillDown(bucket, series);
            _Groups = Table<UsageGroupRow>("No requests in this range.", row => row.Key);
            Column(_Groups, "Group", row => row.Name, 0);
            Column(_Groups, "Requests", row => row.Requests, 10);
            Column(_Groups, "Failed", row => row.Failed, 8);
            Column(_Groups, "Tokens in", row => row.Prompt, 11, false);
            Column(_Groups, "Tokens out", row => row.Completion, 11);
            Column(_Groups, "Median", row => row.Median, 9);
            Column(_Groups, "95th", row => row.P95, 9);
            FramedStack filters = new FramedStack(SplitOrientation.Horizontal);
            Frame(filters, _Range, StackSize.Weighted(1, 12), "Range");
            Frame(filters, _Dataset, StackSize.Weighted(1, 16), "Dataset");
            Frame(filters, _ChartPicker, StackSize.Weighted(2, 22), "Chart");
            Frame(filters, _GroupBy, StackSize.Weighted(1, 10), "Group by");
            Frame(filters, _Metric, StackSize.Weighted(1, 10), "Count");
            Frame(_Root, filters, StackSize.Fixed(1));
            Frame(_Root, _Summary, StackSize.Fixed(3), "Summary");
            Frame(_Root, _Chart, StackSize.Weighted(2, 12));
            Frame(_Root, _Groups, StackSize.Fixed(6), "By group, whole range");
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override Task RefreshAsync()
        {
            if (!Workspace.Usage.IsAvailable)
            {
                _Summary.Clear();
                _Summary.Set("Usage", XenoUsageClient.NotRunningMessage);
                _Chart.Chart = null;
                _Chart.LoadingText = XenoUsageClient.NotRunningMessage;
                _Groups.Bind(new List<UsageGroupRow>());
                return Task.CompletedTask;
            }

            UsageDatasetEnum dataset = SelectedDataset;
            UsageAnalyticsQuery query = Query();
            return LoadAsync(token => Workspace.Usage.GetUsageAsync(dataset, query, token), analytics =>
            {
                _Analytics = analytics;
                RenderSummary(analytics);
                _Groups.Bind(analytics.GroupTotals.Select(UsageGroupRow.From).ToList());
                ShowChart();
            });
        }

        /// <summary>
        /// The query the page sends: the selected range in the shared bucket presets, grouping, and the local UTC offset.
        /// </summary>
        /// <returns>Query.</returns>
        internal UsageAnalyticsQuery Query()
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

        #endregion

        #region Private-Methods

        private UsageDatasetEnum SelectedDataset
        {
            get { return Datasets[Math.Clamp(_Dataset.SelectedIndex, 0, Datasets.Count - 1)]; }
        }

        private void BindChartPicker()
        {
            string? current = _ChartIds.Count > 0 && _ChartPicker.SelectedIndex >= 0 && _ChartPicker.SelectedIndex < _ChartIds.Count ? _ChartIds[_ChartPicker.SelectedIndex] : null;
            _ChartIds = XenoUsageCharts.ChartsFor(SelectedDataset).ToList();
            _ChartPicker.SetItems(_ChartIds.Select(id => XenoChartCatalog.Find(id).Title));
            int index = current == null ? 0 : Math.Max(0, _ChartIds.IndexOf(current));
            _ChartPicker.SelectedIndex = index;
        }

        private void ShowChart()
        {
            UsageAnalytics? analytics = _Analytics;
            if (analytics == null || _ChartIds.Count == 0) return;
            string id = _ChartIds[Math.Clamp(_ChartPicker.SelectedIndex, 0, _ChartIds.Count - 1)];
            _Chart.Chart = XenoUsageCharts.Build(id, analytics, HistoryRanges.Describe(HistoryRanges.FromIndex(_Range.SelectedIndex)));
        }

        private void RenderSummary(UsageAnalytics analytics)
        {
            UsageTotals totals = analytics.Totals;
            _Summary.Clear();
            string requests = totals.Requests.ToString("N0", CultureInfo.CurrentCulture) + " (" + totals.Failed.ToString("N0", CultureInfo.CurrentCulture) + " failed" + (totals.ErrorRate.HasValue ? ", " + Math.Round(totals.ErrorRate.Value * 100, 1).ToString(CultureInfo.CurrentCulture) + "%" : String.Empty) + (totals.Fallbacks.HasValue ? ", " + totals.Fallbacks.Value.ToString("N0", CultureInfo.CurrentCulture) + " fallbacks" : String.Empty) + ")";
            _Summary.Set(SelectedDataset == UsageDatasetEnum.Chat ? "Turns" : "Requests", requests);
            _Summary.Set("Time", "median " + Ms(totals.Latency.P50) + ", 95th " + Ms(totals.Latency.P95) + ", first token " + Ms(totals.TimeToFirstToken.P50) + ", " + (totals.Throughput.P50.HasValue ? totals.Throughput.P50.Value.ToString("0.#", CultureInfo.CurrentCulture) + " tok/s" : "- tok/s"));
            _Summary.Set("Tokens", totals.PromptTokens.ToString("N0", CultureInfo.CurrentCulture) + " in" + (totals.CachedPromptTokens.HasValue ? " (" + totals.CachedPromptTokens.Value.ToString("N0", CultureInfo.CurrentCulture) + " cached)" : String.Empty) + ", " + totals.CompletionTokens.ToString("N0", CultureInfo.CurrentCulture) + " out" + (analytics.Truncated ? "   ! " + analytics.TruncatedReason : String.Empty));
        }

        private void DrillDown(XenoChartBucket bucket, string? series)
        {
            UsageAnalytics? analytics = _Analytics;
            string? model = null;
            if (analytics != null && analytics.GroupBy == UsageGroupByEnum.Model && series != null && _ChartIds.Count > 0 && _ChartIds[Math.Clamp(_ChartPicker.SelectedIndex, 0, _ChartIds.Count - 1)] == XenoChartCatalog.Groups)
            {
                model = analytics.GroupKeys.FirstOrDefault(key => XenoUsageCharts.KeyLabel(key) == series && key != UsageAnalyticsBuilder.OtherKey && key != UsageAnalyticsBuilder.UnknownKey);
            }

            XenoPageEnum page = XenoDrillDown.PageFor(SelectedDataset);
            XenoDrillDownRequest request = XenoDrillDown.Request(page, bucket.StartUtc, bucket.EndUtc, model);
            Context.Notify("Showing " + XenoDrillDown.Describe(request) + ".", NotificationSeverity.Info);
            Context.Navigate(page);
        }

        private static string Ms(double? value)
        {
            return value.HasValue ? XenoChart.FormatMilliseconds(value.Value) : "-";
        }

        #endregion

        #region Private-Classes

        private sealed class UsageGroupRow
        {
            internal string Key { get; set; } = String.Empty;

            internal string Name { get; set; } = String.Empty;

            internal string Requests { get; set; } = String.Empty;

            internal string Failed { get; set; } = String.Empty;

            internal string Prompt { get; set; } = String.Empty;

            internal string Completion { get; set; } = String.Empty;

            internal string Median { get; set; } = String.Empty;

            internal string P95 { get; set; } = String.Empty;

            internal static UsageGroupRow From(UsageGroupTotal total)
            {
                return new UsageGroupRow
                {
                    Key = total.Key,
                    Name = XenoUsageCharts.KeyLabel(total.Key),
                    Requests = total.Requests.ToString("N0", CultureInfo.CurrentCulture),
                    Failed = total.Failed.ToString("N0", CultureInfo.CurrentCulture),
                    Prompt = total.PromptTokens.ToString("N0", CultureInfo.CurrentCulture),
                    Completion = total.CompletionTokens.ToString("N0", CultureInfo.CurrentCulture),
                    Median = Ms(total.Latency.P50),
                    P95 = Ms(total.Latency.P95)
                };
            }
        }

        #endregion
    }
}

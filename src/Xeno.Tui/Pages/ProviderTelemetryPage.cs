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
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Client.Telemetry.Models;
    using Xeno.Tui.Shell;

    /// <summary>
    /// Provider telemetry: health and load per local provider, a metric chart over a range, and stored samples.
    /// </summary>
    internal sealed class ProviderTelemetryPage : TuiPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.ProviderTelemetry; }
        }

        /// <inheritdoc />
        public override IWidget Root
        {
            get { return _Root; }
        }

        /// <inheritdoc />
        public override IReadOnlyList<KeyHint> Hints
        {
            get { return new List<KeyHint> { new KeyHint("Enter", "Models"), new KeyHint("c", "Collect now"), new KeyHint("x", "Clear samples") }; }
        }

        #endregion

        #region Private-Members

        private const string AllProviders = "All providers";
        private readonly FramedStack _Root = new FramedStack(SplitOrientation.Vertical);
        private readonly DataTable<ProviderSampleRow> _Latest;
        private readonly Dropdown<string> _Provider = new Dropdown<string>(new List<string> { AllProviders });
        private readonly Dropdown<string> _Metric = new Dropdown<string>(Enum.GetNames(typeof(ClientProviderTelemetryMetricEnum)));
        private readonly Dropdown<string> _Range = new Dropdown<string>(HistoryRanges.Labels);
        private readonly ChartView _Chart = new ChartView();
        private readonly DataTable<ProviderSampleRow> _Samples;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="context">Context.</param>
        public ProviderTelemetryPage(TuiContext context) : base(context)
        {
            _Provider.SelectedIndex = 0;
            _Metric.SelectedIndex = 0;
            _Range.SelectedIndex = 0;
            _Provider.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Metric.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Range.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Latest = BuildTable("No provider telemetry yet. Press c to collect it.", row => row.Provider);
            _Latest.RowActivated += index => _ = ShowModelsAsync();
            _Samples = BuildTable("No samples in this range.", row => row.Id);
            FramedStack filters = new FramedStack(SplitOrientation.Horizontal);
            Frame(filters, _Provider, StackSize.Weighted(1, 16), "Provider");
            Frame(filters, _Metric, StackSize.Weighted(2, 24), "Metric");
            Frame(filters, _Range, StackSize.Weighted(1, 14), "Range");
            Frame(_Root, _Latest, StackSize.Weighted(1, 4), "Latest by provider");
            Frame(_Root, filters, StackSize.Fixed(1));
            Frame(_Root, _Chart, StackSize.Fixed(9));
            Frame(_Root, _Samples, StackSize.Weighted(2, 3), "Samples");
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override Task RefreshAsync()
        {
            string? provider = _Provider.SelectedItem == AllProviders ? null : _Provider.SelectedItem;
            HistoryRangeEnum range = HistoryRanges.FromIndex(_Range.SelectedIndex);
            ClientProviderTelemetryMetricEnum metric = Enum.TryParse(_Metric.SelectedItem, out ClientProviderTelemetryMetricEnum parsed) ? parsed : ClientProviderTelemetryMetricEnum.ActiveRequests;
            return Context.LoadAsync(async token =>
            {
                List<ProviderSampleRow> latest = await Workspace.GetLatestProviderTelemetryAsync(token).ConfigureAwait(false);
                List<ClientProviderTelemetryRollupBucket> buckets = await Workspace.RollupProviderTelemetryAsync(metric, provider, range, token).ConfigureAwait(false);
                List<ProviderSampleRow> samples = await Workspace.SearchProviderTelemetryAsync(provider, range, 500, token).ConfigureAwait(false);
                List<string> providers = Workspace.LoadSettings().Runners.Select(item => item.Name).ToList();
                return new ProviderTelemetryData(latest, XenoCharts.ProviderMetric(metric, provider, buckets, range), samples, providers);
            }, data =>
            {
                _Latest.Bind(data.Latest);
                List<string> choices = new List<string> { AllProviders };
                choices.AddRange(data.Providers);
                if (!choices.SequenceEqual(_Provider.Items))
                {
                    string? selected = _Provider.SelectedItem;
                    _Provider.SetItems(choices);
                    _Provider.SelectedIndex = Math.Max(0, choices.IndexOf(selected ?? AllProviders));
                }

                _Chart.Chart = data.Chart;
                _Samples.Bind(data.Samples);
            });
        }

        /// <inheritdoc />
        public override bool HandleKey(KeyEvent key)
        {
            if (PageKeys.Is(key, 'c'))
            {
                Context.Progress("Collecting provider telemetry...");
                _ = Context.RunAsync(async token =>
                {
                    List<ProviderSampleRow> rows = await Workspace.CollectProviderTelemetryAsync(token).ConfigureAwait(false);
                    return "Collected telemetry from " + rows.Count + " provider" + (rows.Count == 1 ? String.Empty : "s") + ".";
                }, () => _ = RefreshAsync());
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

        private static DataTable<ProviderSampleRow> BuildTable(string empty, Func<ProviderSampleRow, string> key)
        {
            DataTable<ProviderSampleRow> table = Table(empty, key);
            Column(table, "Collected", row => row.Collected, 20);
            Column(table, "Provider", row => row.Provider, 16);
            Column(table, "Health", row => row.Health, 10);
            Column(table, "Loaded/avail", row => row.Models, 12);
            Column(table, "Active", row => row.Active, 7);
            Column(table, "Queued", row => row.Queued, 7);
            Column(table, "Tok/s", row => row.TokensPerSecond, 7);
            Column(table, "Error", row => row.Error, 0);
            return table;
        }

        private async Task ShowModelsAsync()
        {
            ProviderSampleRow? row = _Latest.SelectedRow;
            if (row == null) return;
            ClientProviderTelemetrySampleDetail? detail = await Workspace.GetProviderTelemetryDetailAsync(row.Provider, System.Threading.CancellationToken.None).ConfigureAwait(false);
            List<string?> blocks = new List<string?>
            {
                XenoMarkdown.Escape(row.Provider + "  " + row.Kind + "  " + row.Health + "  version " + row.Version)
            };

            if (detail == null || detail.Models.Count == 0) blocks.Add("_No model detail reported._");
            else
            {
                blocks.Add(XenoMarkdown.Table(
                    new List<string> { "Model", "Parameters", "Quantization", "Size", "Loaded" },
                    detail.Models.OrderByDescending(item => item.Loaded).ThenBy(item => item.ModelName).Select(model => (IReadOnlyList<string>)new List<string>
                    {
                        model.ModelName,
                        DisplayFormat.OrDash(model.ParameterSize),
                        DisplayFormat.OrDash(model.Quantization),
                        model.SizeBytes == null ? DisplayFormat.Dash : DisplayFormat.Bytes(model.SizeBytes),
                        DisplayFormat.YesNo(model.Loaded)
                    })));
            }

            string markdown = XenoMarkdown.Join(blocks);
            Context.Post(() => _ = Context.ShowMarkdownAsync("Models on " + row.Provider, markdown));
        }

        private async Task ClearAsync()
        {
            bool confirmed = await Context.ConfirmAsync("Clear Provider Telemetry", "Delete every stored provider telemetry sample?", "Clear").ConfigureAwait(false);
            if (!confirmed) return;
            await Context.RunAsync(async token =>
            {
                int removed = await Workspace.ClearProviderTelemetryAsync(token).ConfigureAwait(false);
                return "Deleted " + removed + " provider telemetry samples.";
            }, () => _ = RefreshAsync()).ConfigureAwait(false);
        }

        #endregion
    }
}

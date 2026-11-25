namespace Xeno.Desktop.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia.Controls;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Client.Telemetry.Models;
    using Xeno.Desktop.Common;
    using Xeno.Desktop.Controls;

    /// <summary>
    /// Provider telemetry: live health and load reported by each local provider, the models it has loaded, a metric
    /// chart over a range, and stored samples.
    /// </summary>
    public sealed class ProviderTelemetryPage : DesktopPage
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
            get { return XenoPageEnum.ProviderTelemetry; }
        }

        #endregion

        #region Private-Members

        private const string AllProviders = "All providers";
        private const string AllModels = "All models";
        private ComboBox _Model = new ComboBox();
        private XenoTable<ProviderSampleRow>? _Latest;
        private XenoTable<ProviderSampleRow>? _Samples;
        private ComboBox _Provider = new ComboBox();
        private ComboBox _Metric = new ComboBox();
        private bool _BindingMetrics = false;
        private ComboBox _Range = new ComboBox();
        private ColumnChart _Chart = new ColumnChart();
        private ColumnChart _Health = new ColumnChart();
        private StackPanel _Models = new StackPanel();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="session">Session.</param>
        /// <param name="window">Main window.</param>
        public ProviderTelemetryPage(DesktopSession session, MainWindow window) : base(session, window)
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
                Ui.Button("Clear Samples...", () => _ = ClearAsync()),
                Ui.Primary("Collect Now", () => _ = CollectAsync())
            };
        }

        /// <inheritdoc />
        public override Control Build()
        {
            List<XenoTableColumn<ProviderSampleRow>> columns = new List<XenoTableColumn<ProviderSampleRow>>
            {
                new XenoTableColumn<ProviderSampleRow>("Collected", row => row.Collected) { SortKey = row => row.CollectedUtc }.Fixed(160),
                new XenoTableColumn<ProviderSampleRow>("Provider", row => row.Provider).Star(2),
                new XenoTableColumn<ProviderSampleRow>("Kind", row => row.Kind).Fixed(110).Hidden(),
                new XenoTableColumn<ProviderSampleRow>("Health", row => row.Health) { Cell = HealthCell }.Fixed(100),
                new XenoTableColumn<ProviderSampleRow>("Loaded / available", row => row.Models).Fixed(130),
                new XenoTableColumn<ProviderSampleRow>("Active", row => row.Active).Fixed(70),
                new XenoTableColumn<ProviderSampleRow>("Queued", row => row.Queued).Fixed(70).Hidden(),
                new XenoTableColumn<ProviderSampleRow>("Tokens/s", row => row.TokensPerSecond).Fixed(80).Hidden(),
                new XenoTableColumn<ProviderSampleRow>("Error", row => row.Error).Star(2)
            };

            _Latest = new XenoTable<ProviderSampleRow>(columns, null, "No provider telemetry yet. Click Collect Now, or start the client to collect it automatically.", "ProviderTelemetry.Latest");
            _Latest.Height = 200;
            _Latest.SelectionChanged += (sender, row) => _ = ShowModelsAsync(row);
            _Models = new StackPanel { Spacing = 4 };
            _Provider = new ComboBox { MinWidth = 180 };
            _Provider.SelectionChanged += (sender, args) => _ = RefreshHistoryAsync();
            _Metric = new ComboBox { ItemsSource = Enum.GetNames(typeof(ClientProviderTelemetryMetricEnum)), SelectedIndex = 0, MinWidth = 220 };
            _Metric.SelectionChanged += (sender, args) =>
            {
                if (!_BindingMetrics) _ = RefreshHistoryAsync();
            };
            _Range = new ComboBox { ItemsSource = HistoryRanges.Labels, SelectedIndex = 1, MinWidth = 140 };
            _Model = new ComboBox { MinWidth = 180 };
            _Model.SelectionChanged += (sender, args) => _ = RefreshHistoryAsync();
            _Range.SelectionChanged += (sender, args) => _ = RefreshHistoryAsync();
            _Chart = new ColumnChart { Height = 190 };
            _Health = new ColumnChart { Height = 150 };
            _Health.SetLoadingText("Provider health needs the Xeno client running.");
            _Samples = new XenoTable<ProviderSampleRow>(columns, null, "No samples in this range.", "ProviderTelemetry.Samples");
            _Samples.Height = 300;

            return Ui.Stack(16,
                Ui.Card("Provider metric over time", Ui.Stack(10, Ui.Row(_Provider, _Model, _Metric, _Range), _Chart)),
                Ui.Card("Latest by provider", Ui.Stack(10, _Latest, _Models)),
                Ui.Card("Provider health", _Health),
                Ui.Card("Samples", _Samples));
        }

        /// <inheritdoc />
        public override async Task RefreshAsync()
        {
            await GuardAsync("Provider Telemetry", async () =>
            {
                List<ProviderSampleRow> latest = await Workspace.GetLatestProviderTelemetryAsync(None).ConfigureAwait(true);
                _Latest?.SetRows(latest, (left, right) => left.Provider == right.Provider);
                List<string> providers = new List<string> { AllProviders };
                providers.AddRange(Workspace.LoadSettings().Runners.Select(item => item.Name));
                string? selected = _Provider.SelectedItem as string;
                _Provider.ItemsSource = providers;
                List<string> models = new List<string> { AllModels };
                models.AddRange(Workspace.LoadSettings().Runners.SelectMany(item => item.Models.Select(entry => entry.Name)).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal));
                if (_Model.ItemsSource is not List<string> currentModels || !currentModels.SequenceEqual(models))
                {
                    object? selectedModel = _Model.SelectedItem;
                    _Model.ItemsSource = models;
                    _Model.SelectedItem = selectedModel != null && models.Contains((string)selectedModel) ? selectedModel : AllModels;
                }

                _Provider.SelectedItem = providers.Contains(selected ?? String.Empty) ? selected : AllProviders;
                await RefreshHistoryAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        #endregion

        #region Private-Methods

        private async Task RefreshHistoryAsync()
        {
            if (_Samples == null) return;
            await GuardAsync("Provider Telemetry", async () =>
            {
                string? provider = _Provider.SelectedItem as string;
                if (provider == AllProviders) provider = null;
                HistoryRangeEnum range = HistoryRanges.FromIndex(_Range.SelectedIndex);
                string? model = _Model.SelectedItem as string;
                if (model == AllModels) model = null;

                // Offer only metrics with readings for this provider, model, and range; keep the choice when it still has data.
                List<ClientProviderTelemetryMetricEnum> available = await Workspace.ProviderMetricsWithDataAsync(provider, range, None, model).ConfigureAwait(true);
                List<string> names = (available.Count > 0 ? available : Enum.GetValues<ClientProviderTelemetryMetricEnum>().ToList()).Select(item => item.ToString()).ToList();
                string? chosen = _Metric.SelectedItem as string;
                if (_Metric.ItemsSource is not List<string> current || !current.SequenceEqual(names))
                {
                    _BindingMetrics = true;
                    _Metric.ItemsSource = names;
                    _Metric.SelectedItem = chosen != null && names.Contains(chosen) ? chosen : names[0];
                    _BindingMetrics = false;
                }

                ClientProviderTelemetryMetricEnum metric = Enum.TryParse(_Metric.SelectedItem as string, out ClientProviderTelemetryMetricEnum parsed) ? parsed : ClientProviderTelemetryMetricEnum.ActiveRequests;
                List<ClientProviderTelemetryRollupBucket> buckets = await Workspace.RollupProviderTelemetryAsync(metric, provider, range, None, model).ConfigureAwait(true);
                _Chart.SetChart(XenoCharts.ProviderMetric(metric, provider, buckets, range));
                _Samples.SetRows(await Workspace.SearchProviderTelemetryAsync(provider, range, 500, None).ConfigureAwait(true));
                if (Workspace.Usage.IsAvailable)
                {
                    DateTime to = DateTime.UtcNow;
                    Xeno.Core.Analytics.ProviderHealthQuery query = new Xeno.Core.Analytics.ProviderHealthQuery { FromUtc = HistoryRanges.StartUtc(range, to), ToUtc = to, BucketMinutes = HistoryRanges.DefaultBucketMinutes(range), ProviderName = provider };
                    _Health.SetChart(XenoUsageCharts.Health(await Workspace.Usage.GetProviderHealthAsync(query, None).ConfigureAwait(true), HistoryRanges.Describe(range)));
                }
            }).ConfigureAwait(true);
        }

        private async Task ShowModelsAsync(ProviderSampleRow? row)
        {
            _Models.Children.Clear();
            if (row == null) return;
            ClientProviderTelemetrySampleDetail? detail = await Workspace.GetProviderTelemetryDetailAsync(row.Provider, None).ConfigureAwait(true);
            if (detail == null || detail.Models.Count == 0)
            {
                _Models.Children.Add(Ui.Muted("No model detail reported by " + row.Provider + "."));
                return;
            }

            _Models.Children.Add(Ui.Heading("Models reported by " + row.Provider));
            foreach (ClientProviderTelemetryModelSampleRecord model in detail.Models.OrderByDescending(item => item.Loaded).ThenBy(item => item.ModelName))
            {
                string text = (model.Loaded ? "● " : "○ ") + model.ModelName
                    + (String.IsNullOrWhiteSpace(model.ParameterSize) ? String.Empty : "  " + model.ParameterSize)
                    + (String.IsNullOrWhiteSpace(model.Quantization) ? String.Empty : "  " + model.Quantization)
                    + (model.SizeBytes == null ? String.Empty : "  " + DisplayFormat.Bytes(model.SizeBytes))
                    + (model.Loaded ? "  loaded" + (model.MemoryBytes == null ? String.Empty : " (" + DisplayFormat.Bytes(model.MemoryBytes) + ")") : String.Empty);
                _Models.Children.Add(Ui.Code(text));
            }
        }

        private Control HealthCell(ProviderSampleRow row)
        {
            XenoToneEnum tone = row.Health == "Healthy" ? XenoToneEnum.Success : row.Health == "Degraded" ? XenoToneEnum.Warning : row.Health == "Unhealthy" ? XenoToneEnum.Danger : XenoToneEnum.Neutral;
            return Ui.Badge(row.Health, tone);
        }

        private async Task CollectAsync()
        {
            Report("Collecting provider telemetry...");
            await GuardAsync("Collect Provider Telemetry", async () =>
            {
                List<ProviderSampleRow> rows = await Workspace.CollectProviderTelemetryAsync(None).ConfigureAwait(true);
                Report("Collected telemetry from " + rows.Count + " provider" + (rows.Count == 1 ? String.Empty : "s") + ".");
                await RefreshAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        private async Task ClearAsync()
        {
            bool confirmed = await XenoDialogs.ConfirmAsync(Window, "Clear Provider Telemetry", "Delete every stored provider telemetry sample?", "Clear").ConfigureAwait(true);
            if (!confirmed) return;
            await GuardAsync("Clear Telemetry", async () =>
            {
                int removed = await Workspace.ClearProviderTelemetryAsync(None).ConfigureAwait(true);
                Report("Deleted " + removed + " provider telemetry samples.");
                await RefreshAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        #endregion
    }
}

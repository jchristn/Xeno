namespace Xeno.Desktop.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Client.Telemetry.Models;
    using Xeno.Desktop.Common;
    using Xeno.Desktop.Controls;

    /// <summary>
    /// Host telemetry: the latest CPU, memory, disk, and GPU readings, CPU, memory, and GPU charts over a range (the
    /// same charts as the web dashboard), and stored samples.
    /// </summary>
    public sealed class HostTelemetryPage : DesktopPage
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
            get { return XenoPageEnum.HostTelemetry; }
        }

        #endregion

        #region Private-Members

        private WrapPanel _Latest = new WrapPanel();
        private ComboBox _Range = new ComboBox();
        private ColumnChart _Chart = new ColumnChart();
        private ComboBox _Metric = new ComboBox();
        private XenoTable<HostSampleRow>? _Samples;
        private TextBlock _Status = new TextBlock();
        private StackPanel _Gpus = new StackPanel();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="session">Session.</param>
        /// <param name="window">Main window.</param>
        public HostTelemetryPage(DesktopSession session, MainWindow window) : base(session, window)
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
            _Latest = new WrapPanel();
            _Status = Ui.Muted(String.Empty);
            _Gpus = new StackPanel { Spacing = 6 };
            _Range = new ComboBox { ItemsSource = HistoryRanges.Labels, SelectedIndex = 1, MinWidth = 140 };
            _Range.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Metric = new ComboBox { ItemsSource = XenoCharts.HostMetrics.Select(XenoCharts.HostMetricName).ToList(), SelectedIndex = 0, MinWidth = 160 };
            _Metric.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Chart = new ColumnChart { Height = 220 };

            _Samples = new XenoTable<HostSampleRow>(new List<XenoTableColumn<HostSampleRow>>
            {
                new XenoTableColumn<HostSampleRow>("Collected", row => row.Collected) { SortKey = row => row.CollectedUtc }.Fixed(170),
                new XenoTableColumn<HostSampleRow>("CPU", row => row.Cpu).Star(1),
                new XenoTableColumn<HostSampleRow>("Memory", row => row.Memory).Star(1),
                new XenoTableColumn<HostSampleRow>("Disk", row => row.Disk).Star(1),
                new XenoTableColumn<HostSampleRow>("GPU", row => row.Gpu).Star(1),
                new XenoTableColumn<HostSampleRow>("GPU memory", row => row.GpuMemory).Star(1),
                new XenoTableColumn<HostSampleRow>("GPU temp", row => row.GpuTemperature).Star(1)
            }, null, "No samples in this range. Telemetry is collected while the client runs, or use Collect Now.");
            _Samples.Height = 320;

            return Ui.Stack(16,
                Ui.Card("Latest reading", Ui.Stack(10, _Latest, _Gpus, _Status)),
                Ui.Card("Metric over time", _Chart, _Metric, _Range),
                Ui.Card("Samples", _Samples));
        }

        /// <inheritdoc />
        public override async Task RefreshAsync()
        {
            await GuardAsync("Host Telemetry", async () =>
            {
                ClientTelemetrySampleDetail? latest = await Workspace.GetLatestHostTelemetryAsync(None).ConfigureAwait(true);
                ClientTelemetryPersistenceStatus status = await Workspace.GetHostTelemetryStatusAsync(None).ConfigureAwait(true);
                RenderLatest(latest, status);
                HistoryRangeEnum range = HistoryRanges.FromIndex(_Range.SelectedIndex);
                ClientTelemetryMetricEnum metric = XenoCharts.HostMetrics[Math.Clamp(_Metric.SelectedIndex, 0, XenoCharts.HostMetrics.Count - 1)];
                List<ClientTelemetryRollupBucket> buckets = await Workspace.RollupHostTelemetryAsync(metric, range, None).ConfigureAwait(true);
                _Chart.SetChart(XenoCharts.HostMetric(metric, buckets, range));

                _Samples?.SetRows(await Workspace.SearchHostTelemetryAsync(range, 500, None).ConfigureAwait(true));
            }).ConfigureAwait(true);
        }

        #endregion

        #region Private-Methods

        private void RenderLatest(ClientTelemetrySampleDetail? latest, ClientTelemetryPersistenceStatus status)
        {
            _Latest.Children.Clear();
            _Gpus.Children.Clear();
            _Status.Text = (status.Enabled ? "Collection enabled every " + (status.CollectionIntervalMs / 1000) + " s while the client runs" : "Collection disabled in Settings")
                + ". " + status.Samples + " samples stored, kept for " + status.RetentionDays + " days.";
            if (latest == null)
            {
                _Latest.Children.Add(Ui.Muted("No telemetry yet. Click Collect Now to take a reading."));
                return;
            }

            ClientTelemetrySampleRecord sample = latest.Sample;
            Control[] metrics = new Control[]
            {
                Ui.Metric("CPU", DisplayFormat.Percent(sample.CpuUtilizationPercent), sample.ProcessorCount + " logical processors"),
                Ui.Metric("Memory", DisplayFormat.Percent(sample.MemoryUtilizationPercent), DisplayFormat.Bytes(sample.MemoryUsedBytes) + " of " + DisplayFormat.Bytes(sample.MemoryTotalBytes)),
                Ui.Metric("Disk", DisplayFormat.Percent(sample.DiskUtilizationPercent), DisplayFormat.Bytes(sample.DiskUsedBytes) + " of " + DisplayFormat.Bytes(sample.DiskTotalBytes)),
                Ui.Metric("GPU", sample.GpuDeviceCount == 0 ? DisplayFormat.Dash : DisplayFormat.Percent(sample.GpuAverageUtilizationPercent), sample.GpuDeviceCount == 0 ? "no GPU detected" : sample.GpuDeviceCount + " device" + (sample.GpuDeviceCount == 1 ? String.Empty : "s") + " (" + sample.GpuProvider + ")"),
                Ui.Metric("GPU memory", sample.GpuDeviceCount == 0 ? DisplayFormat.Dash : DisplayFormat.Percent(sample.GpuAverageMemoryUtilizationPercent), sample.GpuDeviceCount == 0 ? null : DisplayFormat.Bytes(sample.GpuUsedMemoryBytes) + " of " + DisplayFormat.Bytes(sample.GpuTotalMemoryBytes))
            };
            foreach (Control metric in metrics)
            {
                metric.Margin = new Thickness(0, 0, 12, 12);
                _Latest.Children.Add(metric);
            }

            foreach (ClientTelemetryGpuSampleRecord gpu in latest.Gpus)
            {
                _Gpus.Children.Add(Ui.Muted(gpu.Name + ": " + DisplayFormat.Percent(gpu.UtilizationPercent) + " busy, " + DisplayFormat.Percent(gpu.MemoryUtilizationPercent) + " memory"
                    + (gpu.TemperatureCelsius == null ? String.Empty : ", " + gpu.TemperatureCelsius.Value.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + " C")));
            }

            _Gpus.Children.Add(Ui.Muted("Collected " + DisplayFormat.Local(sample.CollectedUtc) + " on " + sample.MachineName + " (" + sample.OsDescription + ", " + sample.Architecture + ")."));
        }

        private async Task CollectAsync()
        {
            Report("Collecting host telemetry...");
            await GuardAsync("Collect Telemetry", async () =>
            {
                ClientTelemetrySnapshot? snapshot = await Workspace.CollectHostTelemetryAsync(None).ConfigureAwait(true);
                Report(snapshot == null ? "Telemetry collection did not return a reading." : "Collected a host telemetry reading.");
                await RefreshAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        private async Task ClearAsync()
        {
            bool confirmed = await XenoDialogs.ConfirmAsync(Window, "Clear Host Telemetry", "Delete every stored host telemetry sample?", "Clear").ConfigureAwait(true);
            if (!confirmed) return;
            await GuardAsync("Clear Telemetry", async () =>
            {
                int removed = await Workspace.ClearHostTelemetryAsync(None).ConfigureAwait(true);
                Report("Deleted " + removed + " host telemetry samples.");
                await RefreshAsync().ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        #endregion
    }
}

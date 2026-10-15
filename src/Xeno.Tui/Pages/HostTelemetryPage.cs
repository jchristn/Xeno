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
    /// Host telemetry: latest CPU, memory, disk, and GPU readings, CPU, memory, and GPU charts over a range (the same
    /// charts as the web dashboard), and stored samples.
    /// </summary>
    internal sealed class HostTelemetryPage : TuiPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.HostTelemetry; }
        }

        /// <inheritdoc />
        public override IWidget Root
        {
            get { return _Root; }
        }

        /// <inheritdoc />
        public override IReadOnlyList<KeyHint> Hints
        {
            get { return new List<KeyHint> { new KeyHint("c", "Collect now"), new KeyHint("x", "Clear samples") }; }
        }

        #endregion

        #region Private-Members

        private readonly FramedStack _Root = new FramedStack(SplitOrientation.Vertical);
        private readonly DetailsView _Latest = new DetailsView();
        private readonly Dropdown<string> _Range = new Dropdown<string>(HistoryRanges.Labels);
        private readonly List<ChartView> _Charts = XenoCharts.HostMetrics.Select(item => new ChartView()).ToList();
        private readonly DataTable<HostSampleRow> _Samples;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="context">Context.</param>
        public HostTelemetryPage(TuiContext context) : base(context)
        {
            _Range.SelectedIndex = 0;
            _Range.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Samples = Table<HostSampleRow>("No samples in this range. Press c to collect a reading.", row => row.Id);
            Column(_Samples, "Collected", row => row.Collected, 20);
            Column(_Samples, "CPU", row => row.Cpu, 8);
            Column(_Samples, "Memory", row => row.Memory, 8);
            Column(_Samples, "Disk", row => row.Disk, 8);
            Column(_Samples, "GPU", row => row.Gpu, 8);
            Column(_Samples, "GPU mem", row => row.GpuMemory, 8);
            Column(_Samples, "GPU temp", row => row.GpuTemperature, 0);
            FramedStack charts = new FramedStack(SplitOrientation.Horizontal);
            for (int i = 0; i < _Charts.Count; i++)
            {
                Frame(charts, _Charts[i], StackSize.Weighted(1, 20));
            }

            Frame(_Root, _Latest, StackSize.Fixed(7), "Latest reading");
            Frame(_Root, _Range, StackSize.Fixed(1), "Chart range");
            Frame(_Root, charts, StackSize.Fixed(9));
            Frame(_Root, _Samples, StackSize.Weighted(1, 3), "Samples");
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override Task RefreshAsync()
        {
            HistoryRangeEnum range = HistoryRanges.FromIndex(_Range.SelectedIndex);
            return LoadAsync(async token =>
            {
                ClientTelemetrySampleDetail? latest = await Workspace.GetLatestHostTelemetryAsync(token).ConfigureAwait(false);
                ClientTelemetryPersistenceStatus status = await Workspace.GetHostTelemetryStatusAsync(token).ConfigureAwait(false);
                List<XenoChart> charts = new List<XenoChart>();
                foreach (ClientTelemetryMetricEnum metric in XenoCharts.HostMetrics)
                {
                    List<ClientTelemetryRollupBucket> buckets = await Workspace.RollupHostTelemetryAsync(metric, range, token).ConfigureAwait(false);
                    charts.Add(XenoCharts.HostMetric(metric, buckets, range));
                }

                List<HostSampleRow> samples = await Workspace.SearchHostTelemetryAsync(range, 500, token).ConfigureAwait(false);
                return new HostTelemetryData(latest, status, charts, samples);
            }, data =>
            {
                RenderLatest(data.Latest, data.Status);
                for (int i = 0; i < _Charts.Count && i < data.Charts.Count; i++) _Charts[i].Chart = data.Charts[i];
                _Samples.Bind(data.Samples);
            });
        }

        /// <inheritdoc />
        public override bool HandleKey(KeyEvent key)
        {
            if (PageKeys.Is(key, 'c'))
            {
                Context.Progress("Collecting host telemetry...");
                _ = Context.RunAsync(async token =>
                {
                    ClientTelemetrySnapshot? snapshot = await Workspace.CollectHostTelemetryAsync(token).ConfigureAwait(false);
                    return snapshot == null ? "Telemetry collection did not return a reading." : "Collected a host telemetry reading.";
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

        private void RenderLatest(ClientTelemetrySampleDetail? latest, ClientTelemetryPersistenceStatus status)
        {
            _Latest.Clear();
            _Latest.Set("Collection", (status.Enabled ? "every " + (status.CollectionIntervalMs / 1000) + " s while the client runs" : "disabled in Settings") + ", " + status.Samples + " samples kept " + status.RetentionDays + " days");
            if (latest == null)
            {
                _Latest.Set("Latest", "no readings yet; press c to collect one");
                return;
            }

            ClientTelemetrySampleRecord sample = latest.Sample;
            _Latest.Set("Collected", DisplayFormat.Local(sample.CollectedUtc) + " on " + sample.MachineName);
            _Latest.Set("CPU", DisplayFormat.Percent(sample.CpuUtilizationPercent) + " of " + sample.ProcessorCount + " logical processors");
            _Latest.Set("Memory", DisplayFormat.Percent(sample.MemoryUtilizationPercent) + "  " + DisplayFormat.Bytes(sample.MemoryUsedBytes) + " of " + DisplayFormat.Bytes(sample.MemoryTotalBytes));
            _Latest.Set("Disk", DisplayFormat.Percent(sample.DiskUtilizationPercent) + "  " + DisplayFormat.Bytes(sample.DiskUsedBytes) + " of " + DisplayFormat.Bytes(sample.DiskTotalBytes));
            _Latest.Set("GPU", sample.GpuDeviceCount == 0 ? "no GPU detected" : DisplayFormat.Percent(sample.GpuAverageUtilizationPercent) + " busy, " + DisplayFormat.Percent(sample.GpuAverageMemoryUtilizationPercent) + " memory, " + sample.GpuDeviceCount + " device(s)");
        }

        private async Task ClearAsync()
        {
            bool confirmed = await Context.ConfirmAsync("Clear Host Telemetry", "Delete every stored host telemetry sample?", "Clear").ConfigureAwait(false);
            if (!confirmed) return;
            await Context.RunAsync(async token =>
            {
                int removed = await Workspace.ClearHostTelemetryAsync(token).ConfigureAwait(false);
                return "Deleted " + removed + " host telemetry samples.";
            }, () => _ = RefreshAsync()).ConfigureAwait(false);
        }

        #endregion
    }
}

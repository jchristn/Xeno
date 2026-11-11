namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Text;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Services;
    using Xeno.Client.Telemetry.Models;

    /// <summary>
    /// Builds the charts the desktop and terminal apps show, so both draw the same series, labels, units, and empty
    /// states as the local web dashboard: requests over time (Overview and Requests), CPU, memory, and GPU over time
    /// (Host Telemetry), and a chosen provider metric over time (Provider Telemetry).
    /// </summary>
    public static class XenoCharts
    {
        #region Public-Members

        /// <summary>
        /// Host metrics the Host Telemetry page's metric picker offers, in display order (the same as the web dashboards).
        /// </summary>
        public static readonly IReadOnlyList<ClientTelemetryMetricEnum> HostMetrics = new List<ClientTelemetryMetricEnum>
        {
            ClientTelemetryMetricEnum.CpuUtilizationPercent,
            ClientTelemetryMetricEnum.MemoryUtilizationPercent,
            ClientTelemetryMetricEnum.DiskUtilizationPercent,
            ClientTelemetryMetricEnum.GpuUtilizationPercent,
            ClientTelemetryMetricEnum.GpuMemoryUtilizationPercent,
            ClientTelemetryMetricEnum.GpuTemperatureCelsius,
            ClientTelemetryMetricEnum.GpuPowerWatts
        };

        #endregion

        #region Public-Methods

        /// <summary>
        /// Requests over time: successful and failed requests per bucket.
        /// </summary>
        /// <param name="buckets">Request summary buckets.</param>
        /// <param name="range">Range the buckets cover.</param>
        /// <returns>Chart.</returns>
        /// <exception cref="ArgumentNullException">Buckets is null.</exception>
        public static XenoChart Requests(IReadOnlyList<LocalRequestHistorySummaryBucket> buckets, HistoryRangeEnum range)
        {
            ArgumentNullException.ThrowIfNull(buckets);
            string rangeText = HistoryRanges.Describe(range);
            List<XenoChartSeries> series = new List<XenoChartSeries>
            {
                new XenoChartSeries("Succeeded", XenoChartToneEnum.Success, buckets.Select(item => (double)item.Success).ToList()),
                new XenoChartSeries("Failed", XenoChartToneEnum.Danger, buckets.Select(item => (double)item.Failure).ToList())
            };

            return new XenoChart(
                "Requests",
                rangeText,
                " req",
                buckets.Select(item => Label(item.StartUtc, range)).ToList(),
                series,
                "No requests in the " + rangeText + ". Requests appear here when the Xeno network routes work to this machine.",
                true,
                buckets.Any(item => item.Success + item.Failure > 0));
        }

        /// <summary>
        /// Local endpoint traffic over the last hour: requests answered on this machine, sent to the network, and fallbacks, per 5-minute bucket.
        /// Phase 5 moves this chart onto the shared usage model.
        /// </summary>
        /// <param name="records">Local endpoint request records, any order.</param>
        /// <param name="nowUtc">End of the window (UTC).</param>
        /// <returns>Chart.</returns>
        /// <exception cref="ArgumentNullException">records is null.</exception>
        public static XenoChart LocalEndpointTraffic(IEnumerable<Routing.LocalInferenceRequestRecord> records, DateTime nowUtc)
        {
            ArgumentNullException.ThrowIfNull(records);
            const int buckets = 12;
            DateTime end = new DateTime(nowUtc.Ticks - (nowUtc.Ticks % TimeSpan.FromMinutes(5).Ticks), DateTimeKind.Utc).AddMinutes(5);
            DateTime start = end.AddMinutes(-5 * buckets);
            double[] local = new double[buckets];
            double[] network = new double[buckets];
            double[] fallback = new double[buckets];
            foreach (Routing.LocalInferenceRequestRecord record in records)
            {
                if (record == null || record.ReceivedUtc < start || record.ReceivedUtc >= end) continue;
                int index = (int)((record.ReceivedUtc - start).TotalMinutes / 5);
                if (index < 0 || index >= buckets) continue;
                if (record.Fallback) fallback[index]++;
                else if (record.Route.StartsWith("local", StringComparison.OrdinalIgnoreCase)) local[index]++;
                else network[index]++;
            }

            List<string> labels = Enumerable.Range(0, buckets).Select(i => Label(start.AddMinutes(5 * i), HistoryRangeEnum.Hour)).ToList();
            List<XenoChartSeries> series = new List<XenoChartSeries>
            {
                new XenoChartSeries("This machine", XenoChartToneEnum.Success, local.ToList()),
                new XenoChartSeries("Xeno network", XenoChartToneEnum.Info, network.ToList()),
                new XenoChartSeries("Fallback", XenoChartToneEnum.Warning, fallback.ToList())
            };
            bool hasData = local.Sum() + network.Sum() + fallback.Sum() > 0;
            return new XenoChart("Local endpoint traffic", "last hour", " req", labels, series, "No local endpoint requests in the last hour. Point an app at the local endpoint to see where its requests go.", true, hasData);
        }

        /// <summary>
        /// Chat turns over time, split by where they ran, with failures on top. Matches the web Chat history chart.
        /// </summary>
        /// <param name="analytics">Chat analytics.</param>
        /// <param name="range">Range the analytics cover.</param>
        /// <returns>Chart.</returns>
        /// <exception cref="ArgumentNullException">analytics is null.</exception>
        public static XenoChart ChatActivity(Xeno.Core.Chat.ChatAnalytics analytics, HistoryRangeEnum range)
        {
            ArgumentNullException.ThrowIfNull(analytics);
            List<string> labels = analytics.Buckets.Select(bucket => Label(bucket.StartUtc, range)).ToList();
            List<XenoChartSeries> series = new List<XenoChartSeries>
            {
                new XenoChartSeries("This machine", XenoChartToneEnum.Success, analytics.Buckets.Select(bucket => (double)bucket.Local).ToList()),
                new XenoChartSeries("Xeno network", XenoChartToneEnum.Info, analytics.Buckets.Select(bucket => (double)bucket.Network).ToList()),
                new XenoChartSeries("Errors", XenoChartToneEnum.Danger, analytics.Buckets.Select(bucket => (double)bucket.Errors).ToList())
            };
            return new XenoChart("Chat activity", HistoryRanges.Describe(range).ToLowerInvariant(), " turns", labels, series, "No chat turns in this range.", true, analytics.Turns > 0);
        }

        /// <summary>
        /// A host metric over time (average per bucket).
        /// </summary>
        /// <param name="metric">Metric.</param>
        /// <param name="buckets">Rollup buckets.</param>
        /// <param name="range">Range the buckets cover.</param>
        /// <returns>Chart.</returns>
        /// <exception cref="ArgumentNullException">Buckets is null.</exception>
        public static XenoChart HostMetric(ClientTelemetryMetricEnum metric, IReadOnlyList<ClientTelemetryRollupBucket> buckets, HistoryRangeEnum range)
        {
            ArgumentNullException.ThrowIfNull(buckets);
            string rangeText = HistoryRanges.Describe(range);
            string title = HostMetricName(metric);
            bool hasData = buckets.Any(item => item.Count > 0 && item.Average != null);
            string empty = metric == ClientTelemetryMetricEnum.GpuUtilizationPercent || metric == ClientTelemetryMetricEnum.GpuMemoryUtilizationPercent
                || metric == ClientTelemetryMetricEnum.GpuTemperatureCelsius || metric == ClientTelemetryMetricEnum.GpuPowerWatts
                ? "No " + title + " readings in the " + rangeText + ". GPU telemetry needs an Apple silicon GPU or an NVIDIA GPU (nvidia-smi or DCGM)."
                : "No " + title + " readings in the " + rangeText + ". Host telemetry is collected while the client runs.";
            return RollupChart("telemetry.host", title, rangeText, HostMetricUnit(metric), buckets.Select(item => Label(item.StartUtc, range)).ToList(), buckets.Select(item => item.Average).ToList(), buckets.Select(item => item.Minimum).ToList(), buckets.Select(item => item.Maximum).ToList(), buckets.Select(item => new XenoChartBucket(item.StartUtc, item.EndUtc)).ToList(), empty, hasData);
        }

        /// <summary>
        /// A provider metric over time (average per bucket).
        /// </summary>
        /// <param name="metric">Metric.</param>
        /// <param name="providerName">Provider the buckets cover, or null for every provider.</param>
        /// <param name="buckets">Rollup buckets.</param>
        /// <param name="range">Range the buckets cover.</param>
        /// <returns>Chart.</returns>
        /// <exception cref="ArgumentNullException">Buckets is null.</exception>
        public static XenoChart ProviderMetric(ClientProviderTelemetryMetricEnum metric, string? providerName, IReadOnlyList<ClientProviderTelemetryRollupBucket> buckets, HistoryRangeEnum range)
        {
            ArgumentNullException.ThrowIfNull(buckets);
            string rangeText = HistoryRanges.Describe(range);
            string title = MetricName(metric.ToString());
            string scope = String.IsNullOrWhiteSpace(providerName) ? "any provider" : providerName;
            return RollupChart("telemetry.provider", title + (String.IsNullOrWhiteSpace(providerName) ? String.Empty : " (" + providerName + ")"), rangeText, ProviderMetricUnit(metric), buckets.Select(item => Label(item.StartUtc, range)).ToList(), buckets.Select(item => item.Average).ToList(), buckets.Select(item => item.Minimum).ToList(), buckets.Select(item => item.Maximum).ToList(), buckets.Select(item => new XenoChartBucket(item.StartUtc, item.EndUtc)).ToList(), "No " + title.ToLowerInvariant() + " readings from " + scope + " in the " + rangeText + ". Provider telemetry is collected while the client runs.", buckets.Any(item => item.Count > 0 && item.Average != null));
        }

        /// <summary>
        /// Display name of a host metric: CPU, Memory, Disk, GPU, GPU memory, GPU temperature, or GPU power.
        /// </summary>
        /// <param name="metric">Metric.</param>
        /// <returns>Name.</returns>
        public static string HostMetricName(ClientTelemetryMetricEnum metric)
        {
            switch (metric)
            {
                case ClientTelemetryMetricEnum.CpuUtilizationPercent: return "CPU";
                case ClientTelemetryMetricEnum.MemoryUtilizationPercent: return "Memory";
                case ClientTelemetryMetricEnum.DiskUtilizationPercent: return "Disk";
                case ClientTelemetryMetricEnum.GpuUtilizationPercent: return "GPU";
                case ClientTelemetryMetricEnum.GpuMemoryUtilizationPercent: return "GPU memory";
                case ClientTelemetryMetricEnum.GpuTemperatureCelsius: return "GPU temperature";
                case ClientTelemetryMetricEnum.GpuPowerWatts: return "GPU power";
                default: return MetricName(metric.ToString());
            }
        }

        /// <summary>
        /// Display name of a metric identifier: "ActiveRequests" becomes "Active requests".
        /// </summary>
        /// <param name="identifier">Identifier in PascalCase.</param>
        /// <returns>Name.</returns>
        public static string MetricName(string identifier)
        {
            if (String.IsNullOrEmpty(identifier)) return String.Empty;
            StringBuilder builder = new StringBuilder();
            for (int i = 0; i < identifier.Length; i++)
            {
                char c = identifier[i];
                if (i > 0 && Char.IsUpper(c)) builder.Append(' ').Append(Char.ToLowerInvariant(c));
                else builder.Append(c);
            }

            return builder.ToString();
        }

        #endregion

        #region Private-Methods

        private static XenoChart RollupChart(string id, string title, string rangeText, string unit, List<string> labels, List<double?> average, List<double?> minimum, List<double?> maximum, List<XenoChartBucket> windows, string empty, bool hasData)
        {
            List<XenoChartSeries> series = new List<XenoChartSeries>
            {
                new XenoChartSeries("Average", XenoChartToneEnum.Info, average.Select(item => item ?? Double.NaN).ToList()),
                new XenoChartSeries("Minimum", XenoChartToneEnum.Neutral, minimum.Select(item => item ?? Double.NaN).ToList()),
                new XenoChartSeries("Maximum", XenoChartToneEnum.Warning, maximum.Select(item => item ?? Double.NaN).ToList())
            };
            return new XenoChart(id, XenoChartKindEnum.Lines, title, rangeText, unit, labels, series, empty, false, hasData, false, null, null, windows, null);
        }

        private static string Label(DateTime startUtc, HistoryRangeEnum range)
        {
            DateTime local = DateTime.SpecifyKind(startUtc, DateTimeKind.Utc).ToLocalTime();
            string format = range == HistoryRangeEnum.Hour || range == HistoryRangeEnum.Day ? "HH:mm" : "MM-dd HH:mm";
            return local.ToString(format, CultureInfo.InvariantCulture);
        }

        private static string HostMetricUnit(ClientTelemetryMetricEnum metric)
        {
            if (metric == ClientTelemetryMetricEnum.GpuTemperatureCelsius) return " C";
            if (metric == ClientTelemetryMetricEnum.GpuPowerWatts) return " W";
            return "%";
        }

        private static string ProviderMetricUnit(ClientProviderTelemetryMetricEnum metric)
        {
            string name = metric.ToString();
            if (name.EndsWith("Percent", StringComparison.Ordinal)) return "%";
            if (name.EndsWith("PerSecond", StringComparison.Ordinal)) return "/s";
            if (name.EndsWith("Ms", StringComparison.Ordinal)) return " ms";
            return String.Empty;
        }

        #endregion
    }
}

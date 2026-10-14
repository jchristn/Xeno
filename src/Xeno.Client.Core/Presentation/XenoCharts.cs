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
        /// Host metrics charted on the Host Telemetry page, in display order (the same as the web dashboard).
        /// </summary>
        public static readonly IReadOnlyList<ClientTelemetryMetricEnum> HostMetrics = new List<ClientTelemetryMetricEnum>
        {
            ClientTelemetryMetricEnum.CpuUtilizationPercent,
            ClientTelemetryMetricEnum.MemoryUtilizationPercent,
            ClientTelemetryMetricEnum.GpuUtilizationPercent
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
                ? "No " + title + " readings in the " + rangeText + ". GPU telemetry needs an NVIDIA GPU (nvidia-smi or DCGM)."
                : "No " + title + " readings in the " + rangeText + ". Host telemetry is collected while the client runs.";
            return new XenoChart(
                title,
                rangeText,
                HostMetricUnit(metric),
                buckets.Select(item => Label(item.StartUtc, range)).ToList(),
                new List<XenoChartSeries> { new XenoChartSeries(title, HostMetricTone(metric), buckets.Select(item => item.Average ?? 0).ToList()) },
                empty,
                false,
                hasData);
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
            return new XenoChart(
                title + (String.IsNullOrWhiteSpace(providerName) ? String.Empty : " (" + providerName + ")"),
                rangeText,
                ProviderMetricUnit(metric),
                buckets.Select(item => Label(item.StartUtc, range)).ToList(),
                new List<XenoChartSeries> { new XenoChartSeries(title, XenoChartToneEnum.Info, buckets.Select(item => item.Average ?? 0).ToList()) },
                "No " + title.ToLowerInvariant() + " readings from " + scope + " in the " + rangeText + ". Provider telemetry is collected while the client runs.",
                false,
                buckets.Any(item => item.Count > 0 && item.Average != null));
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

        private static XenoChartToneEnum HostMetricTone(ClientTelemetryMetricEnum metric)
        {
            switch (metric)
            {
                case ClientTelemetryMetricEnum.CpuUtilizationPercent: return XenoChartToneEnum.Info;
                case ClientTelemetryMetricEnum.MemoryUtilizationPercent: return XenoChartToneEnum.Success;
                case ClientTelemetryMetricEnum.GpuUtilizationPercent: return XenoChartToneEnum.Warning;
                default: return XenoChartToneEnum.Accent;
            }
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

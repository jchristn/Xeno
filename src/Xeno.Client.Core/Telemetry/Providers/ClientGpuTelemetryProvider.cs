namespace Xeno.Client.Telemetry.Providers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Telemetry.Abstractions;
    using Xeno.Client.Telemetry.Models;

    /// <summary>
    /// Best-effort GPU telemetry provider.
    /// </summary>
    public class ClientGpuTelemetryProvider : IClientGpuTelemetryProvider
    {
        private readonly IClientTelemetryCommandRunner _CommandRunner;

        /// <summary>
        /// Instantiate the GPU provider.
        /// </summary>
        /// <param name="commandRunner">Command runner.</param>
        public ClientGpuTelemetryProvider(IClientTelemetryCommandRunner commandRunner)
        {
            _CommandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
        }

        /// <inheritdoc />
        public async Task<ClientGpuTelemetry> CollectAsync(ClientGpuTelemetrySettings settings, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            if (settings == null) throw new ArgumentNullException(nameof(settings));
            if (!settings.Enabled)
            {
                return new ClientGpuTelemetry { Status = new ClientTelemetryCollectionStatus { Status = ClientTelemetryCollectionStatusEnum.Disabled } };
            }

            if (OperatingSystem.IsMacOS())
            {
                ClientGpuTelemetry apple = await CollectAppleAsync(settings, token).ConfigureAwait(false);
                if (apple.Status.Status == ClientTelemetryCollectionStatusEnum.Available) return apple;
            }

            if (!String.IsNullOrWhiteSpace(settings.NvidiaDcgmExporterUrl))
            {
                ClientGpuTelemetry dcgm = await CollectDcgmAsync(settings.NvidiaDcgmExporterUrl, token).ConfigureAwait(false);
                if (dcgm.Status.Status == ClientTelemetryCollectionStatusEnum.Available) return dcgm;
            }

            return await CollectNvidiaSmiAsync(settings, token).ConfigureAwait(false);
        }

        /// <summary>
        /// Read Apple silicon GPUs from 'ioreg -r -d 1 -c IOAccelerator' output: the chip name and GPU core count, device
        /// utilization, and the unified memory the GPU is using. Temperature and power need root, so they stay empty.
        /// </summary>
        /// <param name="output">ioreg output.</param>
        /// <param name="systemMemoryBytes">Physical memory, shared by the CPU and GPU, or null when unknown.</param>
        /// <returns>One device per accelerator that reports statistics.</returns>
        public static List<ClientGpuDeviceTelemetry> ParseIoreg(string? output, long? systemMemoryBytes)
        {
            List<ClientGpuDeviceTelemetry> devices = new List<ClientGpuDeviceTelemetry>();
            if (String.IsNullOrWhiteSpace(output)) return devices;
            string[] blocks = output.Split(new[] { "+-o " }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string block in blocks)
            {
                System.Text.RegularExpressions.Match statistics = System.Text.RegularExpressions.Regex.Match(block, "\"PerformanceStatistics\" = \\{([^}]*)\\}");
                if (!statistics.Success) continue;
                string stats = statistics.Groups[1].Value;
                string model = Capture(block, "\"model\" = \"([^\"]+)\"") ?? "Apple GPU";
                string? cores = Capture(block, "\"gpu-core-count\" = (\\d+)");
                double? utilization = ParseDouble(Capture(stats, "\"Device Utilization %\"=(\\d+(?:\\.\\d+)?)"));
                long? used = ParseLong(Capture(stats, "\"In use system memory\"=(\\d+)"));
                ClientGpuDeviceTelemetry device = new ClientGpuDeviceTelemetry
                {
                    Uuid = "apple-gpu-" + devices.Count.ToString(CultureInfo.InvariantCulture),
                    Name = cores != null ? model + " (" + cores + "-core GPU)" : model,
                    UtilizationPercent = utilization,
                    MemoryUsedBytes = used,
                    MemoryTotalBytes = systemMemoryBytes
                };
                if (used.HasValue && systemMemoryBytes.HasValue && systemMemoryBytes.Value > 0)
                {
                    device.MemoryFreeBytes = Math.Max(0, systemMemoryBytes.Value - used.Value);
                    device.MemoryUtilizationPercent = Math.Round((double)used.Value / systemMemoryBytes.Value * 100.0, 2);
                }

                devices.Add(device);
            }

            return devices;
        }

        private async Task<ClientGpuTelemetry> CollectAppleAsync(ClientGpuTelemetrySettings settings, CancellationToken token)
        {
            ClientGpuTelemetry telemetry = new ClientGpuTelemetry { Provider = ClientTelemetryGpuProviderEnum.AppleIoreg };
            try
            {
                string output = await _CommandRunner.RunAsync("ioreg", "-r -d 1 -c IOAccelerator", TimeSpan.FromMilliseconds(settings.CommandTimeoutMs), token).ConfigureAwait(false);
                long memory = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes;
                telemetry.Devices.AddRange(ParseIoreg(output, memory > 0 ? memory : null));
                FinalizeGpuTelemetry(telemetry);
                telemetry.Status.Status = telemetry.DeviceCount > 0 ? ClientTelemetryCollectionStatusEnum.Available : ClientTelemetryCollectionStatusEnum.Unavailable;
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.ComponentModel.Win32Exception || ex is System.IO.IOException || ex is TimeoutException || ex is OperationCanceledException && !token.IsCancellationRequested)
            {
                telemetry.Status.Status = ClientTelemetryCollectionStatusEnum.Unavailable;
                telemetry.Status.Message = ex.Message;
            }

            return telemetry;
        }

        private static string? Capture(string text, string pattern)
        {
            System.Text.RegularExpressions.Match match = System.Text.RegularExpressions.Regex.Match(text, pattern);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static async Task<ClientGpuTelemetry> CollectDcgmAsync(string url, CancellationToken token)
        {
            ClientGpuTelemetry telemetry = new ClientGpuTelemetry { Provider = ClientTelemetryGpuProviderEnum.NvidiaDcgm };
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    string body = await client.GetStringAsync(url, token).ConfigureAwait(false);
                    string[] lines = body.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    foreach (string line in lines)
                    {
                        if (line.StartsWith("#", StringComparison.Ordinal)) continue;
                        if (!line.StartsWith("DCGM_FI_DEV_", StringComparison.OrdinalIgnoreCase)) continue;
                        ApplyDcgmMetric(telemetry, line);
                    }
                }

                FinalizeGpuTelemetry(telemetry);
                telemetry.Status.Status = telemetry.DeviceCount > 0 ? ClientTelemetryCollectionStatusEnum.Available : ClientTelemetryCollectionStatusEnum.Unavailable;
            }
            catch (Exception ex)
            {
                telemetry.Status.Status = ClientTelemetryCollectionStatusEnum.Error;
                telemetry.Status.Message = ex.Message;
            }

            return telemetry;
        }

        private async Task<ClientGpuTelemetry> CollectNvidiaSmiAsync(ClientGpuTelemetrySettings settings, CancellationToken token)
        {
            ClientGpuTelemetry telemetry = new ClientGpuTelemetry { Provider = ClientTelemetryGpuProviderEnum.NvidiaSmi };
            try
            {
                string arguments = "--query-gpu=uuid,name,pci.bus_id,driver_version,utilization.gpu,memory.total,memory.used,memory.free,temperature.gpu,power.draw --format=csv,noheader,nounits";
                string output = await _CommandRunner.RunAsync("nvidia-smi", arguments, TimeSpan.FromMilliseconds(settings.CommandTimeoutMs), token).ConfigureAwait(false);
                string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in lines)
                {
                    string[] parts = line.Split(',').Select(item => item.Trim()).ToArray();
                    if (parts.Length < 10) continue;
                    long totalMb = ParseLong(parts[5]) ?? 0;
                    long usedMb = ParseLong(parts[6]) ?? 0;
                    long freeMb = ParseLong(parts[7]) ?? 0;
                    ClientGpuDeviceTelemetry device = new ClientGpuDeviceTelemetry
                    {
                        Uuid = parts[0],
                        Name = parts[1],
                        BusId = parts[2],
                        DriverVersion = parts[3],
                        UtilizationPercent = ParseDouble(parts[4]),
                        MemoryTotalBytes = totalMb * 1024L * 1024L,
                        MemoryUsedBytes = usedMb * 1024L * 1024L,
                        MemoryFreeBytes = freeMb * 1024L * 1024L,
                        TemperatureCelsius = ParseDouble(parts[8]),
                        PowerWatts = ParseDouble(parts[9])
                    };
                    if (device.MemoryTotalBytes.HasValue && device.MemoryTotalBytes.Value > 0 && device.MemoryUsedBytes.HasValue)
                    {
                        device.MemoryUtilizationPercent = Math.Round((double)device.MemoryUsedBytes.Value / device.MemoryTotalBytes.Value * 100.0, 2);
                    }

                    telemetry.Devices.Add(device);
                }

                FinalizeGpuTelemetry(telemetry);
                telemetry.Status.Status = telemetry.DeviceCount > 0 ? ClientTelemetryCollectionStatusEnum.Available : ClientTelemetryCollectionStatusEnum.Unavailable;
            }
            catch (Exception ex)
            {
                telemetry.Status.Status = ClientTelemetryCollectionStatusEnum.Unavailable;
                telemetry.Status.Message = ex.Message;
            }

            return telemetry;
        }

        private static void ApplyDcgmMetric(ClientGpuTelemetry telemetry, string line)
        {
            int labelStart = line.IndexOf('{');
            int labelEnd = line.IndexOf('}');
            if (labelStart < 0 || labelEnd <= labelStart) return;
            string metricName = line.Substring(0, labelStart);
            string labelText = line.Substring(labelStart + 1, labelEnd - labelStart - 1);
            string valueText = line.Substring(labelEnd + 1).Trim();
            double? value = ParseDouble(valueText);
            if (!value.HasValue) return;
            string uuid = ReadDcgmLabel(labelText, "UUID");
            if (String.IsNullOrWhiteSpace(uuid)) uuid = ReadDcgmLabel(labelText, "gpu");
            ClientGpuDeviceTelemetry device = telemetry.Devices.FirstOrDefault(item => String.Equals(item.Uuid, uuid, StringComparison.OrdinalIgnoreCase)) ?? new ClientGpuDeviceTelemetry { Uuid = uuid };
            if (!telemetry.Devices.Contains(device)) telemetry.Devices.Add(device);
            device.Name = FirstNonEmpty(device.Name, ReadDcgmLabel(labelText, "modelName"));
            device.BusId = FirstNonEmpty(device.BusId, ReadDcgmLabel(labelText, "pci_bus_id"));
            device.DriverVersion = FirstNonEmpty(device.DriverVersion, ReadDcgmLabel(labelText, "driver_version"));
            if (metricName.Contains("GPU_UTIL", StringComparison.OrdinalIgnoreCase)) device.UtilizationPercent = value;
            if (metricName.Contains("FB_TOTAL", StringComparison.OrdinalIgnoreCase)) device.MemoryTotalBytes = (long)(value.Value * 1024 * 1024);
            if (metricName.Contains("FB_USED", StringComparison.OrdinalIgnoreCase)) device.MemoryUsedBytes = (long)(value.Value * 1024 * 1024);
            if (metricName.Contains("FB_FREE", StringComparison.OrdinalIgnoreCase)) device.MemoryFreeBytes = (long)(value.Value * 1024 * 1024);
            if (metricName.Contains("GPU_TEMP", StringComparison.OrdinalIgnoreCase)) device.TemperatureCelsius = value;
            if (metricName.Contains("POWER_USAGE", StringComparison.OrdinalIgnoreCase)) device.PowerWatts = value;
            if (device.MemoryTotalBytes.HasValue && device.MemoryTotalBytes.Value > 0 && device.MemoryUsedBytes.HasValue)
            {
                device.MemoryUtilizationPercent = Math.Round((double)device.MemoryUsedBytes.Value / device.MemoryTotalBytes.Value * 100.0, 2);
            }
        }

        private static string ReadDcgmLabel(string labels, string key)
        {
            string needle = key + "=\"";
            int start = labels.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return string.Empty;
            start += needle.Length;
            int end = labels.IndexOf('"', start);
            return end > start ? labels.Substring(start, end - start) : string.Empty;
        }

        private static void FinalizeGpuTelemetry(ClientGpuTelemetry telemetry)
        {
            telemetry.DeviceCount = telemetry.Devices.Count;
            telemetry.AverageUtilizationPercent = Average(telemetry.Devices.Select(item => item.UtilizationPercent));
            telemetry.AverageMemoryUtilizationPercent = Average(telemetry.Devices.Select(item => item.MemoryUtilizationPercent));
            telemetry.AverageTemperatureCelsius = Average(telemetry.Devices.Select(item => item.TemperatureCelsius));
            telemetry.AveragePowerWatts = Average(telemetry.Devices.Select(item => item.PowerWatts));
            telemetry.TotalMemoryBytes = Sum(telemetry.Devices.Select(item => item.MemoryTotalBytes));
            telemetry.UsedMemoryBytes = Sum(telemetry.Devices.Select(item => item.MemoryUsedBytes));
        }

        private static double? Average(IEnumerable<double?> values)
        {
            double[] present = values.Where(item => item.HasValue).Select(item => item!.Value).ToArray();
            return present.Length == 0 ? null : Math.Round(present.Average(), 2);
        }

        private static long? Sum(IEnumerable<long?> values)
        {
            long[] present = values.Where(item => item.HasValue).Select(item => item!.Value).ToArray();
            return present.Length == 0 ? null : present.Sum();
        }

        private static double? ParseDouble(string? value)
        {
            string cleaned = (value ?? string.Empty).Replace("W", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            return Double.TryParse(cleaned, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed) ? Math.Round(parsed, 2) : null;
        }

        private static long? ParseLong(string? value)
        {
            return Int64.TryParse((value ?? string.Empty).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long parsed) ? parsed : null;
        }

        private static string FirstNonEmpty(string? first, string second)
        {
            return String.IsNullOrWhiteSpace(first) ? second : first;
        }
    }
}

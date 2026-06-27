namespace Xeno.Client.Telemetry.Providers
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Telemetry.Abstractions;
    using Xeno.Client.Telemetry.Models;

    /// <summary>
    /// Cross-platform best-effort system telemetry provider.
    /// </summary>
    public class ClientSystemTelemetryProvider : IClientSystemTelemetryProvider
    {
        private readonly IClientTelemetryCommandRunner _CommandRunner;

        /// <summary>
        /// Instantiate the provider.
        /// </summary>
        /// <param name="commandRunner">Command runner.</param>
        public ClientSystemTelemetryProvider(IClientTelemetryCommandRunner commandRunner)
        {
            _CommandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
        }

        /// <inheritdoc />
        public async Task<ClientTelemetrySnapshot> CollectAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ClientTelemetrySnapshot snapshot = new ClientTelemetrySnapshot();
            snapshot.System.OsDescription = RuntimeInformation.OSDescription;
            snapshot.System.Architecture = RuntimeInformation.ProcessArchitecture.ToString();
            snapshot.System.ProcessorCount = Environment.ProcessorCount;
            snapshot.System.Status.Status = ClientTelemetryCollectionStatusEnum.Available;
            snapshot.Cpu.LogicalProcessorCount = Environment.ProcessorCount;
            try
            {
                snapshot.Cpu.UtilizationPercent = await CollectCpuAsync(token).ConfigureAwait(false);
                snapshot.Cpu.Status.Status = snapshot.Cpu.UtilizationPercent.HasValue ? ClientTelemetryCollectionStatusEnum.Available : ClientTelemetryCollectionStatusEnum.Unavailable;
            }
            catch (Exception ex)
            {
                snapshot.Cpu.Status.Status = ClientTelemetryCollectionStatusEnum.Error;
                snapshot.Cpu.Status.Message = ex.Message;
            }

            snapshot.Memory = await CollectMemoryAsync(token).ConfigureAwait(false);
            snapshot.Disk = CollectDisk();
            return snapshot;
        }

        private async Task<double?> CollectCpuAsync(CancellationToken token)
        {
            if (OperatingSystem.IsLinux()) return await CollectLinuxCpuAsync(token).ConfigureAwait(false);
            if (OperatingSystem.IsWindows()) return await CollectWindowsCpuAsync(token).ConfigureAwait(false);
            if (OperatingSystem.IsMacOS()) return await CollectMacCpuAsync(token).ConfigureAwait(false);
            return null;
        }

        private static async Task<double?> CollectLinuxCpuAsync(CancellationToken token)
        {
            long[] first = ReadLinuxCpu();
            await Task.Delay(250, token).ConfigureAwait(false);
            long[] second = ReadLinuxCpu();
            long idle = second[3] - first[3];
            long total = second.Sum() - first.Sum();
            if (total <= 0) return null;
            return Math.Round((1.0 - ((double)idle / total)) * 100.0, 2);
        }

        private async Task<double?> CollectWindowsCpuAsync(CancellationToken token)
        {
            string output = await _CommandRunner.RunAsync("powershell", "-NoProfile -Command \"(Get-Counter '\\Processor(_Total)\\% Processor Time').CounterSamples.CookedValue\"", TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
            return ParseFirstDouble(output);
        }

        private async Task<double?> CollectMacCpuAsync(CancellationToken token)
        {
            string output = await _CommandRunner.RunAsync("sh", "-c \"top -l 2 -n 0 | grep 'CPU usage' | tail -1\"", TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
            int userIndex = output.IndexOf("% user", StringComparison.OrdinalIgnoreCase);
            if (userIndex <= 0) return null;
            int start = output.LastIndexOf(' ', userIndex - 1);
            if (start < 0) return null;
            string value = output.Substring(start, userIndex - start).Trim();
            return ParseFirstDouble(value);
        }

        private async Task<ClientMemoryTelemetry> CollectMemoryAsync(CancellationToken token)
        {
            if (OperatingSystem.IsLinux()) return CollectLinuxMemory();
            if (OperatingSystem.IsWindows()) return await CollectWindowsMemoryAsync(token).ConfigureAwait(false);
            if (OperatingSystem.IsMacOS()) return await CollectMacMemoryAsync(token).ConfigureAwait(false);
            ClientMemoryTelemetry telemetry = new ClientMemoryTelemetry();
            telemetry.Status.Status = ClientTelemetryCollectionStatusEnum.Unavailable;
            return telemetry;
        }

        private static ClientMemoryTelemetry CollectLinuxMemory()
        {
            ClientMemoryTelemetry telemetry = new ClientMemoryTelemetry();
            try
            {
                string[] lines = File.ReadAllLines("/proc/meminfo");
                long total = ReadMemInfoKb(lines, "MemTotal") * 1024;
                long available = ReadMemInfoKb(lines, "MemAvailable") * 1024;
                long used = Math.Max(0, total - available);
                telemetry.TotalBytes = total;
                telemetry.AvailableBytes = available;
                telemetry.UsedBytes = used;
                telemetry.UtilizationPercent = total > 0 ? Math.Round((double)used / total * 100.0, 2) : null;
                telemetry.Status.Status = ClientTelemetryCollectionStatusEnum.Available;
            }
            catch (Exception ex)
            {
                telemetry.Status.Status = ClientTelemetryCollectionStatusEnum.Error;
                telemetry.Status.Message = ex.Message;
            }

            return telemetry;
        }

        private async Task<ClientMemoryTelemetry> CollectWindowsMemoryAsync(CancellationToken token)
        {
            ClientMemoryTelemetry telemetry = new ClientMemoryTelemetry();
            try
            {
                string output = await _CommandRunner.RunAsync("powershell", "-NoProfile -Command \"Get-CimInstance Win32_OperatingSystem | Select-Object -ExpandProperty TotalVisibleMemorySize; Get-CimInstance Win32_OperatingSystem | Select-Object -ExpandProperty FreePhysicalMemory\"", TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
                string[] lines = output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length >= 2)
                {
                    long total = Int64.Parse(lines[0].Trim(), CultureInfo.InvariantCulture) * 1024;
                    long available = Int64.Parse(lines[1].Trim(), CultureInfo.InvariantCulture) * 1024;
                    long used = Math.Max(0, total - available);
                    telemetry.TotalBytes = total;
                    telemetry.AvailableBytes = available;
                    telemetry.UsedBytes = used;
                    telemetry.UtilizationPercent = total > 0 ? Math.Round((double)used / total * 100.0, 2) : null;
                    telemetry.Status.Status = ClientTelemetryCollectionStatusEnum.Available;
                    return telemetry;
                }
            }
            catch (Exception ex)
            {
                telemetry.Status.Message = ex.Message;
            }

            telemetry.Status.Status = ClientTelemetryCollectionStatusEnum.Unavailable;
            return telemetry;
        }

        private async Task<ClientMemoryTelemetry> CollectMacMemoryAsync(CancellationToken token)
        {
            ClientMemoryTelemetry telemetry = new ClientMemoryTelemetry();
            try
            {
                string totalText = await _CommandRunner.RunAsync("sysctl", "-n hw.memsize", TimeSpan.FromSeconds(5), token).ConfigureAwait(false);
                long total = Int64.Parse(totalText.Trim(), CultureInfo.InvariantCulture);
                telemetry.TotalBytes = total;
                telemetry.Status.Status = ClientTelemetryCollectionStatusEnum.Available;
            }
            catch (Exception ex)
            {
                telemetry.Status.Status = ClientTelemetryCollectionStatusEnum.Unavailable;
                telemetry.Status.Message = ex.Message;
            }

            return telemetry;
        }

        private static ClientDiskTelemetry CollectDisk()
        {
            ClientDiskTelemetry telemetry = new ClientDiskTelemetry();
            try
            {
                foreach (DriveInfo drive in DriveInfo.GetDrives())
                {
                    if (!drive.IsReady) continue;
                    long total = drive.TotalSize;
                    long available = drive.AvailableFreeSpace;
                    long used = Math.Max(0, total - available);
                    telemetry.TotalBytes += total;
                    telemetry.AvailableBytes += available;
                    telemetry.UsedBytes += used;
                    telemetry.Volumes.Add(new ClientDiskVolumeTelemetry
                    {
                        Name = drive.Name,
                        Format = drive.DriveFormat,
                        TotalBytes = total,
                        AvailableBytes = available,
                        UsedBytes = used,
                        UtilizationPercent = total > 0 ? Math.Round((double)used / total * 100.0, 2) : 0
                    });
                }

                telemetry.UtilizationPercent = telemetry.TotalBytes > 0 ? Math.Round((double)telemetry.UsedBytes / telemetry.TotalBytes * 100.0, 2) : null;
                telemetry.Status.Status = ClientTelemetryCollectionStatusEnum.Available;
            }
            catch (Exception ex)
            {
                telemetry.Status.Status = ClientTelemetryCollectionStatusEnum.Error;
                telemetry.Status.Message = ex.Message;
            }

            return telemetry;
        }

        private static long[] ReadLinuxCpu()
        {
            string line = File.ReadLines("/proc/stat").First(item => item.StartsWith("cpu ", StringComparison.Ordinal));
            string[] parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Skip(1).ToArray();
            long[] values = new long[parts.Length];
            for (int i = 0; i < parts.Length; i++)
            {
                values[i] = Int64.Parse(parts[i], CultureInfo.InvariantCulture);
            }

            return values;
        }

        private static long ReadMemInfoKb(string[] lines, string key)
        {
            string line = lines.First(item => item.StartsWith(key + ":", StringComparison.OrdinalIgnoreCase));
            string numeric = line.Substring(line.IndexOf(':') + 1).Replace("kB", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
            return Int64.Parse(numeric, CultureInfo.InvariantCulture);
        }

        private static double? ParseFirstDouble(string value)
        {
            string[] parts = (value ?? string.Empty).Split(new[] { ' ', '\r', '\n', '\t', '%' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                if (Double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out double parsed)) return Math.Round(parsed, 2);
            }

            return null;
        }
    }
}

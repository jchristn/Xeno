namespace Xeno.Client.Telemetry.Services
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Telemetry.Abstractions;

    /// <summary>
    /// Process-based telemetry command runner.
    /// </summary>
    public class ClientTelemetryCommandRunner : IClientTelemetryCommandRunner
    {
        /// <inheritdoc />
        public async Task<string> RunAsync(string filename, string arguments, TimeSpan timeout, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = filename,
                Arguments = arguments,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            using (Process process = new Process { StartInfo = startInfo })
            using (CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeoutSource.CancelAfter(timeout);
                if (!process.Start()) throw new InvalidOperationException("Unable to start telemetry command " + filename + ".");
                string output = await process.StandardOutput.ReadToEndAsync(timeoutSource.Token).ConfigureAwait(false);
                await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
                return output;
            }
        }
    }
}

namespace Xeno.Tray
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Default process runner for xeno CLI commands.
    /// </summary>
    public sealed class XenoTrayProcessRunner : IXenoTrayProcessRunner
    {
        /// <inheritdoc />
        public async Task<XenoTrayProcessResult> RunAsync(string filename, IReadOnlyList<string> arguments, int timeoutMs, CancellationToken token)
        {
            using (CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(token))
            {
                timeoutSource.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1000, timeoutMs)));
                ProcessStartInfo startInfo = new ProcessStartInfo
                {
                    FileName = filename,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                };
                foreach (string argument in arguments)
                {
                    startInfo.ArgumentList.Add(argument);
                }

                StringBuilder output = new StringBuilder();
                StringBuilder error = new StringBuilder();
                using (Process process = new Process { StartInfo = startInfo, EnableRaisingEvents = true })
                {
                    process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs args)
                    {
                        if (args.Data != null) output.AppendLine(args.Data);
                    };
                    process.ErrorDataReceived += delegate(object sender, DataReceivedEventArgs args)
                    {
                        if (args.Data != null) error.AppendLine(args.Data);
                    };
                    if (!process.Start()) throw new InvalidOperationException("Unable to start " + filename + ".");
                    process.BeginOutputReadLine();
                    process.BeginErrorReadLine();
                    await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
                    return new XenoTrayProcessResult
                    {
                        ExitCode = process.ExitCode,
                        StandardOutput = output.ToString(),
                        StandardError = error.ToString()
                    };
                }
            }
        }
    }
}

namespace Xeno.Tray
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client;
    using Xeno.Client.LocalDashboard.Database;
    using Xeno.Client.LocalDashboard.Models;

    /// <summary>
    /// Default tray controller backed by Xeno client settings, state, and CLI commands.
    /// </summary>
    public sealed class XenoTrayClientController : IXenoTrayClientController
    {
        private readonly XenoTrayOptions _Options;
        private readonly IXenoTrayProcessRunner _ProcessRunner;

        /// <summary>
        /// Instantiate the controller.
        /// </summary>
        /// <param name="options">Tray options.</param>
        /// <param name="processRunner">Process runner.</param>
        public XenoTrayClientController(XenoTrayOptions options, IXenoTrayProcessRunner processRunner)
        {
            _Options = options ?? throw new ArgumentNullException(nameof(options));
            _ProcessRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        }

        /// <inheritdoc />
        public Task<XenoTrayStatus> GetStatusAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            ClientSettings settings = ClientSettings.Load();
            ClientConnectionState state = ClientConnectionState.Load();
            return Task.FromResult(XenoTrayStatus.From(settings, state));
        }

        /// <inheritdoc />
        public async Task<XenoTrayOperationResult> StartAsync(CancellationToken token)
        {
            XenoTrayStatus status = await GetStatusAsync(token).ConfigureAwait(false);
            if (status.Running) return XenoTrayOperationResult.Success("Xeno Client is already running.");
            XenoTrayProcessResult result = await _ProcessRunner.RunAsync(_Options.ClientExecutablePath, XenoTrayCommandBuilder.BuildStartArguments(), _Options.StartTimeoutMs, token).ConfigureAwait(false);
            return ResultFromProcess(result, "Xeno Client started.", "Unable to start Xeno Client.");
        }

        /// <inheritdoc />
        public async Task<XenoTrayOperationResult> StopAsync(CancellationToken token)
        {
            XenoTrayStatus status = await GetStatusAsync(token).ConfigureAwait(false);
            if (!status.Running) return XenoTrayOperationResult.Success("Xeno Client is not running.");
            XenoTrayProcessResult result = await _ProcessRunner.RunAsync(_Options.ClientExecutablePath, XenoTrayCommandBuilder.BuildStopArguments(), _Options.StopTimeoutMs, token).ConfigureAwait(false);
            return ResultFromProcess(result, "Xeno Client stopped.", "Unable to stop Xeno Client.");
        }

        /// <inheritdoc />
        public async Task<XenoTrayStatistics> GetStatisticsAsync(CancellationToken token)
        {
            try
            {
                ClientSettings settings = ClientSettings.Load();
                using (LocalDashboardDatabaseDriverBase driver = LocalDashboardDatabaseDriverFactory.Create(settings.LocalDashboard.Database))
                {
                    await driver.InitializeAsync(token).ConfigureAwait(false);
                    LocalDashboardStatisticsResponse statistics = await driver.RequestHistory.StatisticsAsync(token).ConfigureAwait(false);
                    return XenoTrayStatistics.From(statistics);
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is System.IO.IOException || ex is UnauthorizedAccessException)
            {
                return new XenoTrayStatistics { Message = "Statistics unavailable: " + ex.Message };
            }
        }

        /// <inheritdoc />
        public async Task<XenoTrayOperationResult> OpenDashboardAsync(CancellationToken token)
        {
            XenoTrayStatus status = await GetStatusAsync(token).ConfigureAwait(false);
            if (!status.LocalDashboardListening || string.IsNullOrWhiteSpace(status.LocalDashboardUrl))
            {
                return XenoTrayOperationResult.Failure("Local dashboard is not listening.");
            }

            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = status.LocalDashboardUrl,
                UseShellExecute = true
            };
            Process.Start(startInfo);
            return XenoTrayOperationResult.Success("Opened local dashboard.");
        }

        private static XenoTrayOperationResult ResultFromProcess(XenoTrayProcessResult result, string successMessage, string failureMessage)
        {
            if (result.Succeeded) return XenoTrayOperationResult.Success(successMessage);
            string detail = string.IsNullOrWhiteSpace(result.StandardError) ? result.StandardOutput : result.StandardError;
            return XenoTrayOperationResult.Failure(failureMessage + " " + detail.Trim());
        }
    }
}

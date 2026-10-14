namespace Xeno.Client.Services
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Hosting;

    /// <summary>
    /// Starts and stops the background client worker (xeno connect --foreground --worker). Shared by the CLI, the
    /// desktop app, the terminal app, and the tray agent. Coordination happens through files in the client home
    /// directory: state.json records the worker process, and the stop file asks it to exit. Thread-safe.
    /// </summary>
    public static class ClientWorkerController
    {
        #region Public-Members

        /// <summary>
        /// Default time to wait for a new worker to register, in milliseconds.
        /// </summary>
        public const int DefaultStartTimeoutMs = 30000;

        /// <summary>
        /// Default time to wait for a worker to exit after a stop request, in milliseconds.
        /// </summary>
        public const int DefaultStopTimeoutMs = 10000;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Determine whether a registered worker with fresh activity is running.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="state">Connection state.</param>
        /// <returns>True when a healthy worker is running.</returns>
        /// <exception cref="ArgumentNullException">Settings or state is null.</exception>
        public static bool IsHealthyWorkerRunning(ClientSettings settings, ClientConnectionState state)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(state);
            return state.IsConnected
                && state.IsRegistered
                && state.IsRecordedProcessRunning()
                && state.HasFreshActivity(DateTime.UtcNow, settings.Server.LivenessTimeoutMs);
        }

        /// <summary>
        /// Build the process start information for a background worker.
        /// </summary>
        /// <param name="cli">Location of the xeno executable.</param>
        /// <returns>Start information.</returns>
        /// <exception cref="ArgumentNullException">Location is null.</exception>
        public static ProcessStartInfo CreateWorkerStartInfo(XenoExecutableLocation cli)
        {
            ArgumentNullException.ThrowIfNull(cli);
            bool isWindows = OperatingSystem.IsWindows();
            ProcessStartInfo startInfo = cli.CreateStartInfo(new[] { "--no-color", "connect", "--foreground", "--worker" });
            startInfo.CreateNoWindow = true;
            startInfo.RedirectStandardError = !isWindows;
            startInfo.RedirectStandardOutput = !isWindows;
            startInfo.UseShellExecute = isWindows;
            startInfo.WindowStyle = ProcessWindowStyle.Hidden;
            startInfo.WorkingDirectory = Environment.CurrentDirectory;
            if (!isWindows)
            {
                string? clientHome = Environment.GetEnvironmentVariable("XENO_CLIENT_HOME");
                if (!String.IsNullOrWhiteSpace(clientHome)) startInfo.Environment["XENO_CLIENT_HOME"] = clientHome;
                startInfo.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
                startInfo.Environment["XENO_CLIENT_BACKGROUND"] = "1";
            }

            return startInfo;
        }

        /// <summary>
        /// Start a background worker unless a healthy one is already running, then wait for it to register with the
        /// Xeno server and for its local listeners to report.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="cli">Location of the xeno executable used to start the worker.</param>
        /// <param name="timeoutMs">Registration timeout in milliseconds (minimum 1000).</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Start result.</returns>
        /// <exception cref="ArgumentNullException">Settings or location is null.</exception>
        /// <exception cref="ArgumentException">A provider endpoint loops back to the local inference server.</exception>
        /// <exception cref="InvalidOperationException">No access key is configured, or the process could not be started.</exception>
        public static async Task<ClientStartResult> StartAsync(ClientSettings settings, XenoExecutableLocation cli, int timeoutMs, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(cli);
            settings.ValidateLocalInferenceProviderEndpoints();
            ServerEndpoints.EnsureClientId(settings);
            if (!ServerEndpoints.HasWebSocketCredential(settings))
            {
                throw new InvalidOperationException("Run 'xeno login -u <username>' or configure an access key before connecting.");
            }

            ClientConnectionState existing = ClientConnectionState.Load();
            if (IsHealthyWorkerRunning(settings, existing))
            {
                return new ClientStartResult { Outcome = ClientStartOutcomeEnum.AlreadyRunning, ProcessId = existing.ProcessId ?? 0, State = existing };
            }

            ClientConnectionState.ClearStopRequest();
            ProcessStartInfo startInfo = CreateWorkerStartInfo(cli);
            using (Process process = new Process { StartInfo = startInfo })
            {
                if (!process.Start()) throw new InvalidOperationException("Unable to start Xeno client background process.");
                int effectiveTimeout = Math.Max(1000, timeoutMs);
                bool registered = await WaitForRegisteredStateAsync(process.Id, effectiveTimeout, token).ConfigureAwait(false);
                ClientConnectionState state = await WaitForListenerStateAsync(process.Id, settings, registered, token).ConfigureAwait(false);
                return new ClientStartResult
                {
                    Outcome = registered ? ClientStartOutcomeEnum.Started : ClientStartOutcomeEnum.NotRegistered,
                    ProcessId = process.Id,
                    State = state
                };
            }
        }

        /// <summary>
        /// Ask the running worker to stop and wait for it to exit.
        /// </summary>
        /// <param name="force">True to terminate the worker when it does not exit in time.</param>
        /// <param name="timeoutMs">Time to wait for a graceful exit, in milliseconds.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Stop result.</returns>
        public static async Task<ClientStopResult> StopAsync(bool force, int timeoutMs, CancellationToken token)
        {
            ClientConnectionState state = ClientConnectionState.Load();
            if (state.ProcessId == null || !state.IsRecordedProcessRunning())
            {
                return new ClientStopResult { Stopped = true, WasRunning = false, ProcessId = state.ProcessId };
            }

            ClientConnectionState.RequestStop();
            bool stopped = await WaitForProcessExitAsync(state.ProcessId.Value, Math.Max(1, timeoutMs), token).ConfigureAwait(false);
            bool forced = false;
            if (!stopped && force)
            {
                stopped = KillProcess(state.ProcessId.Value, timeoutMs);
                forced = true;
            }

            return new ClientStopResult { Stopped = stopped, WasRunning = true, ProcessId = state.ProcessId, Forced = forced };
        }

        /// <summary>
        /// Wait for a worker process to record a registered connection.
        /// </summary>
        /// <param name="processId">Worker process identifier.</param>
        /// <param name="timeoutMs">Timeout in milliseconds.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the worker registered before the timeout.</returns>
        public static async Task<bool> WaitForRegisteredStateAsync(int processId, int timeoutMs, CancellationToken token)
        {
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTime.UtcNow < deadline && !token.IsCancellationRequested)
            {
                ClientConnectionState state = ClientConnectionState.Load();
                if (state.ProcessId == processId && state.IsConnected && state.IsRegistered) return true;
                if (!IsProcessRunning(processId)) return false;
                await Task.Delay(250, token).ConfigureAwait(false);
            }

            return false;
        }

        /// <summary>
        /// Wait up to five seconds for the local dashboard and local inference listeners to report listening or an
        /// error.
        /// </summary>
        /// <param name="processId">Worker process identifier.</param>
        /// <param name="settings">Client settings.</param>
        /// <param name="registered">Whether the worker registered.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Latest connection state.</returns>
        /// <exception cref="ArgumentNullException">Settings is null.</exception>
        public static async Task<ClientConnectionState> WaitForListenerStateAsync(int processId, ClientSettings settings, bool registered, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ClientConnectionState state = ClientConnectionState.Load();
            bool waitForDashboard = settings.LocalDashboard.Enabled && registered;
            bool waitForInference = settings.LocalInferenceServer.Enabled;
            if (!waitForDashboard && !waitForInference) return state;

            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (DateTime.UtcNow < deadline && !token.IsCancellationRequested)
            {
                state = ClientConnectionState.Load();
                bool dashboardReady = !waitForDashboard ||
                    (state.LocalDashboardEnabled && (state.LocalDashboardListening || !String.IsNullOrWhiteSpace(state.LocalDashboardLastError)));
                bool inferenceReady = !waitForInference ||
                    (state.LocalInferenceServerEnabled && (state.LocalInferenceServerListening || !String.IsNullOrWhiteSpace(state.LocalInferenceServerLastError)));
                if (state.ProcessId == processId && dashboardReady && inferenceReady) return state;
                if (!IsProcessRunning(processId)) return state;
                await Task.Delay(100, token).ConfigureAwait(false);
            }

            return ClientConnectionState.Load();
        }

        /// <summary>
        /// Wait for a process to exit.
        /// </summary>
        /// <param name="processId">Process identifier.</param>
        /// <param name="timeoutMs">Timeout in milliseconds.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>True when the process exited or does not exist.</returns>
        public static async Task<bool> WaitForProcessExitAsync(int processId, int timeoutMs, CancellationToken token)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                using (CancellationTokenSource timeout = CancellationTokenSource.CreateLinkedTokenSource(token))
                {
                    timeout.CancelAfter(timeoutMs);
                    try
                    {
                        await process.WaitForExitAsync(timeout.Token).ConfigureAwait(false);
                        return true;
                    }
                    catch (OperationCanceledException)
                    {
                        return process.HasExited;
                    }
                }
            }
            catch (ArgumentException)
            {
                return true;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        /// <summary>
        /// Terminate a process and its children.
        /// </summary>
        /// <param name="processId">Process identifier.</param>
        /// <param name="timeoutMs">Time to wait for exit after the kill, in milliseconds.</param>
        /// <returns>True when the process exited or does not exist.</returns>
        public static bool KillProcess(int processId, int timeoutMs)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    process.Kill(true);
                    process.WaitForExit(Math.Max(1, timeoutMs));
                    return process.HasExited;
                }
            }
            catch (ArgumentException)
            {
                return true;
            }
            catch (InvalidOperationException)
            {
                return true;
            }
        }

        /// <summary>
        /// Determine whether a process is running.
        /// </summary>
        /// <param name="processId">Process identifier.</param>
        /// <returns>True when the process exists and has not exited.</returns>
        public static bool IsProcessRunning(int processId)
        {
            try
            {
                using (Process process = Process.GetProcessById(processId))
                {
                    return !process.HasExited;
                }
            }
            catch (ArgumentException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        #endregion
    }
}

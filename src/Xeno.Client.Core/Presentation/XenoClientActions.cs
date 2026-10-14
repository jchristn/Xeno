namespace Xeno.Client.Presentation
{
    using System;
    using System.Globalization;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Hosting;
    using Xeno.Client.Services;

    /// <summary>
    /// Client actions shared by the desktop app, the terminal app, and the tray agent. Each returns a one-line
    /// message describing the outcome so every surface reports results the same way. Exceptions from the client
    /// lifecycle are converted into messages.
    /// </summary>
    public static class XenoClientActions
    {
        /// <summary>
        /// Describe a completed sign-in and the next step: start the client, or restart it when it is already running
        /// with the previous credential.
        /// </summary>
        /// <param name="status">Status captured after signing in.</param>
        /// <returns>Follow-up.</returns>
        /// <exception cref="ArgumentNullException">Status is null.</exception>
        public static XenoFollowUp AfterSignIn(ClientStatusSnapshot status)
        {
            ArgumentNullException.ThrowIfNull(status);
            string server = String.IsNullOrWhiteSpace(status.ServerLabel) ? status.RestBaseUrl : status.ServerLabel + " (" + status.RestBaseUrl + ")";
            string message = "Signed in to " + server + (String.IsNullOrWhiteSpace(status.TenantId) ? String.Empty : " in tenant " + status.TenantId)
                + ". This machine's credential is saved in " + status.SettingsFile + "; your password is not stored.";
            if (status.Running)
            {
                return new XenoFollowUp
                {
                    Title = "Signed In",
                    Message = message + Environment.NewLine + Environment.NewLine + "The client is still running with its previous credential. Restart it to connect with the new one.",
                    ActionText = "Restart Client",
                    Command = XenoCommandEnum.RestartClient
                };
            }

            return new XenoFollowUp
            {
                Title = "Signed In",
                Message = message + Environment.NewLine + Environment.NewLine + "Start the client to connect " + (String.IsNullOrWhiteSpace(status.Name) ? "this machine" : status.Name) + " to the Xeno network.",
                ActionText = "Start Client",
                Command = XenoCommandEnum.StartClient
            };
        }

        /// <summary>
        /// Start the client worker.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Outcome message.</returns>
        /// <exception cref="ArgumentNullException">Workspace is null.</exception>
        public static async Task<string> StartAsync(XenoWorkspace workspace, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            try
            {
                ClientStartResult result = await workspace.StartClientAsync(token).ConfigureAwait(false);
                return DescribeStart(result);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is System.ComponentModel.Win32Exception)
            {
                return "Could not start the client: " + ex.Message;
            }
        }

        /// <summary>
        /// Stop the client worker.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Outcome message.</returns>
        /// <exception cref="ArgumentNullException">Workspace is null.</exception>
        public static async Task<string> StopAsync(XenoWorkspace workspace, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            ClientStopResult result = await workspace.StopClientAsync(true, token).ConfigureAwait(false);
            if (!result.WasRunning) return "The client is not running.";
            if (!result.Stopped) return "The client did not stop (process " + result.ProcessId + ").";
            return result.Forced ? "Stopped the client (forced)." : "Stopped the client.";
        }

        /// <summary>
        /// Restart the client worker.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Outcome message.</returns>
        /// <exception cref="ArgumentNullException">Workspace is null.</exception>
        public static async Task<string> RestartAsync(XenoWorkspace workspace, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            try
            {
                ClientStartResult result = await workspace.RestartClientAsync(token).ConfigureAwait(false);
                return result.Connected ? "Restarted the client. " + DescribeStart(result) : DescribeStart(result);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException || ex is System.ComponentModel.Win32Exception)
            {
                return "Could not restart the client: " + ex.Message;
            }
        }

        /// <summary>
        /// Ask the worker to re-advertise providers and models.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <returns>Outcome message.</returns>
        /// <exception cref="ArgumentNullException">Workspace is null.</exception>
        public static string RequestInventoryUpdate(XenoWorkspace workspace)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            workspace.RequestInventoryUpdate();
            ClientStatusSnapshot status = workspace.GetStatus();
            return status.Connected
                ? "The client will re-advertise its models on the next heartbeat."
                : "Saved. Models will be advertised the next time the client connects.";
        }

        /// <summary>
        /// Ping the Xeno server and summarize the result.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <param name="count">Attempts.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Outcome message.</returns>
        /// <exception cref="ArgumentNullException">Workspace is null.</exception>
        public static async Task<string> PingAsync(XenoWorkspace workspace, int count, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            try
            {
                System.Collections.Generic.List<PingAttemptResult> attempts = await workspace.PingServerAsync(count, token).ConfigureAwait(false);
                int received = attempts.Count(item => item.Success);
                string target = DisplayFormat.OrDash(workspace.GetStatus().RestBaseUrl);
                if (received == 0)
                {
                    PingAttemptResult? last = attempts.LastOrDefault();
                    string reason = last == null ? "no response" : (last.Error ?? ("HTTP " + last.StatusCode.ToString(CultureInfo.InvariantCulture)));
                    return "The Xeno server at " + target + " did not respond (" + reason + ").";
                }

                double average = attempts.Where(item => item.Success).Average(item => item.ElapsedMs);
                return "The Xeno server at " + target + " responded to " + received + " of " + attempts.Count + " pings, averaging " + average.ToString("0", CultureInfo.InvariantCulture) + " ms.";
            }
            catch (UriFormatException ex)
            {
                return "The server URL is invalid: " + ex.Message;
            }
        }

        /// <summary>
        /// Open the local web dashboard.
        /// </summary>
        /// <returns>Outcome message.</returns>
        public static string OpenWebDashboard()
        {
            return XenoAppLauncher.OpenLocalDashboard().Message;
        }

        /// <summary>
        /// Open the terminal app in a new terminal window.
        /// </summary>
        /// <returns>Outcome message.</returns>
        public static string OpenTerminalApp()
        {
            return XenoAppLauncher.LaunchTerminalApp().Message;
        }

        /// <summary>
        /// Open the desktop app.
        /// </summary>
        /// <returns>Outcome message.</returns>
        public static string OpenDesktopApp()
        {
            return XenoAppLauncher.LaunchDesktop().Message;
        }

        /// <summary>
        /// Describe a start result.
        /// </summary>
        /// <param name="result">Result.</param>
        /// <returns>Message.</returns>
        /// <exception cref="ArgumentNullException">Result is null.</exception>
        public static string DescribeStart(ClientStartResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            switch (result.Outcome)
            {
                case ClientStartOutcomeEnum.AlreadyRunning:
                    return "The client is already connected.";
                case ClientStartOutcomeEnum.Started:
                    return "Connected to the Xeno network (process " + result.ProcessId + ").";
                default:
                    string reason = String.IsNullOrWhiteSpace(result.State.LastDisconnectReason) ? "check the log for details" : result.State.LastDisconnectReason;
                    return "The client started but did not register with the Xeno server: " + reason + ".";
            }
        }
    }
}

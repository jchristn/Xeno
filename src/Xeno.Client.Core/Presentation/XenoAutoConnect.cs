namespace Xeno.Client.Presentation
{
    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Client.Hosting;
    using Xeno.Client.Services;

    /// <summary>
    /// Connects this computer to the Xeno network when a front end starts (the desktop app, the terminal app, and the
    /// tray agent), unless a worker is already running, nobody is signed in, or XENO_AUTOCONNECT=0. Starts are serialized
    /// across processes with a lock in the client home, so two front ends opening together start one worker. Thread safe.
    /// </summary>
    public static class XenoAutoConnect
    {
        #region Public-Members

        /// <summary>
        /// Environment variable that turns automatic connection off when set to 0.
        /// </summary>
        public const string DisableVariable = "XENO_AUTOCONNECT";

        /// <summary>
        /// Lock name that serializes worker starts across front ends.
        /// </summary>
        public const string LockName = "connect";

        /// <summary>
        /// Message shown when there is no credential to connect with.
        /// </summary>
        public const string NotSignedInMessage = "Sign in to connect this computer to the Xeno network.";

        /// <summary>
        /// Progress message while connecting.
        /// </summary>
        public const string ProgressMessage = "Connecting to the Xeno network...";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Decide what to do, from explicit inputs.
        /// </summary>
        /// <param name="settings">Client settings.</param>
        /// <param name="state">Connection state.</param>
        /// <param name="variable">Value of <see cref="DisableVariable"/>, or null.</param>
        /// <returns>Decision.</returns>
        /// <exception cref="ArgumentNullException">settings or state is null.</exception>
        public static XenoAutoConnectDecisionEnum Decide(ClientSettings settings, ClientConnectionState state, string? variable)
        {
            ArgumentNullException.ThrowIfNull(settings);
            ArgumentNullException.ThrowIfNull(state);
            if (String.Equals(variable?.Trim(), "0", StringComparison.Ordinal)) return XenoAutoConnectDecisionEnum.Disabled;
            if (!ServerEndpoints.HasWebSocketCredential(settings)) return XenoAutoConnectDecisionEnum.NotSignedIn;
            if (state.IsRecordedProcessRunning()) return XenoAutoConnectDecisionEnum.AlreadyRunning;
            return XenoAutoConnectDecisionEnum.Connect;
        }

        /// <summary>
        /// Decide what to do for the workspace's client home.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <returns>Decision.</returns>
        /// <exception cref="ArgumentNullException">workspace is null.</exception>
        public static XenoAutoConnectDecisionEnum Decide(XenoWorkspace workspace)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            try
            {
                return Decide(workspace.LoadSettings(), ClientConnectionState.Load(), Environment.GetEnvironmentVariable(DisableVariable));
            }
            catch (Exception ex) when (ex is System.IO.IOException || ex is System.IO.InvalidDataException || ex is UnauthorizedAccessException)
            {
                return XenoAutoConnectDecisionEnum.Disabled;
            }
        }

        /// <summary>
        /// Start the worker under the cross-process lock. When another front end is already starting it, waits for that
        /// start instead and reports how it went.
        /// </summary>
        /// <param name="workspace">Workspace.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>One-line outcome.</returns>
        /// <exception cref="ArgumentNullException">workspace is null.</exception>
        public static async Task<string> ConnectAsync(XenoWorkspace workspace, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(workspace);
            Stopwatch waited = Stopwatch.StartNew();
            SingleInstanceLock? gate = SingleInstanceLock.TryAcquire(LockName);
            while (gate == null)
            {
                if (waited.ElapsedMilliseconds > ClientWorkerController.DefaultStartTimeoutMs + 10000) return "Another Xeno app is still connecting this computer.";
                await Task.Delay(250, token).ConfigureAwait(false);
                gate = SingleInstanceLock.TryAcquire(LockName);
            }

            using (gate)
            {
                ClientConnectionState state = ClientConnectionState.Load();
                if (state.IsRecordedProcessRunning())
                {
                    return ClientWorkerController.IsHealthyWorkerRunning(workspace.LoadSettings(), state)
                        ? "Connected to the Xeno network."
                        : "The client is running and still connecting.";
                }

                return await XenoClientActions.StartAsync(workspace, token).ConfigureAwait(false);
            }
        }

        #endregion
    }
}

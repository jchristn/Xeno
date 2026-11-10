namespace Xeno.Client.Hosting
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;

    /// <summary>
    /// Launches the Xeno front ends from one another: the local web dashboard, the desktop app, the terminal app in a
    /// new terminal window, and the tray agent. Every launch is detached; failures are returned, never thrown.
    /// Thread-safe.
    /// </summary>
    public static class XenoAppLauncher
    {
        #region Public-Members

        /// <summary>
        /// Single-instance lock name used by the tray agent.
        /// </summary>
        public const string TrayLockName = "tray";

        /// <summary>
        /// Single-instance lock name used by the desktop app.
        /// </summary>
        public const string DesktopLockName = "desktop";

        /// <summary>
        /// Environment variable that disables starting the tray agent from the desktop app when set to 0.
        /// </summary>
        public const string AgentAutostartVariable = "XENO_AGENT_AUTOSTART";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Determine whether an instance holding a named single-instance lock is running.
        /// </summary>
        /// <param name="lockName">Lock name, for example <see cref="TrayLockName"/>.</param>
        /// <returns>True when another process holds the lock.</returns>
        public static bool IsRunning(string lockName)
        {
            using (SingleInstanceLock? probe = SingleInstanceLock.TryAcquire(lockName))
            {
                return probe == null;
            }
        }

        /// <summary>
        /// Start the tray agent unless it is already running or autostart is disabled with XENO_AGENT_AUTOSTART=0.
        /// </summary>
        /// <returns>Launch result; success when the agent was started or is already running.</returns>
        public static LaunchResult EnsureTrayRunning()
        {
            if (String.Equals(Environment.GetEnvironmentVariable(AgentAutostartVariable), "0", StringComparison.Ordinal))
            {
                return LaunchResult.Ok("Tray agent autostart is disabled.");
            }

            if (IsRunning(TrayLockName)) return LaunchResult.Ok(XenoProduct.TrayName + " is already running.");
            return LaunchTray();
        }

        /// <summary>
        /// Open the local web dashboard served by the running client worker in the default browser.
        /// </summary>
        /// <returns>Launch result. Fails when the dashboard is disabled or not listening.</returns>
        public static LaunchResult OpenLocalDashboard()
        {
            ClientSettings settings = ClientSettings.Load();
            ClientConnectionState state = ClientConnectionState.Load();
            if (!settings.LocalDashboard.Enabled && !state.LocalDashboardEnabled)
            {
                return LaunchResult.Fail("The local dashboard is disabled. Enable it in Settings, then restart the client.");
            }

            if (!state.LocalDashboardListening || String.IsNullOrWhiteSpace(state.LocalDashboardUrl))
            {
                string reason = String.IsNullOrWhiteSpace(state.LocalDashboardLastError) ? String.Empty : " Last error: " + state.LocalDashboardLastError;
                return LaunchResult.Fail("The local dashboard is not running. Start the client first." + reason);
            }

            return OpenUrl(state.LocalDashboardUrl);
        }

        /// <summary>
        /// Open a URL, file, or folder with the platform's default handler.
        /// </summary>
        /// <param name="target">URL or path.</param>
        /// <returns>Launch result.</returns>
        public static LaunchResult OpenUrl(string target)
        {
            if (String.IsNullOrWhiteSpace(target)) return LaunchResult.Fail("Nothing to open.");
            return Run(TerminalLaunchPlanner.PlanOpen(TerminalLaunchPlanner.CurrentPlatform(), target), "Opened " + target + ".");
        }

        /// <summary>
        /// Reveal a file or folder in the platform file manager.
        /// </summary>
        /// <param name="path">File or folder.</param>
        /// <returns>Launch result.</returns>
        public static LaunchResult Reveal(string path)
        {
            if (String.IsNullOrWhiteSpace(path)) return LaunchResult.Fail("Nothing to reveal.");
            if (!File.Exists(path) && !Directory.Exists(path)) return LaunchResult.Fail("Not found: " + path);
            return Run(TerminalLaunchPlanner.PlanReveal(TerminalLaunchPlanner.CurrentPlatform(), path), "Revealed " + path + ".");
        }

        /// <summary>
        /// Launch the desktop application.
        /// </summary>
        /// <returns>Launch result.</returns>
        public static LaunchResult LaunchDesktop()
        {
            return LaunchApplication(XenoApplicationEnum.Desktop, XenoProduct.DesktopName);
        }

        /// <summary>
        /// Launch the desktop app on a page.
        /// </summary>
        /// <param name="page">Page to open.</param>
        /// <returns>Launch result.</returns>
        public static LaunchResult LaunchDesktop(Presentation.XenoPageEnum page)
        {
            return LaunchApplication(XenoApplicationEnum.Desktop, XenoProduct.DesktopName, new List<string> { "--page", page.ToString().ToLowerInvariant() });
        }

        /// <summary>
        /// Launch the tray agent.
        /// </summary>
        /// <returns>Launch result.</returns>
        public static LaunchResult LaunchTray()
        {
            return LaunchApplication(XenoApplicationEnum.Tray, XenoProduct.TrayName);
        }

        /// <summary>
        /// Open a new terminal window running the terminal app (xeno tui). A non-default client home is passed on with
        /// --home because some terminals do not inherit this process's environment.
        /// </summary>
        /// <returns>Launch result.</returns>
        public static LaunchResult LaunchTerminalApp()
        {
            XenoExecutableLocation? cli = XenoExecutableLocator.Locate(XenoApplicationEnum.Cli);
            if (cli == null)
            {
                return LaunchResult.Fail("Could not find the xeno command. Install it with 'dotnet tool install -g xeno' or set " + XenoExecutableLocator.CliEnvironmentVariable + ".");
            }

            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            List<string> arguments = new List<string> { "tui" };
            arguments.AddRange(XenoLaunchOptions.ForwardHomeArguments());
            List<LaunchCommand> plan = TerminalLaunchPlanner.PlanTerminal(
                TerminalLaunchPlanner.CurrentPlatform(),
                cli,
                arguments,
                String.IsNullOrWhiteSpace(home) ? Environment.CurrentDirectory : home,
                XenoProduct.TerminalName,
                Path.GetTempPath());

            string lastError = "No terminal emulator was found.";
            foreach (LaunchCommand command in plan)
            {
                LaunchResult result = Run(command, "Opened " + XenoProduct.TerminalName + " in a new terminal window.");
                if (result.Success) return result;
                lastError = result.Message;
            }

            return LaunchResult.Fail(lastError);
        }

        /// <summary>
        /// Run a planned launch command, writing its script first when one is required.
        /// </summary>
        /// <param name="command">Command.</param>
        /// <param name="successMessage">Message returned on success.</param>
        /// <returns>Launch result.</returns>
        /// <exception cref="ArgumentNullException">Command is null.</exception>
        public static LaunchResult Run(LaunchCommand command, string successMessage)
        {
            ArgumentNullException.ThrowIfNull(command);
            try
            {
                if (!String.IsNullOrWhiteSpace(command.ScriptPath) && command.ScriptContent != null)
                {
                    File.WriteAllText(command.ScriptPath, command.ScriptContent);
                    if (!OperatingSystem.IsWindows())
                    {
                        File.SetUnixFileMode(command.ScriptPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                    }
                }

                using (Process? process = Process.Start(command.ToStartInfo()))
                {
                    if (process == null && !command.UseShellExecute) return LaunchResult.Fail("Unable to start " + command.FileName + ".");
                }

                return LaunchResult.Ok(successMessage);
            }
            catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException || ex is IOException || ex is UnauthorizedAccessException || ex is PlatformNotSupportedException)
            {
                return LaunchResult.Fail("Unable to start " + command.FileName + ": " + ex.Message);
            }
        }

        #endregion

        #region Private-Methods

        private static LaunchResult LaunchApplication(XenoApplicationEnum application, string displayName, IReadOnlyList<string>? extraArguments = null)
        {
            XenoExecutableLocation? location = XenoExecutableLocator.Locate(application);
            if (location == null)
            {
                return LaunchResult.Fail("Could not find " + displayName + " (" + XenoExecutableLocator.BaseName(application) + "). Set " + XenoExecutableLocator.EnvironmentVariable(application) + " to its location.");
            }

            List<string> arguments = new List<string>(XenoLaunchOptions.ForwardHomeArguments());
            if (extraArguments != null) arguments.AddRange(extraArguments);
            ProcessStartInfo startInfo = location.CreateStartInfo(arguments);
            startInfo.UseShellExecute = false;
            startInfo.CreateNoWindow = true;
            string? directory = Path.GetDirectoryName(location.LeadingArguments.Count > 0 ? location.LeadingArguments[0] : location.Executable);
            if (!String.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)) startInfo.WorkingDirectory = directory;
            try
            {
                using (Process? process = Process.Start(startInfo))
                {
                    if (process == null) return LaunchResult.Fail("Unable to start " + displayName + ".");
                }

                return LaunchResult.Ok("Started " + displayName + ".");
            }
            catch (Exception ex) when (ex is Win32Exception || ex is InvalidOperationException || ex is IOException)
            {
                return LaunchResult.Fail("Unable to start " + displayName + ": " + ex.Message);
            }
        }

        #endregion
    }
}

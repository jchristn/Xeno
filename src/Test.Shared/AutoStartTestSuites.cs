namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client;
    using Xeno.Client.Hosting;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;

    /// <summary>
    /// Starting a front end: when the tray agent comes up with it, and when it connects this computer on its own.
    /// </summary>
    public static class AutoStartTestSuites
    {
        /// <summary>
        /// Auto start suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor AutoStartSuite()
        {
            const string suite = "AutoStart";
            return new TestSuiteDescriptor(suite, "Tray and connection on start", new List<TestCaseDescriptor>
            {
                new TestCaseDescriptor(suite, "TrayStartsOnlyWithADesktop", "The tray agent starts with a desktop session and stays off over SSH, under WSL, and without a display", executeAsync: delegate
                {
                    Check(XenoAppLauncher.ShouldAutostartTray(Env(), false, false, true, false), "macOS with a desktop should start the tray.");
                    Check(!XenoAppLauncher.ShouldAutostartTray(Env("SSH_CONNECTION", "10.0.0.2 5000 10.0.0.1 22"), false, false, true, false), "An SSH session should not start the tray.");
                    Check(XenoAppLauncher.ShouldAutostartTray(Env("DISPLAY", ":0"), false, true, true, false) && XenoAppLauncher.ShouldAutostartTray(Env("WAYLAND_DISPLAY", "wayland-0"), false, true, true, false), "Linux with a display should start the tray.");
                    Check(!XenoAppLauncher.ShouldAutostartTray(Env(), false, true, true, false), "Headless Linux should not start the tray.");
                    Check(!XenoAppLauncher.ShouldAutostartTray(Env("DISPLAY", ":0"), false, true, true, true), "WSL should not start the tray.");
                    Check(XenoAppLauncher.ShouldAutostartTray(Env(), true, false, true, false) && !XenoAppLauncher.ShouldAutostartTray(Env(), true, false, false, false), "Windows should follow the interactive session.");
                    Check(!XenoAppLauncher.ShouldAutostartTray(Env(XenoAppLauncher.AgentAutostartVariable, "0"), false, false, true, false), "XENO_AGENT_AUTOSTART=0 should win.");
                    Check(XenoAppLauncher.ShouldAutostartTray(Env(XenoAppLauncher.AgentAutostartVariable, "1"), false, true, true, true), "XENO_AGENT_AUTOSTART=1 should win.");
                    Check(XenoAppLauncher.EnsureTrayRunning().Success, "With autostart off for tests, EnsureTrayRunning should succeed without launching.");
                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "AutoConnectDecides", "A front end connects only when signed in, no worker is running, and it is not turned off", executeAsync: delegate
                {
                    ClientSettings signedIn = new ClientSettings();
                    signedIn.Server.AccessKey = "xak_test_key_123456";
                    ClientSettings signedOut = new ClientSettings();
                    signedOut.Server.AccessKey = null;
                    signedOut.Server.Token = null;
                    ClientConnectionState stopped = new ClientConnectionState { ProcessId = Int32.MaxValue };
                    ClientConnectionState running = new ClientConnectionState { ProcessId = Environment.ProcessId };
                    Check(XenoAutoConnect.Decide(signedIn, stopped, null) == XenoAutoConnectDecisionEnum.Connect, "A signed-in client with no worker should connect.");
                    Check(XenoAutoConnect.Decide(signedIn, running, null) == XenoAutoConnectDecisionEnum.AlreadyRunning, "A running worker, even one still connecting, should be left alone.");
                    Check(XenoAutoConnect.Decide(signedOut, stopped, null) == XenoAutoConnectDecisionEnum.NotSignedIn, "With no credential the user should be asked to sign in.");
                    Check(XenoAutoConnect.Decide(signedIn, stopped, " 0 ") == XenoAutoConnectDecisionEnum.Disabled, "XENO_AUTOCONNECT=0 should turn it off.");
                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "ConnectWaitsForAnotherStart", "Connecting waits while another front end holds the start lock, and leaves a running worker alone", executeAsync: async delegate
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (XenoWorkspace workspace = new XenoWorkspace(new XenoServerClient(new StubHttpHandler()), () => null))
                    {
                        ClientSettings settings = ClientSettings.Load();
                        settings.Server.AccessKey = "xak_test_key_123456";
                        settings.Save();
                        new ClientConnectionState { ProcessId = Environment.ProcessId }.Save();
                        using (SingleInstanceLock? held = SingleInstanceLock.TryAcquire(XenoAutoConnect.LockName))
                        {
                            Check(held != null, "The test could not take the start lock.");
                            using (CancellationTokenSource cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(700)))
                            {
                                bool waited = false;
                                try
                                {
                                    await XenoAutoConnect.ConnectAsync(workspace, cancel.Token).ConfigureAwait(false);
                                }
                                catch (OperationCanceledException)
                                {
                                    waited = true;
                                }

                                Check(waited, "Connect did not wait for the lock.");
                            }
                        }

                        string message = await XenoAutoConnect.ConnectAsync(workspace, CancellationToken.None).ConfigureAwait(false);
                        Check(message == "The client is running and still connecting.", "A running worker should not be started again: " + message);
                    }
                }),
                new TestCaseDescriptor(suite, "QuitReachesOtherApps", "A quit request from another process reaches apps started before it, not later ones or the asker", executeAsync: delegate
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        DateTime before = DateTime.UtcNow.AddSeconds(-5);
                        Check(!XenoQuitBroadcast.IsRequestedSince(before), "No request should mean no quit.");
                        XenoQuitBroadcast.Post();
                        Check(!XenoQuitBroadcast.IsRequestedSince(before), "The process that asked should not quit itself through the request.");
                        WriteQuitRequest(DateTime.UtcNow);
                        Check(XenoQuitBroadcast.IsRequestedSince(before), "An app started before the request should quit.");
                        Check(!XenoQuitBroadcast.IsRequestedSince(DateTime.UtcNow.AddSeconds(5)), "An app started after the request should keep running.");
                        System.IO.File.WriteAllText(XenoQuitBroadcast.FilePath, "not json");
                        Check(!XenoQuitBroadcast.IsRequestedSince(before), "A damaged request should be ignored.");
                    }

                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "TerminalAppQuitsOnRequest", "The terminal app quits when the Xeno Agent asks every app to quit", executeAsync: delegate
                {
                    using (TuiHarness tui = new TuiHarness(120, 36, XenoPageEnum.Overview))
                    {
                        Check(!tui.Tui.QuitIfRequested(), "The terminal app should not quit without a request.");
                        WriteQuitRequest(DateTime.UtcNow.AddSeconds(1));
                        Check(tui.Tui.QuitIfRequested(), "The terminal app did not take the quit request.");
                    }

                    return Task.CompletedTask;
                })
            });
        }

        private static void WriteQuitRequest(DateTime requestedUtc)
        {
            System.IO.File.WriteAllText(XenoQuitBroadcast.FilePath, Xeno.Core.Serialization.JsonUtil.Serialize(new XenoQuitRequest { RequestedUtc = requestedUtc, ProcessId = Environment.ProcessId + 100000 }, true));
        }

        private static Func<string, string?> Env(params string[] pairs)
        {
            Dictionary<string, string> values = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int i = 0; i + 1 < pairs.Length; i += 2) values[pairs[i]] = pairs[i + 1];
            return name => values.TryGetValue(name, out string? value) ? value : null;
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}

namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
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
                }),
                new TestCaseDescriptor(suite, "AppleGpuFromIoreg", "Apple silicon GPU utilization and unified memory use are read from ioreg output", executeAsync: delegate
                {
                    string output = "+-o AGXAcceleratorG17X  <class AGXAcceleratorG17X, id 0x1000>\n    {\n      \"PerformanceStatistics\" = {\"In use system memory (driver)\"=0,\"Alloc system memory\"=6390398976,\"Renderer Utilization %\"=22,\"Device Utilization %\"=37,\"In use system memory\"=1812758528}\n      \"model\" = \"Apple M5 Max\"\n      \"gpu-core-count\" = 40\n    }\n";
                    List<Xeno.Client.Telemetry.Models.ClientGpuDeviceTelemetry> devices = Xeno.Client.Telemetry.Providers.ClientGpuTelemetryProvider.ParseIoreg(output, 128L * 1024 * 1024 * 1024);
                    Check(devices.Count == 1, "Expected one Apple GPU, got " + devices.Count + ".");
                    Check(devices[0].Name == "Apple M5 Max (40-core GPU)" && devices[0].UtilizationPercent == 37 && devices[0].MemoryUsedBytes == 1812758528, "Parsed values are wrong: " + devices[0].Name + " " + devices[0].UtilizationPercent);
                    Check(devices[0].MemoryUtilizationPercent > 1.3 && devices[0].MemoryUtilizationPercent < 1.4, "Memory use is wrong: " + devices[0].MemoryUtilizationPercent);
                    Check(Xeno.Client.Telemetry.Providers.ClientGpuTelemetryProvider.ParseIoreg("+-o IOAccelerator  <class IOAccelerator>\n  {\n  }\n", null).Count == 0 && Xeno.Client.Telemetry.Providers.ClientGpuTelemetryProvider.ParseIoreg(null, null).Count == 0, "Output without statistics should give no devices.");
                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "GpuProvidersMatchTheWireFormat", "Every client GPU source converts to the shared telemetry format the server reads", executeAsync: delegate
                {
                    List<string> client = Enum.GetNames<Xeno.Client.Telemetry.Models.ClientTelemetryGpuProviderEnum>().ToList();
                    List<string> shared = Enum.GetNames<Xeno.Core.Models.Telemetry.ClientTelemetryGpuProviderEnum>().ToList();
                    Check(client.SequenceEqual(shared), "Client and shared GPU sources differ: " + String.Join(",", client) + " vs " + String.Join(",", shared));
                    Xeno.Client.Telemetry.Models.ClientTelemetrySnapshot local = new Xeno.Client.Telemetry.Models.ClientTelemetrySnapshot();
                    local.Gpu.Provider = Xeno.Client.Telemetry.Models.ClientTelemetryGpuProviderEnum.AppleIoreg;
                    Xeno.Core.Models.Telemetry.ClientTelemetrySnapshot? wire = Xeno.Core.Serialization.JsonUtil.Deserialize<Xeno.Core.Models.Telemetry.ClientTelemetrySnapshot>(Xeno.Core.Serialization.JsonUtil.Serialize(local));
                    Check(wire != null && wire.Gpu.Provider == Xeno.Core.Models.Telemetry.ClientTelemetryGpuProviderEnum.AppleIoreg, "An Apple GPU snapshot did not convert to the wire format.");
                    return Task.CompletedTask;
                }),
                new TestCaseDescriptor(suite, "AppsStartWithoutTheCallersConsole", "Apps started by another Xeno app get their own streams instead of the caller's terminal", executeAsync: async delegate
                {
                    System.Diagnostics.ProcessStartInfo original = new System.Diagnostics.ProcessStartInfo { FileName = "/usr/bin/true" };
                    original.ArgumentList.Add("--page");
                    original.ArgumentList.Add("chat page");
                    System.Diagnostics.ProcessStartInfo detached = XenoAppLauncher.Detached(original);
                    if (OperatingSystem.IsWindows())
                    {
                        Check(Object.ReferenceEquals(detached, original), "Windows should launch the app directly.");
                        return;
                    }

                    Check(detached.FileName == "/bin/sh" && detached.ArgumentList[0] == "-c" && detached.ArgumentList[1].Contains("/dev/null", StringComparison.Ordinal), "The app should start through /bin/sh with its streams on /dev/null.");
                    Check(detached.ArgumentList.Skip(2).SequenceEqual(new[] { "/usr/bin/true", "--page", "chat page" }), "The app and its arguments were not passed through: " + String.Join(" | ", detached.ArgumentList));
                    detached.RedirectStandardOutput = true;
                    using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(detached) ?? throw new InvalidOperationException("Could not start the detached command."))
                    {
                        string output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
                        await process.WaitForExitAsync().ConfigureAwait(false);
                        Check(process.ExitCode == 0 && output.Length == 0, "The detached command failed or wrote to the caller: exit " + process.ExitCode);
                    }
                }),
                new TestCaseDescriptor(suite, "ConnectHelpExplainsQuietTerminal", "xeno connect help explains what the terminal shows and offers --verbose", executeAsync: async delegate
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    {
                        LocalUsageTestSuites.CliResult help = await LocalUsageTestSuites.RunCliAsync(home.Directory, "connect", "--help").ConfigureAwait(false);
                        Check(help.Output.Contains("--verbose", StringComparison.Ordinal) && help.Output.Contains("everything else goes to the log", StringComparison.Ordinal), "Connect help does not explain the terminal output.");
                    }
                }),
                new TestCaseDescriptor(suite, "CliDeletesSeveralChats", "xeno chat delete removes several conversations at once, reports missing ones, and --all needs --yes", executeAsync: async delegate
                {
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (ChatApiFixture fixture = new ChatApiFixture("Answer"))
                    using (Xeno.Client.Chat.XenoChatClient chat = new Xeno.Client.Chat.XenoChatClient())
                    {
                        List<string> ids = new List<string>();
                        for (int i = 0; i < 3; i++)
                        {
                            Xeno.Core.Chat.ChatConversation created = await chat.CreateConversationAsync(new Xeno.Core.Chat.ChatConversationRequest { Model = "llama3.1:8b", Title = "chat " + i }, CancellationToken.None).ConfigureAwait(false);
                            ids.Add(created.Id);
                            Check(created.SystemPrompt == Xeno.Core.Chat.ChatDefaults.SystemPrompt, "A new chat should start with the default system prompt.");
                        }

                        LocalUsageTestSuites.CliResult two = await LocalUsageTestSuites.RunCliAsync(home.Directory, "chat", "delete", ids[0], ids[1]).ConfigureAwait(false);
                        Check(two.ExitCode == 0 && two.Output.Contains("Deleted 2 conversations", StringComparison.Ordinal), "Deleting two chats failed: " + two.Output + two.Error);
                        LocalUsageTestSuites.CliResult missing = await LocalUsageTestSuites.RunCliAsync(home.Directory, "chat", "delete", ids[2], "cnv_missing").ConfigureAwait(false);
                        Check(missing.ExitCode != 0 && missing.Error.Contains("cnv_missing", StringComparison.Ordinal), "A missing chat should be reported: " + missing.Output + missing.Error);
                        LocalUsageTestSuites.CliResult unconfirmed = await LocalUsageTestSuites.RunCliAsync(home.Directory, "chat", "delete", "--all").ConfigureAwait(false);
                        Check(unconfirmed.ExitCode != 0 && unconfirmed.Error.Contains("--yes", StringComparison.Ordinal), "--all without --yes should refuse.");
                        Check((await chat.ListConversationsAsync(null, 100, CancellationToken.None).ConfigureAwait(false)).Count == 0, "Every created chat should be gone.");
                    }
                }),
                new TestCaseDescriptor(suite, "LogTimeWindow", "The log reader keeps lines written in a time window, with untimed lines following the line before them", executeAsync: async delegate
                {
                    string file = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "xeno-log-window-" + Guid.NewGuid().ToString("N") + ".log");
                    try
                    {
                        System.IO.File.WriteAllLines(file, new[]
                        {
                            "2026-11-10 08:00:00.000000Z [INFO] old line",
                            "   at old stack frame",
                            "2026-11-11 08:00:00.000000Z [ERROR] new line",
                            "   at new stack frame",
                            "2026-11-11 09:00:00.000000Z [INFO] newest line"
                        });
                        List<string> recent = await ClientLogReader.ReadTailAsync(file, 100, "all", new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc), CancellationToken.None).ConfigureAwait(false);
                        Check(recent.Count == 3 && recent[0].Contains("new line", StringComparison.Ordinal) && recent[1].Contains("new stack", StringComparison.Ordinal), "Window filter is wrong: " + String.Join(" | ", recent));
                        List<string> all = await ClientLogReader.ReadTailAsync(file, 100, "all", null, CancellationToken.None).ConfigureAwait(false);
                        Check(all.Count == 5, "Without a window every line should be read.");
                        Check(ClientLogReader.Timestamp("2026-11-11 16:55:01.690684Z [DEBUG] x") == new DateTime(2026, 11, 11, 16, 55, 1, 690, DateTimeKind.Utc).AddTicks(6840) && ClientLogReader.Timestamp("   at frame") == null, "Timestamps are not read correctly.");
                    }
                    finally
                    {
                        System.IO.File.Delete(file);
                    }
                }),
                new TestCaseDescriptor(suite, "ProviderModelsIncludeDiscovered", "A provider's model list shows configured models and models the provider reported", executeAsync: delegate
                {
                    ClientRunnerSettings provider = new ClientRunnerSettings { Name = "local-ollama" };
                    provider.Models.Clear();
                    provider.Models.Add(new ClientModelSettings { Name = "llama3.2:latest", SupportsCompletions = true });
                    List<ProviderModelRow> rows = ProviderModelRow.ForProvider(provider, new[] { "gemma4:31b", "llama3.2", "qwen3.8:latest" });
                    Check(rows.Count == 3, "Expected the configured model plus two found ones, got " + rows.Count + ": " + String.Join(", ", rows.Select(item => item.Name)));
                    Check(rows[0].Configured && rows[0].Source == "Configured, found on provider", "The configured model should be first and marked as found.");
                    Check(rows.Skip(1).All(item => !item.Configured && item.Source == "Found on provider"), "Found models should be marked as not configured.");
                    using (ClientHomeScope home = new ClientHomeScope())
                    using (XenoWorkspace workspace = new XenoWorkspace(new XenoServerClient(new StubHttpHandler()), () => null))
                    {
                        ClientSettings settings = ClientSettings.Load();
                        settings.Runners.Clear();
                        settings.Runners.Add(provider);
                        settings.Save();
                        XenoFormSpec? form = XenoForms.EditModel(workspace, "local-ollama", "gemma4:31b");
                        Check(form != null && form.Fields.First(item => item.Key == "name").Value == "gemma4:31b", "Editing a found model should offer a form to configure it.");
                        Check(XenoForms.EditModel(workspace, "missing", "x") == null, "A missing provider should still give no form.");
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

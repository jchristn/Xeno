namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client;
    using Xeno.Client.Presentation;
    using Xeno.Tui;
    using Xeno.Tui.Modals;
    using Xeno.Tui.Pages;

    /// <summary>
    /// Headless tests for the Xeno terminal app.
    /// </summary>
    public static class TuiTestSuites
    {
        #region Public-Methods

        /// <summary>
        /// Terminal app suite.
        /// </summary>
        /// <returns>Suite descriptor.</returns>
        public static TestSuiteDescriptor TuiSuite()
        {
            const string suite = "Tui";
            return new TestSuiteDescriptor(suite, "Terminal App", new List<TestCaseDescriptor>
            {
                Case(suite, "ShellShowsHeaderMenuSidebarAndHints", "The shell shows the header, menu bar, sidebar, and key hints", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 36, XenoPageEnum.Overview))
                    {
                        string screen = tui.Screen();
                        foreach (string expected in new[] { "Xeno", "File", "View", "Client", "Help", "Overview", "Models", "Providers", "Requests", "Logs", "TELEMETRY", "Host", "Provider", "General", "Settings", "F10", "Status", "Not signed in", "Xeno cloud" })
                        {
                            Contains(screen, expected);
                        }
                    }
                }),
                Case(suite, "EveryPageRendersAtCommonSizes", "Every page renders at the minimum size and at 140x45", () =>
                {
                    foreach (int[] size in new[] { new[] { XenoTuiApp.MinimumWidth, XenoTuiApp.MinimumHeight }, new[] { 140, 45 } })
                    {
                        using (TuiHarness tui = new TuiHarness(size[0], size[1], XenoPageEnum.Overview))
                        {
                            foreach (XenoPageDefinition page in XenoNavigationCatalog.Pages)
                            {
                                tui.Tui.Navigate(page.Page);
                                tui.Settle();
                                string screen = tui.Screen();
                                Check(tui.Tui.CurrentPage == page.Page, "Navigation to " + page.Title + " failed.");
                                Check(screen.Trim().Length > 0, page.Title + " rendered nothing.");
                                string? snapshots = Environment.GetEnvironmentVariable("XENO_TUI_SNAPSHOT_DIR");
                                if (!String.IsNullOrWhiteSpace(snapshots))
                                {
                                    System.IO.Directory.CreateDirectory(snapshots);
                                    System.IO.File.WriteAllText(System.IO.Path.Combine(snapshots, page.Page + "-" + size[0] + "x" + size[1] + ".txt"), screen);
                                }
                            }
                        }
                    }
                }),
                Case(suite, "SmallTerminalShowsTooSmall", "Below the minimum size the terminal app shows TUIKit's terminal-too-small screen", () =>
                {
                    Check(XenoTuiApp.MinimumWidth >= 120 && XenoTuiApp.MinimumHeight >= 36, "The minimum size should expect a large terminal.");
                    using (TuiHarness tui = new TuiHarness(100, 30, XenoPageEnum.Overview))
                    {
                        string screen = tui.Screen();
                        Contains(screen, "Terminal too small");
                        Contains(screen, XenoTuiApp.MinimumWidth + "x" + XenoTuiApp.MinimumHeight);
                    }

                    using (TuiHarness tui = new TuiHarness(XenoTuiApp.MinimumWidth, XenoTuiApp.MinimumHeight, XenoPageEnum.Overview))
                    {
                        Check(!tui.Screen().Contains("Terminal too small", StringComparison.Ordinal), "The minimum size itself must render the app.");
                    }
                }),
                Case(suite, "PanesKeepOneColumnOfPadding", "Text, tables, charts, and buttons keep one empty column inside every pane border", () =>
                {
                    using (TuiHarness tui = new TuiHarness(140, 45, XenoPageEnum.Overview))
                    {
                        foreach (XenoPageDefinition page in XenoNavigationCatalog.Pages)
                        {
                            tui.Tui.Navigate(page.Page);
                            tui.Settle();
                            string[] rows = tui.Screen().Split('\n');
                            for (int y = 2; y < rows.Length - 2; y++)
                            {
                                string row = rows[y].TrimEnd('\r');
                                for (int x = 24; x < row.Length; x++)
                                {
                                    if (row[x] != '│' && row[x] != '┃') continue;
                                    if (x + 1 < row.Length && !BoxGlyphs.Contains(row[x + 1]) && row[x + 1] != ' ')
                                    {
                                        throw new InvalidOperationException(page.Title + ": content touches a left border at row " + y + ", column " + x + ":" + Environment.NewLine + row);
                                    }

                                    if (x > 24 && !BoxGlyphs.Contains(row[x - 1]) && row[x - 1] != ' ')
                                    {
                                        throw new InvalidOperationException(page.Title + ": content touches a right border at row " + y + ", column " + x + ":" + Environment.NewLine + row);
                                    }
                                }
                            }
                        }
                    }
                }),
                Case(suite, "ChartsExplainThemselves", "Charts show a heading with the range, and say why they are empty instead of drawing a bare 0", () =>
                {
                    using (TuiHarness tui = new TuiHarness(140, 45, XenoPageEnum.Overview))
                    {
                        string screen = tui.Screen();
                        Contains(screen, "Requests, last hour");
                        Contains(screen, "No requests in the last hour.");
                        Contains(screen, "Succeeded");
                        tui.Tui.Navigate(XenoPageEnum.HostTelemetry);
                        tui.Settle();
                        screen = tui.Screen();
                        foreach (string heading in new[] { "CPU, last hour", "Memory, last hour", "GPU, last hour" }) Contains(screen, heading);
                        tui.Tui.Navigate(XenoPageEnum.Requests);
                        tui.Settle();
                        Contains(tui.Screen(), "Requests, last hour");
                        tui.Tui.Navigate(XenoPageEnum.ProviderTelemetry);
                        tui.Settle();
                        Contains(tui.Screen(), "Active requests, last hour");
                    }
                }),
                Case(suite, "MessageLineReportsWhatHappened", "Commands report their outcome on the message line with a time and a marker", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 36, XenoPageEnum.Overview))
                    {
                        tui.Tui.Execute(XenoCommandEnum.ReloadConfiguration);
                        tui.Settle();
                        string message = tui.Tui.Context.Message;
                        Check(message.Length > 0, "Reload did not report anything.");
                        string[] rows = tui.Screen().Split('\n');
                        string line = rows[rows.Length - 2];
                        Check(line.Contains(message.Substring(0, Math.Min(30, message.Length)), StringComparison.Ordinal), "The message line does not show the outcome: " + line);
                        Check(line.Contains(DateTime.Now.ToString("HH:", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal) || line.Contains(DateTime.Now.AddMinutes(-1).ToString("HH:", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal), "The message line has no time: " + line);
                        tui.Tui.Context.Progress("Starting the client...");
                        Check(tui.Tui.Context.Working, "Progress should mark work in progress.");
                        tui.Tui.Context.Notify("Could not start the client: boom", TuiContext.SeverityOf("Could not start the client: boom"));
                        Check(!tui.Tui.Context.Working && tui.Tui.Context.MessageSeverity == TUIKit.Modals.NotificationSeverity.Error, "A failure should end the progress and report an error.");
                        Contains(tui.Screen(), "✗");
                    }
                }),
                Case(suite, "SignInConfirmsAndOffersToStart", "Signing in reports success and offers to start the client", () =>
                {
                    StubHttpHandler server = new StubHttpHandler().On("/v1.0/api/auth/login", System.Net.HttpStatusCode.OK, "{\"id\":\"cred_1\",\"tenantId\":\"ten_default\",\"accessKey\":\"xeno_key\"}");
                    using (TuiHarness tui = new TuiHarness(120, 36, XenoPageEnum.Server, server))
                    {
                        tui.Press("i");
                        Check(tui.App.Modals.IsActive, "i should open the sign-in form.");
                        Contains(tui.Screen(), "Ctrl+S sign in");
                        tui.Press("tab");
                        tui.Press("tab");
                        tui.Type("admin@xeno");
                        tui.Press("tab");
                        tui.Type("password");
                        tui.Press("ctrl+s");
                        WaitFor(() => tui.Screen().Contains("Signed In", StringComparison.Ordinal), tui, "No sign-in confirmation appeared.");
                        string screen = tui.Screen();
                        Contains(screen, "Start Client");
                        Contains(screen, "ten_default");
                        Check(tui.Tui.Context.Message.StartsWith("Signed in to https://app.xenocloud.ai:9000", StringComparison.Ordinal), "The message line should report the sign-in: " + tui.Tui.Context.Message);
                        Check(ClientSettings.Load().Server.AccessKey == "xeno_key", "The credential was not saved.");
                        tui.Press("escape");
                    }
                }),
                Case(suite, "OverviewFocusesActionsFirst", "Overview starts with focus on its action buttons", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 36, XenoPageEnum.Overview))
                    {
                        TUIKit.Widgets.Button? focused = tui.App.CurrentFocusPath.Leaf as TUIKit.Widgets.Button;
                        Check(focused != null, "Overview should focus an action button first, not " + tui.App.CurrentFocusPath.Leaf?.GetType().Name + ".");
                        tui.Press("alt+3");
                        tui.Press("alt+1");
                        Check(tui.App.CurrentFocusPath.Leaf is TUIKit.Widgets.Button, "Returning to Overview should focus an action button.");
                    }
                }),
                Case(suite, "AltDigitsNavigatePages","Alt+digit opens the matching page", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 36, XenoPageEnum.Overview))
                    {
                        tui.Press("alt+3");
                        Check(tui.Tui.CurrentPage == XenoPageEnum.Providers, "Alt+3 should open Providers.");
                        Contains(tui.Screen(), "local-ollama");
                        tui.Press("alt+9");
                        Check(tui.Tui.CurrentPage == XenoPageEnum.Settings, "Alt+9 should open Settings.");
                    }
                }),
                Case(suite, "F10OpensTheMenuBar", "F10 opens the menu bar and Escape closes it", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 36, XenoPageEnum.Overview))
                    {
                        tui.Press("f10");
                        Check(tui.Tui.MenuBar.IsOpen, "F10 should open the menu.");
                        Contains(tui.Screen(), "Add Provider");
                        tui.Press("escape");
                        Check(!tui.Tui.MenuBar.IsOpen, "Escape should close the menu.");
                    }
                }),
                Case(suite, "PaletteAndHelpOpen", "Ctrl+K opens the command palette and F1 opens key help", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 36, XenoPageEnum.Overview))
                    {
                        tui.Press("ctrl+k");
                        Check(tui.App.Modals.IsActive, "Ctrl+K should open the palette.");
                        Contains(tui.Screen(), "Xeno Commands");
                        tui.Press("escape");
                        Check(!tui.App.Modals.IsActive, "Escape should close the palette.");
                        tui.Press("f1");
                        Check(tui.App.Modals.IsActive, "F1 should open key help.");
                        Contains(tui.Screen(), "Keyboard Shortcuts");
                    }
                }),
                Case(suite, "MenuCommandsMatchSharedLayout", "Terminal commands come from the shared menu layout", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 36, XenoPageEnum.Overview))
                    {
                        List<XenoCommandEnum> shared = XenoMenuLayout.Commands(XenoMenuLayout.MenuBar(XenoSurfaceEnum.Terminal, false));
                        Check(tui.Tui.Commands.Count == shared.Count, "Command count differs from the shared layout.");
                        Check(tui.Tui.Commands.Any(item => item.Id == "xeno." + XenoCommandEnum.OpenDesktopApp), "Terminal should launch the desktop app.");
                        Check(tui.Tui.Commands.Single(item => item.Id == "xeno." + XenoCommandEnum.GoLogs).Chord.HasValue, "Pages should have Alt+digit chords.");
                    }
                }),
                Case(suite, "AddProviderThroughForm", "A provider can be added through the form dialog", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.Providers))
                    {
                        foreach (ClientRunnerSettings existing in ClientSettings.Load().Runners.ToList())
                        {
                            tui.Workspace.DeleteProviderAsync(existing.Name, System.Threading.CancellationToken.None).GetAwaiter().GetResult();
                        }

                        tui.Tui.Navigate(XenoPageEnum.Providers);
                        tui.Settle();
                        tui.Press("a");
                        Check(tui.App.Modals.IsActive, "a should open the Add Provider form.");
                        Contains(tui.Screen(), "Add Provider");
                        tui.Type("lab-vllm");
                        tui.Press("tab");
                        tui.Press("tab");
                        for (int i = 0; i < 30; i++) tui.Backend.FeedKey("backspace");
                        tui.Settle();
                        tui.Type("http://127.0.0.1:8000");
                        tui.Press("ctrl+s");
                        WaitFor(() => !tui.App.Modals.IsActive, tui, "The form should close after saving.");
                        ClientRunnerSettings? saved = ClientSettings.Load().Runners.FirstOrDefault(item => item.Name == "lab-vllm");
                        Check(saved != null && saved.Endpoint == "http://127.0.0.1:8000", "The provider was not saved: " + String.Join(", ", ClientSettings.Load().Runners.Select(item => item.Name + "=" + item.Endpoint)));
                        tui.Settle();
                        Contains(tui.Screen(), "lab-vllm");
                    }
                }),
                Case(suite, "FormRejectsMissingRequiredFields", "The form keeps open and explains a missing required field", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.Providers))
                    {
                        tui.Press("a");
                        Contains(tui.Screen(), "Base URL of the provider.");
                        tui.Press("ctrl+s");
                        Check(tui.App.Modals.IsActive, "The form should stay open.");
                        Contains(tui.Screen(), "! Name is required.");
                        tui.Press("escape");
                        Check(!tui.App.Modals.IsActive, "Escape should cancel the form.");
                    }
                }),
                Case(suite, "FormTakesPastesAndRevealsSecrets", "Form dialogs take pastes into the focused field and show a secret on Ctrl+R", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.Providers))
                    {
                        tui.Press("a");
                        tui.Backend.FeedInput("\u001b[200~pasted-provider\u001b[201~");
                        tui.Settle();
                        Contains(tui.Screen(), "pasted-provider");
                        tui.Press("tab");
                        tui.Press("tab");
                        tui.Press("tab");
                        tui.Type("s3cret");
                        Check(!tui.Screen().Contains("s3cret", StringComparison.Ordinal), "A secret should be masked.");
                        tui.Press("ctrl+r");
                        Contains(tui.Screen(), "s3cret");
                        tui.Press("escape");
                    }
                }),
                Case(suite, "SettingsShowsSectionsAndHelp", "The Settings page groups fields under section headings with their help text", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.Settings))
                    {
                        string screen = tui.Screen();
                        Contains(screen, "Web dashboard");
                        Contains(screen, "The browser dashboard for this machine");
                        Check(!screen.Contains("Web dashboard: ", StringComparison.Ordinal), "Labels should not repeat the section name.");
                    }
                }),
                Case(suite, "TablesLeaveLoadingState", "Tables say Loading... until their page's data arrives, then show rows or their empty message", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.Overview))
                    {
                        foreach (XenoPageEnum page in new[] { XenoPageEnum.Overview, XenoPageEnum.Providers, XenoPageEnum.Models, XenoPageEnum.Requests, XenoPageEnum.HostTelemetry, XenoPageEnum.ProviderTelemetry })
                        {
                            tui.Tui.Navigate(page);
                            WaitFor(() => !tui.Screen().Contains("Loading...", StringComparison.Ordinal), tui, page + " stayed in its loading state.");
                        }

                        Contains(tui.Screen(), "No samples in this range.");
                    }
                }),
                Case(suite, "PasteReachesFocusedSearch", "A paste outside a dialog goes to the focused search field", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.Models))
                    {
                        TUIKit.Point? search = tui.Find("type to filter by name");
                        Check(search != null, "The model filter was not shown.");
                        tui.Click(search!.Value.X, search.Value.Y);
                        tui.Backend.FeedInput("\u001b[200~llama\u001b[201~");
                        tui.Settle();
                        Contains(tui.Screen(), "llama");
                        Check(!tui.Screen().Contains("type to filter by name", StringComparison.Ordinal), "The paste did not reach the filter.");
                    }
                }),
                Case(suite, "SettingsPageSavesThroughSharedCatalog", "The Settings page saves through the shared settings catalog", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.Settings))
                    {
                        SettingsPage page = (SettingsPage)tui.Tui.CurrentView;
                        Check(page.Collect()["dashboard.port"] == "9343", "Settings were not loaded.");
                        page.SetValue("history.maxEntries", "77");
                        tui.Press("ctrl+s");
                        WaitFor(() => ClientSettings.Load().LocalDashboard.HistoryRetention.MaxEntries == 77, tui, "History setting was not saved.");
                        page.SetValue("inference.port", "notanumber");
                        tui.Press("ctrl+s");
                        Check(ClientSettings.Load().LocalInferenceServer.Port == 11434, "An invalid port must not be saved.");
                    }
                }),
                Case(suite, "TabLeavesTheSettingsForm", "Tab moves from the last setting to the Actions buttons and on to the sidebar instead of wrapping", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.Settings))
                    {
                        bool reachedActions = false;
                        bool reachedSidebar = false;
                        for (int i = 0; i < 60 && !reachedSidebar; i++)
                        {
                            tui.Press("tab");
                            string screen = tui.Screen();
                            if (screen.Contains("> Actions", StringComparison.Ordinal)) reachedActions = true;
                            if (reachedActions && !screen.Contains("> Settings (client.json)", StringComparison.Ordinal) && !screen.Contains("> Actions", StringComparison.Ordinal)) reachedSidebar = true;
                        }

                        Check(reachedActions, "Tab never reached the Actions buttons.");
                        Check(reachedSidebar, "Tab never left the Settings page.");
                    }
                }),
                Case(suite, "ViewersRenderMarkdown", "Detail viewers lay Markdown out with TUIKit: tables get borders, values stay verbatim, and the viewer scrolls", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 36, XenoPageEnum.Overview))
                    {
                        tui.Tui.Execute(XenoCommandEnum.About);
                        tui.Settle();
                        string screen = tui.Screen();
                        Check(screen.Contains("╭", StringComparison.Ordinal) && screen.Contains("Signed in", StringComparison.Ordinal), "About did not render its fields as a table.");
                        Check(!screen.Contains("| ---", StringComparison.Ordinal) && !screen.Contains("# Xeno", StringComparison.Ordinal), "About shows raw Markdown.");
                        tui.Press("esc");
                    }

                    string markdown = XenoMarkdown.Join(new List<string?>
                    {
                        XenoMarkdown.Details(new List<KeyValuePair<string, string>> { new KeyValuePair<string, string>("Model", "llama3 *latest*") }),
                        XenoMarkdown.Heading("Response body"),
                        XenoMarkdown.CodeBlock("{\"content\":\"**bold**\"}", "json")
                    });

                    MarkdownViewerModal viewer = new MarkdownViewerModal("Request", markdown);
                    string rows = String.Join("\n", viewer.RenderedRows(80));
                    Check(rows.Contains("llama3 *latest*", StringComparison.Ordinal), "Escaped value did not render literally: " + rows);
                    Check(rows.Contains("**bold**", StringComparison.Ordinal), "Code block did not render verbatim: " + rows);
                    Check(!rows.Contains("##", StringComparison.Ordinal) && !rows.Contains("```", StringComparison.Ordinal), "Markup leaked into the output: " + rows);
                    Check(viewer.RenderedRows(80).All(row => row.Length <= 80), "A row is wider than the viewer.");

                    using (TuiHarness tui = new TuiHarness(120, 36, XenoPageEnum.Overview))
                    {
                        List<string> lines = new List<string>();
                        for (int i = 1; i <= 80; i++) lines.Add("- line " + i.ToString(System.Globalization.CultureInfo.InvariantCulture));
                        _ = tui.Tui.Context.ShowMarkdownAsync("Long", String.Join("\n", lines));
                        tui.Settle();
                        Contains(tui.Screen(), "line 1");
                        tui.Press("end");
                        Contains(tui.Screen(), "line 80");
                        tui.Press("home");
                        Contains(tui.Screen(), "line 1");
                        tui.Press("escape");
                        Check(!tui.App.Modals.IsActive, "Escape should close the viewer.");
                    }
                }),
                Case(suite, "MouseOpensAndRunsMenus", "Clicking a menu title opens it, clicking an item runs it, and clicking elsewhere closes it", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.Overview))
                    {
                        tui.ClickText(" View", 1);
                        Check(tui.Tui.MenuBar.IsOpen, "Clicking View should open the menu.");
                        tui.ClickText("Providers", 2);
                        Check(tui.Tui.CurrentPage == XenoPageEnum.Providers, "Clicking View > Providers should open Providers.");
                        Check(!tui.Tui.MenuBar.IsOpen && !tui.App.Modals.IsActive, "The menu should close after running an item.");
                        tui.ClickText(" File", 1);
                        tui.ClickText(" Help", 1);
                        Check(tui.Tui.MenuBar.IsOpen, "Clicking another title should switch menus.");
                        tui.ClickText(" Help", 1);
                        Check(!tui.Tui.MenuBar.IsOpen && !tui.App.Modals.IsActive, "Clicking the open title should close the menu.");
                        tui.ClickText(" View", 1);
                        tui.Click(110, 30);
                        Check(!tui.Tui.MenuBar.IsOpen && !tui.App.Modals.IsActive, "Clicking outside should close the menu.");
                        Check(tui.Tui.CurrentPage == XenoPageEnum.Providers, "A click that closes the menu should not reach the page.");
                        tui.Press("f10");
                        Check(tui.Tui.MenuBar.IsOpen && tui.App.Modals.IsActive, "F10 should open the menu.");
                        tui.Press("escape");
                        Check(!tui.Tui.MenuBar.IsOpen && !tui.App.Modals.IsActive, "Escape should close the menu.");
                    }
                }),
                Case(suite, "MouseWorksOnPages", "Clicks navigate the sidebar, press buttons, open rows, toggle settings, and pick from page dropdowns", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.Overview))
                    {
                        tui.ClickText("Last hour");
                        Contains(tui.Screen(), "Last day");
                        tui.ClickText("Last day");
                        Contains(tui.Screen(), "Requests, last day");
                        tui.ClickText("Add Provider ]");
                        Check(tui.App.Modals.IsActive, "Clicking Add Provider should open the form.");
                        tui.Press("escape");
                        tui.ClickText("Providers", 2);
                        Check(tui.Tui.CurrentPage == XenoPageEnum.Providers, "Clicking the sidebar should navigate.");
                        TUIKit.Point row = tui.Find("local-ollama", 3) ?? throw new InvalidOperationException("No provider row.");
                        tui.DoubleClick(row.X, row.Y);
                        Check(tui.App.Modals.IsActive, "Double-clicking a provider should open it.");
                        tui.Press("escape");
                        tui.Tui.Navigate(XenoPageEnum.Requests);
                        tui.Settle();
                        tui.ClickText("All outcomes");
                        tui.ClickText("  Failed ");
                        Check(!tui.Screen().Contains("All outcomes", StringComparison.Ordinal), "Picking Failed should change the outcome filter.");
                        tui.Tui.Navigate(XenoPageEnum.Settings);
                        tui.Settle();
                        TUIKit.Point box = tui.Find("[x] Serve the local web dashboard") ?? throw new InvalidOperationException("No checkbox.");
                        tui.Click(box.X + 1, box.Y);
                        Contains(tui.Screen(), "[ ] Serve the local web dashboard");
                    }
                }),
                Case(suite, "MouseWorksInDialogs", "Clicks reach form fields, confirmation buttons, the command palette, and status bar hints", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 40, XenoPageEnum.Overview))
                    {
                        tui.Press("ctrl+n");
                        TUIKit.Point label = tui.Find("Endpoint") ?? throw new InvalidOperationException("No Endpoint field.");
                        tui.Click(label.X + 2, label.Y + 1);
                        tui.Press("ctrl+a");
                        tui.Type("QQ");
                        string[] rows = tui.Screen().Split('\n');
                        Check(rows[label.Y + 1].Contains("QQ", StringComparison.Ordinal), "Typing should land in the clicked field.");
                        TUIKit.Point apiType = tui.Find("API type") ?? throw new InvalidOperationException("No API type field.");
                        tui.Click(apiType.X + 2, apiType.Y + 1);
                        TUIKit.Point vllm = tui.Find("vLLM", apiType.Y + 2) ?? throw new InvalidOperationException("Clicking API type should open its list:" + Environment.NewLine + tui.Screen());
                        tui.Click(vllm.X, vllm.Y);
                        Check(tui.Screen().Split('\n')[apiType.Y + 1].Contains("vLLM", StringComparison.Ordinal), "Clicking vLLM should choose it.");
                        tui.Press("escape");
                        tui.Tui.Navigate(XenoPageEnum.Providers);
                        tui.Settle();
                        tui.Press("d");
                        TUIKit.Point cancel = tui.Find("[ Cancel ]") ?? throw new InvalidOperationException("No confirmation.");
                        tui.Click(cancel.X + 2, cancel.Y);
                        Check(!tui.App.Modals.IsActive, "Clicking Cancel should close the confirmation.");
                        Contains(tui.Screen(), "local-ollama");
                        tui.Press("ctrl+k");
                        tui.Type("Overview");
                        TUIKit.Point item = tui.Find("Overview  View") ?? throw new InvalidOperationException("No palette row.");
                        tui.Click(item.X, item.Y);
                        Check(tui.Tui.CurrentPage == XenoPageEnum.Overview, "Clicking a palette row should run it.");
                        tui.ClickText("F1 Help", 38);
                        Check(tui.App.Modals.IsActive, "Clicking the F1 hint should open key help.");
                        tui.Press("escape");
                        tui.ClickText("F10 Menu", 38);
                        Check(tui.Tui.MenuBar.IsOpen, "Clicking the F10 hint should open the menu.");
                    }
                }),
                Case(suite, "AppearancePersists", "Choosing an appearance updates the theme and preferences", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 36, XenoPageEnum.Overview))
                    {
                        tui.Tui.Execute(XenoCommandEnum.AppearanceLight);
                        tui.Settle();
                        Check(tui.App.Theme.Name == XenoTuiTheme.Light.Name, "Light theme was not applied.");
                        Check(tui.Tui.Context.Preferences.TerminalAppearance == XenoAppearanceEnum.Light, "Preference was not saved.");
                    }
                }),
                Case(suite, "ThemesUseTheLogoColors", "The terminal themes use the Xeno logo green and purple", () =>
                {
                    TUIKit.Color purple = XenoTuiTheme.ToColor(XenoBrand.Purple);
                    Check(XenoTuiTheme.Dark.Accent.Foreground == XenoTuiTheme.ToColor(XenoBrand.Green), "Dark accent is not the logo green.");
                    Check(XenoTuiTheme.Light.Accent.Foreground == XenoTuiTheme.ToColor(XenoBrand.GreenDeep), "Light accent is not the deep logo green.");
                    Check(XenoTuiTheme.Dark.Selection.Background == purple && XenoTuiTheme.Light.Selection.Background == purple, "Selections are not the logo purple.");
                    Check(XenoTuiTheme.ToColor("#42dc7d").R == 0x42 && XenoTuiTheme.ToColor("#42dc7d").G == 0xdc, "Hex conversion is wrong.");
                    using (TuiHarness tui = new TuiHarness(120, 36, XenoPageEnum.Overview))
                    {
                        Check(tui.App.Theme.Name == XenoTuiTheme.Dark.Name, "The Xeno dark theme is not the default.");
                    }
                }),
                Case(suite, "StartWithoutCredentialExplainsSignIn", "Starting without a credential explains how to sign in", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 36, XenoPageEnum.Overview))
                    {
                        Check(!tui.Tui.CanExecute(XenoCommandEnum.StartClient), "Start should be disabled before sign-in.");
                        tui.Tui.Execute(XenoCommandEnum.StartClient);
                        tui.Settle();
                        Check(tui.Tui.Context.Message.Contains("Sign in", StringComparison.Ordinal), "The status bar should explain sign-in.");
                    }
                })
            });
        }

        #endregion

        #region Private-Methods

        private static TestCaseDescriptor Case(string suite, string id, string name, Action body)
        {
            return new TestCaseDescriptor(suite, id, name, executeAsync: delegate
            {
                body();
                return Task.CompletedTask;
            });
        }

        private const string BoxGlyphs = "│┃─━┌┐└┘├┤┬┴┼┏┓┗┛┣┫┳┻╋╭╮╰╯┠┨┯┷┿╂┝┥┰┸";

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void Contains(string screen, string expected)
        {
            if (!screen.Contains(expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Expected '" + expected + "' on screen:" + Environment.NewLine + screen);
            }
        }

        private static void WaitFor(Func<bool> condition, TuiHarness tui, string message)
        {
            for (int i = 0; i < 100; i++)
            {
                if (condition()) return;
                tui.Settle(200);
            }

            throw new InvalidOperationException(message + Environment.NewLine + tui.Screen());
        }

        #endregion
    }
}

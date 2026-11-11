namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Touchstone.Core;
    using Xeno.Client.Chat;
    using Xeno.Client.Presentation;
    using Xeno.Core.Chat;
    using Xeno.Tui.Pages;
    using Xeno.Tui.Shell;

    /// <summary>
    /// The terminal Usage page and charts: the minimum size, every chart through the Chart picker, drill-down with the
    /// keyboard and the mouse, and the navigation entry.
    /// </summary>
    public static class UsageTuiTestSuites
    {
        /// <summary>
        /// Terminal usage suite.
        /// </summary>
        /// <returns>Suite.</returns>
        public static TestSuiteDescriptor TuiUsageSuite()
        {
            const string suite = "TuiUsage";
            return new TestSuiteDescriptor(suite, "Terminal usage page", new List<TestCaseDescriptor>
            {
                Case(suite, "UsageIsInTheCatalog", "Usage is an Activity page before Requests in every front end", () =>
                {
                    List<XenoPageDefinition> activity = XenoNavigationCatalog.Pages.Where(item => item.Section == "Activity").ToList();
                    Check(activity[0].Page == XenoPageEnum.Usage && activity[1].Page == XenoPageEnum.Requests, "Usage should come first in Activity.");
                    Check(XenoNavigationCatalog.PageFor(XenoCommandEnum.GoUsage) == XenoPageEnum.Usage, "GoUsage does not open Usage.");
                    Check(XenoMenuLayout.Commands(XenoMenuLayout.MenuBar(XenoSurfaceEnum.Terminal, false)).Contains(XenoCommandEnum.GoUsage), "The View menu has no Usage item.");
                }),
                Case(suite, "UsageSaysWhenTheClientIsNotRunning", "The terminal Usage page explains that usage needs the client", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 36, XenoPageEnum.Usage))
                    {
                        WaitFor(() => tui.Screen().Contains("Usage needs the Xeno client running", StringComparison.Ordinal), tui, "The not-running message is missing.");
                    }
                }),
                Case(suite, "UsageChartsEveryKindAndDrillsDown", "At 120 x 36 the Usage page draws every chart and opens Chat history for a bucket by keyboard and by mouse", () =>
                {
                    using (TuiHarness tui = new TuiHarness(120, 36, XenoPageEnum.Overview))
                    using (ChatApiFixture api = new ChatApiFixture("Usage answer"))
                    using (XenoChatClient chat = new XenoChatClient())
                    {
                        ChatConversation conversation = chat.CreateConversationAsync(new ChatConversationRequest { Model = "llama3.1:8b", Title = "usage turn" }, CancellationToken.None).GetAwaiter().GetResult();
                        chat.SendAsync(conversation.Id, new ChatSendRequest { Content = "count my tokens" }, null, CancellationToken.None).GetAwaiter().GetResult();
                        tui.Tui.Navigate(XenoPageEnum.Usage);
                        tui.Settle();
                        UsagePage page = (UsagePage)tui.Tui.CurrentView;
                        page.DatasetPicker.SelectedIndex = 2;
                        WaitFor(() => tui.Screen().Contains("Turns", StringComparison.Ordinal) && tui.Screen().Contains("Requests by outcome", StringComparison.Ordinal) && page.ChartWidget.Chart?.HasData == true, tui, "Chat usage did not load.");
                        Check(page.ChartPicker.Items.Contains("Feedback") && page.ChartPicker.Items.Contains("Tool calls"), "Chat should offer feedback and tool charts.");
                        HashSet<XenoChartKindEnum> kinds = new HashSet<XenoChartKindEnum>();
                        for (int i = 0; i < page.ChartPicker.Items.Count; i++)
                        {
                            page.ChartPicker.SelectedIndex = i;
                            tui.Settle();
                            XenoChart drawn = page.ChartWidget.Chart ?? throw new InvalidOperationException("No chart for " + page.ChartPicker.Items[i]);
                            kinds.Add(drawn.Kind);
                            if (drawn.Kind == XenoChartKindEnum.Heatmap)
                            {
                                // TUIKit's HeatMap draws the grid and its Less/More legend.
                                Check(tui.Screen().Contains("Less", StringComparison.Ordinal) && tui.Screen().Contains("More", StringComparison.Ordinal), "The heatmap is not drawn by TUIKit's HeatMap:" + Environment.NewLine + tui.Screen());
                                Check(tui.Screen().Contains("·· ·· ··", StringComparison.Ordinal), "Heatmap cells should have one column of padding between them:" + Environment.NewLine + tui.Screen());
                            }
                            Check(tui.Screen().Contains(drawn.Title, StringComparison.Ordinal), "The " + page.ChartPicker.Items[i] + " chart heading is not on screen:" + Environment.NewLine + tui.Screen());
                        }

                        Check(kinds.SetEquals(new[] { XenoChartKindEnum.StackedColumns, XenoChartKindEnum.Lines, XenoChartKindEnum.HorizontalBars, XenoChartKindEnum.Heatmap }), "Not every chart kind was drawn: " + String.Join(",", kinds));
                        Check(tui.Screen().Contains("By group", StringComparison.Ordinal) && tui.Screen().Contains("llama3.1:8b", StringComparison.Ordinal), "The group table is missing.");

                        // Keyboard: the cursor moves with the arrow keys and Enter opens the bucket in Chat history.
                        page.ChartPicker.SelectedIndex = 0;
                        tui.Settle();
                        UsageChartView view = page.ChartWidget;
                        Check(view.HandleKey(new TUIKit.Input.KeyEvent(TUIKit.Input.KeyCode.End, 0, TUIKit.Input.KeyModifiers.None)) && view.Cursor.HasValue, "End did not place the cursor.");
                        Check(view.HandleKey(new TUIKit.Input.KeyEvent(TUIKit.Input.KeyCode.Left, 0, TUIKit.Input.KeyModifiers.None)) && view.HandleKey(new TUIKit.Input.KeyEvent(TUIKit.Input.KeyCode.Right, 0, TUIKit.Input.KeyModifiers.None)), "Arrow keys did not move the cursor.");
                        tui.Settle();
                        Check(view.CursorText().Length > 0 && tui.Screen().Contains(view.CursorText().Split("  ")[0], StringComparison.Ordinal), "The cursor readout is not shown.");
                        view.HandleKey(new TUIKit.Input.KeyEvent(TUIKit.Input.KeyCode.Enter, 0, TUIKit.Input.KeyModifiers.None));
                        tui.Settle();
                        WaitFor(() => tui.Tui.CurrentPage == XenoPageEnum.ChatHistory && tui.Screen().Contains("usage turn", StringComparison.Ordinal), tui, "Enter did not open the bucket in Chat history.");

                        // Mouse: two clicks on the last column drill down the same way.
                        tui.Tui.Navigate(XenoPageEnum.Usage);
                        tui.Settle();
                        TUIKit.Point heading = tui.Find("Requests by outcome, last day") ?? throw new InvalidOperationException("No chart heading:" + Environment.NewLine + tui.Screen());
                        int x = heading.X + 9;
                        tui.Click(x, heading.Y + 3);
                        UsageChartView clicked = ((UsagePage)tui.Tui.CurrentView).ChartWidget;
                        Check(clicked.Cursor == x - heading.X - 8, "The first click did not select bucket " + (x - heading.X - 8) + " (cursor " + clicked.Cursor + ") at " + x + "," + (heading.Y + 3) + ":" + Environment.NewLine + tui.Screen());
                        tui.Click(x, heading.Y + 3);
                        WaitFor(() => tui.Tui.CurrentPage == XenoPageEnum.ChatHistory, tui, "Clicking a bucket did not open Chat history.");
                    }
                })
            });
        }

        private static TestCaseDescriptor Case(string suite, string id, string name, Action body)
        {
            return new TestCaseDescriptor(suite, id, name, executeAsync: delegate
            {
                body();
                return Task.CompletedTask;
            });
        }

        private static void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void WaitFor(Func<bool> condition, TuiHarness tui, string message)
        {
            for (int i = 0; i < 150; i++)
            {
                tui.Settle();
                if (condition()) return;
                Thread.Sleep(50);
            }

            throw new InvalidOperationException(message + Environment.NewLine + tui.Screen());
        }
    }
}

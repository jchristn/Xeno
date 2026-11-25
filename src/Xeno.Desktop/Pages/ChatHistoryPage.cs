namespace Xeno.Desktop.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using Avalonia;
    using Avalonia.Controls;
    using Xeno.Client.Chat;
    using Xeno.Core.Chat;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Desktop.Common;
    using Xeno.Desktop.Controls;

    /// <summary>
    /// Chat history: totals, latency percentiles, ratings, a chart of turns by where they ran, a per-model table, and
    /// every chat turn from every Xeno app, filtered by range, route, outcome, and rating.
    /// </summary>
    public sealed class ChatHistoryPage : DesktopPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override bool AutoRefreshes
        {
            get { return !_DrillActive; }
        }

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.ChatHistory; }
        }

        #endregion

        #region Private-Members

        private bool _DrillActive = false;
        private static readonly List<string> Routes = new List<string> { "All routes", "This machine", "Xeno network" };
        private static readonly List<string> Outcomes = new List<string> { "All outcomes", "Answered", "Failed", "Stopped" };
        private static readonly List<string> Ratings = new List<string> { "All ratings", "Thumbs up", "Thumbs down", "Not rated" };
        private readonly ColumnChart _Chart = new ColumnChart();
        private readonly WrapPanel _Metrics = new WrapPanel();
        private readonly StackPanel _Notice = new StackPanel();
        private ComboBox _Range = new ComboBox();
        private ComboBox _Route = new ComboBox();
        private ComboBox _Outcome = new ComboBox();
        private ComboBox _Rating = new ComboBox();
        private XenoTable<ChatModelBreakdown>? _Models;
        private XenoTable<ChatTurnRow>? _Turns;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="session">Desktop session.</param>
        /// <param name="window">Main window.</param>
        public ChatHistoryPage(DesktopSession session, MainWindow window) : base(session, window)
        {
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override IReadOnlyList<Control> Actions()
        {
            return new List<Control> { Ui.Button("Refresh", () => _ = RefreshAsync()) };
        }

        /// <inheritdoc />
        public override Control Build()
        {
            _Range = Combo(HistoryRanges.Labels.ToList(), 1);
            _Route = Combo(Routes, 0);
            _Outcome = Combo(Outcomes, 0);
            _Rating = Combo(Ratings, 0);
            _Models = new XenoTable<ChatModelBreakdown>(new List<XenoTableColumn<ChatModelBreakdown>>
            {
                new XenoTableColumn<ChatModelBreakdown>("Model", row => row.Model) { Monospace = true }.Star(3),
                new XenoTableColumn<ChatModelBreakdown>("Turns", row => row.Turns.ToString(CultureInfo.InvariantCulture)).Fixed(80),
                new XenoTableColumn<ChatModelBreakdown>("Errors", row => row.Errors.ToString(CultureInfo.InvariantCulture)).Fixed(80),
                new XenoTableColumn<ChatModelBreakdown>("Median time", row => ChatFormat.Duration(row.P50Ms)).Fixed(110),
                new XenoTableColumn<ChatModelBreakdown>("Thumbs up", row => row.ThumbsUp.ToString(CultureInfo.InvariantCulture)).Fixed(100),
                new XenoTableColumn<ChatModelBreakdown>("Thumbs down", row => row.ThumbsDown.ToString(CultureInfo.InvariantCulture)).Fixed(110),
                new XenoTableColumn<ChatModelBreakdown>("Tokens", row => row.CompletionTokens.ToString("N0", CultureInfo.CurrentCulture)).Fixed(100).Hidden()
            }, null, "No chat turns in this range.");
            _Models.Height = 160;
            _Turns = new XenoTable<ChatTurnRow>(new List<XenoTableColumn<ChatTurnRow>>
            {
                new XenoTableColumn<ChatTurnRow>("When", row => row.When) { SortKey = row => row.Turn.CreatedUtc }.Fixed(110),
                new XenoTableColumn<ChatTurnRow>("Conversation", row => row.Conversation).Star(3),
                new XenoTableColumn<ChatTurnRow>("Model", row => row.Model) { Monospace = true }.Star(2),
                new XenoTableColumn<ChatTurnRow>("Went to", row => row.Route).Star(2),
                new XenoTableColumn<ChatTurnRow>("Outcome", row => row.Outcome).Fixed(90),
                new XenoTableColumn<ChatTurnRow>("Time", row => row.Time) { SortKey = row => row.Turn.TotalMs }.Fixed(80),
                new XenoTableColumn<ChatTurnRow>("Rating", row => row.Rating).Fixed(110).Hidden(),
                new XenoTableColumn<ChatTurnRow>("Source", row => row.Source).Fixed(80).Hidden()
            }, TurnActions, "No chat turns match.");
            _Turns.Height = 340;
            _Turns.RowActivated += (sender, row) => _ = ShowTurnAsync(row.Turn.Id);
            return Ui.Stack(16,
                _Notice,
                Ui.Row(_Range, _Route, _Outcome, _Rating),
                _Metrics,
                Ui.Card("Chat activity", _Chart),
                Ui.Card("By model", _Models),
                Ui.Card("Chat turns", _Turns));
        }

        /// <inheritdoc />
        public override async Task RefreshAsync()
        {
            _Notice.Children.Clear();
            if (!Workspace.Chat.IsAvailable)
            {
                _Notice.Children.Add(Ui.Notice(XenoChatClient.NotRunningMessage, XenoToneEnum.Warning));
                return;
            }

            await GuardAsync("Chat history", async () =>
            {
                HistoryRangeEnum range = HistoryRanges.FromIndex(_Range.SelectedIndex);
                DateTime to = DateTime.UtcNow;
                DateTime from = HistoryRanges.StartUtc(range, to);
                XenoDrillDownRequest? window = XenoDrillDown.Take(Page);
                _DrillActive = window != null;
                if (window != null) _Notice.Children.Add(Ui.Notice("Showing " + XenoDrillDown.Describe(window) + " from the Usage page. Refresh or change a filter to see the whole range.", XenoToneEnum.Info));
                ChatAnalytics analytics = await Workspace.Chat.AnalyticsAsync(from, to, HistoryRanges.ChatBucketSeconds(range), None).ConfigureAwait(true);
                ChatPage<ChatTurn> page = await Workspace.Chat.QueryHistoryAsync(new ChatHistoryQuery
                {
                    FromUtc = window?.FromUtc ?? from,
                    ToUtc = window?.ToUtc ?? to,
                    Model = window?.Model,
                    Route = _Route.SelectedIndex == 1 ? "local" : _Route.SelectedIndex == 2 ? "network" : null,
                    Status = _Outcome.SelectedIndex == 1 ? ChatTurnStatusEnum.Success : _Outcome.SelectedIndex == 2 ? ChatTurnStatusEnum.Error : _Outcome.SelectedIndex == 3 ? ChatTurnStatusEnum.Cancelled : null,
                    Rating = _Rating.SelectedIndex == 1 ? 1 : _Rating.SelectedIndex == 2 ? -1 : _Rating.SelectedIndex == 3 ? 0 : null,
                    PageSize = 500
                }, None).ConfigureAwait(true);
                RenderMetrics(analytics);
                _Chart.SetChart(XenoCharts.ChatActivity(analytics, range));
                _Models?.SetRows(analytics.Models);
                _Turns?.SetRows(page.Items.Select(ChatTurnRow.From).ToList(), (left, right) => left.Turn.Id == right.Turn.Id);
            }).ConfigureAwait(true);
        }

        #endregion

        #region Private-Methods

        private ComboBox Combo(List<string> items, int selected)
        {
            ComboBox combo = new ComboBox { ItemsSource = items, SelectedIndex = selected, MinWidth = 140 };
            combo.SelectionChanged += (sender, args) => _ = RefreshAsync();
            return combo;
        }

        private void RenderMetrics(ChatAnalytics analytics)
        {
            _Metrics.Children.Clear();
            Add(Ui.Metric("Chat turns", analytics.Turns.ToString("N0", CultureInfo.CurrentCulture), analytics.Local.ToString(CultureInfo.InvariantCulture) + " here, " + analytics.Network.ToString(CultureInfo.InvariantCulture) + " on the network"));
            Add(Ui.Metric("Median time", ChatFormat.Duration(analytics.P50Ms), analytics.P95Ms.HasValue ? "95th percentile " + ChatFormat.Duration(analytics.P95Ms) : "No data"));
            Add(Ui.Metric("First token", ChatFormat.Duration(analytics.P50TimeToFirstTokenMs), "Median"));
            Add(Ui.Metric("Thumbs down share", analytics.NegativeRate.HasValue ? Math.Round(analytics.NegativeRate.Value * 100).ToString(CultureInfo.InvariantCulture) + "%" : "-", analytics.ThumbsUp.ToString(CultureInfo.InvariantCulture) + " up, " + analytics.ThumbsDown.ToString(CultureInfo.InvariantCulture) + " down"));
            Add(Ui.Metric("Errors", analytics.Errors.ToString("N0", CultureInfo.CurrentCulture), Math.Round(analytics.ErrorRate * 100).ToString(CultureInfo.InvariantCulture) + "% of turns", analytics.Errors > 0 ? XenoTheme.Current.Danger : null));
            Add(Ui.Metric("Tool calls", analytics.ToolCalls.ToString("N0", CultureInfo.CurrentCulture), analytics.Fallbacks.ToString(CultureInfo.InvariantCulture) + " network fallbacks"));
        }

        private void Add(Control metric)
        {
            metric.Margin = new Thickness(0, 0, 12, 12);
            metric.Width = 220;
            _Metrics.Children.Add(metric);
        }

        private IReadOnlyList<XenoRowAction<ChatTurnRow>> TurnActions(ChatTurnRow row)
        {
            return new List<XenoRowAction<ChatTurnRow>>
            {
                new XenoRowAction<ChatTurnRow>("Details...", item => _ = ShowTurnAsync(item.Turn.Id)),
                new XenoRowAction<ChatTurnRow>("Open Conversation", item => _ = OpenConversationAsync(item.Turn.ConversationId))
            };
        }

        private async Task ShowTurnAsync(string turnId)
        {
            await GuardAsync("Chat turn", async () =>
            {
                ChatTurn turn = await Workspace.Chat.GetTurnAsync(turnId, None).ConfigureAwait(true);
                List<string> lines = new List<string>
                {
                    "Conversation: " + (turn.ConversationTitle ?? "(deleted)"),
                    "Model: " + turn.Model,
                    "Went to: " + ChatFormat.Route(turn.Route, turn.Fallback),
                    "Outcome: " + ChatFormat.Outcome(turn.Status) + (turn.Error != null ? " (" + turn.Error + ")" : String.Empty),
                    "Time: " + ChatFormat.Duration(turn.TotalMs) + ", first token " + ChatFormat.Duration(turn.TimeToFirstTokenMs),
                    "Tokens: " + (turn.PromptTokens?.ToString(CultureInfo.InvariantCulture) ?? "-") + " in, " + (turn.CompletionTokens?.ToString(CultureInfo.InvariantCulture) ?? "-") + " out",
                    "Model calls: " + turn.ModelCalls.ToString(CultureInfo.InvariantCulture) + ", tool calls: " + turn.ToolCallCount.ToString(CultureInfo.InvariantCulture),
                    "Rating: " + (ChatFormat.Rating(turn.FeedbackRating) is string rating && rating.Length > 0 ? rating : "Not rated"),
                    "Source: " + ChatFormat.Surface(turn.Surface),
                    "Started: " + ChatFormat.When(turn.CreatedUtc)
                };
                foreach (ChatToolCallRecord call in turn.ToolCalls) lines.Add("Tool " + call.ToolName + ": " + call.Approval + ", " + (call.Success ? "done" : "failed") + ", " + ChatFormat.Duration(call.DurationMs));
                List<string> timing = XenoUsageCharts.TimingLines(XenoUsageCharts.StagesFor(turn), 16);
                if (timing.Count > 0)
                {
                    lines.Add(String.Empty);
                    lines.Add("Where the time went:");
                    lines.AddRange(timing);
                }
                await XenoDialogs.MessageAsync(Window, "Chat turn", String.Join("\n", lines), XenoToneEnum.Info).ConfigureAwait(true);
            }).ConfigureAwait(true);
        }

        private async Task OpenConversationAsync(string conversationId)
        {
            Window.Navigate(XenoPageEnum.Chat);
            if (Window.CurrentPage is ChatPage chat) await chat.OpenAsync(conversationId).ConfigureAwait(true);
        }

        #endregion
    }
}

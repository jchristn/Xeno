namespace Xeno.Tui.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Threading.Tasks;
    using TUIKit.Layout;
    using TUIKit.Widgets;
    using Xeno.Client.Chat;
    using Xeno.Core.Chat;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;
    using Xeno.Tui.Shell;

    /// <summary>
    /// Chat history in the terminal app: totals and percentiles, the activity chart, and every chat turn, filtered by
    /// range, route, and outcome. Enter opens a turn's details.
    /// </summary>
    internal sealed class ChatHistoryPage : TuiPage
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

        /// <inheritdoc />
        public override IWidget Root
        {
            get { return _Root; }
        }

        #endregion

        #region Private-Members

        private bool _DrillActive = false;
        private static readonly List<string> Routes = new List<string> { "All routes", "This machine", "Xeno network" };
        private static readonly List<string> Outcomes = new List<string> { "All outcomes", "Answered", "Failed", "Stopped" };
        private readonly FramedStack _Root = new FramedStack(SplitOrientation.Vertical);
        private readonly Dropdown<string> _Range = new Dropdown<string>(HistoryRanges.Labels);
        private readonly Dropdown<string> _Route = new Dropdown<string>(Routes);
        private readonly Dropdown<string> _Outcome = new Dropdown<string>(Outcomes);
        private readonly DetailsView _Summary = new DetailsView();
        private readonly ChartView _Chart = new ChartView();
        private readonly DataTable<ChatTurnRow> _Table;
        private readonly DataTable<ChatModelBreakdown> _Models;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="context">Context.</param>
        public ChatHistoryPage(TuiContext context) : base(context)
        {
            _Range.SelectedIndex = 1;
            _Route.SelectedIndex = 0;
            _Outcome.SelectedIndex = 0;
            _Range.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Route.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Outcome.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Table = Table<ChatTurnRow>("No chat turns in this range. Chat on the Chat page, in the web dashboard, or with xeno chat.", row => row.Turn.Id);
            Column(_Table, "When", row => row.When, 12);
            Column(_Table, "Conversation", row => row.Conversation, 0);
            Column(_Table, "Model", row => row.Model, 16);
            Column(_Table, "Went to", row => row.Route, 19);
            Column(_Table, "Outcome", row => row.Outcome, 9);
            Column(_Table, "Time", row => row.Time, 7);
            Column(_Table, "Rating", row => row.Rating, 12, false);
            _Table.RowStyle = row => row.Turn.Status == ChatTurnStatusEnum.Error ? Context.Theme.Error : (TUIKit.CellStyle?)null;
            _Table.RowActivated += index => _ = ShowDetailAsync();
            _Models = Table<ChatModelBreakdown>("No chat turns in this range.", row => row.Model);
            Column(_Models, "Model", row => row.Model, 0);
            Column(_Models, "Turns", row => row.Turns.ToString(CultureInfo.InvariantCulture), 7);
            Column(_Models, "Errors", row => row.Errors.ToString(CultureInfo.InvariantCulture), 7);
            Column(_Models, "Median time", row => ChatFormat.Duration(row.P50Ms), 12);
            Column(_Models, "Thumbs up", row => row.ThumbsUp.ToString(CultureInfo.InvariantCulture), 10);
            Column(_Models, "Thumbs down", row => row.ThumbsDown.ToString(CultureInfo.InvariantCulture), 12);
            Column(_Models, "Tokens", row => row.CompletionTokens.ToString("N0", CultureInfo.CurrentCulture), 10, false);
            FramedStack filters = new FramedStack(SplitOrientation.Horizontal);
            Frame(filters, _Range, StackSize.Weighted(1, 14), "Range");
            Frame(filters, _Route, StackSize.Weighted(1, 16), "Went to");
            Frame(filters, _Outcome, StackSize.Weighted(1, 16), "Outcome");
            Frame(_Root, filters, StackSize.Fixed(1));
            Frame(_Root, _Summary, StackSize.Fixed(3), "Summary");
            Frame(_Root, _Chart, StackSize.Fixed(8));
            Frame(_Root, _Models, StackSize.Fixed(5), "By model");
            Frame(_Root, _Table, StackSize.Weighted(1, 4), "Chat turns");
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override Task RefreshAsync()
        {
            if (!Workspace.Chat.IsAvailable)
            {
                _Summary.Clear();
                _Summary.Set("Chat", XenoChatClient.NotRunningMessage);
                _Table.Bind(new List<ChatTurnRow>());
                _Models.Bind(new List<ChatModelBreakdown>());
                return Task.CompletedTask;
            }

            HistoryRangeEnum range = HistoryRanges.FromIndex(_Range.SelectedIndex);
            DateTime to = DateTime.UtcNow;
            DateTime from = HistoryRanges.StartUtc(range, to);
            XenoDrillDownRequest? window = XenoDrillDown.Take(Page);
            _DrillActive = window != null;
            ChatHistoryQuery query = new ChatHistoryQuery
            {
                FromUtc = window?.FromUtc ?? from,
                ToUtc = window?.ToUtc ?? to,
                Model = window?.Model,
                Route = _Route.SelectedIndex == 1 ? "local" : _Route.SelectedIndex == 2 ? "network" : null,
                Status = _Outcome.SelectedIndex == 1 ? ChatTurnStatusEnum.Success : _Outcome.SelectedIndex == 2 ? ChatTurnStatusEnum.Error : _Outcome.SelectedIndex == 3 ? ChatTurnStatusEnum.Cancelled : null,
                PageSize = 500
            };
            return LoadAsync(async token =>
            {
                ChatAnalytics analytics = await Workspace.Chat.AnalyticsAsync(from, to, HistoryRanges.ChatBucketSeconds(range), token).ConfigureAwait(false);
                ChatPage<ChatTurn> page = await Workspace.Chat.QueryHistoryAsync(query, token).ConfigureAwait(false);
                return new KeyValuePair<ChatAnalytics, ChatPage<ChatTurn>>(analytics, page);
            }, data =>
            {
                ChatAnalytics analytics = data.Key;
                _Summary.Clear();
                _Summary.Set("Turns", analytics.Turns.ToString(CultureInfo.InvariantCulture) + " (" + analytics.Local.ToString(CultureInfo.InvariantCulture) + " here, " + analytics.Network.ToString(CultureInfo.InvariantCulture) + " network, " + analytics.Errors.ToString(CultureInfo.InvariantCulture) + " errors, " + analytics.Fallbacks.ToString(CultureInfo.InvariantCulture) + " fallbacks)");
                _Summary.Set("Time", "median " + ChatFormat.Duration(analytics.P50Ms) + ", 95th " + ChatFormat.Duration(analytics.P95Ms) + ", first token " + ChatFormat.Duration(analytics.P50TimeToFirstTokenMs));
                _Summary.Set("Ratings", analytics.ThumbsUp.ToString(CultureInfo.InvariantCulture) + " up, " + analytics.ThumbsDown.ToString(CultureInfo.InvariantCulture) + " down" + (analytics.NegativeRate.HasValue ? " (" + Math.Round(analytics.NegativeRate.Value * 100).ToString(CultureInfo.InvariantCulture) + "% down)" : String.Empty) + ", " + analytics.ToolCalls.ToString(CultureInfo.InvariantCulture) + " tool calls");
                _Chart.Chart = XenoCharts.ChatActivity(analytics, range);
                _Table.Bind(data.Value.Items.Select(ChatTurnRow.From).ToList());
                _Models.Bind(analytics.Models.ToList());
            });
        }

        #endregion

        #region Private-Methods

        private async Task ShowDetailAsync()
        {
            ChatTurnRow? row = _Table.SelectedRow;
            if (row == null) return;
            ChatTurn turn = await Workspace.Chat.GetTurnAsync(row.Turn.Id, System.Threading.CancellationToken.None).ConfigureAwait(false);
            List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Conversation", turn.ConversationTitle ?? "(deleted)"),
                new KeyValuePair<string, string>("Model", turn.Model),
                new KeyValuePair<string, string>("Went to", ChatFormat.Route(turn.Route, turn.Fallback)),
                new KeyValuePair<string, string>("Outcome", ChatFormat.Outcome(turn.Status) + (turn.Error != null ? ": " + turn.Error : String.Empty)),
                new KeyValuePair<string, string>("Time", ChatFormat.Duration(turn.TotalMs) + ", first token " + ChatFormat.Duration(turn.TimeToFirstTokenMs)),
                new KeyValuePair<string, string>("Tokens", (turn.PromptTokens?.ToString(CultureInfo.InvariantCulture) ?? "-") + " in, " + (turn.CompletionTokens?.ToString(CultureInfo.InvariantCulture) ?? "-") + " out"),
                new KeyValuePair<string, string>("Calls", turn.ModelCalls.ToString(CultureInfo.InvariantCulture) + " model, " + turn.ToolCallCount.ToString(CultureInfo.InvariantCulture) + " tool"),
                new KeyValuePair<string, string>("Rating", ChatFormat.Rating(turn.FeedbackRating) is string rating && rating.Length > 0 ? rating : "Not rated"),
                new KeyValuePair<string, string>("Source", ChatFormat.Surface(turn.Surface)),
                new KeyValuePair<string, string>("Started", ChatFormat.When(turn.CreatedUtc))
            };
            foreach (ChatToolCallRecord call in turn.ToolCalls) pairs.Add(new KeyValuePair<string, string>("Tool", ChatTranscript.ToolLine(call)));
            string? timing = XenoUsageCharts.TimingMarkdown(XenoUsageCharts.StagesFor(turn));
            await Context.ShowMarkdownAsync("Chat turn", XenoMarkdown.Join(new[] { XenoMarkdown.Details(pairs), timing })).ConfigureAwait(false);
        }

        #endregion
    }
}

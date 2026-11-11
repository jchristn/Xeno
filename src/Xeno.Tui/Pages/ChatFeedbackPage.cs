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
    /// Chat feedback in the terminal app: rating counts and every rating with its comment. Enter shows the rating and
    /// the start of the answer.
    /// </summary>
    internal sealed class ChatFeedbackPage : TuiPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.ChatFeedback; }
        }

        /// <inheritdoc />
        public override IWidget Root
        {
            get { return _Root; }
        }

        #endregion

        #region Private-Members

        private static readonly List<string> Ratings = new List<string> { "All ratings", "Thumbs up", "Thumbs down" };
        private readonly FramedStack _Root = new FramedStack(SplitOrientation.Vertical);
        private readonly Dropdown<string> _Range = new Dropdown<string>(HistoryRanges.Labels);
        private readonly Dropdown<string> _Rating = new Dropdown<string>(Ratings);
        private readonly DetailsView _Summary = new DetailsView();
        private readonly DataTable<ChatFeedbackRow> _Table;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="context">Context.</param>
        public ChatFeedbackPage(TuiContext context) : base(context)
        {
            _Range.SelectedIndex = 2;
            _Rating.SelectedIndex = 0;
            _Range.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Rating.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Table = Table<ChatFeedbackRow>("No ratings yet. Rate answers on the Chat page.", row => row.Feedback.Id);
            Column(_Table, "Rated", row => row.When, 12);
            Column(_Table, "Rating", row => row.Rating, 12);
            Column(_Table, "Comment", row => row.Comment, 0);
            Column(_Table, "Model", row => row.Model, 16);
            Column(_Table, "Conversation", row => row.Conversation, 20);
            _Table.RowActivated += index => _ = ShowDetailAsync();
            FramedStack filters = new FramedStack(SplitOrientation.Horizontal);
            Frame(filters, _Range, StackSize.Weighted(1, 14), "Range");
            Frame(filters, _Rating, StackSize.Weighted(1, 16), "Rating");
            Frame(_Root, filters, StackSize.Fixed(1));
            Frame(_Root, _Summary, StackSize.Fixed(2), "Summary");
            Frame(_Root, _Table, StackSize.Weighted(1, 4), "Ratings");
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
                _Table.Bind(new List<ChatFeedbackRow>());
                return Task.CompletedTask;
            }

            HistoryRangeEnum range = HistoryRanges.FromIndex(_Range.SelectedIndex);
            DateTime to = DateTime.UtcNow;
            DateTime from = HistoryRanges.StartUtc(range, to);
            int? rating = _Rating.SelectedIndex == 1 ? 1 : _Rating.SelectedIndex == 2 ? -1 : null;
            return LoadAsync(async token =>
            {
                ChatAnalytics analytics = await Workspace.Chat.AnalyticsAsync(from, to, HistoryRanges.ChatBucketSeconds(range), token).ConfigureAwait(false);
                ChatPage<ChatFeedback> page = await Workspace.Chat.QueryFeedbackAsync(new ChatFeedbackQuery { FromUtc = from, ToUtc = to, Rating = rating, PageSize = 500 }, token).ConfigureAwait(false);
                return new KeyValuePair<ChatAnalytics, ChatPage<ChatFeedback>>(analytics, page);
            }, data =>
            {
                ChatAnalytics analytics = data.Key;
                int rated = analytics.ThumbsUp + analytics.ThumbsDown;
                _Summary.Clear();
                _Summary.Set("Ratings", analytics.ThumbsUp.ToString(CultureInfo.InvariantCulture) + " up, " + analytics.ThumbsDown.ToString(CultureInfo.InvariantCulture) + " down" + (analytics.NegativeRate.HasValue ? " (" + Math.Round(analytics.NegativeRate.Value * 100).ToString(CultureInfo.InvariantCulture) + "% down)" : String.Empty));
                _Summary.Set("Rated", analytics.Turns > 0 ? Math.Round(100.0 * rated / analytics.Turns).ToString(CultureInfo.InvariantCulture) + "% of " + analytics.Turns.ToString(CultureInfo.InvariantCulture) + " chat turns" : "No chat turns in this range");
                _Table.Bind(data.Value.Items.Select(ChatFeedbackRow.From).ToList());
            });
        }

        #endregion

        #region Private-Methods

        private Task ShowDetailAsync()
        {
            ChatFeedbackRow? row = _Table.SelectedRow;
            if (row == null) return Task.CompletedTask;
            List<KeyValuePair<string, string>> pairs = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Rating", row.Rating),
                new KeyValuePair<string, string>("Comment", String.IsNullOrWhiteSpace(row.Comment) ? "-" : row.Comment),
                new KeyValuePair<string, string>("Model", row.Model),
                new KeyValuePair<string, string>("Went to", row.Route),
                new KeyValuePair<string, string>("Conversation", row.Conversation),
                new KeyValuePair<string, string>("Rated", row.When),
                new KeyValuePair<string, string>("Answer", row.Feedback.Excerpt ?? "-")
            };
            return Context.ShowMarkdownAsync("Rating", XenoMarkdown.Details(pairs));
        }

        #endregion
    }
}

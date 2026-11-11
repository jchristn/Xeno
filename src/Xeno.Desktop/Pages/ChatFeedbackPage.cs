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

    /// <summary>
    /// Chat feedback: thumbs up and down counts, the thumbs down share, and every rating with its comment.
    /// </summary>
    public sealed class ChatFeedbackPage : DesktopPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.ChatFeedback; }
        }

        #endregion

        #region Private-Members

        private static readonly List<string> Ratings = new List<string> { "All ratings", "Thumbs up", "Thumbs down" };
        private readonly WrapPanel _Metrics = new WrapPanel();
        private readonly StackPanel _Notice = new StackPanel();
        private ComboBox _Range = new ComboBox();
        private ComboBox _Rating = new ComboBox();
        private CheckBox _Comments = new CheckBox();
        private XenoTable<ChatFeedbackRow>? _Table;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="session">Desktop session.</param>
        /// <param name="window">Main window.</param>
        public ChatFeedbackPage(DesktopSession session, MainWindow window) : base(session, window)
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
            _Range = new ComboBox { ItemsSource = HistoryRanges.Labels, SelectedIndex = 2, MinWidth = 140 };
            _Range.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Rating = new ComboBox { ItemsSource = Ratings, SelectedIndex = 0, MinWidth = 140 };
            _Rating.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Comments = new CheckBox { Content = "With comments only" };
            _Comments.IsCheckedChanged += (sender, args) => _ = RefreshAsync();
            _Table = new XenoTable<ChatFeedbackRow>(new List<XenoTableColumn<ChatFeedbackRow>>
            {
                new XenoTableColumn<ChatFeedbackRow>("Rated", row => row.When) { SortKey = row => row.Feedback.UpdatedUtc }.Fixed(110),
                new XenoTableColumn<ChatFeedbackRow>("Rating", row => row.Rating).Fixed(110),
                new XenoTableColumn<ChatFeedbackRow>("Comment", row => row.Comment).Star(3),
                new XenoTableColumn<ChatFeedbackRow>("Model", row => row.Model) { Monospace = true }.Star(2),
                new XenoTableColumn<ChatFeedbackRow>("Went to", row => row.Route).Star(2),
                new XenoTableColumn<ChatFeedbackRow>("Conversation", row => row.Conversation).Star(2)
            }, Actions, "No ratings yet. Rate answers with thumbs up or down on the Chat page.");
            _Table.Height = 420;
            _Table.RowActivated += (sender, row) => _ = ShowAsync(row);
            return Ui.Stack(16, _Notice, Ui.Row(_Range, _Rating, _Comments), _Metrics, Ui.Card("Ratings", _Table));
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

            await GuardAsync("Chat feedback", async () =>
            {
                HistoryRangeEnum range = HistoryRanges.FromIndex(_Range.SelectedIndex);
                DateTime to = DateTime.UtcNow;
                DateTime from = HistoryRanges.StartUtc(range, to);
                ChatAnalytics analytics = await Workspace.Chat.AnalyticsAsync(from, to, HistoryRanges.ChatBucketSeconds(range), None).ConfigureAwait(true);
                ChatPage<ChatFeedback> page = await Workspace.Chat.QueryFeedbackAsync(new ChatFeedbackQuery
                {
                    FromUtc = from,
                    ToUtc = to,
                    Rating = _Rating.SelectedIndex == 1 ? 1 : _Rating.SelectedIndex == 2 ? -1 : null,
                    HasComment = _Comments.IsChecked == true ? true : null,
                    PageSize = 500
                }, None).ConfigureAwait(true);
                _Metrics.Children.Clear();
                int ratings = analytics.ThumbsUp + analytics.ThumbsDown;
                Add(Ui.Metric("Thumbs up", analytics.ThumbsUp.ToString("N0", CultureInfo.CurrentCulture), null, XenoTheme.Current.Success));
                Add(Ui.Metric("Thumbs down", analytics.ThumbsDown.ToString("N0", CultureInfo.CurrentCulture), null, analytics.ThumbsDown > 0 ? XenoTheme.Current.Danger : null));
                Add(Ui.Metric("Thumbs down share", analytics.NegativeRate.HasValue ? Math.Round(analytics.NegativeRate.Value * 100).ToString(CultureInfo.InvariantCulture) + "%" : "-", ratings.ToString(CultureInfo.InvariantCulture) + " rated answers"));
                Add(Ui.Metric("Answers rated", analytics.Turns > 0 ? Math.Round(100.0 * ratings / analytics.Turns).ToString(CultureInfo.InvariantCulture) + "%" : "-", "Share of chat turns with a rating"));
                _Table?.SetRows(page.Items.Select(ChatFeedbackRow.From).ToList(), (left, right) => left.Feedback.Id == right.Feedback.Id);
            }).ConfigureAwait(true);
        }

        #endregion

        #region Private-Methods

        private void Add(Control metric)
        {
            metric.Margin = new Thickness(0, 0, 12, 12);
            metric.Width = 220;
            _Metrics.Children.Add(metric);
        }

        private IReadOnlyList<XenoRowAction<ChatFeedbackRow>> Actions(ChatFeedbackRow row)
        {
            return new List<XenoRowAction<ChatFeedbackRow>>
            {
                new XenoRowAction<ChatFeedbackRow>("Details...", item => _ = ShowAsync(item)),
                new XenoRowAction<ChatFeedbackRow>("Open Conversation", item => _ = OpenConversationAsync(item.Feedback.ConversationId))
            };
        }

        private async Task ShowAsync(ChatFeedbackRow row)
        {
            string text = row.Rating + (String.IsNullOrWhiteSpace(row.Comment) ? String.Empty : "\n\nComment: " + row.Comment)
                + "\n\nModel: " + row.Model + "\nWent to: " + row.Route + "\nConversation: " + row.Conversation + "\nRated: " + row.When
                + (String.IsNullOrWhiteSpace(row.Feedback.Excerpt) ? String.Empty : "\n\nAnswer: " + row.Feedback.Excerpt);
            await XenoDialogs.MessageAsync(Window, "Rating", text, row.Feedback.Rating > 0 ? XenoToneEnum.Success : XenoToneEnum.Warning).ConfigureAwait(true);
        }

        private async Task OpenConversationAsync(string conversationId)
        {
            Window.Navigate(XenoPageEnum.Chat);
            if (Window.CurrentPage is ChatPage chat) await chat.OpenAsync(conversationId).ConfigureAwait(true);
        }

        #endregion
    }
}

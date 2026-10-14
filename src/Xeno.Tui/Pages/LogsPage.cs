namespace Xeno.Tui.Pages
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Threading.Tasks;
    using TUIKit.Input;
    using TUIKit.Layout;
    using TUIKit.Widgets;
    using Xeno.Client;
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;

    /// <summary>
    /// Logs: the worker log or error log with tail length and severity filters, refreshed while following.
    /// </summary>
    internal sealed class LogsPage : TuiPage
    {
        #region Public-Members

        /// <inheritdoc />
        public override XenoPageEnum Page
        {
            get { return XenoPageEnum.Logs; }
        }

        /// <inheritdoc />
        public override IWidget Root
        {
            get { return _Root; }
        }

        /// <inheritdoc />
        public override bool RefreshWithStatus
        {
            get { return _Follow.Checked; }
        }

        /// <inheritdoc />
        public override IReadOnlyList<KeyHint> Hints
        {
            get { return new List<KeyHint> { new KeyHint("PgUp/PgDn", "Scroll"), new KeyHint("f", "Follow") }; }
        }

        #endregion

        #region Private-Members

        private static readonly List<string> Files = new List<string> { "Worker log", "Error log" };
        private static readonly List<string> Tails = new List<string> { "100", "250", "500", "1000", "5000" };
        private readonly FramedStack _Root = new FramedStack(SplitOrientation.Vertical);
        private readonly Dropdown<string> _File = new Dropdown<string>(Files);
        private readonly Dropdown<string> _Tail = new Dropdown<string>(Tails);
        private readonly Dropdown<string> _Severity = new Dropdown<string>(ClientLogReader.SeverityFilters);
        private readonly Checkbox _Follow = new Checkbox("Follow", true);
        private readonly LogView _Log = new LogView();
        private string? _Last = null;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the page.
        /// </summary>
        /// <param name="context">Context.</param>
        public LogsPage(TuiContext context) : base(context)
        {
            _File.SelectedIndex = 0;
            _Tail.SelectedIndex = 1;
            _Severity.SelectedIndex = 0;
            _File.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Tail.SelectionChanged += (sender, args) => _ = RefreshAsync();
            _Severity.SelectionChanged += (sender, args) => _ = RefreshAsync();
            FramedStack filters = new FramedStack(SplitOrientation.Horizontal);
            Frame(filters, _File, StackSize.Weighted(1, 14), "Log");
            Frame(filters, _Tail, StackSize.Weighted(1, 8), "Lines");
            Frame(filters, _Severity, StackSize.Weighted(1, 10), "Severity");
            Frame(filters, _Follow, StackSize.Weighted(1, 12));
            Frame(_Root, filters, StackSize.Fixed(1));
            Frame(_Root, _Log, StackSize.Weighted(1, 4), "Log");
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public override Task RefreshAsync()
        {
            bool error = _File.SelectedIndex == 1;
            int tail = Int32.Parse(_Tail.SelectedItem ?? "250", CultureInfo.InvariantCulture);
            string severity = _Severity.SelectedItem ?? "all";
            return Context.LoadAsync(token => Workspace.ReadLogAsync(tail, severity, error, token), lines =>
            {
                string joined = String.Join("\n", lines);
                if (String.Equals(joined, _Last, StringComparison.Ordinal)) return;
                _Last = joined;
                _Log.SetLines(lines.Count == 0 ? new List<string> { "(no log lines yet; start the client to create " + (error ? ClientConnectionState.ErrorLogFile : ClientConnectionState.LogFile) + ")" } : lines);
            });
        }

        /// <inheritdoc />
        public override bool HandleKey(KeyEvent key)
        {
            if (PageKeys.Is(key, 'f'))
            {
                _Follow.Checked = !_Follow.Checked;
                return true;
            }

            return false;
        }

        #endregion
    }
}

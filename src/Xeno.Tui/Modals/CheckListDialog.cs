namespace Xeno.Tui.Modals
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TUIKit;
    using TUIKit.Content;
    using TUIKit.Input;
    using TUIKit.Modals;
    using TUIKit.Theming;
    using TUIKit.Unicode;

    /// <summary>
    /// A themed checklist with a row of buttons, for choosing several things at once (table columns, provider
    /// endpoints). Up and Down move, Space toggles, Left and Right pick a button, Enter presses it, and Esc cancels. A
    /// click on a row toggles it and a click on a button presses it; the wheel scrolls. Completes with a
    /// <see cref="CheckListResult"/>, or null when cancelled.
    /// </summary>
    internal sealed class CheckListDialog : DialogModal, IThemeable
    {
        #region Public-Members

        /// <summary>
        /// Rows.
        /// </summary>
        public IReadOnlyList<CheckListItem> Items
        {
            get { return _Items; }
        }

        /// <summary>
        /// Fewest rows that must stay checked; unchecking below it is refused. Defaults to 0.
        /// </summary>
        public int MinimumChecked { get; set; } = 0;

        /// <summary>
        /// Row under the cursor.
        /// </summary>
        public int Cursor
        {
            get { return _Cursor; }
        }

        #endregion

        #region Private-Members

        private const int MaxRows = 16;
        private readonly string _Message;
        private readonly List<CheckListItem> _Items;
        private readonly List<string> _Buttons;
        private readonly List<Rect> _ButtonRects = new List<Rect>();
        private int _Cursor = 0;
        private int _Top = 0;
        private int _Selected = 0;
        private int _ListRow = 0;
        private int _ListHeight = 0;
        private Theme _Theme = XenoTuiTheme.Dark;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a checklist dialog.
        /// </summary>
        /// <param name="title">Title.</param>
        /// <param name="message">Text above the list, or empty.</param>
        /// <param name="items">Rows.</param>
        /// <param name="buttons">Button labels; the first is pressed by Enter.</param>
        /// <exception cref="ArgumentException">There are no buttons.</exception>
        public CheckListDialog(string title, string message, IEnumerable<CheckListItem> items, IEnumerable<string> buttons)
        {
            Title = " " + title + " ";
            _Message = message ?? String.Empty;
            _Items = (items ?? Enumerable.Empty<CheckListItem>()).ToList();
            _Buttons = (buttons ?? Enumerable.Empty<string>()).ToList();
            if (_Buttons.Count == 0) throw new ArgumentException("At least one button is required.", nameof(buttons));
            _Cursor = Math.Max(0, _Items.FindIndex(item => item.Enabled));
            FooterHint = " ↑↓ move  Space toggle  ←→ button  Enter " + _Buttons[0].ToLowerInvariant() + "  Esc cancel ";
            MinContentWidth = 44;
            MaxContentWidth = 72;
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Apply a theme.
        /// </summary>
        /// <param name="theme">Theme.</param>
        public void ApplyTheme(Theme theme)
        {
            _Theme = theme ?? XenoTuiTheme.Dark;
            ApplyFocusTheme(_Theme);
        }

        /// <summary>
        /// Toggle a row, unless it is disabled or unchecking it would go below <see cref="MinimumChecked"/>.
        /// </summary>
        /// <param name="index">Row.</param>
        /// <returns>True when toggled.</returns>
        public bool Toggle(int index)
        {
            if (index < 0 || index >= _Items.Count || !_Items[index].Enabled) return false;
            CheckListItem item = _Items[index];
            if (item.Checked && _Items.Count(entry => entry.Checked) <= MinimumChecked) return false;
            item.Checked = !item.Checked;
            return true;
        }

        /// <summary>
        /// Move, toggle, pick a button, or cancel.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True.</returns>
        public override bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Up:
                    MoveCursor(-1);
                    return true;
                case KeyCode.Down:
                    MoveCursor(1);
                    return true;
                case KeyCode.Left:
                    _Selected = _Selected > 0 ? _Selected - 1 : _Buttons.Count - 1;
                    return true;
                case KeyCode.Right:
                case KeyCode.Tab:
                    _Selected = (_Selected + 1) % _Buttons.Count;
                    return true;
                case KeyCode.Enter:
                    Finish(_Selected);
                    return true;
                case KeyCode.Escape:
                    RequestClose(null);
                    return true;
                case KeyCode.Character:
                    if (key.Rune == ' ') Toggle(_Cursor);
                    return true;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Toggle a clicked row, press a clicked button, and scroll with the wheel.
        /// </summary>
        /// <param name="mouse">Mouse event in screen coordinates.</param>
        /// <returns>True.</returns>
        public override bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null) return true;
            int button = _ButtonRects.FindIndex(rect => rect.Contains(new Point(mouse.X, mouse.Y)));
            if (button >= 0)
            {
                if (mouse.Kind == MouseEventKind.Move) _Selected = button;
                if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left) Finish(button);
                return true;
            }

            if (mouse.Kind == MouseEventKind.Wheel)
            {
                MoveCursor(mouse.Button == MouseButton.WheelDown ? 1 : -1);
                return true;
            }

            Rect content = ContentBounds;
            int row = mouse.Y - content.Y - _ListRow;
            if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left && row >= 0 && row < _ListHeight && mouse.X >= content.X && mouse.X < content.X + content.Width)
            {
                int index = _Top + row;
                if (index < _Items.Count)
                {
                    _Cursor = index;
                    Toggle(index);
                }
            }

            return true;
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Content width.
        /// </summary>
        /// <param name="availableWidth">Available width.</param>
        /// <returns>Width.</returns>
        protected override int MeasureContentWidth(int availableWidth)
        {
            int widest = _Items.Count == 0 ? 0 : _Items.Max(item => TextFit.Width(item.Label) + 4);
            return Math.Min(availableWidth, Math.Max(ButtonsWidth(), Math.Max(widest, Math.Min(TextFit.Width(_Message), 64))));
        }

        /// <summary>
        /// Content height: the message, a gap, the list, a gap, and the buttons.
        /// </summary>
        /// <param name="contentWidth">Width.</param>
        /// <returns>Height.</returns>
        protected override int MeasureContentHeight(int contentWidth)
        {
            int message = MessageLines(contentWidth).Count;
            return (message > 0 ? message + 1 : 0) + Math.Max(1, Math.Min(MaxRows, _Items.Count)) + 2;
        }

        /// <summary>
        /// Render the message, the rows, and the buttons.
        /// </summary>
        /// <param name="content">Surface.</param>
        protected override void RenderContent(ISurface content)
        {
            int width = content.Size.Width;
            IReadOnlyList<StyledText> lines = MessageLines(width);
            int row = 0;
            foreach (StyledText line in lines) content.DrawStyledText(0, row++, line);
            if (lines.Count > 0) row++;
            _ListRow = row;
            _ListHeight = Math.Max(1, content.Size.Height - row - 2);
            if (_Cursor < _Top) _Top = _Cursor;
            if (_Cursor >= _Top + _ListHeight) _Top = _Cursor - _ListHeight + 1;
            if (_Items.Count == 0) content.DrawText(0, row, "Nothing to choose.", _Theme.Muted);
            for (int i = 0; i < _ListHeight && _Top + i < _Items.Count; i++)
            {
                int index = _Top + i;
                CheckListItem item = _Items[index];
                string text = (item.Checked ? "[x] " : "[ ] ") + item.Label;
                if (TextFit.Width(text) > width) text = Truncate(text, width);
                CellStyle style = index == _Cursor ? _Theme.Selection : !item.Enabled ? _Theme.Disabled : item.Checked ? _Theme.Text : _Theme.Muted;
                content.DrawText(0, row + i, text.PadRight(Math.Max(0, width)), style);
            }

            int buttonRow = content.Size.Height - 1;
            int x = Math.Max(0, (width - ButtonsWidth()) / 2);
            _ButtonRects.Clear();
            for (int i = 0; i < _Buttons.Count; i++)
            {
                string label = "[ " + _Buttons[i] + " ]";
                int drawn = content.DrawText(x, buttonRow, label, i == _Selected ? _Theme.Selection : _Theme.Text);
                _ButtonRects.Add(new Rect(ContentBounds.X + x, ContentBounds.Y + buttonRow, drawn, 1));
                x += drawn + 1;
            }
        }

        #endregion

        #region Private-Methods

        private void MoveCursor(int delta)
        {
            if (_Items.Count == 0) return;
            _Cursor = Math.Clamp(_Cursor + delta, 0, _Items.Count - 1);
        }

        private void Finish(int button)
        {
            Close(new CheckListResult { Button = button, Checked = _Items.Select(item => item.Checked).ToList() });
        }

        private int ButtonsWidth()
        {
            return _Buttons.Sum(item => TextFit.Width(item) + 5) - 1;
        }

        private IReadOnlyList<StyledText> MessageLines(int width)
        {
            if (String.IsNullOrWhiteSpace(_Message)) return new List<StyledText>();
            return TextWrapper.Wrap(StyledText.From(_Message, _Theme.Muted), Math.Max(1, width));
        }

        #endregion
    }
}

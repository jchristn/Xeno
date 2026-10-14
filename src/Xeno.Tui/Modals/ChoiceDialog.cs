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
    /// A themed message with a row of buttons, for confirmations and follow-ups. Left, Right, and Tab move between
    /// buttons, Enter picks, Esc cancels, and a click on a button picks it. Completes with the button index, or -1
    /// when cancelled.
    /// </summary>
    internal sealed class ChoiceDialog : DialogModal, IThemeable
    {
        #region Public-Members

        /// <summary>
        /// Button labels.
        /// </summary>
        public IReadOnlyList<string> Buttons
        {
            get { return _Buttons; }
        }

        /// <summary>
        /// Highlighted button.
        /// </summary>
        public int SelectedIndex
        {
            get { return _Selected; }
        }

        #endregion

        #region Private-Members

        private readonly string _Message;
        private readonly List<string> _Buttons;
        private readonly List<Rect> _ButtonRects = new List<Rect>();
        private int _Selected = 0;
        private Theme _Theme = XenoTuiTheme.Dark;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a dialog.
        /// </summary>
        /// <param name="title">Title.</param>
        /// <param name="message">Message.</param>
        /// <param name="buttons">Button labels; the first is highlighted.</param>
        /// <exception cref="ArgumentException">There are no buttons.</exception>
        public ChoiceDialog(string title, string message, IEnumerable<string> buttons)
        {
            Title = " " + title + " ";
            _Message = message ?? String.Empty;
            _Buttons = (buttons ?? Enumerable.Empty<string>()).ToList();
            if (_Buttons.Count == 0) throw new ArgumentException("At least one button is required.", nameof(buttons));
            FooterHint = " ←→ choose  Enter select  Esc cancel ";
            MinContentWidth = 40;
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
        /// Move between buttons, pick, or cancel.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True.</returns>
        public override bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Left:
                    _Selected = _Selected > 0 ? _Selected - 1 : _Buttons.Count - 1;
                    return true;
                case KeyCode.Right:
                case KeyCode.Tab:
                    _Selected = (_Selected + 1) % _Buttons.Count;
                    return true;
                case KeyCode.Enter:
                    Close(_Selected);
                    return true;
                case KeyCode.Escape:
                    RequestClose(-1);
                    return true;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Pick a button on click; highlight it on hover.
        /// </summary>
        /// <param name="mouse">Mouse event in screen coordinates.</param>
        /// <returns>True.</returns>
        public override bool HandleMouse(MouseEvent mouse)
        {
            if (mouse == null) return true;
            int index = _ButtonRects.FindIndex(rect => rect.Contains(new Point(mouse.X, mouse.Y)));
            if (index < 0) return true;
            if (mouse.Kind == MouseEventKind.Move) _Selected = index;
            if (mouse.Kind == MouseEventKind.Press && mouse.Button == MouseButton.Left)
            {
                _Selected = index;
                Close(index);
            }

            return true;
        }

        #endregion

        #region Protected-Methods

        /// <summary>
        /// Content width: the message, within the dialog's limits.
        /// </summary>
        /// <param name="availableWidth">Available width.</param>
        /// <returns>Width.</returns>
        protected override int MeasureContentWidth(int availableWidth)
        {
            return Math.Min(availableWidth, Math.Max(ButtonsWidth(), TextFit.Width(_Message)));
        }

        /// <summary>
        /// Content height: the wrapped message, a gap, and the buttons.
        /// </summary>
        /// <param name="contentWidth">Width.</param>
        /// <returns>Height.</returns>
        protected override int MeasureContentHeight(int contentWidth)
        {
            return Lines(contentWidth).Count + 2;
        }

        /// <summary>
        /// Render the message and the buttons.
        /// </summary>
        /// <param name="content">Surface.</param>
        protected override void RenderContent(ISurface content)
        {
            int width = content.Size.Width;
            IReadOnlyList<StyledText> lines = Lines(width);
            for (int i = 0; i < lines.Count && i < content.Size.Height; i++) content.DrawStyledText(0, i, lines[i]);
            int row = Math.Min(content.Size.Height - 1, lines.Count + 1);
            int x = Math.Max(0, (width - ButtonsWidth()) / 2);
            _ButtonRects.Clear();
            for (int i = 0; i < _Buttons.Count; i++)
            {
                string label = "[ " + _Buttons[i] + " ]";
                int drawn = content.DrawText(x, row, label, i == _Selected ? _Theme.Selection : _Theme.Text);
                _ButtonRects.Add(new Rect(ContentBounds.X + x, ContentBounds.Y + row, drawn, 1));
                x += drawn + 1;
            }
        }

        #endregion

        #region Private-Methods

        private int ButtonsWidth()
        {
            return _Buttons.Sum(item => TextFit.Width(item) + 5) - 1;
        }

        private IReadOnlyList<StyledText> Lines(int width)
        {
            return TextWrapper.Wrap(StyledText.From(_Message, _Theme.Text), Math.Max(1, width));
        }

        #endregion
    }
}

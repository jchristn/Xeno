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
    using Xeno.Client.Presentation;
    using Xeno.Client.Services;

    /// <summary>
    /// The model step of Import Models: each provider's live models under its heading (or why it could not be read),
    /// a checkbox per model, and a completions-or-embeddings choice per model. Models already advertised are listed
    /// but cannot be picked. Up and Down move, Space toggles, e switches completions and embeddings, Enter imports, and
    /// Esc cancels; a click on a model toggles it, a click on its capability switches it, and the buttons take clicks.
    /// Completes with the <see cref="ModelImportItem"/> list to import, or null when cancelled.
    /// </summary>
    internal sealed class ModelImportModal : DialogModal, IThemeable
    {
        #region Public-Members

        /// <summary>
        /// Sections being chosen from.
        /// </summary>
        public IReadOnlyList<ModelImportSource> Sources
        {
            get { return _Sources; }
        }

        #endregion

        #region Private-Members

        private const int MaxRows = 18;
        private const int SizeWidth = 10;
        private const int CapabilityWidth = 15;
        private const int StatusWidth = 11;
        private static readonly List<string> Buttons = new List<string> { "Import", "Cancel" };
        private readonly List<ModelImportSource> _Sources;
        private readonly List<Line> _Lines = new List<Line>();
        private readonly List<Rect> _ButtonRects = new List<Rect>();
        private int _Cursor = -1;
        private int _Top = 0;
        private int _Selected = 0;
        private int _ListRow = 0;
        private int _ListHeight = 0;
        private Theme _Theme = XenoTuiTheme.Dark;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the dialog.
        /// </summary>
        /// <param name="sources">Sections from <see cref="XenoModelImport.LoadAsync"/>.</param>
        public ModelImportModal(IEnumerable<ModelImportSource> sources)
        {
            _Sources = (sources ?? Enumerable.Empty<ModelImportSource>()).ToList();
            foreach (ModelImportSource source in _Sources)
            {
                _Lines.Add(new Line(source, null));
                foreach (ModelImportCandidate candidate in source.Candidates) _Lines.Add(new Line(source, candidate));
            }

            _Cursor = _Lines.FindIndex(line => line.Candidate != null && !line.Candidate.Advertised);
            if (_Cursor < 0) _Cursor = _Lines.FindIndex(line => line.Candidate != null);
            Title = " " + XenoModelImport.Title + " ";
            FooterHint = " ↑↓ move  Space pick  e capability  Enter import  Esc cancel ";
            MinContentWidth = 64;
            MaxContentWidth = 100;
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
        /// Move, pick, switch the capability, import, or cancel.
        /// </summary>
        /// <param name="key">Key.</param>
        /// <returns>True.</returns>
        public override bool HandleKey(KeyEvent key)
        {
            switch (key.Code)
            {
                case KeyCode.Up:
                    Move(-1);
                    return true;
                case KeyCode.Down:
                    Move(1);
                    return true;
                case KeyCode.Left:
                    _Selected = _Selected > 0 ? _Selected - 1 : Buttons.Count - 1;
                    return true;
                case KeyCode.Right:
                case KeyCode.Tab:
                    _Selected = (_Selected + 1) % Buttons.Count;
                    return true;
                case KeyCode.Enter:
                    Finish(_Selected);
                    return true;
                case KeyCode.Escape:
                    RequestClose(null);
                    return true;
                case KeyCode.Character:
                    if (key.Rune == ' ') TogglePick(_Cursor);
                    else if (key.Rune == 'e' || key.Rune == 'E') ToggleCapability(_Cursor);
                    return true;
                default:
                    return true;
            }
        }

        /// <summary>
        /// Pick a clicked model, switch a clicked capability, press a clicked button, and scroll with the wheel.
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
                Move(mouse.Button == MouseButton.WheelDown ? 1 : -1);
                return true;
            }

            if (mouse.Kind != MouseEventKind.Press || mouse.Button != MouseButton.Left) return true;
            Rect content = ContentBounds;
            int row = mouse.Y - content.Y - _ListRow;
            int x = mouse.X - content.X;
            if (row < 0 || row >= _ListHeight || x < 0 || x >= content.Width) return true;
            int index = _Top + row;
            if (index >= _Lines.Count || _Lines[index].Candidate == null) return true;
            _Cursor = index;
            int capabilityStart = content.Width - StatusWidth - CapabilityWidth;
            if (x >= capabilityStart && x < capabilityStart + CapabilityWidth) ToggleCapability(index);
            else TogglePick(index);
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
            int widest = _Lines.Count == 0 ? 0 : _Lines.Max(line => line.Candidate == null ? TextFit.Width(line.Source.Heading) : TextFit.Width(line.Candidate.Name) + 4 + SizeWidth + CapabilityWidth + StatusWidth);
            return Math.Min(availableWidth, Math.Max(64, widest));
        }

        /// <summary>
        /// Content height: the intro, the list, the count, and the buttons.
        /// </summary>
        /// <param name="contentWidth">Width.</param>
        /// <returns>Height.</returns>
        protected override int MeasureContentHeight(int contentWidth)
        {
            return IntroLines(contentWidth).Count + 1 + Math.Max(1, Math.Min(MaxRows, _Lines.Count)) + 3;
        }

        /// <summary>
        /// Render the intro, the sections, the selection count, and the buttons.
        /// </summary>
        /// <param name="content">Surface.</param>
        protected override void RenderContent(ISurface content)
        {
            int width = content.Size.Width;
            int row = 0;
            foreach (StyledText line in IntroLines(width)) content.DrawStyledText(0, row++, line);
            row++;
            _ListRow = row;
            _ListHeight = Math.Max(1, content.Size.Height - row - 3);
            if (_Cursor >= 0 && _Cursor < _Top) _Top = _Cursor;
            if (_Cursor >= _Top + _ListHeight) _Top = _Cursor - _ListHeight + 1;
            if (_Lines.Count == 0) content.DrawText(0, row, "No providers chosen.", _Theme.Muted);
            for (int i = 0; i < _ListHeight && _Top + i < _Lines.Count; i++)
            {
                int index = _Top + i;
                Line line = _Lines[index];
                if (line.Candidate == null)
                {
                    content.DrawText(0, row + i, Fit(line.Source.Heading, width), line.Source.Reachable ? _Theme.Accent.WithAttribute(CellAttributes.Bold, true) : _Theme.Error);
                    continue;
                }

                ModelImportCandidate candidate = line.Candidate;
                int nameWidth = Math.Max(4, width - 4 - SizeWidth - CapabilityWidth - StatusWidth);
                string text = (candidate.Advertised ? "[-] " : candidate.Selected ? "[x] " : "[ ] ")
                    + Fit(candidate.Name, nameWidth).PadRight(nameWidth)
                    + Fit(candidate.Size, SizeWidth - 1).PadRight(SizeWidth)
                    + ("‹" + candidate.Capability + "›").PadRight(CapabilityWidth)
                    + candidate.Status.PadRight(StatusWidth);
                CellStyle style = index == _Cursor ? _Theme.Selection : candidate.Advertised ? _Theme.Disabled : candidate.Selected ? _Theme.Text : _Theme.Muted;
                content.DrawText(0, row + i, Fit(text, width).PadRight(width), style);
            }

            int count = XenoModelImport.SelectedCount(_Sources);
            content.DrawText(0, content.Size.Height - 2, count == 0 ? "No models selected." : count + " model" + (count == 1 ? String.Empty : "s") + " selected.", _Theme.Muted);
            int buttonRow = content.Size.Height - 1;
            int x = Math.Max(0, (width - (Buttons.Sum(item => item.Length + 5) - 1)) / 2);
            _ButtonRects.Clear();
            for (int i = 0; i < Buttons.Count; i++)
            {
                int drawn = content.DrawText(x, buttonRow, "[ " + Buttons[i] + " ]", i == _Selected ? _Theme.Selection : _Theme.Text);
                _ButtonRects.Add(new Rect(ContentBounds.X + x, ContentBounds.Y + buttonRow, drawn, 1));
                x += drawn + 1;
            }
        }

        #endregion

        #region Private-Methods

        private void Move(int delta)
        {
            if (_Lines.Count == 0) return;
            int index = _Cursor < 0 ? 0 : _Cursor;
            for (int step = 0; step < _Lines.Count; step++)
            {
                index += delta;
                if (index < 0 || index >= _Lines.Count) return;
                if (_Lines[index].Candidate != null)
                {
                    _Cursor = index;
                    return;
                }
            }
        }

        private void TogglePick(int index)
        {
            ModelImportCandidate? candidate = index >= 0 && index < _Lines.Count ? _Lines[index].Candidate : null;
            if (candidate == null || candidate.Advertised) return;
            candidate.Selected = !candidate.Selected;
        }

        private void ToggleCapability(int index)
        {
            ModelImportCandidate? candidate = index >= 0 && index < _Lines.Count ? _Lines[index].Candidate : null;
            if (candidate == null || candidate.Advertised) return;
            candidate.Embeddings = !candidate.Embeddings;
        }

        private void Finish(int button)
        {
            if (button != 0)
            {
                RequestClose(null);
                return;
            }

            Close(XenoModelImport.Items(_Sources));
        }

        private IReadOnlyList<StyledText> IntroLines(int width)
        {
            return TextWrapper.Wrap(StyledText.From(XenoModelImport.ModelsIntro, _Theme.Muted), Math.Max(1, width));
        }

        private static string Fit(string text, int width)
        {
            if (width <= 0) return String.Empty;
            return TextFit.Width(text) <= width ? text : Truncate(text, width);
        }

        #endregion

        #region Private-Classes

        private sealed class Line
        {
            internal Line(ModelImportSource source, ModelImportCandidate? candidate)
            {
                Source = source;
                Candidate = candidate;
            }

            internal ModelImportSource Source { get; }

            internal ModelImportCandidate? Candidate { get; }
        }

        #endregion
    }
}

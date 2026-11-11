namespace Xeno.Desktop.Common
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.Templates;
    using Avalonia.Input;
    using Avalonia.Layout;
    using Avalonia.Media;

    /// <summary>
    /// A sortable, selectable table built from a header row and a virtualized list. Rows can be activated with a
    /// double-click or Enter, and offer actions through a context menu. Call from the UI thread.
    /// </summary>
    /// <typeparam name="TRow">Row type.</typeparam>
    public sealed class XenoTable<TRow> : Border where TRow : class
    {
        #region Public-Members

        /// <summary>
        /// Raised when a row is double-clicked or Enter is pressed on it.
        /// </summary>
        public event EventHandler<TRow>? RowActivated;

        /// <summary>
        /// Raised when the selected row changes.
        /// </summary>
        public event EventHandler<TRow?>? SelectionChanged;

        /// <summary>
        /// Selected row, or null.
        /// </summary>
        public TRow? SelectedRow
        {
            get { return _List.SelectedItem as TRow; }
        }

        /// <summary>
        /// Rows currently shown, in display order.
        /// </summary>
        public IReadOnlyList<TRow> Rows
        {
            get { return _Rows; }
        }

        #endregion

        #region Private-Members

        private readonly List<XenoTableColumn<TRow>> _Columns;
        private readonly Func<TRow, IReadOnlyList<XenoRowAction<TRow>>>? _Actions;
        private readonly Grid _Header = new Grid();
        private readonly ListBox _List = new ListBox();
        private readonly TextBlock _Empty = new TextBlock();
        private List<TRow> _Rows = new List<TRow>();
        private int _SortColumn = -1;
        private bool _Descending = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a table.
        /// </summary>
        /// <param name="columns">Columns.</param>
        /// <param name="actions">Row actions for the context menu, or null.</param>
        /// <param name="emptyText">Text shown when there are no rows.</param>
        /// <exception cref="ArgumentNullException">Columns is null.</exception>
        public XenoTable(IEnumerable<XenoTableColumn<TRow>> columns, Func<TRow, IReadOnlyList<XenoRowAction<TRow>>>? actions, string emptyText)
        {
            ArgumentNullException.ThrowIfNull(columns);
            _Columns = columns.ToList();
            _Actions = actions;
            XenoPalette palette = XenoTheme.Current;
            BorderBrush = palette.Border;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(8);
            Background = palette.Panel;
            ClipToBounds = true;

            ApplyColumns(_Header);
            BuildHeader();
            Border headerHost = new Border
            {
                Background = palette.PanelSoft,
                BorderBrush = palette.Border,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 8),
                Child = _Header
            };

            _List.Background = Brushes.Transparent;
            _List.ItemTemplate = new FuncDataTemplate<TRow>((row, scope) => row is null ? new Panel() : BuildRow(row));
            _List.SelectionChanged += (sender, args) => SelectionChanged?.Invoke(this, SelectedRow);
            _List.DoubleTapped += (sender, args) => ActivateSelected();
            _List.KeyDown += (sender, args) =>
            {
                if (args.Key == Key.Enter)
                {
                    ActivateSelected();
                    args.Handled = true;
                }
            };

            _Empty.Text = emptyText;
            _Empty.Foreground = palette.Muted;
            _Empty.Margin = new Thickness(14, 16);
            _Empty.TextWrapping = TextWrapping.Wrap;

            DockPanel root = new DockPanel();
            DockPanel.SetDock(headerHost, Dock.Top);
            root.Children.Add(headerHost);
            Panel body = new Panel();
            body.Children.Add(_List);
            body.Children.Add(_Empty);
            root.Children.Add(body);
            Child = root;
            UpdateEmpty();
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Replace the rows, keeping the current sort and, when possible, the selection.
        /// </summary>
        /// <param name="rows">Rows.</param>
        /// <param name="sameRow">Identity comparer used to restore the selection, or null.</param>
        public void SetRows(IEnumerable<TRow> rows, Func<TRow, TRow, bool>? sameRow = null)
        {
            TRow? previous = SelectedRow;
            _Rows = rows == null ? new List<TRow>() : rows.ToList();
            ApplySort();
            if (previous != null && sameRow != null)
            {
                TRow? match = _Rows.FirstOrDefault(item => sameRow(item, previous));
                if (match != null) _List.SelectedItem = match;
            }
        }

        /// <summary>
        /// Set the text shown when there are no rows.
        /// </summary>
        /// <param name="text">Text.</param>
        public void SetEmptyText(string text)
        {
            _Empty.Text = text;
        }

        /// <summary>
        /// Move keyboard focus to the table.
        /// </summary>
        public void FocusList()
        {
            _List.Focus();
        }

        #endregion

        #region Private-Methods

        private void ApplyColumns(Grid grid)
        {
            grid.ColumnDefinitions.Clear();
            foreach (XenoTableColumn<TRow> column in _Columns)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(column.IsStar ? new GridLength(column.Width, GridUnitType.Star) : new GridLength(column.Width)));
            }
        }

        private void BuildHeader()
        {
            _Header.Children.Clear();
            XenoPalette palette = XenoTheme.Current;
            for (int i = 0; i < _Columns.Count; i++)
            {
                int index = i;
                string arrow = _SortColumn == i ? (_Descending ? " ▾" : " ▴") : String.Empty;
                TextBlock text = new TextBlock
                {
                    Text = _Columns[i].Header.ToUpperInvariant() + arrow,
                    FontSize = 11,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = palette.Muted,
                    Margin = new Thickness(0, 0, 10, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Cursor = new Cursor(StandardCursorType.Hand)
                };
                text.PointerPressed += (sender, args) => SortBy(index);
                Grid.SetColumn(text, i);
                _Header.Children.Add(text);
            }
        }

        private Control BuildRow(TRow row)
        {
            XenoPalette palette = XenoTheme.Current;
            Grid grid = new Grid { Margin = new Thickness(0, 2) };
            ApplyColumns(grid);
            for (int i = 0; i < _Columns.Count; i++)
            {
                XenoTableColumn<TRow> column = _Columns[i];
                Control cell;
                if (column.Cell != null)
                {
                    cell = column.Cell(row);
                }
                else
                {
                    string value = SafeText(column, row);
                    TextBlock text = new TextBlock
                    {
                        Text = value,
                        Foreground = palette.Text,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    if (column.Monospace)
                    {
                        text.FontFamily = Ui.Mono;
                        text.FontSize = 12.5;
                    }

                    if (value.Length > 24) ToolTip.SetTip(text, value);
                    cell = text;
                }

                cell.Margin = new Thickness(0, 0, 10, 0);
                Grid.SetColumn(cell, i);
                grid.Children.Add(cell);
            }

            if (_Actions != null)
            {
                IReadOnlyList<XenoRowAction<TRow>> actions = _Actions(row);
                if (actions.Count > 0)
                {
                    ContextMenu menu = new ContextMenu();
                    foreach (XenoRowAction<TRow> action in actions)
                    {
                        MenuItem item = new MenuItem { Header = action.Header };
                        if (action.Destructive) item.Foreground = palette.Danger;
                        item.Click += (sender, args) => action.Invoke(row);
                        menu.Items.Add(item);
                    }

                    grid.ContextMenu = menu;
                }
            }

            grid.Background = Brushes.Transparent;
            return grid;
        }

        private void SortBy(int column)
        {
            if (_SortColumn == column) _Descending = !_Descending;
            else
            {
                _SortColumn = column;
                _Descending = false;
            }

            BuildHeader();
            ApplySort();
        }

        private void ApplySort()
        {
            IEnumerable<TRow> ordered = _Rows;
            if (_SortColumn >= 0 && _SortColumn < _Columns.Count)
            {
                XenoTableColumn<TRow> column = _Columns[_SortColumn];
                Func<TRow, IComparable?> key = column.SortKey ?? (row => SafeText(column, row));
                ordered = _Descending
                    ? _Rows.OrderByDescending(key, Comparer<IComparable?>.Create(Compare))
                    : _Rows.OrderBy(key, Comparer<IComparable?>.Create(Compare));
            }

            _Rows = ordered.ToList();
            _List.ItemsSource = _Rows;
            UpdateEmpty();
        }

        private static int Compare(IComparable? left, IComparable? right)
        {
            if (left == null && right == null) return 0;
            if (left == null) return -1;
            if (right == null) return 1;
            if (left is string leftText && right is string rightText) return String.Compare(leftText, rightText, StringComparison.OrdinalIgnoreCase);
            try
            {
                return left.CompareTo(right);
            }
            catch (ArgumentException)
            {
                return String.Compare(Convert.ToString(left, CultureInfo.InvariantCulture), Convert.ToString(right, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
            }
        }

        private static string SafeText(XenoTableColumn<TRow> column, TRow? row)
        {
            // The list can build a row container before its item arrives.
            if (row is null) return String.Empty;
            return column.Text(row) ?? String.Empty;
        }

        private void ActivateSelected()
        {
            TRow? row = SelectedRow;
            if (row != null) RowActivated?.Invoke(this, row);
        }

        private void UpdateEmpty()
        {
            _Empty.IsVisible = _Rows.Count == 0;
        }

        #endregion
    }
}

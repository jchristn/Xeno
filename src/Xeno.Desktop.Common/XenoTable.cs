namespace Xeno.Desktop.Common
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using Avalonia;
    using Avalonia.Controls;
    using Avalonia.Controls.Primitives;
    using Avalonia.Controls.Templates;
    using Avalonia.Input;
    using Avalonia.Layout;
    using Avalonia.Media;
    using Xeno.Client.Presentation;

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
        /// Select the first row when nothing is selected.
        /// </summary>
        public void SelectFirstIfNone()
        {
            if (_List.SelectedItem == null && _Rows.Count > 0) _List.SelectedItem = _Rows[0];
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
        private readonly string _TableId;
        private List<XenoTableColumn<TRow>> _Visible = new List<XenoTableColumn<TRow>>();
        private readonly Button _ColumnsButton;
        private readonly Func<TRow, IReadOnlyList<XenoRowAction<TRow>>>? _Actions;
        private readonly Grid _Header = new Grid();
        private readonly ListBox _List = new ListBox();
        private readonly TextBlock _Empty = new TextBlock();
        private List<TRow> _Rows = new List<TRow>();
        private XenoTableColumn<TRow>? _SortColumn = null;
        private bool _Descending = false;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a table.
        /// </summary>
        /// <param name="columns">Columns.</param>
        /// <param name="actions">Row actions for the context menu, or null.</param>
        /// <param name="emptyText">Text shown when there are no rows.</param>
        /// <param name="tableId">Identifies the table's stored column choice, or null to derive it from the row type and headers. Pass one when two tables share a row type and headers.</param>
        /// <exception cref="ArgumentNullException">Columns is null.</exception>
        public XenoTable(IEnumerable<XenoTableColumn<TRow>> columns, Func<TRow, IReadOnlyList<XenoRowAction<TRow>>>? actions, string emptyText, string? tableId = null)
        {
            ArgumentNullException.ThrowIfNull(columns);
            _Columns = columns.ToList();
            _Actions = actions;
            _TableId = XenoTableColumns.TableId("desktop", tableId ?? typeof(TRow).Name, _Columns.Select(item => item.Header));
            _Visible = ResolveVisible();
            XenoPalette palette = XenoTheme.Current;
            BorderBrush = palette.Border;
            BorderThickness = new Thickness(1);
            CornerRadius = new CornerRadius(8);
            Background = palette.Panel;
            ClipToBounds = true;

            ApplyColumns(_Header);
            BuildHeader();
            _ColumnsButton = new Button
            {
                Content = XenoTableColumns.ControlText,
                FontSize = 11,
                Padding = new Thickness(8, 1),
                Margin = new Thickness(8, -2, -4, -2),
                Background = Brushes.Transparent,
                Foreground = palette.Muted,
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            ToolTip.SetTip(_ColumnsButton, "Choose which columns this table shows.");
            _ColumnsButton.Click += (sender, args) => ShowColumnChooser();
            DockPanel headerRow = new DockPanel();
            DockPanel.SetDock(_ColumnsButton, Dock.Right);
            headerRow.Children.Add(_ColumnsButton);
            headerRow.Children.Add(_Header);
            Border headerHost = new Border
            {
                Background = palette.PanelSoft,
                BorderBrush = palette.Border,
                BorderThickness = new Thickness(0, 0, 0, 1),
                Padding = new Thickness(12, 8),
                Child = headerRow
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

        /// <summary>
        /// Every column header, in display order.
        /// </summary>
        public IReadOnlyList<string> ColumnHeaders
        {
            get { return _Columns.Select(item => item.Header).ToList(); }
        }

        /// <summary>
        /// Headers of the columns shown now, in display order.
        /// </summary>
        public IReadOnlyList<string> VisibleColumnHeaders
        {
            get { return _Visible.Select(item => item.Header).ToList(); }
        }

        /// <summary>
        /// Show exactly the named columns, remember the choice, and redraw. Unknown headers are ignored; a choice that
        /// would hide every column is refused.
        /// </summary>
        /// <param name="headers">Headers to show.</param>
        /// <returns>True when applied.</returns>
        public bool SetVisibleColumns(IEnumerable<string> headers)
        {
            ArgumentNullException.ThrowIfNull(headers);
            List<string> chosen = headers.ToList();
            List<XenoTableColumn<TRow>> visible = _Columns.Where(item => chosen.Contains(item.Header, StringComparer.Ordinal)).ToList();
            if (visible.Count == 0) return false;
            _Visible = visible;
            XenoFrontEndPreferences? preferences = XenoTableColumnStore.Preferences;
            if (preferences != null)
            {
                XenoTableColumns.Save(preferences, _TableId, _Visible.Select(item => item.Header));
                XenoTableColumnStore.Save?.Invoke();
            }

            Relayout();
            return true;
        }

        /// <summary>
        /// Show the table's default columns again and forget the stored choice.
        /// </summary>
        public void ResetColumns()
        {
            XenoFrontEndPreferences? preferences = XenoTableColumnStore.Preferences;
            if (preferences != null && XenoTableColumns.Reset(preferences, _TableId)) XenoTableColumnStore.Save?.Invoke();
            _Visible = ResolveVisible();
            Relayout();
        }

        #endregion

        #region Private-Methods

        private List<XenoTableColumn<TRow>> ResolveVisible()
        {
            List<string> headers = _Columns.Select(item => item.Header).ToList();
            List<string> defaults = _Columns.Where(item => item.DefaultVisible).Select(item => item.Header).ToList();
            XenoFrontEndPreferences? preferences = XenoTableColumnStore.Preferences;
            List<string> visible = preferences == null ? XenoTableColumns.Defaults(headers, defaults) : XenoTableColumns.Visible(preferences, _TableId, headers, defaults);
            return _Columns.Where(item => visible.Contains(item.Header, StringComparer.Ordinal)).ToList();
        }

        private void Relayout()
        {
            ApplyColumns(_Header);
            BuildHeader();
            TRow? selected = SelectedRow;
            _List.ItemsSource = null;
            _List.ItemsSource = _Rows;
            if (selected != null) _List.SelectedItem = selected;
        }

        private void ShowColumnChooser()
        {
            XenoPalette palette = XenoTheme.Current;
            StackPanel stack = new StackPanel { Spacing = 4, Margin = new Thickness(4), MinWidth = 200 };
            stack.Children.Add(new TextBlock { Text = "Show columns", FontWeight = FontWeight.SemiBold, Foreground = palette.Text, Margin = new Thickness(0, 0, 0, 4) });
            List<CheckBox> boxes = new List<CheckBox>();
            foreach (XenoTableColumn<TRow> column in _Columns)
            {
                CheckBox box = new CheckBox { Content = column.Header, IsChecked = _Visible.Contains(column), Tag = column.Header };
                boxes.Add(box);
                stack.Children.Add(box);
            }

            void Sync()
            {
                int checkedCount = boxes.Count(item => item.IsChecked == true);
                foreach (CheckBox item in boxes) item.IsEnabled = item.IsChecked != true || checkedCount > 1;
            }

            foreach (CheckBox box in boxes)
            {
                box.IsCheckedChanged += (sender, args) =>
                {
                    SetVisibleColumns(boxes.Where(item => item.IsChecked == true).Select(item => (string)item.Tag!));
                    Sync();
                };
            }

            Sync();
            Button reset = Ui.Button("Show Default Columns", () =>
            {
                ResetColumns();
                foreach (CheckBox item in boxes) item.IsChecked = _Visible.Any(column => String.Equals(column.Header, (string)item.Tag!, StringComparison.Ordinal));
                Sync();
            });
            reset.Margin = new Thickness(0, 6, 0, 0);
            stack.Children.Add(reset);
            Flyout flyout = new Flyout { Content = stack, Placement = PlacementMode.BottomEdgeAlignedRight };
            flyout.ShowAt(_ColumnsButton);
        }

        private void ApplyColumns(Grid grid)
        {
            grid.ColumnDefinitions.Clear();
            foreach (XenoTableColumn<TRow> column in _Visible)
            {
                grid.ColumnDefinitions.Add(new ColumnDefinition(column.IsStar ? new GridLength(column.Width, GridUnitType.Star) : new GridLength(column.Width)));
            }
        }

        private void BuildHeader()
        {
            _Header.Children.Clear();
            XenoPalette palette = XenoTheme.Current;
            for (int i = 0; i < _Visible.Count; i++)
            {
                XenoTableColumn<TRow> sortColumn = _Visible[i];
                string arrow = ReferenceEquals(_SortColumn, sortColumn) ? (_Descending ? " ▾" : " ▴") : String.Empty;
                TextBlock text = new TextBlock
                {
                    Text = sortColumn.Header.ToUpperInvariant() + arrow,
                    FontSize = 11,
                    FontWeight = FontWeight.SemiBold,
                    Foreground = palette.Muted,
                    Margin = new Thickness(0, 0, 10, 0),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    Cursor = new Cursor(StandardCursorType.Hand)
                };
                text.PointerPressed += (sender, args) => SortBy(sortColumn);
                Grid.SetColumn(text, i);
                _Header.Children.Add(text);
            }
        }

        private Control BuildRow(TRow row)
        {
            XenoPalette palette = XenoTheme.Current;
            Grid grid = new Grid { Margin = new Thickness(0, 2) };
            ApplyColumns(grid);
            for (int i = 0; i < _Visible.Count; i++)
            {
                XenoTableColumn<TRow> column = _Visible[i];
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

        private void SortBy(XenoTableColumn<TRow> column)
        {
            if (ReferenceEquals(_SortColumn, column)) _Descending = !_Descending;
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
            if (_SortColumn != null)
            {
                XenoTableColumn<TRow> column = _SortColumn;
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

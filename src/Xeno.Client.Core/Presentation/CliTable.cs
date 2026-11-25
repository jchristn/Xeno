namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using Xeno.Client.Services;

    /// <summary>
    /// A plain-text table for the xeno CLI. Every CLI table goes through this class so --columns works the same everywhere:
    /// columns are picked by name (case-insensitive), "all" shows every column, and an unknown name is an error that lists
    /// the valid ones. Default column sets are chosen to fit a 120-column terminal.
    /// </summary>
    public sealed class CliTable
    {
        #region Public-Members

        /// <summary>
        /// The --columns value that shows every column.
        /// </summary>
        public const string AllColumns = "all";

        /// <summary>
        /// Spaces between columns.
        /// </summary>
        public const int Gap = 2;

        /// <summary>
        /// Columns, in display order.
        /// </summary>
        public List<CliTableColumn> Columns { get; } = new List<CliTableColumn>();

        /// <summary>
        /// Rows; each holds one value per column, in column order.
        /// </summary>
        public List<List<string>> Rows { get; } = new List<List<string>>();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Add a column.
        /// </summary>
        /// <param name="name">Name for --columns.</param>
        /// <param name="header">Header text.</param>
        /// <param name="maxWidth">Longest value before clipping; 0 for no limit.</param>
        /// <param name="rightAligned">Right-align values.</param>
        /// <param name="defaultVisible">Shown without --columns.</param>
        /// <returns>This table.</returns>
        /// <exception cref="ArgumentException">The name is empty or already used.</exception>
        public CliTable AddColumn(string name, string header, int maxWidth = 0, bool rightAligned = false, bool defaultVisible = true)
        {
            if (String.IsNullOrWhiteSpace(name)) throw new ArgumentException("Column name is required.", nameof(name));
            if (Columns.Any(item => String.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Duplicate column name: " + name, nameof(name));
            Columns.Add(new CliTableColumn
            {
                Name = name.Trim(),
                Header = header ?? String.Empty,
                MaxWidth = Math.Max(0, maxWidth),
                RightAligned = rightAligned,
                DefaultVisible = defaultVisible
            });
            return this;
        }

        /// <summary>
        /// Add a row with one value per column.
        /// </summary>
        /// <param name="values">Values in column order; null shows as empty.</param>
        /// <exception cref="ArgumentException">The value count does not match the column count.</exception>
        public void AddRow(params string?[] values)
        {
            ArgumentNullException.ThrowIfNull(values);
            if (values.Length != Columns.Count) throw new ArgumentException("Expected " + Columns.Count + " values but got " + values.Length + ".", nameof(values));
            Rows.Add(values.Select(item => (item ?? String.Empty).Replace('\r', ' ').Replace('\n', ' ')).ToList());
        }

        /// <summary>
        /// The column names, comma separated, for messages.
        /// </summary>
        /// <returns>Names.</returns>
        public string ColumnNames()
        {
            return String.Join(", ", Columns.Select(item => item.Name));
        }

        /// <summary>
        /// Resolve a --columns value to columns. Null or blank gives the default columns; "all" gives every column.
        /// </summary>
        /// <param name="spec">Comma-separated column names, "all", or null.</param>
        /// <returns>Columns to show, in the order given.</returns>
        /// <exception cref="ArgumentException">A name is unknown or no name was given.</exception>
        public List<CliTableColumn> SelectColumns(string? spec)
        {
            if (spec == null) return Columns.Where(item => item.DefaultVisible).ToList();
            List<string> names = spec.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            if (names.Count == 0) throw new ArgumentException("--columns needs at least one column name. Valid columns: " + ColumnNames() + ".");
            if (names.Count == 1 && String.Equals(names[0], AllColumns, StringComparison.OrdinalIgnoreCase)) return Columns.ToList();
            List<string> unknown = names.Where(name => !Columns.Any(item => String.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase))).ToList();
            if (unknown.Count > 0)
            {
                throw new ArgumentException("Unknown column" + (unknown.Count == 1 ? " '" + unknown[0] + "'" : "s '" + String.Join("', '", unknown) + "'") + ". Valid columns: " + ColumnNames() + " (or 'all').");
            }

            List<CliTableColumn> selected = new List<CliTableColumn>();
            foreach (string name in names)
            {
                CliTableColumn column = Columns.First(item => String.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
                if (!selected.Contains(column)) selected.Add(column);
            }

            return selected;
        }

        /// <summary>
        /// Render the header and rows as lines. Values longer than a column's limit are clipped with an ellipsis; trailing
        /// spaces are trimmed.
        /// </summary>
        /// <param name="spec">--columns value, or null for the default columns.</param>
        /// <param name="indent">Prefix for every line.</param>
        /// <returns>Lines.</returns>
        /// <exception cref="ArgumentException">The --columns value is invalid.</exception>
        public List<string> Render(string? spec, string indent = "  ")
        {
            List<CliTableColumn> selected = SelectColumns(spec);
            List<int> indexes = selected.Select(item => Columns.IndexOf(item)).ToList();
            List<List<string>> cells = Rows.Select(row => indexes.Select(index => Clip(row[index], Columns[index].MaxWidth)).ToList()).ToList();
            List<int> widths = new List<int>();
            for (int i = 0; i < selected.Count; i++)
            {
                int width = selected[i].Header.Length;
                foreach (List<string> row in cells) width = Math.Max(width, row[i].Length);
                widths.Add(width);
            }

            List<string> lines = new List<string>();
            lines.Add(FormatLine(indent, selected.Select(item => item.Header).ToList(), selected, widths));
            foreach (List<string> row in cells) lines.Add(FormatLine(indent, row, selected, widths));
            return lines;
        }

        /// <summary>
        /// The width of the rendered table, without its indent.
        /// </summary>
        /// <param name="spec">--columns value, or null for the default columns.</param>
        /// <returns>Characters.</returns>
        /// <exception cref="ArgumentException">The --columns value is invalid.</exception>
        public int Width(string? spec)
        {
            return Render(spec, String.Empty).Select(item => item.Length).DefaultIfEmpty(0).Max();
        }

        #endregion

        #region Private-Methods

        private static string Clip(string value, int maxWidth)
        {
            return maxWidth > 0 && value.Length > maxWidth ? DisplayFormat.Truncate(value, maxWidth) : value;
        }

        private static string FormatLine(string indent, List<string> values, List<CliTableColumn> columns, List<int> widths)
        {
            StringBuilder builder = new StringBuilder(indent);
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0) builder.Append(' ', Gap);
                builder.Append(columns[i].RightAligned ? values[i].PadLeft(widths[i]) : values[i].PadRight(widths[i]));
            }

            return builder.ToString().TrimEnd();
        }

        #endregion
    }
}

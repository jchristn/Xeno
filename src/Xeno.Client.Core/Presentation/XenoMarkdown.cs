namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;

    /// <summary>
    /// Builds Markdown safely from untrusted values (request bodies, headers, model names), so the terminal app can
    /// render detail views with its Markdown engine. Values are escaped or fenced so they never become markup.
    /// </summary>
    public static class XenoMarkdown
    {
        #region Private-Members

        private const string SpecialCharacters = "\\`*_{}[]<>()#+-.!|~";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Escape text so it renders literally inside a paragraph or table cell. Line breaks become spaces.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <returns>Escaped text.</returns>
        public static string Escape(string? text)
        {
            if (String.IsNullOrEmpty(text)) return String.Empty;
            StringBuilder builder = new StringBuilder(text.Length + 8);
            foreach (char c in text)
            {
                if (c == '\r') continue;
                if (c == '\n' || c == '\t')
                {
                    builder.Append(' ');
                    continue;
                }

                if (SpecialCharacters.IndexOf(c) >= 0) builder.Append('\\');
                builder.Append(c);
            }

            return builder.ToString();
        }

        /// <summary>
        /// A level-two heading.
        /// </summary>
        /// <param name="text">Heading text.</param>
        /// <returns>Markdown.</returns>
        public static string Heading(string text)
        {
            return "## " + Escape(text);
        }

        /// <summary>
        /// A fenced code block that shows the text verbatim. The fence is longer than any backtick run in the text,
        /// so the text cannot close it.
        /// </summary>
        /// <param name="text">Text.</param>
        /// <param name="language">Language for highlighting, or null.</param>
        /// <returns>Markdown.</returns>
        public static string CodeBlock(string? text, string? language = null)
        {
            string body = (text ?? String.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n').TrimEnd('\n');
            string fence = new string('`', Math.Max(3, LongestRun(body, '`') + 1));
            return fence + (String.IsNullOrWhiteSpace(language) ? String.Empty : language.Trim()) + "\n" + body + "\n" + fence;
        }

        /// <summary>
        /// A table. Cells are escaped; a row shorter than the header is padded with empty cells.
        /// </summary>
        /// <param name="headers">Column headers.</param>
        /// <param name="rows">Rows of cell values.</param>
        /// <returns>Markdown.</returns>
        /// <exception cref="ArgumentNullException">Headers or rows is null.</exception>
        /// <exception cref="ArgumentException">There are no headers.</exception>
        public static string Table(IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string>> rows)
        {
            ArgumentNullException.ThrowIfNull(headers);
            ArgumentNullException.ThrowIfNull(rows);
            if (headers.Count == 0) throw new ArgumentException("A table needs at least one column.", nameof(headers));
            StringBuilder builder = new StringBuilder();
            builder.Append("| ").Append(String.Join(" | ", headers.Select(Cell))).Append(" |\n");
            builder.Append('|').Append(String.Join("|", headers.Select(item => " --- "))).Append("|\n");
            foreach (IReadOnlyList<string> row in rows)
            {
                List<string> cells = new List<string>();
                for (int i = 0; i < headers.Count; i++) cells.Add(i < row.Count ? Cell(row[i]) : String.Empty);
                builder.Append("| ").Append(String.Join(" | ", cells)).Append(" |\n");
            }

            return builder.ToString().TrimEnd('\n');
        }

        /// <summary>
        /// A two-column table of label and value pairs.
        /// </summary>
        /// <param name="pairs">Pairs.</param>
        /// <param name="keyHeader">Header of the label column.</param>
        /// <param name="valueHeader">Header of the value column.</param>
        /// <returns>Markdown.</returns>
        /// <exception cref="ArgumentNullException">Pairs is null.</exception>
        public static string Details(IEnumerable<KeyValuePair<string, string>> pairs, string keyHeader = "Field", string valueHeader = "Value")
        {
            ArgumentNullException.ThrowIfNull(pairs);
            return Table(new List<string> { keyHeader, valueHeader }, pairs.Select(item => (IReadOnlyList<string>)new List<string> { item.Key, item.Value }));
        }

        /// <summary>
        /// Join blocks with a blank line between them, skipping empty ones.
        /// </summary>
        /// <param name="blocks">Blocks.</param>
        /// <returns>Markdown.</returns>
        public static string Join(IEnumerable<string?> blocks)
        {
            ArgumentNullException.ThrowIfNull(blocks);
            return String.Join("\n\n", blocks.Where(item => !String.IsNullOrWhiteSpace(item)));
        }

        #endregion

        #region Private-Methods

        private static string Cell(string? text)
        {
            string escaped = Escape(text);
            return escaped.Length == 0 ? " " : escaped;
        }

        private static int LongestRun(string text, char c)
        {
            int longest = 0;
            int current = 0;
            foreach (char item in text)
            {
                current = item == c ? current + 1 : 0;
                longest = Math.Max(longest, current);
            }

            return longest;
        }

        #endregion
    }
}

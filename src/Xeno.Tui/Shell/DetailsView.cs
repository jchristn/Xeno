namespace Xeno.Tui.Shell
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using TUIKit;
    using TUIKit.Theming;
    using TUIKit.Widgets;

    /// <summary>
    /// Label and value rows with the values left-aligned in a column after the longest label, for status and detail
    /// panels. Values are truncated to the panel width. Mutate on the loop thread.
    /// </summary>
    internal sealed class DetailsView : IWidget, IThemeable
    {
        #region Private-Members

        private readonly List<KeyValuePair<string, string>> _Rows = new List<KeyValuePair<string, string>>();
        private Theme _Theme = XenoTuiTheme.Dark;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Set a row's value, adding the row when the label is new.
        /// </summary>
        /// <param name="label">Label.</param>
        /// <param name="value">Value.</param>
        public void Set(string label, string? value)
        {
            int index = _Rows.FindIndex(item => String.Equals(item.Key, label, StringComparison.Ordinal));
            KeyValuePair<string, string> row = new KeyValuePair<string, string>(label, value ?? String.Empty);
            if (index >= 0) _Rows[index] = row;
            else _Rows.Add(row);
        }

        /// <summary>
        /// Remove every row.
        /// </summary>
        public void Clear()
        {
            _Rows.Clear();
        }

        /// <summary>
        /// Value for a label, for tests.
        /// </summary>
        /// <param name="label">Label.</param>
        /// <returns>Value, or null.</returns>
        public string? Get(string label)
        {
            KeyValuePair<string, string> row = _Rows.FirstOrDefault(item => String.Equals(item.Key, label, StringComparison.Ordinal));
            return row.Key == null ? null : row.Value;
        }

        /// <summary>
        /// Measure.
        /// </summary>
        /// <param name="available">Available size.</param>
        /// <returns>Desired size.</returns>
        public Size Measure(Size available)
        {
            return new Size(available.Width, Math.Min(_Rows.Count, available.Height));
        }

        /// <summary>
        /// Render the rows.
        /// </summary>
        /// <param name="surface">Surface.</param>
        public void Render(ISurface surface)
        {
            int width = surface.Size.Width;
            if (width <= 0 || _Rows.Count == 0) return;
            int labelWidth = Math.Min(_Rows.Max(item => item.Key.Length) + 2, Math.Max(4, width / 2));
            for (int i = 0; i < _Rows.Count && i < surface.Size.Height; i++)
            {
                surface.DrawText(0, i, Fit(_Rows[i].Key, labelWidth - 1), _Theme.Muted);
                surface.DrawText(labelWidth, i, Fit(_Rows[i].Value, width - labelWidth), _Theme.Text);
            }
        }

        /// <summary>
        /// Apply a theme.
        /// </summary>
        /// <param name="theme">Theme.</param>
        public void ApplyTheme(Theme theme)
        {
            _Theme = theme ?? XenoTuiTheme.Dark;
        }

        #endregion

        #region Private-Methods

        private static string Fit(string text, int width)
        {
            if (width <= 0) return String.Empty;
            if (text.Length <= width) return text;
            return width <= 1 ? text.Substring(0, width) : text.Substring(0, width - 1) + "…";
        }

        #endregion
    }
}

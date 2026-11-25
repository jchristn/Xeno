namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Which columns a table shows. Every table in the desktop and terminal apps has a default set chosen to fit without
    /// horizontal scrolling (at most <see cref="MaxDefaultVisible"/> columns), and users choose their own set with the
    /// table's Columns control. Choices are stored per table in <see cref="XenoFrontEndPreferences.TableColumns"/>.
    /// </summary>
    public static class XenoTableColumns
    {
        #region Public-Members

        /// <summary>
        /// Most columns a wide table shows by default; the rest start hidden and can be turned on.
        /// </summary>
        public const int MaxDefaultVisible = 6;

        /// <summary>
        /// Label of the Columns control.
        /// </summary>
        public const string ControlText = "Columns";

        #endregion

        #region Public-Methods

        /// <summary>
        /// Visible columns for a table: the stored choice when there is one (ignoring columns the table no longer has),
        /// otherwise the defaults. Never empty when the table has columns.
        /// </summary>
        /// <param name="preferences">Preferences.</param>
        /// <param name="tableId">Table id.</param>
        /// <param name="columns">Every column header, in display order.</param>
        /// <param name="defaults">Headers visible by default.</param>
        /// <returns>Visible headers, in display order.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static List<string> Visible(XenoFrontEndPreferences preferences, string tableId, IReadOnlyList<string> columns, IReadOnlyCollection<string> defaults)
        {
            ArgumentNullException.ThrowIfNull(preferences);
            ArgumentNullException.ThrowIfNull(tableId);
            ArgumentNullException.ThrowIfNull(columns);
            ArgumentNullException.ThrowIfNull(defaults);
            if (preferences.TableColumns.TryGetValue(tableId, out List<string>? stored) && stored != null)
            {
                List<string> chosen = columns.Where(column => stored.Contains(column, StringComparer.Ordinal)).ToList();
                if (chosen.Count > 0) return chosen;
            }

            return Defaults(columns, defaults);
        }

        /// <summary>
        /// Default visible columns: the given defaults in display order, or the first column when none match.
        /// </summary>
        /// <param name="columns">Every column header, in display order.</param>
        /// <param name="defaults">Headers visible by default.</param>
        /// <returns>Visible headers.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static List<string> Defaults(IReadOnlyList<string> columns, IReadOnlyCollection<string> defaults)
        {
            ArgumentNullException.ThrowIfNull(columns);
            ArgumentNullException.ThrowIfNull(defaults);
            List<string> visible = columns.Where(column => defaults.Contains(column, StringComparer.Ordinal)).ToList();
            if (visible.Count == 0 && columns.Count > 0) visible.Add(columns[0]);
            return visible;
        }

        /// <summary>
        /// Store a table's visible columns. An empty choice is ignored, since a table always shows a column.
        /// </summary>
        /// <param name="preferences">Preferences.</param>
        /// <param name="tableId">Table id.</param>
        /// <param name="visible">Visible headers.</param>
        /// <returns>True when stored.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static bool Save(XenoFrontEndPreferences preferences, string tableId, IEnumerable<string> visible)
        {
            ArgumentNullException.ThrowIfNull(preferences);
            ArgumentNullException.ThrowIfNull(tableId);
            ArgumentNullException.ThrowIfNull(visible);
            List<string> columns = visible.Where(item => !String.IsNullOrWhiteSpace(item)).Distinct(StringComparer.Ordinal).ToList();
            if (columns.Count == 0) return false;
            preferences.TableColumns[tableId] = columns;
            return true;
        }

        /// <summary>
        /// Forget a table's choice so it shows its default columns again.
        /// </summary>
        /// <param name="preferences">Preferences.</param>
        /// <param name="tableId">Table id.</param>
        /// <returns>True when a choice was removed.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static bool Reset(XenoFrontEndPreferences preferences, string tableId)
        {
            ArgumentNullException.ThrowIfNull(preferences);
            ArgumentNullException.ThrowIfNull(tableId);
            return preferences.TableColumns.Remove(tableId);
        }

        /// <summary>
        /// Build a stable table id from the surface, a scope (such as the page), and the table's column headers.
        /// </summary>
        /// <param name="surface">Surface, for example "desktop" or "terminal".</param>
        /// <param name="scope">Scope, for example the page or row type.</param>
        /// <param name="columns">Every column header.</param>
        /// <returns>Table id.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        public static string TableId(string surface, string scope, IEnumerable<string> columns)
        {
            ArgumentNullException.ThrowIfNull(surface);
            ArgumentNullException.ThrowIfNull(scope);
            ArgumentNullException.ThrowIfNull(columns);
            return surface + ":" + scope + ":" + String.Join("|", columns);
        }

        #endregion
    }
}

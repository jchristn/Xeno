namespace Xeno.Client.LocalDashboard.Database.Sqlite
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// SQLite identifier sanitizer for local dashboard storage.
    /// </summary>
    public static class Sanitizer
    {
        private static readonly HashSet<string> _Tables = new HashSet<string>(StringComparer.Ordinal)
        {
            "local_request_headers",
            "local_response_headers",
            "local_request_bodies",
            "local_response_bodies",
            "local_request_telemetry",
            "local_request_timeline"
        };

        /// <summary>
        /// Validate a known child table name.
        /// </summary>
        /// <param name="table">Table name.</param>
        /// <returns>Validated table name.</returns>
        public static string Table(string table)
        {
            if (!_Tables.Contains(table)) throw new ArgumentException("Unknown local dashboard table: " + table, nameof(table));
            return table;
        }
    }
}

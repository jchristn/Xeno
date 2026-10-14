namespace Xeno.Client.Services
{
    using System;
    using System.Globalization;

    /// <summary>
    /// Text formatting shared by the desktop app, terminal app, and tray agent so values read the same everywhere.
    /// </summary>
    public static class DisplayFormat
    {
        /// <summary>
        /// Placeholder for an empty value.
        /// </summary>
        public const string Dash = "-";

        /// <summary>
        /// Return the value, or a dash when it is empty.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>Display text.</returns>
        public static string OrDash(string? value)
        {
            return String.IsNullOrWhiteSpace(value) ? Dash : value;
        }

        /// <summary>
        /// Format a UTC time as yyyy-MM-dd HH:mm:ss UTC, or a dash.
        /// </summary>
        /// <param name="value">Time.</param>
        /// <returns>Display text.</returns>
        public static string Utc(DateTime? value)
        {
            return value == null ? Dash : value.Value.ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Format a UTC time in the local time zone as yyyy-MM-dd HH:mm:ss, or a dash.
        /// </summary>
        /// <param name="value">Time.</param>
        /// <returns>Display text.</returns>
        public static string Local(DateTime? value)
        {
            return value == null ? Dash : value.Value.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Format how long ago a time was, for example "12s ago", "5m ago", "3h ago", "2d ago", or a dash.
        /// </summary>
        /// <param name="value">Time (UTC).</param>
        /// <param name="nowUtc">Current time (UTC).</param>
        /// <returns>Display text.</returns>
        public static string Ago(DateTime? value, DateTime nowUtc)
        {
            if (value == null) return Dash;
            TimeSpan elapsed = nowUtc - value.Value.ToUniversalTime();
            if (elapsed.TotalSeconds < 0) return "just now";
            if (elapsed.TotalSeconds < 60) return ((int)elapsed.TotalSeconds).ToString(CultureInfo.InvariantCulture) + "s ago";
            if (elapsed.TotalMinutes < 60) return ((int)elapsed.TotalMinutes).ToString(CultureInfo.InvariantCulture) + "m ago";
            if (elapsed.TotalHours < 48) return ((int)elapsed.TotalHours).ToString(CultureInfo.InvariantCulture) + "h ago";
            return ((int)elapsed.TotalDays).ToString(CultureInfo.InvariantCulture) + "d ago";
        }

        /// <summary>
        /// Format a duration in milliseconds, for example "850 ms" or "2.4 s", or a dash.
        /// </summary>
        /// <param name="milliseconds">Duration.</param>
        /// <returns>Display text.</returns>
        public static string Milliseconds(double? milliseconds)
        {
            if (milliseconds == null) return Dash;
            if (milliseconds.Value < 1000) return milliseconds.Value.ToString("0", CultureInfo.InvariantCulture) + " ms";
            return (milliseconds.Value / 1000.0).ToString("0.0", CultureInfo.InvariantCulture) + " s";
        }

        /// <summary>
        /// Format a byte count with binary units, for example "512 B", "1.5 KB", "3.2 MB".
        /// </summary>
        /// <param name="bytes">Byte count.</param>
        /// <returns>Display text.</returns>
        public static string Bytes(long? bytes)
        {
            if (bytes == null) return Dash;
            double value = bytes.Value;
            string[] units = new[] { "B", "KB", "MB", "GB", "TB" };
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            return unit == 0
                ? bytes.Value.ToString(CultureInfo.InvariantCulture) + " B"
                : value.ToString("0.0", CultureInfo.InvariantCulture) + " " + units[unit];
        }

        /// <summary>
        /// Format a percentage from 0 to 100, or a dash.
        /// </summary>
        /// <param name="percent">Percentage.</param>
        /// <returns>Display text.</returns>
        public static string Percent(double? percent)
        {
            return percent == null ? Dash : percent.Value.ToString("0.0", CultureInfo.InvariantCulture) + "%";
        }

        /// <summary>
        /// Format a boolean as yes or no.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>Display text.</returns>
        public static string YesNo(bool value)
        {
            return value ? "yes" : "no";
        }

        /// <summary>
        /// Format an integer with invariant culture.
        /// </summary>
        /// <param name="value">Value.</param>
        /// <returns>Display text.</returns>
        public static string Number(long? value)
        {
            return value == null ? Dash : value.Value.ToString("N0", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Mask a secret, showing only its last four characters.
        /// </summary>
        /// <param name="value">Secret.</param>
        /// <returns>Display text.</returns>
        public static string Mask(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "(not set)";
            if (value.Length <= 4) return "configured";
            return "configured (..." + value.Substring(value.Length - 4) + ")";
        }

        /// <summary>
        /// Model capability label: "completions", "embeddings", or "completions, embeddings".
        /// </summary>
        /// <param name="completions">Serves completions.</param>
        /// <param name="embeddings">Serves embeddings.</param>
        /// <returns>Display text.</returns>
        public static string Capabilities(bool completions, bool embeddings)
        {
            if (completions && embeddings) return "completions, embeddings";
            if (embeddings) return "embeddings";
            if (completions) return "completions";
            return "none";
        }

        /// <summary>
        /// Truncate text to a maximum length, appending an ellipsis.
        /// </summary>
        /// <param name="value">Text.</param>
        /// <param name="maxLength">Maximum length (minimum 4).</param>
        /// <returns>Display text.</returns>
        public static string Truncate(string? value, int maxLength)
        {
            string text = value ?? String.Empty;
            int max = Math.Max(4, maxLength);
            return text.Length <= max ? text : text.Substring(0, max - 3) + "...";
        }
    }
}

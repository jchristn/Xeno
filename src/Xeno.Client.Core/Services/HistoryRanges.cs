namespace Xeno.Client.Services
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Range math for request history and telemetry views, shared by the local dashboard and the native front ends.
    /// </summary>
    public static class HistoryRanges
    {
        /// <summary>
        /// Range choices for range pickers, in <see cref="HistoryRangeEnum"/> order.
        /// </summary>
        public static readonly IReadOnlyList<string> Labels = new List<string> { "Last hour", "Last day", "Last week", "Last month" };

        /// <summary>
        /// Describe a range in lower case for headings, for example "last hour".
        /// </summary>
        /// <param name="range">Range.</param>
        /// <returns>Description.</returns>
        public static string Describe(HistoryRangeEnum range)
        {
            int index = (int)range;
            return index >= 0 && index < Labels.Count ? Labels[index].ToLowerInvariant() : Labels[0].ToLowerInvariant();
        }

        /// <summary>
        /// The range at a picker index; out-of-range indexes map to <see cref="HistoryRangeEnum.Hour"/>.
        /// </summary>
        /// <param name="index">Picker index.</param>
        /// <returns>Range.</returns>
        public static HistoryRangeEnum FromIndex(int index)
        {
            return index >= 0 && index < Labels.Count ? (HistoryRangeEnum)index : HistoryRangeEnum.Hour;
        }

        /// <summary>
        /// Parse a range name (hour, day, week, month). Unknown values map to <see cref="HistoryRangeEnum.Hour"/>.
        /// </summary>
        /// <param name="value">Range name.</param>
        /// <returns>Range.</returns>
        public static HistoryRangeEnum Parse(string? value)
        {
            if (String.Equals(value, "day", StringComparison.OrdinalIgnoreCase)) return HistoryRangeEnum.Day;
            if (String.Equals(value, "week", StringComparison.OrdinalIgnoreCase)) return HistoryRangeEnum.Week;
            if (String.Equals(value, "month", StringComparison.OrdinalIgnoreCase)) return HistoryRangeEnum.Month;
            return HistoryRangeEnum.Hour;
        }

        /// <summary>
        /// Start of a range ending at a given time.
        /// </summary>
        /// <param name="range">Range.</param>
        /// <param name="toUtc">End of the range (UTC).</param>
        /// <returns>Start of the range (UTC).</returns>
        public static DateTime StartUtc(HistoryRangeEnum range, DateTime toUtc)
        {
            switch (range)
            {
                case HistoryRangeEnum.Day: return toUtc.AddDays(-1);
                case HistoryRangeEnum.Week: return toUtc.AddDays(-7);
                case HistoryRangeEnum.Month: return toUtc.AddDays(-30);
                default: return toUtc.AddHours(-1);
            }
        }

        /// <summary>
        /// Default chart bucket size for a range, in minutes: 1 (hour), 15 (day), 120 (week), 480 (month).
        /// </summary>
        /// <param name="range">Range.</param>
        /// <returns>Bucket size in minutes.</returns>
        public static int DefaultBucketMinutes(HistoryRangeEnum range)
        {
            switch (range)
            {
                case HistoryRangeEnum.Day: return 15;
                case HistoryRangeEnum.Week: return 120;
                case HistoryRangeEnum.Month: return 480;
                default: return 1;
            }
        }
    }
}

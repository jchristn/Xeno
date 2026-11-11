namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Token activity heatmap.
    /// </summary>
    public class UsageHeatmap
    {
        /// <summary>
        /// HourByMinute, DayByHour, or WeekdayByHour.
        /// </summary>
        public string Layout { get; set; } = String.Empty;

        /// <summary>
        /// Rows.
        /// </summary>
        public int Rows { get; set; }

        /// <summary>
        /// Columns.
        /// </summary>
        public int Columns { get; set; }

        /// <summary>
        /// Offset used for hours and weekdays.
        /// </summary>
        public int UtcOffsetMinutes { get; set; }

        /// <summary>
        /// Cells, row by row.
        /// </summary>
        public List<UsageHeatmapCell> Cells { get; set; } = new List<UsageHeatmapCell>();

        /// <summary>
        /// Largest cell value.
        /// </summary>
        public long MaximumTokens { get; set; }
    }
}

namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One heatmap cell.
    /// </summary>
    public sealed class UsageHeatmapCell
    {
        /// <summary>
        /// Row, from 0.
        /// </summary>
        public int Row { get; set; } = 0;

        /// <summary>
        /// Column, from 0.
        /// </summary>
        public int Column { get; set; } = 0;

        /// <summary>
        /// UTC start of the cell's first occurrence in the range (HourByMinute and DayByHour), or DateTime.MinValue for WeekdayByHour cells, which repeat.
        /// </summary>
        public DateTime StartUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// Prompt plus completion tokens.
        /// </summary>
        public long Tokens { get; set; } = 0;

        /// <summary>
        /// Requests.
        /// </summary>
        public long Requests { get; set; } = 0;
    }
}

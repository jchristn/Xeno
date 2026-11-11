namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Token activity heatmap.
    /// </summary>
    public sealed class UsageHeatmap
    {
        /// <summary>
        /// Layout.
        /// </summary>
        public UsageHeatmapLayoutEnum Layout { get; set; } = UsageHeatmapLayoutEnum.WeekdayByHour;

        /// <summary>
        /// Rows.
        /// </summary>
        public int Rows { get; set; } = 0;

        /// <summary>
        /// Columns.
        /// </summary>
        public int Columns { get; set; } = 0;

        /// <summary>
        /// Offset used for hour-of-day and weekday cells.
        /// </summary>
        public int UtcOffsetMinutes { get; set; } = 0;

        /// <summary>
        /// Every cell, row by row.
        /// </summary>
        public List<UsageHeatmapCell> Cells { get; set; } = new List<UsageHeatmapCell>();

        /// <summary>
        /// Largest cell value, for shading.
        /// </summary>
        public long MaximumTokens { get; set; } = 0;
    }
}

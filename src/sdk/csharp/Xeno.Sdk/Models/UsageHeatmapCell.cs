namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One heatmap cell.
    /// </summary>
    public class UsageHeatmapCell
    {
        /// <summary>
        /// Row, from 0.
        /// </summary>
        public int Row { get; set; }

        /// <summary>
        /// Column, from 0.
        /// </summary>
        public int Column { get; set; }

        /// <summary>
        /// Cell start, UTC; DateTime.MinValue for repeating weekday cells.
        /// </summary>
        public DateTime StartUtc { get; set; }

        /// <summary>
        /// Tokens.
        /// </summary>
        public long Tokens { get; set; }

        /// <summary>
        /// Requests.
        /// </summary>
        public long Requests { get; set; }
    }
}

namespace Xeno.Core.Analytics
{
    /// <summary>
    /// Grid layout of the token activity heatmap, chosen from the range.
    /// </summary>
    public enum UsageHeatmapLayoutEnum
    {
        /// <summary>
        /// Up to 2 hours: one row per 10 minutes, one column per minute.
        /// </summary>
        HourByMinute,

        /// <summary>
        /// Up to 32 days (a day, a week, or a month): one row per day, one column per hour of the day.
        /// </summary>
        DayByHour,

        /// <summary>
        /// Longer ranges: one row per weekday (Sunday first), one column per hour of the day.
        /// </summary>
        WeekdayByHour
    }
}

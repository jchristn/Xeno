namespace Xeno.Client.Presentation
{
    /// <summary>
    /// Value of a status strip cell. Every front end pairs each status with its own glyph or pattern as well as a color.
    /// </summary>
    public enum XenoChartStatusEnum
    {
        /// <summary>
        /// No data for the cell.
        /// </summary>
        NoData = 0,

        /// <summary>
        /// Healthy.
        /// </summary>
        Healthy = 1,

        /// <summary>
        /// Partly failing.
        /// </summary>
        Degraded = 2,

        /// <summary>
        /// Failing.
        /// </summary>
        Unhealthy = 3
    }
}

namespace Xeno.Client.Presentation
{
    /// <summary>
    /// Color role of a chart series. Each front end maps it to its own palette.
    /// </summary>
    public enum XenoChartToneEnum
    {
        /// <summary>
        /// The brand accent (green).
        /// </summary>
        Accent,

        /// <summary>
        /// A successful outcome.
        /// </summary>
        Success,

        /// <summary>
        /// A failure.
        /// </summary>
        Danger,

        /// <summary>
        /// Informational (purple).
        /// </summary>
        Info,

        /// <summary>
        /// A warning.
        /// </summary>
        Warning
    }
}

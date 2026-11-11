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
        Warning,

        /// <summary>
        /// Categorical color 1, for group series (models, providers, stages).
        /// </summary>
        Series1,

        /// <summary>
        /// Categorical color 2.
        /// </summary>
        Series2,

        /// <summary>
        /// Categorical color 3.
        /// </summary>
        Series3,

        /// <summary>
        /// Categorical color 4.
        /// </summary>
        Series4,

        /// <summary>
        /// Categorical color 5.
        /// </summary>
        Series5,

        /// <summary>
        /// Categorical color 6.
        /// </summary>
        Series6,

        /// <summary>
        /// Categorical color 7.
        /// </summary>
        Series7,

        /// <summary>
        /// Categorical color 8.
        /// </summary>
        Series8,

        /// <summary>
        /// Neutral gray, for Other and Unknown.
        /// </summary>
        Neutral
    }
}

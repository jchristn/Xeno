namespace Xeno.Client.Presentation
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// One series of a <see cref="XenoChart"/>: a name, a color role, and one value per bucket.
    /// </summary>
    public sealed class XenoChartSeries
    {
        #region Public-Members

        /// <summary>
        /// Name shown in the legend.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Color role.
        /// </summary>
        public XenoChartToneEnum Tone { get; }

        /// <summary>
        /// Values, one per bucket. Buckets without data are zero.
        /// </summary>
        public IReadOnlyList<double> Values { get; }

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create a series.
        /// </summary>
        /// <param name="name">Name.</param>
        /// <param name="tone">Color role.</param>
        /// <param name="values">Values, one per bucket.</param>
        /// <exception cref="ArgumentNullException">Name or values is null.</exception>
        public XenoChartSeries(string name, XenoChartToneEnum tone, IReadOnlyList<double> values)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Tone = tone;
            Values = values ?? throw new ArgumentNullException(nameof(values));
        }

        #endregion
    }
}

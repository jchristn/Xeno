namespace Xeno.Client.Presentation
{
    using System;

    /// <summary>
    /// A time window (and optional model) a Usage chart asked another page to show.
    /// </summary>
    public sealed class XenoDrillDownRequest
    {
        /// <summary>
        /// Target page.
        /// </summary>
        public XenoPageEnum Page { get; set; } = XenoPageEnum.Requests;

        /// <summary>
        /// Start, UTC.
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.MinValue;

        /// <summary>
        /// End, UTC.
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.MaxValue;

        /// <summary>
        /// Only this model, or null.
        /// </summary>
        public string? Model { get; set; } = null;

        /// <summary>
        /// True when a time falls in the window.
        /// </summary>
        /// <param name="utc">Time, UTC.</param>
        /// <returns>True inside [FromUtc, ToUtc).</returns>
        public bool Contains(DateTime utc)
        {
            return utc >= FromUtc && utc < ToUtc;
        }

        /// <summary>
        /// True when a record at a time and with a model is in the window and, when the request names one, the model.
        /// </summary>
        /// <param name="utc">Time, UTC.</param>
        /// <param name="model">Model.</param>
        /// <returns>True when it matches.</returns>
        public bool Matches(DateTime utc, string? model)
        {
            if (!Contains(utc)) return false;
            return Model == null || String.Equals(Model, model, StringComparison.Ordinal);
        }
    }
}

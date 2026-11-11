namespace Xeno.Client.Tools
{
    using System;

    /// <summary>
    /// Arguments of the xeno_usage tool.
    /// </summary>
    public sealed class XenoToolUsageArguments
    {
        /// <summary>
        /// served (default), endpoint, or chat.
        /// </summary>
        public string? Dataset { get; set; } = null;

        /// <summary>
        /// hour, day (default), week, or month.
        /// </summary>
        public string? Range { get; set; } = null;

        /// <summary>
        /// Model (default), Provider, or Route.
        /// </summary>
        public string? GroupBy { get; set; } = null;

        /// <summary>
        /// Requests (default) or Tokens.
        /// </summary>
        public string? GroupMetric { get; set; } = null;

        /// <summary>
        /// Only this model, or null.
        /// </summary>
        public string? Model { get; set; } = null;
    }
}

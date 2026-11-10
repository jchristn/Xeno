namespace Xeno.Client.Presentation
{
    using System;
    using System.Globalization;
    using Xeno.Client.Routing;
    using Xeno.Client.Services;

    /// <summary>
    /// One local endpoint request as the Local endpoint page lists it.
    /// </summary>
    public sealed class LocalInferenceRequestRow
    {
        /// <summary>
        /// Local time text. Never null.
        /// </summary>
        public string When { get; set; } = String.Empty;

        /// <summary>
        /// Model, or the path for model lists. Never null.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Route. Never null.
        /// </summary>
        public string Route { get; set; } = String.Empty;

        /// <summary>
        /// Status code text. Never null.
        /// </summary>
        public string Status { get; set; } = String.Empty;

        /// <summary>
        /// Latency text. Never null.
        /// </summary>
        public string Latency { get; set; } = String.Empty;

        /// <summary>
        /// Tokens text, for example "120 / 48", or a dash. Never null.
        /// </summary>
        public string Tokens { get; set; } = String.Empty;

        /// <summary>
        /// The record.
        /// </summary>
        public LocalInferenceRequestRecord Source { get; set; } = new LocalInferenceRequestRecord();

        /// <summary>
        /// Build a row.
        /// </summary>
        /// <param name="record">Record.</param>
        /// <returns>Row.</returns>
        /// <exception cref="ArgumentNullException">record is null.</exception>
        public static LocalInferenceRequestRow From(LocalInferenceRequestRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            string tokens = record.PromptTokens.HasValue || record.CompletionTokens.HasValue
                ? (record.PromptTokens ?? 0).ToString(CultureInfo.InvariantCulture) + " / " + (record.CompletionTokens ?? 0).ToString(CultureInfo.InvariantCulture)
                : DisplayFormat.Dash;
            return new LocalInferenceRequestRow
            {
                When = DateTime.SpecifyKind(record.ReceivedUtc, DateTimeKind.Utc).ToLocalTime().ToString("MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                Model = String.IsNullOrWhiteSpace(record.Model) ? record.Method + " " + record.Path : record.Model,
                Route = record.Route + (record.Fallback ? " (fallback)" : String.Empty),
                Status = record.StatusCode.ToString(CultureInfo.InvariantCulture),
                Latency = DisplayFormat.Milliseconds(record.LatencyMs),
                Tokens = tokens,
                Source = record
            };
        }
    }
}

namespace Xeno.Client.Presentation
{
    using System;
    using System.Globalization;
    using Xeno.Client.LocalDashboard.Models;
    using Xeno.Client.Services;

    /// <summary>
    /// A request history entry formatted for tables. Request and response bodies are not copied into the row.
    /// </summary>
    public sealed class RequestRow
    {
        /// <summary>
        /// Request identifier.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Receive time in local time.
        /// </summary>
        public string Received { get; set; } = String.Empty;

        /// <summary>
        /// Receive time (UTC), for sorting.
        /// </summary>
        public DateTime ReceivedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// HTTP method.
        /// </summary>
        public string Method { get; set; } = String.Empty;

        /// <summary>
        /// Request path.
        /// </summary>
        public string Path { get; set; } = String.Empty;

        /// <summary>
        /// Model.
        /// </summary>
        public string Model { get; set; } = String.Empty;

        /// <summary>
        /// Provider.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// HTTP status code, or a dash.
        /// </summary>
        public string Status { get; set; } = String.Empty;

        /// <summary>
        /// Outcome.
        /// </summary>
        public string Outcome { get; set; } = String.Empty;

        /// <summary>
        /// True when the outcome is a failure.
        /// </summary>
        public bool Failed { get; set; } = false;

        /// <summary>
        /// Duration.
        /// </summary>
        public string Duration { get; set; } = String.Empty;

        /// <summary>
        /// Duration in milliseconds, for sorting.
        /// </summary>
        public double DurationMs { get; set; } = 0;

        /// <summary>
        /// Total tokens, or a dash.
        /// </summary>
        public string Tokens { get; set; } = String.Empty;

        /// <summary>
        /// "yes" for streamed responses.
        /// </summary>
        public string Streamed { get; set; } = String.Empty;

        /// <summary>
        /// Build a row.
        /// </summary>
        /// <param name="entry">History entry.</param>
        /// <returns>Row.</returns>
        /// <exception cref="ArgumentNullException">Entry is null.</exception>
        public static RequestRow From(LocalRequestHistoryEntry entry)
        {
            ArgumentNullException.ThrowIfNull(entry);
            bool failed = entry.Outcome != LocalRequestOutcomeEnum.Success && entry.Outcome != LocalRequestOutcomeEnum.Processing;
            return new RequestRow
            {
                Id = entry.Id,
                Received = DisplayFormat.Local(entry.ReceivedUtc),
                ReceivedUtc = entry.ReceivedUtc,
                Method = entry.Method,
                Path = entry.Path,
                Model = DisplayFormat.OrDash(entry.ModelName),
                Provider = DisplayFormat.OrDash(entry.ProviderName),
                Status = entry.StatusCode == null ? DisplayFormat.Dash : entry.StatusCode.Value.ToString(CultureInfo.InvariantCulture),
                Outcome = entry.Outcome.ToString(),
                Failed = failed,
                Duration = DisplayFormat.Milliseconds(entry.Telemetry.DurationMs),
                DurationMs = entry.Telemetry.DurationMs ?? 0,
                Tokens = entry.Telemetry.TotalTokens == null ? DisplayFormat.Dash : entry.Telemetry.TotalTokens.Value.ToString(CultureInfo.InvariantCulture),
                Streamed = DisplayFormat.YesNo(entry.Streamed)
            };
        }
    }
}

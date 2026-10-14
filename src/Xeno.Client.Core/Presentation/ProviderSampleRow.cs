namespace Xeno.Client.Presentation
{
    using System;
    using System.Globalization;
    using Xeno.Client.Services;
    using Xeno.Client.Telemetry.Models;

    /// <summary>
    /// A provider telemetry sample or live provider status formatted for tables.
    /// </summary>
    public sealed class ProviderSampleRow
    {
        /// <summary>
        /// Sample identifier, or an empty string for live status.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Collection time in local time.
        /// </summary>
        public string Collected { get; set; } = String.Empty;

        /// <summary>
        /// Collection time (UTC), for sorting.
        /// </summary>
        public DateTime CollectedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Provider name.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Provider kind or API type.
        /// </summary>
        public string Kind { get; set; } = String.Empty;

        /// <summary>
        /// Collection status.
        /// </summary>
        public string Status { get; set; } = String.Empty;

        /// <summary>
        /// Health.
        /// </summary>
        public string Health { get; set; } = String.Empty;

        /// <summary>
        /// Loaded and available models, for example "1 / 4".
        /// </summary>
        public string Models { get; set; } = String.Empty;

        /// <summary>
        /// Active requests.
        /// </summary>
        public string Active { get; set; } = String.Empty;

        /// <summary>
        /// Queued requests.
        /// </summary>
        public string Queued { get; set; } = String.Empty;

        /// <summary>
        /// Generation tokens per second.
        /// </summary>
        public string TokensPerSecond { get; set; } = String.Empty;

        /// <summary>
        /// Version.
        /// </summary>
        public string Version { get; set; } = String.Empty;

        /// <summary>
        /// Last error, or a dash.
        /// </summary>
        public string Error { get; set; } = String.Empty;

        /// <summary>
        /// Build a row from a stored sample.
        /// </summary>
        /// <param name="sample">Sample.</param>
        /// <returns>Row.</returns>
        /// <exception cref="ArgumentNullException">Sample is null.</exception>
        public static ProviderSampleRow From(ClientProviderTelemetrySampleRecord sample)
        {
            ArgumentNullException.ThrowIfNull(sample);
            return new ProviderSampleRow
            {
                Id = sample.Id,
                Collected = DisplayFormat.Local(sample.CollectedUtc),
                CollectedUtc = sample.CollectedUtc,
                Provider = sample.ProviderName,
                Kind = sample.ProviderKind.ToString(),
                Status = sample.Status.ToString(),
                Health = sample.HealthStatus.ToString(),
                Models = sample.LoadedModelCount.ToString(CultureInfo.InvariantCulture) + " / " + sample.AvailableModelCount.ToString(CultureInfo.InvariantCulture),
                Active = Number(sample.ActiveRequests),
                Queued = Number(sample.QueuedRequests),
                TokensPerSecond = Number(sample.GenerationTokensPerSecond),
                Version = DisplayFormat.OrDash(sample.Version),
                Error = DisplayFormat.OrDash(sample.LastError)
            };
        }

        /// <summary>
        /// Build a row from live provider status.
        /// </summary>
        /// <param name="status">Status.</param>
        /// <returns>Row.</returns>
        /// <exception cref="ArgumentNullException">Status is null.</exception>
        public static ProviderSampleRow From(ClientProviderTelemetryProviderStatus status)
        {
            ArgumentNullException.ThrowIfNull(status);
            return new ProviderSampleRow
            {
                Collected = DisplayFormat.Local(status.CollectedUtc),
                CollectedUtc = status.CollectedUtc,
                Provider = status.Endpoint.ProviderName,
                Kind = status.Endpoint.Kind.ToString(),
                Status = status.Status.ToString(),
                Health = status.HealthStatus.ToString(),
                Models = status.LoadedModelCount.ToString(CultureInfo.InvariantCulture) + " / " + status.AvailableModelCount.ToString(CultureInfo.InvariantCulture),
                Active = Number(status.ActiveRequests),
                Queued = Number(status.QueuedRequests),
                TokensPerSecond = Number(status.GenerationTokensPerSecond),
                Version = DisplayFormat.OrDash(status.Version),
                Error = DisplayFormat.OrDash(status.LastError)
            };
        }

        private static string Number(double? value)
        {
            return value == null ? DisplayFormat.Dash : value.Value.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}

namespace Xeno.Client.Presentation
{
    using System;
    using System.Globalization;
    using Xeno.Client.Services;

    /// <summary>
    /// A model a provider has right now, formatted for the Manage Models tables.
    /// </summary>
    public sealed class ProviderInventoryRow
    {
        /// <summary>
        /// Provider name.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Model name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Size on disk, or a dash.
        /// </summary>
        public string Size { get; set; } = "-";

        /// <summary>
        /// Size in bytes, for sorting.
        /// </summary>
        public long SizeBytes { get; set; } = 0;

        /// <summary>
        /// Parameter size, for example "3.2B", or a dash.
        /// </summary>
        public string ParameterSize { get; set; } = "-";

        /// <summary>
        /// Quantization, for example "Q4_K_M", or a dash.
        /// </summary>
        public string Quantization { get; set; } = "-";

        /// <summary>
        /// When the model was last modified, in local time, or a dash.
        /// </summary>
        public string Modified { get; set; } = "-";

        /// <summary>
        /// "yes" when the model is advertised to the Xeno network, otherwise "no".
        /// </summary>
        public string Advertised { get; set; } = "no";

        /// <summary>
        /// True when the model is advertised.
        /// </summary>
        public bool IsAdvertised { get; set; } = false;

        /// <summary>
        /// Build a row.
        /// </summary>
        /// <param name="provider">Provider name.</param>
        /// <param name="model">Model.</param>
        /// <returns>Row.</returns>
        /// <exception cref="ArgumentNullException">Model is null.</exception>
        public static ProviderInventoryRow From(string provider, ProviderInventoryModel model)
        {
            ArgumentNullException.ThrowIfNull(model);
            return new ProviderInventoryRow
            {
                Provider = provider ?? String.Empty,
                Name = model.Name,
                Size = DisplayFormat.Bytes(model.SizeBytes),
                SizeBytes = model.SizeBytes ?? 0,
                ParameterSize = DisplayFormat.OrDash(model.ParameterSize),
                Quantization = DisplayFormat.OrDash(model.Quantization),
                Modified = FormatModified(model.Modified),
                Advertised = model.Advertised ? "yes" : "no",
                IsAdvertised = model.Advertised
            };
        }

        /// <summary>
        /// Format a provider's modified timestamp in local time, or return it unchanged when it does not parse.
        /// </summary>
        /// <param name="value">Timestamp.</param>
        /// <returns>Formatted value, or a dash.</returns>
        public static string FormatModified(string? value)
        {
            if (String.IsNullOrWhiteSpace(value)) return "-";
            if (DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out DateTimeOffset parsed))
            {
                return parsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
            }

            return value;
        }
    }
}

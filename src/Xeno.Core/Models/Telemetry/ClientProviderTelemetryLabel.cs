namespace Xeno.Core.Models.Telemetry
{
    /// <summary>
    /// Provider telemetry metric label.
    /// </summary>
    public class ClientProviderTelemetryLabel
    {
        /// <summary>
        /// Label name.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Label value.
        /// </summary>
        public string Value { get; set; } = string.Empty;
    }
}

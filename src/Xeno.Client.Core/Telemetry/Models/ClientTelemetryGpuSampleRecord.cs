namespace Xeno.Client.Telemetry.Models
{
    /// <summary>
    /// Persisted GPU telemetry sample record.
    /// </summary>
    public class ClientTelemetryGpuSampleRecord : ClientGpuDeviceTelemetry
    {
        /// <summary>
        /// GPU sample ID.
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Parent telemetry sample ID.
        /// </summary>
        public string SampleId { get; set; } = string.Empty;
    }
}

namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno server client telemetry collection attempt record DTO.
    /// </summary>
    public class ServerClientTelemetryCollectionAttemptRecord
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the tenant id.
        /// </summary>
        public string TenantId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the client id.
        /// </summary>
        public string ClientId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the client name.
        /// </summary>
        public string ClientName { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the started utc.
        /// </summary>
        public DateTime? StartedUtc { get; set; }

        /// <summary>
        /// Gets or sets the completed utc.
        /// </summary>
        public DateTime? CompletedUtc { get; set; }

        /// <summary>
        /// Gets or sets the elapsed ms.
        /// </summary>
        public double ElapsedMs { get; set; }

        /// <summary>
        /// Gets or sets the host requested.
        /// </summary>
        public bool HostRequested { get; set; }

        /// <summary>
        /// Gets or sets the provider requested.
        /// </summary>
        public bool ProviderRequested { get; set; }

        /// <summary>
        /// Gets or sets the host succeeded.
        /// </summary>
        public bool HostSucceeded { get; set; }

        /// <summary>
        /// Gets or sets the provider succeeded.
        /// </summary>
        public bool ProviderSucceeded { get; set; }

        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        public string Status { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        public string ErrorCode { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        public string ErrorMessage { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the host sample id.
        /// </summary>
        public string HostSampleId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the provider sample count.
        /// </summary>
        public int ProviderSampleCount { get; set; }
    }
}

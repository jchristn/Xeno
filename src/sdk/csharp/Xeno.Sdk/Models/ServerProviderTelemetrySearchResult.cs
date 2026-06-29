namespace Xeno.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Xeno server provider telemetry search result DTO.
    /// </summary>
    public class ServerProviderTelemetrySearchResult
    {
        /// <summary>
        /// Gets or sets the items.
        /// </summary>
        public List<ServerProviderTelemetrySampleRecord> Items { get; set; } = new List<ServerProviderTelemetrySampleRecord>();

        /// <summary>
        /// Gets or sets the total.
        /// </summary>
        public int Total { get; set; }

        /// <summary>
        /// Gets or sets the page.
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Gets or sets the page size.
        /// </summary>
        public int PageSize { get; set; }
    }
}

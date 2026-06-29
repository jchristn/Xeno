namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno health response DTO.
    /// </summary>
    public class HealthResponse
    {
        /// <summary>
        /// Gets or sets the status.
        /// </summary>
        public string Status { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the product.
        /// </summary>
        public string Product { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the version.
        /// </summary>
        public string Version { get; set; } = String.Empty;
    }
}

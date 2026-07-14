namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno ping heartbeat response DTO.
    /// </summary>
    public class PingResponse
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

        /// <summary>
        /// Gets or sets the echoed sequence number.
        /// </summary>
        public int Sequence { get; set; } = 0;

        /// <summary>
        /// Gets or sets the echoed client sent timestamp.
        /// </summary>
        public DateTime? ClientSentUtc { get; set; } = null;

        /// <summary>
        /// Gets or sets the server response timestamp.
        /// </summary>
        public DateTime? ServerUtc { get; set; } = null;
    }
}

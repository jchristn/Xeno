namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno tenant DTO.
    /// </summary>
    public class Tenant
    {
        /// <summary>
        /// Gets or sets the id.
        /// </summary>
        public string Id { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the active.
        /// </summary>
        public bool Active { get; set; }

        /// <summary>
        /// Gets or sets the created utc.
        /// </summary>
        public DateTime? CreatedUtc { get; set; }

        /// <summary>
        /// Gets or sets the last update utc.
        /// </summary>
        public DateTime? LastUpdateUtc { get; set; }
    }
}

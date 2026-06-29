namespace Xeno.Sdk
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Xeno xeno client record DTO.
    /// </summary>
    public class XenoClientRecord
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
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the connected.
        /// </summary>
        public bool Connected { get; set; }

        /// <summary>
        /// Gets or sets the models.
        /// </summary>
        public List<XenoClientModel> Models { get; set; } = new List<XenoClientModel>();
    }
}

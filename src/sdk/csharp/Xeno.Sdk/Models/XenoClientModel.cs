namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno xeno client model DTO.
    /// </summary>
    public class XenoClientModel
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the provider.
        /// </summary>
        public string Provider { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the endpoint.
        /// </summary>
        public string Endpoint { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the available.
        /// </summary>
        public bool Available { get; set; }
    }
}

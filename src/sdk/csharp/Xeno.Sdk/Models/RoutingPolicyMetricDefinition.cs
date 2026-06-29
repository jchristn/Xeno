namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno routing policy metric definition DTO.
    /// </summary>
    public class RoutingPolicyMetricDefinition
    {
        /// <summary>
        /// Gets or sets the name.
        /// </summary>
        public string Name { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the value type.
        /// </summary>
        public string ValueType { get; set; } = String.Empty;
    }
}

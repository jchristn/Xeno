namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno routing policy validation issue DTO.
    /// </summary>
    public class RoutingPolicyValidationIssue
    {
        /// <summary>
        /// Gets or sets the field.
        /// </summary>
        public string Field { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the code.
        /// </summary>
        public string Code { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the message.
        /// </summary>
        public string Message { get; set; } = String.Empty;
    }
}

namespace Xeno.Sdk
{
    using System.Collections.Generic;

    /// <summary>
    /// Xeno routing policy validation result DTO.
    /// </summary>
    public class RoutingPolicyValidationResult
    {
        /// <summary>
        /// Gets or sets the valid.
        /// </summary>
        public bool Valid { get; set; }

        /// <summary>
        /// Gets or sets the issues.
        /// </summary>
        public List<RoutingPolicyValidationIssue> Issues { get; set; } = new List<RoutingPolicyValidationIssue>();
    }
}

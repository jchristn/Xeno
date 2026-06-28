namespace Xeno.Core.Models
{
    using System.Collections.Generic;

    /// <summary>
    /// Routing policy validation result.
    /// </summary>
    public class RoutingPolicyValidationResult
    {
        /// <summary>
        /// Whether the policy request is valid.
        /// </summary>
        public bool Valid { get; set; } = true;

        /// <summary>
        /// Validation issues.
        /// </summary>
        public List<RoutingPolicyValidationIssue> Issues { get; set; } = new List<RoutingPolicyValidationIssue>();
    }
}

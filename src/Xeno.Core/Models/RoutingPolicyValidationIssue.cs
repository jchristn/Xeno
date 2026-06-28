namespace Xeno.Core.Models
{
    /// <summary>
    /// Routing policy validation issue.
    /// </summary>
    public class RoutingPolicyValidationIssue
    {
        /// <summary>
        /// Field path that failed validation.
        /// </summary>
        public string Field { get; set; } = string.Empty;

        /// <summary>
        /// Machine-readable validation code.
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Human-readable validation message.
        /// </summary>
        public string Message { get; set; } = string.Empty;
    }
}

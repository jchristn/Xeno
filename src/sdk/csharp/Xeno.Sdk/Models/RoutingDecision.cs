namespace Xeno.Sdk
{
    using System;

    /// <summary>
    /// Xeno routing decision DTO.
    /// </summary>
    public class RoutingDecision
    {
        /// <summary>
        /// Gets or sets the success.
        /// </summary>
        public bool Success { get; set; }

        /// <summary>
        /// Gets or sets the outcome code.
        /// </summary>
        public string OutcomeCode { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the message.
        /// </summary>
        public string Message { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the selected client id.
        /// </summary>
        public string SelectedClientId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the selected client name.
        /// </summary>
        public string SelectedClientName { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the policy id.
        /// </summary>
        public string PolicyId { get; set; } = String.Empty;

        /// <summary>
        /// Gets or sets the policy name.
        /// </summary>
        public string PolicyName { get; set; } = String.Empty;
    }
}

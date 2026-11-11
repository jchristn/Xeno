namespace Xeno.Client.Tools.Workspace.Planning
{
    using System.Collections.Generic;

    /// <summary>
    /// Outcome of validating a proposed task plan: whether it is usable and, when not, the problems in the order they
    /// were found. The problems are returned to the model so it can correct and re-send the plan.
    /// </summary>
    public sealed class TaskPlanValidationResult
    {
        #region Public-Members

        /// <summary>
        /// True when there are no problems.
        /// </summary>
        public bool IsValid
        {
            get { return _Problems.Count == 0; }
        }

        /// <summary>
        /// Problems in the order found. Never null; empty when valid.
        /// </summary>
        public List<string> Problems
        {
            get { return _Problems; }
            set { _Problems = value ?? new List<string>(); }
        }

        #endregion

        #region Private-Members

        private List<string> _Problems = new List<string>();

        #endregion
    }
}

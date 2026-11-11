namespace Xeno.Client.Tools.Workspace.Planning
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Thrown when a task plan that fails <see cref="TaskPlanValidator"/> is applied to a <see cref="TaskPlan"/>.
    /// </summary>
    public sealed class TaskPlanValidationException : Exception
    {
        #region Public-Members

        /// <summary>
        /// Problems that made the plan invalid. Never null.
        /// </summary>
        public IReadOnlyList<string> Problems
        {
            get { return _Problems; }
        }

        #endregion

        #region Private-Members

        private readonly List<string> _Problems = new List<string>();

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the exception.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="problems">Validation problems; null is treated as none.</param>
        /// <exception cref="ArgumentNullException">message is null.</exception>
        public TaskPlanValidationException(string message, IReadOnlyList<string>? problems)
            : base(message ?? throw new ArgumentNullException(nameof(message)))
        {
            if (problems != null) _Problems = new List<string>(problems);
        }

        #endregion
    }
}

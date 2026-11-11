namespace Xeno.Client.Tools.Workspace.Planning
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// Creates the task planning tools (plan_tasks and update_task), ported from Mux.
    /// </summary>
    public static class PlanningTools
    {
        #region Public-Methods

        /// <summary>
        /// Create the planning tools.
        /// </summary>
        /// <param name="plans">Plans by conversation, shared by both tools.</param>
        /// <returns>plan_tasks and update_task.</returns>
        /// <exception cref="ArgumentNullException">plans is null.</exception>
        public static IReadOnlyList<IWorkspaceTool> Create(TaskPlanStore plans)
        {
            ArgumentNullException.ThrowIfNull(plans);
            return new List<IWorkspaceTool> { new PlanTasksTool(plans), new UpdateTaskTool(plans) };
        }

        #endregion
    }
}

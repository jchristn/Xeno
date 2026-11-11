namespace Xeno.Client.Tools.Workspace.Planning
{
    using System.Collections.Generic;

    /// <summary>
    /// Tally of a task plan's tasks by status.
    /// </summary>
    public sealed class TaskPlanSummary
    {
        #region Public-Members

        /// <summary>
        /// Total number of tasks.
        /// </summary>
        public int Total { get; set; } = 0;

        /// <summary>
        /// Tasks that completed.
        /// </summary>
        public int Completed { get; set; } = 0;

        /// <summary>
        /// Tasks still pending.
        /// </summary>
        public int Pending { get; set; } = 0;

        /// <summary>
        /// Tasks in progress.
        /// </summary>
        public int InProgress { get; set; } = 0;

        /// <summary>
        /// Tasks that failed.
        /// </summary>
        public int Failed { get; set; } = 0;

        /// <summary>
        /// Tasks that were skipped.
        /// </summary>
        public int Skipped { get; set; } = 0;

        /// <summary>
        /// Tasks that are blocked.
        /// </summary>
        public int Blocked { get; set; } = 0;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Tally tasks by status.
        /// </summary>
        /// <param name="tasks">Tasks; null is treated as an empty plan.</param>
        /// <returns>Summary.</returns>
        public static TaskPlanSummary FromTasks(IReadOnlyList<AgentTask>? tasks)
        {
            TaskPlanSummary summary = new TaskPlanSummary();
            if (tasks == null) return summary;

            summary.Total = tasks.Count;
            foreach (AgentTask task in tasks)
            {
                switch (task.Status)
                {
                    case AgentTaskStatusEnum.Completed: summary.Completed++; break;
                    case AgentTaskStatusEnum.Pending: summary.Pending++; break;
                    case AgentTaskStatusEnum.InProgress: summary.InProgress++; break;
                    case AgentTaskStatusEnum.Failed: summary.Failed++; break;
                    case AgentTaskStatusEnum.Skipped: summary.Skipped++; break;
                    case AgentTaskStatusEnum.Blocked: summary.Blocked++; break;
                }
            }

            return summary;
        }

        #endregion
    }
}

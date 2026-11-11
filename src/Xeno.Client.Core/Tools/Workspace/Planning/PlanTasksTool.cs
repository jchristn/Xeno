namespace Xeno.Client.Tools.Workspace.Planning
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// plan_tasks: establishes or replaces the conversation's task plan. Changes only in-memory plan state, so it is not
    /// mutating and never needs approval.
    /// </summary>
    public sealed class PlanTasksTool : IWorkspaceTool
    {
        #region Public-Members

        /// <summary>
        /// Tool name.
        /// </summary>
        public const string ToolName = "plan_tasks";

        /// <inheritdoc />
        public string Name
        {
            get { return ToolName; }
        }

        /// <inheritdoc />
        public string Description
        {
            get { return "Establishes or replaces the plan of tasks for the current request. Call this at the start of any request that will take more than a couple of steps or spans several files, then keep the plan current with update_task. Each task has a stable id, a short title, and optional dependsOn ids of tasks that must complete first. Re-calling replaces the whole plan."; }
        }

        /// <inheritdoc />
        public ChatToolSchemaDto Schema
        {
            get
            {
                return WorkspaceSchema.Object(
                    WorkspaceSchema.Array("tasks", "The ordered list of tasks that make up the plan.", WorkspaceSchema.Nested(
                        "One task.",
                        WorkspaceSchema.Text("id", "A short stable id unique within the plan, for example \"t1\"."),
                        WorkspaceSchema.Text("title", "A short imperative label for the task."),
                        WorkspaceSchema.Array("dependsOn", "Ids of tasks that must complete before this one can start. Omit or leave empty when the task has no prerequisites.", new ChatToolPropertyDto { Type = "string" }),
                        "id",
                        "title")),
                    "tasks");
            }
        }

        /// <inheritdoc />
        public bool Mutating
        {
            get { return false; }
        }

        #endregion

        #region Private-Members

        private readonly TaskPlanStore _Plans;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Create the tool.
        /// </summary>
        /// <param name="plans">Plans by conversation.</param>
        /// <exception cref="ArgumentNullException">plans is null.</exception>
        public PlanTasksTool(TaskPlanStore plans)
        {
            _Plans = plans ?? throw new ArgumentNullException(nameof(plans));
        }

        #endregion

        #region Public-Methods

        /// <inheritdoc />
        public Task<object> InvokeAsync(string arguments, WorkspaceToolContext context, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(context);
            token.ThrowIfCancellationRequested();
            PlanTasksArguments args = WorkspaceJson.Read<PlanTasksArguments>(arguments);
            if (String.IsNullOrEmpty(context.ConversationId))
            {
                return Task.FromResult<object>(new TaskPlanErrorResult { Error = "task_planning_disabled", Message = "Task planning is not active for this run." });
            }

            List<AgentTask> tasks = new List<AgentTask>();
            List<AgentTask?> proposed = new List<AgentTask?>();
            if (args.Tasks != null)
            {
                foreach (PlanTaskArgument? item in args.Tasks)
                {
                    if (item == null)
                    {
                        proposed.Add(null);
                        continue;
                    }

                    AgentTask task = new AgentTask { Id = item.Id ?? String.Empty, Title = item.Title ?? String.Empty };
                    if (item.DependsOn != null)
                    {
                        foreach (string? dependency in item.DependsOn)
                        {
                            if (dependency != null) task.DependsOn.Add(dependency);
                        }
                    }

                    tasks.Add(task);
                    proposed.Add(task);
                }
            }

            TaskPlanValidationResult validation = TaskPlanValidator.Validate(proposed);
            if (!validation.IsValid)
            {
                return Task.FromResult<object>(new TaskPlanErrorResult { Error = "invalid_plan", Problems = validation.Problems });
            }

            _Plans.GetOrCreate(context.ConversationId).SetPlan(tasks);
            return Task.FromResult<object>(new PlanTasksResult { Ok = true, TaskCount = tasks.Count });
        }

        #endregion
    }
}

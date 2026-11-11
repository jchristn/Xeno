namespace Xeno.Client.Tools.Workspace.Planning
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Chat.Wire;

    /// <summary>
    /// update_task: sets one task's status and optionally a note in the conversation's plan. Changes only in-memory plan
    /// state, so it is not mutating and never needs approval.
    /// </summary>
    public sealed class UpdateTaskTool : IWorkspaceTool
    {
        #region Public-Members

        /// <summary>
        /// Tool name.
        /// </summary>
        public const string ToolName = "update_task";

        /// <inheritdoc />
        public string Name
        {
            get { return ToolName; }
        }

        /// <inheritdoc />
        public string Description
        {
            get { return "Updates the status of one task in the current plan. Set status to in_progress when you start a task and completed when you finish it; keep exactly one task in_progress at a time. Use blocked (with a note) when a task cannot proceed, skipped when it is no longer needed, and failed (a note is required) when an attempt failed."; }
        }

        /// <inheritdoc />
        public ChatToolSchemaDto Schema
        {
            get
            {
                return WorkspaceSchema.Object(
                    WorkspaceSchema.Text("id", "The id of the task to update."),
                    WorkspaceSchema.Choice("status", "The new status.", "pending", "in_progress", "completed", "failed", "skipped", "blocked"),
                    WorkspaceSchema.Text("note", "An optional note. Required when status is \"failed\"."),
                    "id",
                    "status");
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
        public UpdateTaskTool(TaskPlanStore plans)
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
            UpdateTaskArguments args = WorkspaceJson.Read<UpdateTaskArguments>(arguments);
            if (String.IsNullOrEmpty(context.ConversationId)) return Error("task_planning_disabled", "Task planning is not active for this run.");

            string id = args.Id ?? String.Empty;
            if (String.IsNullOrWhiteSpace(id)) return Error("invalid_argument", "The 'id' parameter is required.");

            string statusText = args.Status ?? String.Empty;
            AgentTaskStatusEnum? status = ParseStatus(statusText);
            if (status == null) return Error("invalid_status", "Unknown status '" + statusText + "'. Valid values: pending, in_progress, completed, failed, skipped, blocked.");
            if (status == AgentTaskStatusEnum.Failed && String.IsNullOrWhiteSpace(args.Note)) return Error("note_required", "A note is required when marking a task failed.");

            TaskPlan plan = _Plans.GetOrCreate(context.ConversationId);
            if (!plan.TryUpdateTask(id, status.Value, args.Note, out TaskPlanChangeKindEnum changeKind))
            {
                List<string> ids = new List<string>();
                foreach (AgentTask task in plan.Snapshot()) ids.Add(task.Id);
                return Task.FromResult<object>(new TaskPlanErrorResult { Error = "unknown_task", Message = "No task with id '" + id + "'.", ValidIds = ids });
            }

            return Task.FromResult<object>(new UpdateTaskResult { Ok = true, Id = id, Status = statusText });
        }

        #endregion

        #region Private-Methods

        private static AgentTaskStatusEnum? ParseStatus(string value)
        {
            switch (value.Trim().ToLowerInvariant())
            {
                case "pending": return AgentTaskStatusEnum.Pending;
                case "in_progress": return AgentTaskStatusEnum.InProgress;
                case "completed": return AgentTaskStatusEnum.Completed;
                case "failed": return AgentTaskStatusEnum.Failed;
                case "skipped": return AgentTaskStatusEnum.Skipped;
                case "blocked": return AgentTaskStatusEnum.Blocked;
                default: return null;
            }
        }

        private static Task<object> Error(string code, string message)
        {
            return Task.FromResult<object>(new TaskPlanErrorResult { Error = code, Message = message });
        }

        #endregion
    }
}

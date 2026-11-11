namespace Xeno.Client.Tools.Workspace.Planning
{
    using System;
    using System.Collections.Generic;

    /// <summary>
    /// A conversation's plan of tasks: the ordered tasks the model authors with plan_tasks and advances with
    /// update_task, plus a <see cref="Version"/> that increments on every mutation. Thread safe: every read and write
    /// is serialized on a private lock, and every accessor that returns tasks returns deep copies.
    /// </summary>
    public sealed class TaskPlan
    {
        #region Public-Members

        /// <summary>
        /// Counter incremented on every mutation, so observers can tell whether the plan changed.
        /// </summary>
        public int Version
        {
            get { lock (_Sync) { return _Version; } }
        }

        /// <summary>
        /// When the plan was first populated (UTC), or null while it is empty.
        /// </summary>
        public DateTime? CreatedUtc
        {
            get { lock (_Sync) { return _CreatedUtc; } }
        }

        /// <summary>
        /// Kind of the most recent change.
        /// </summary>
        public TaskPlanChangeKindEnum LastChangeKind
        {
            get { lock (_Sync) { return _LastChangeKind; } }
        }

        /// <summary>
        /// Id of the task touched by the most recent change, or null for a whole-plan change.
        /// </summary>
        public string? LastChangedTaskId
        {
            get { lock (_Sync) { return _LastChangedTaskId; } }
        }

        /// <summary>
        /// True when the plan has no tasks.
        /// </summary>
        public bool IsEmpty
        {
            get { lock (_Sync) { return _Tasks.Count == 0; } }
        }

        /// <summary>
        /// Number of tasks.
        /// </summary>
        public int TotalCount
        {
            get { lock (_Sync) { return _Tasks.Count; } }
        }

        /// <summary>
        /// Number of completed tasks.
        /// </summary>
        public int CompletedCount
        {
            get
            {
                lock (_Sync)
                {
                    int count = 0;
                    foreach (AgentTask task in _Tasks)
                    {
                        if (task.Status == AgentTaskStatusEnum.Completed) count++;
                    }

                    return count;
                }
            }
        }

        #endregion

        #region Private-Members

        private readonly object _Sync = new object();
        private List<AgentTask> _Tasks = new List<AgentTask>();
        private int _Version = 0;
        private DateTime? _CreatedUtc = null;
        private TaskPlanChangeKindEnum _LastChangeKind = TaskPlanChangeKindEnum.PlanCreated;
        private string? _LastChangedTaskId = null;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Replace the plan with a validated set of tasks. An empty or null set clears the plan.
        /// </summary>
        /// <param name="tasks">New tasks; each is copied into the plan.</param>
        /// <returns>The change: created, replaced, or cleared.</returns>
        /// <exception cref="TaskPlanValidationException">The tasks fail validation.</exception>
        public TaskPlanChangeKindEnum SetPlan(IReadOnlyList<AgentTask>? tasks)
        {
            TaskPlanValidationResult validation = TaskPlanValidator.Validate(tasks);
            if (!validation.IsValid) throw new TaskPlanValidationException("The proposed task plan is invalid.", validation.Problems);

            lock (_Sync)
            {
                bool hadTasks = _Tasks.Count > 0;
                if (tasks == null || tasks.Count == 0)
                {
                    _Tasks = new List<AgentTask>();
                    _CreatedUtc = null;
                    _Version++;
                    _LastChangeKind = TaskPlanChangeKindEnum.PlanCleared;
                    _LastChangedTaskId = null;
                    return TaskPlanChangeKindEnum.PlanCleared;
                }

                List<AgentTask> copied = new List<AgentTask>(tasks.Count);
                foreach (AgentTask task in tasks) copied.Add(task.Clone());

                _Tasks = copied;
                if (_CreatedUtc == null) _CreatedUtc = DateTime.UtcNow;
                _Version++;
                _LastChangeKind = hadTasks ? TaskPlanChangeKindEnum.PlanReplaced : TaskPlanChangeKindEnum.PlanCreated;
                _LastChangedTaskId = null;
                return _LastChangeKind;
            }
        }

        /// <summary>
        /// Set one task's status and optionally its note, stamping start and finish times on the right transitions.
        /// </summary>
        /// <param name="id">Task id.</param>
        /// <param name="status">New status.</param>
        /// <param name="note">Optional note; required when the status is <see cref="AgentTaskStatusEnum.Failed"/>.</param>
        /// <param name="changeKind">On success, the kind of change.</param>
        /// <returns>True when the task was found and updated.</returns>
        /// <exception cref="ArgumentNullException">id is null.</exception>
        /// <exception cref="ArgumentException">The status is failed and the note is blank.</exception>
        public bool TryUpdateTask(string id, AgentTaskStatusEnum status, string? note, out TaskPlanChangeKindEnum changeKind)
        {
            ArgumentNullException.ThrowIfNull(id);
            if (status == AgentTaskStatusEnum.Failed && String.IsNullOrWhiteSpace(note))
            {
                throw new ArgumentException("A note is required when marking a task as failed.", nameof(note));
            }

            changeKind = TaskPlanChangeKindEnum.TaskStatusChanged;
            lock (_Sync)
            {
                AgentTask? target = null;
                foreach (AgentTask task in _Tasks)
                {
                    if (String.Equals(task.Id, id, StringComparison.Ordinal))
                    {
                        target = task;
                        break;
                    }
                }

                if (target == null) return false;

                bool statusChanged = target.Status != status;
                if (status == AgentTaskStatusEnum.InProgress && target.StartedUtc == null) target.StartedUtc = DateTime.UtcNow;
                if (IsTerminal(status))
                {
                    target.CompletedUtc = DateTime.UtcNow;
                    if (target.StartedUtc != null) target.DurationMs = (long)(target.CompletedUtc.Value - target.StartedUtc.Value).TotalMilliseconds;
                }

                target.Status = status;
                target.FailureMessage = status == AgentTaskStatusEnum.Failed ? note : null;
                if (note != null) target.Note = note;

                changeKind = statusChanged ? TaskPlanChangeKindEnum.TaskStatusChanged : TaskPlanChangeKindEnum.TaskNoteUpdated;
                _Version++;
                _LastChangeKind = changeKind;
                _LastChangedTaskId = target.Id;
                return true;
            }
        }

        /// <summary>
        /// Copy of the tasks in plan order.
        /// </summary>
        /// <returns>Cloned tasks; never null.</returns>
        public IReadOnlyList<AgentTask> Snapshot()
        {
            lock (_Sync)
            {
                List<AgentTask> copy = new List<AgentTask>(_Tasks.Count);
                foreach (AgentTask task in _Tasks) copy.Add(task.Clone());
                return copy;
            }
        }

        /// <summary>
        /// Tasks ready to start: pending, with every dependency completed.
        /// </summary>
        /// <returns>Cloned ready tasks; never null.</returns>
        public IReadOnlyList<AgentTask> ReadyTasks()
        {
            lock (_Sync)
            {
                HashSet<string> completed = new HashSet<string>(StringComparer.Ordinal);
                foreach (AgentTask task in _Tasks)
                {
                    if (task.Status == AgentTaskStatusEnum.Completed) completed.Add(task.Id);
                }

                List<AgentTask> ready = new List<AgentTask>();
                foreach (AgentTask task in _Tasks)
                {
                    if (task.Status != AgentTaskStatusEnum.Pending) continue;
                    bool met = true;
                    foreach (string dependency in task.DependsOn)
                    {
                        if (!completed.Contains(dependency))
                        {
                            met = false;
                            break;
                        }
                    }

                    if (met) ready.Add(task.Clone());
                }

                return ready;
            }
        }

        /// <summary>
        /// Tasks in a status, in plan order.
        /// </summary>
        /// <param name="status">Status.</param>
        /// <returns>Cloned matching tasks; never null.</returns>
        public IReadOnlyList<AgentTask> TasksInStatus(AgentTaskStatusEnum status)
        {
            lock (_Sync)
            {
                List<AgentTask> matches = new List<AgentTask>();
                foreach (AgentTask task in _Tasks)
                {
                    if (task.Status == status) matches.Add(task.Clone());
                }

                return matches;
            }
        }

        /// <summary>
        /// Tally of the tasks by status.
        /// </summary>
        /// <returns>Summary.</returns>
        public TaskPlanSummary Summary()
        {
            return TaskPlanSummary.FromTasks(Snapshot());
        }

        #endregion

        #region Private-Methods

        private static bool IsTerminal(AgentTaskStatusEnum status)
        {
            return status == AgentTaskStatusEnum.Completed || status == AgentTaskStatusEnum.Failed || status == AgentTaskStatusEnum.Skipped;
        }

        #endregion
    }
}

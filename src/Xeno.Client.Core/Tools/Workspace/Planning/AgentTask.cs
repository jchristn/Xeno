namespace Xeno.Client.Tools.Workspace.Planning
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json.Serialization;

    /// <summary>
    /// One node in a conversation's task plan: a short, model-authored unit of work with a stable id, a status, and its
    /// dependencies on other tasks in the same plan.
    /// </summary>
    public sealed class AgentTask
    {
        #region Public-Members

        /// <summary>
        /// Stable, model-assigned identifier (for example "t1"). Unique within a plan. Never null; an empty id fails
        /// plan validation.
        /// </summary>
        [JsonPropertyName("id")]
        public string Id
        {
            get { return _Id; }
            set { _Id = value ?? String.Empty; }
        }

        /// <summary>
        /// Short imperative label. Never null; an empty title fails plan validation.
        /// </summary>
        [JsonPropertyName("title")]
        public string Title
        {
            get { return _Title; }
            set { _Title = value ?? String.Empty; }
        }

        /// <summary>
        /// Current lifecycle status. Defaults to <see cref="AgentTaskStatusEnum.Pending"/>.
        /// </summary>
        [JsonPropertyName("status")]
        public AgentTaskStatusEnum Status { get; set; } = AgentTaskStatusEnum.Pending;

        /// <summary>
        /// Ids of tasks that must be completed before this task is ready to start. Never null; empty means no
        /// prerequisites. These edges must not contain a cycle.
        /// </summary>
        [JsonPropertyName("dependsOn")]
        public List<string> DependsOn
        {
            get { return _DependsOn; }
            set { _DependsOn = value ?? new List<string>(); }
        }

        /// <summary>
        /// Optional running annotation (a blocker, a decision, or a result), or null. Required when the status is
        /// <see cref="AgentTaskStatusEnum.Failed"/>.
        /// </summary>
        [JsonPropertyName("note")]
        public string? Note { get; set; } = null;

        /// <summary>
        /// When the task was added to a plan (UTC).
        /// </summary>
        [JsonPropertyName("createdUtc")]
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// When the task first entered <see cref="AgentTaskStatusEnum.InProgress"/> (UTC), or null.
        /// </summary>
        [JsonPropertyName("startedUtc")]
        public DateTime? StartedUtc { get; set; } = null;

        /// <summary>
        /// When the task reached a terminal status (completed, failed, or skipped) (UTC), or null.
        /// </summary>
        [JsonPropertyName("completedUtc")]
        public DateTime? CompletedUtc { get; set; } = null;

        /// <summary>
        /// Milliseconds between <see cref="StartedUtc"/> and <see cref="CompletedUtc"/>, set when the task reaches a
        /// terminal status after having started; otherwise null.
        /// </summary>
        [JsonPropertyName("durationMs")]
        public long? DurationMs { get; set; } = null;

        /// <summary>
        /// Why the task failed, set when <see cref="Status"/> is <see cref="AgentTaskStatusEnum.Failed"/>; otherwise null.
        /// </summary>
        [JsonPropertyName("failureMessage")]
        public string? FailureMessage { get; set; } = null;

        #endregion

        #region Private-Members

        private string _Id = String.Empty;
        private string _Title = String.Empty;
        private List<string> _DependsOn = new List<string>();

        #endregion

        #region Public-Methods

        /// <summary>
        /// Deep copy, so callers never hold a reference into live plan state.
        /// </summary>
        /// <returns>Copy with an independent <see cref="DependsOn"/> list.</returns>
        public AgentTask Clone()
        {
            return new AgentTask
            {
                Id = _Id,
                Title = _Title,
                Status = Status,
                DependsOn = new List<string>(_DependsOn),
                Note = Note,
                CreatedUtc = CreatedUtc,
                StartedUtc = StartedUtc,
                CompletedUtc = CompletedUtc,
                DurationMs = DurationMs,
                FailureMessage = FailureMessage
            };
        }

        #endregion
    }
}

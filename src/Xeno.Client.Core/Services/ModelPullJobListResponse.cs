namespace Xeno.Client.Services
{
    using System.Collections.Generic;

    /// <summary>
    /// Model pulls tracked by a process, newest first.
    /// </summary>
    public sealed class ModelPullJobListResponse
    {
        /// <summary>
        /// Jobs.
        /// </summary>
        public List<ModelPullJob> Jobs { get; set; } = new List<ModelPullJob>();

        /// <summary>
        /// Number of jobs still queued or running.
        /// </summary>
        public int Active { get; set; } = 0;
    }
}

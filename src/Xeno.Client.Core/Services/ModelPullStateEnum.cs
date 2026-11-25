namespace Xeno.Client.Services
{
    /// <summary>
    /// Lifecycle of a background model pull.
    /// </summary>
    public enum ModelPullStateEnum
    {
        /// <summary>
        /// Accepted and waiting to start.
        /// </summary>
        Queued,

        /// <summary>
        /// Downloading.
        /// </summary>
        Running,

        /// <summary>
        /// The provider finished the pull.
        /// </summary>
        Succeeded,

        /// <summary>
        /// The provider rejected the pull or the connection failed.
        /// </summary>
        Failed,

        /// <summary>
        /// The user cancelled the pull.
        /// </summary>
        Cancelled
    }
}

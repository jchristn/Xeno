namespace Xeno.Client.LocalDashboard.Models
{
    /// <summary>
    /// Local dashboard runtime status.
    /// </summary>
    public enum LocalDashboardStatusEnum
    {
        /// <summary>
        /// Local dashboard is disabled.
        /// </summary>
        Disabled = 0,

        /// <summary>
        /// Local dashboard is starting.
        /// </summary>
        Starting = 1,

        /// <summary>
        /// Local dashboard is listening.
        /// </summary>
        Listening = 2,

        /// <summary>
        /// Local dashboard failed to start or run.
        /// </summary>
        Error = 3,

        /// <summary>
        /// Local dashboard has stopped.
        /// </summary>
        Stopped = 4
    }
}

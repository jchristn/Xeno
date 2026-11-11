namespace Xeno.Core.Chat
{
    /// <summary>
    /// How a tool call was approved.
    /// </summary>
    public enum ChatToolApprovalEnum
    {
        /// <summary>
        /// A read-only tool that ran without asking.
        /// </summary>
        Auto,

        /// <summary>
        /// The tool is waiting for the user.
        /// </summary>
        Pending,

        /// <summary>
        /// The user allowed the call.
        /// </summary>
        Allowed,

        /// <summary>
        /// The user denied the call.
        /// </summary>
        Denied,

        /// <summary>
        /// Nobody answered in time, so the call was not run.
        /// </summary>
        TimedOut
    }
}

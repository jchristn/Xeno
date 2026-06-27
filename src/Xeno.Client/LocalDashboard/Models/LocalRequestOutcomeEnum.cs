namespace Xeno.Client.LocalDashboard.Models
{
    /// <summary>
    /// Local request processing outcome.
    /// </summary>
    public enum LocalRequestOutcomeEnum
    {
        /// <summary>
        /// Request is currently processing.
        /// </summary>
        Processing = 0,

        /// <summary>
        /// Request completed successfully.
        /// </summary>
        Success = 1,

        /// <summary>
        /// Request completed with a failure.
        /// </summary>
        Failure = 2
    }
}

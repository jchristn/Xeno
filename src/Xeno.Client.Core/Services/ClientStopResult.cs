namespace Xeno.Client.Services
{
    /// <summary>
    /// Result of stopping the client worker.
    /// </summary>
    public class ClientStopResult
    {
        /// <summary>
        /// True when no worker is running after the call.
        /// </summary>
        public bool Stopped { get; set; } = false;

        /// <summary>
        /// True when no worker was running before the call.
        /// </summary>
        public bool WasRunning { get; set; } = false;

        /// <summary>
        /// Worker process identifier, or null when no worker was recorded.
        /// </summary>
        public int? ProcessId { get; set; } = null;

        /// <summary>
        /// True when the worker had to be terminated forcibly.
        /// </summary>
        public bool Forced { get; set; } = false;
    }
}

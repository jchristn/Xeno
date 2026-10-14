namespace Xeno.Client.Services
{
    /// <summary>
    /// Result of starting the client worker.
    /// </summary>
    public class ClientStartResult
    {
        /// <summary>
        /// Outcome.
        /// </summary>
        public ClientStartOutcomeEnum Outcome { get; set; } = ClientStartOutcomeEnum.NotRegistered;

        /// <summary>
        /// True when a registered worker is running after the call.
        /// </summary>
        public bool Connected
        {
            get { return Outcome == ClientStartOutcomeEnum.Started || Outcome == ClientStartOutcomeEnum.AlreadyRunning; }
        }

        /// <summary>
        /// Worker process identifier.
        /// </summary>
        public int ProcessId { get; set; } = 0;

        /// <summary>
        /// Connection state observed after the start.
        /// </summary>
        public ClientConnectionState State { get; set; } = new ClientConnectionState();
    }
}

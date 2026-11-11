namespace Xeno.Client.Presentation
{
    /// <summary>
    /// What a front end should do about connecting when it starts.
    /// </summary>
    public enum XenoAutoConnectDecisionEnum
    {
        /// <summary>
        /// Start the client worker.
        /// </summary>
        Connect,

        /// <summary>
        /// A worker is already running (connected, or still connecting on its own).
        /// </summary>
        AlreadyRunning,

        /// <summary>
        /// No credential is configured, so the user needs to sign in first.
        /// </summary>
        NotSignedIn,

        /// <summary>
        /// Turned off with XENO_AUTOCONNECT=0.
        /// </summary>
        Disabled
    }
}

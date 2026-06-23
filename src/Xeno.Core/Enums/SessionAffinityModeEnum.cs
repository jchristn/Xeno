namespace Xeno.Core.Enums
{

    /// <summary>
    /// Sticky session mode.
    /// </summary>
    public enum SessionAffinityModeEnum
    {
        /// <summary>
        /// Disable session affinity.
        /// </summary>
        None = 0,

        /// <summary>
        /// Pin by source IP.
        /// </summary>
        SourceIP = 1,

        /// <summary>
        /// Pin by API key or bearer token.
        /// </summary>
        ApiKey = 2,

        /// <summary>
        /// Pin by configured header.
        /// </summary>
        Header = 3
    }
}

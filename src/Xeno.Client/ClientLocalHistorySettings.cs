namespace Xeno.Client
{
    /// <summary>
    /// Local dashboard request-history retention settings.
    /// </summary>
    public class ClientLocalHistorySettings
    {
        /// <summary>
        /// Maximum retained local request history entries. Default is 1000.
        /// </summary>
        public int MaxEntries { get; set; } = 1000;

        /// <summary>
        /// Maximum stored request or response body bytes. Default is 65536.
        /// </summary>
        public int MaxBodyBytes { get; set; } = 65536;

        /// <summary>
        /// Persist request and response bodies locally. Default is true.
        /// </summary>
        public bool PersistBodies { get; set; } = true;
    }
}

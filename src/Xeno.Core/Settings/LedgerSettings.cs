namespace Xeno.Core.Settings
{
    using System;

    /// <summary>
    /// Ledger integration settings.
    /// </summary>
    public class LedgerSettings
    {
        /// <summary>
        /// Whether ledgering is enabled.
        /// </summary>
        public bool Enabled { get; set; } = false;

        /// <summary>
        /// NetLedger base URL.
        /// </summary>
        public string BaseUrl { get; set; } = "http://localhost:8080";

        /// <summary>
        /// NetLedger bearer token or access key.
        /// </summary>
        public string? ApiKey { get; set; } = null;

        /// <summary>
        /// Optional NetLedger secret key for access-key authentication.
        /// </summary>
        public string? SecretKey { get; set; } = null;

        /// <summary>
        /// Drain interval in milliseconds. Minimum 100, maximum 600000. Default is 1000.
        /// </summary>
        public int DrainIntervalMs
        {
            get { return _DrainIntervalMs; }
            set { _DrainIntervalMs = Math.Clamp(value, 100, 600000); }
        }

        /// <summary>
        /// Maximum events drained per cycle. Minimum 1, maximum 1000. Default is 100.
        /// </summary>
        public int MaxBatchSize
        {
            get { return _MaxBatchSize; }
            set { _MaxBatchSize = Math.Clamp(value, 1, 1000); }
        }

        /// <summary>
        /// Maximum in-memory queue depth. Minimum 1, maximum 100000. Default is 10000.
        /// </summary>
        public int MaxQueueDepth
        {
            get { return _MaxQueueDepth; }
            set { _MaxQueueDepth = Math.Clamp(value, 1, 100000); }
        }

        /// <summary>
        /// NetLedger request timeout in milliseconds. Minimum 1000, maximum 300000. Default is 30000.
        /// </summary>
        public int RequestTimeoutMs
        {
            get { return _RequestTimeoutMs; }
            set { _RequestTimeoutMs = Math.Clamp(value, 1000, 300000); }
        }

        /// <summary>
        /// Whether tenant links may be created automatically.
        /// </summary>
        public bool CreateTenantsAutomatically { get; set; } = true;

        /// <summary>
        /// Whether account links may be created automatically.
        /// </summary>
        public bool CreateAccountsAutomatically { get; set; } = true;

        /// <summary>
        /// Whether debit and credit entries should be committed during creation.
        /// </summary>
        public bool CommitEntriesImmediately { get; set; } = true;

        #region Private-Members

        private int _DrainIntervalMs = 1000;
        private int _MaxBatchSize = 100;
        private int _MaxQueueDepth = 10000;
        private int _RequestTimeoutMs = 30000;

        #endregion
    }
}

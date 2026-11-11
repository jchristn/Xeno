namespace Xeno.Client
{
    using System;

    /// <summary>
    /// Usage analytics settings: how much one chart query may read, and how many groups charts show by name.
    /// </summary>
    public class ClientAnalyticsSettings
    {
        private int _MaxRecordsPerQuery = 250000;
        private int _DefaultTopN = 8;

        /// <summary>
        /// Records one usage analytics query reads before it stops and reports the range as truncated. Default 250,000.
        /// Clamped to 10,000 through 5,000,000.
        /// </summary>
        public int MaxRecordsPerQuery
        {
            get { return _MaxRecordsPerQuery; }
            set { _MaxRecordsPerQuery = Math.Clamp(value, 10000, 5000000); }
        }

        /// <summary>
        /// Groups (models, providers, clients, users) shown by name before the rest fold into Other. Default 8.
        /// Clamped to 1 through 20.
        /// </summary>
        public int DefaultTopN
        {
            get { return _DefaultTopN; }
            set { _DefaultTopN = Math.Clamp(value, 1, 20); }
        }

        /// <summary>
        /// Builder options for these settings.
        /// </summary>
        /// <returns>Options.</returns>
        public Xeno.Core.Analytics.UsageAnalyticsOptions ToOptions()
        {
            return new Xeno.Core.Analytics.UsageAnalyticsOptions { MaxRecords = MaxRecordsPerQuery, DefaultTopN = DefaultTopN };
        }
    }
}

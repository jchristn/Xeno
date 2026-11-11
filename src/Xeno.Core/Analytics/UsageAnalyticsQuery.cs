namespace Xeno.Core.Analytics
{
    using System;

    /// <summary>
    /// Typed query for usage analytics. Setters clamp numeric values and trim text filters.
    /// </summary>
    public sealed class UsageAnalyticsQuery
    {
        #region Private-Members

        private int _BucketMinutes = 15;
        private int? _TopN = null;
        private int _UtcOffsetMinutes = 0;
        private string? _Model = null;
        private string? _Provider = null;
        private string? _ClientId = null;
        private string? _UserId = null;
        private string? _CredentialId = null;
        private string? _TenantId = null;

        #endregion

        #region Public-Members

        /// <summary>
        /// Range start, UTC.
        /// </summary>
        public DateTime FromUtc { get; set; } = DateTime.UtcNow.AddDays(-1);

        /// <summary>
        /// Range end, UTC.
        /// </summary>
        public DateTime ToUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Bucket size in minutes. Minimum 1, maximum 525,600 (one year). Default 15.
        /// </summary>
        public int BucketMinutes
        {
            get { return _BucketMinutes; }
            set { _BucketMinutes = Math.Clamp(value, 1, 525600); }
        }

        /// <summary>
        /// Only this model, or null for every model.
        /// </summary>
        public string? Model
        {
            get { return _Model; }
            set { _Model = Clean(value); }
        }

        /// <summary>
        /// Only this provider (or answering client name), or null.
        /// </summary>
        public string? Provider
        {
            get { return _Provider; }
            set { _Provider = Clean(value); }
        }

        /// <summary>
        /// Only this client (server), or null.
        /// </summary>
        public string? ClientId
        {
            get { return _ClientId; }
            set { _ClientId = Clean(value); }
        }

        /// <summary>
        /// Only this user (server), or null.
        /// </summary>
        public string? UserId
        {
            get { return _UserId; }
            set { _UserId = Clean(value); }
        }

        /// <summary>
        /// Only this credential (server), or null.
        /// </summary>
        public string? CredentialId
        {
            get { return _CredentialId; }
            set { _CredentialId = Clean(value); }
        }

        /// <summary>
        /// Tenant to read (server, global administrators only), or null for the caller's tenant.
        /// </summary>
        public string? TenantId
        {
            get { return _TenantId; }
            set { _TenantId = Clean(value); }
        }

        /// <summary>
        /// What activity is grouped by. Default Model.
        /// </summary>
        public UsageGroupByEnum GroupBy { get; set; } = UsageGroupByEnum.Model;

        /// <summary>
        /// What ranks the groups. Default Requests.
        /// </summary>
        public UsageGroupMetricEnum GroupMetric { get; set; } = UsageGroupMetricEnum.Requests;

        /// <summary>
        /// Group keys shown before the rest fold into Other, 1 to 20, or null for the builder default.
        /// </summary>
        public int? TopN
        {
            get { return _TopN; }
            set { _TopN = value.HasValue ? Math.Clamp(value.Value, 1, 20) : null; }
        }

        /// <summary>
        /// The viewer's offset from UTC in minutes, for hour-of-day and weekday heatmap cells. Clamped to -840 to 840.
        /// </summary>
        public int UtcOffsetMinutes
        {
            get { return _UtcOffsetMinutes; }
            set { _UtcOffsetMinutes = Math.Clamp(value, -840, 840); }
        }

        /// <summary>
        /// Sections to compute. Default all.
        /// </summary>
        public UsageSectionsEnum Sections { get; set; } = UsageSectionsEnum.All;

        #endregion

        #region Private-Methods

        private static string? Clean(string? value)
        {
            return String.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        #endregion
    }
}

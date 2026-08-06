namespace Xeno.Core.Settings
{
    using System;

    /// <summary>
    /// Service-layer cache settings. The control plane is authoritative in the database; hot, slow-changing
    /// entities (currently routing policies) are cached per node with a bounded, sliding time-to-live so a
    /// node evicts an entry after it has gone unused for the configured interval. Security-sensitive
    /// entities (tenants, users, credentials, sessions, permissions, roles, role assignments) are never
    /// cached and are always read directly from the database.
    /// </summary>
    public class CacheSettings
    {
        #region Public-Members

        /// <summary>
        /// Whether service-layer caching is enabled. When false, cached entities are read directly from the
        /// database on every request (highest consistency, more database load).
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Sliding time-to-live in seconds for cached routing policies; an entry expires this long after its
        /// last use. Clamped to the range [1, 3600].
        /// </summary>
        public int RoutingPolicyTtlSeconds
        {
            get
            {
                return _RoutingPolicyTtlSeconds;
            }
            set
            {
                _RoutingPolicyTtlSeconds = Math.Clamp(value, 1, 3600);
            }
        }

        /// <summary>
        /// Maximum number of tenants' routing-policy lists retained before least-recently-used eviction.
        /// Clamped to the range [1, 100000].
        /// </summary>
        public int RoutingPolicyCapacity
        {
            get
            {
                return _RoutingPolicyCapacity;
            }
            set
            {
                _RoutingPolicyCapacity = Math.Clamp(value, 1, 100000);
            }
        }

        #endregion

        #region Private-Members

        private int _RoutingPolicyTtlSeconds = 30;
        private int _RoutingPolicyCapacity = 256;

        #endregion
    }
}

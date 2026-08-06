namespace Xeno.Core.Caching
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// A bounded, sliding-expiration read-through cache for a single control-plane entity type, used by
    /// the service layer for the cache-eligible entities (routing policies, model definitions, model
    /// configurations, clients, ledger links). Auth/authz entities are never cached here.
    ///
    /// <para>Backed by <c>Caching.LRUCache</c>: capacity bounds memory (least-recently-used
    /// eviction) and the underlying absolute-expiration sweeper drops idle entries. Sliding behaviour
    /// ("expire after N seconds of non-use") is layered on top by refreshing an entry's expiry on every
    /// cache hit. Instances are safe for concurrent use.</para>
    /// </summary>
    /// <typeparam name="TValue">Cached value type.</typeparam>
    public sealed class EntityCache<TValue> where TValue : class
    {
        #region Private-Members

        private readonly global::Caching.LRUCache<string, TValue> _Cache;
        private readonly int _TtlSeconds;

        #endregion

        #region Constructors-and-Factories

        /// <summary>
        /// Instantiate the cache.
        /// </summary>
        /// <param name="capacity">Maximum number of entries retained before least-recently-used eviction. Clamped to a minimum of 1.</param>
        /// <param name="ttlSeconds">Idle time-to-live in seconds; an entry expires this long after its last access. Clamped to a minimum of 1.</param>
        /// <param name="evictCount">Number of entries removed when capacity is exceeded. Clamped to the range [1, capacity].</param>
        public EntityCache(int capacity, int ttlSeconds, int evictCount = 1)
        {
            int boundedCapacity = Math.Max(1, capacity);
            int boundedEvict = Math.Clamp(evictCount, 1, boundedCapacity);
            _TtlSeconds = Math.Max(1, ttlSeconds);
            _Cache = new global::Caching.LRUCache<string, TValue>(boundedCapacity, boundedEvict, StringComparer.Ordinal);
        }

        #endregion

        #region Public-Methods

        /// <summary>
        /// Return the cached value for a key, refreshing its idle expiry; on a miss, invoke the loader,
        /// cache any non-null result, and return it. The loader is only invoked on a miss.
        /// </summary>
        /// <param name="key">Cache key (typically the entity identifier, optionally tenant-qualified).</param>
        /// <param name="loader">Asynchronous loader invoked on a cache miss.</param>
        /// <param name="token">Cancellation token passed to the loader.</param>
        /// <returns>The cached or freshly loaded value, or null when the loader yields null.</returns>
        public async Task<TValue?> GetOrLoadAsync(string key, Func<CancellationToken, Task<TValue?>> loader, CancellationToken token = default)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (loader == null) throw new ArgumentNullException(nameof(loader));

            if (_Cache.TryGet(key, out TValue? cached) && cached != null)
            {
                _Cache.AddReplace(key, cached, Expiry());
                return cached;
            }

            TValue? loaded = await loader(token).ConfigureAwait(false);
            if (loaded != null) _Cache.AddReplace(key, loaded, Expiry());
            return loaded;
        }

        /// <summary>
        /// Insert or replace a value, resetting its idle expiry. Use after a write so the cache reflects
        /// the just-persisted state on the local node.
        /// </summary>
        /// <param name="key">Cache key.</param>
        /// <param name="value">Value to cache.</param>
        public void Put(string key, TValue value)
        {
            if (String.IsNullOrEmpty(key)) throw new ArgumentNullException(nameof(key));
            if (value == null) throw new ArgumentNullException(nameof(value));
            _Cache.AddReplace(key, value, Expiry());
        }

        /// <summary>
        /// Remove a key from the cache. Use after a write or delete so a stale local copy is not served.
        /// </summary>
        /// <param name="key">Cache key.</param>
        public void Invalidate(string key)
        {
            if (String.IsNullOrEmpty(key)) return;
            _Cache.Remove(key);
        }

        /// <summary>
        /// Remove all entries.
        /// </summary>
        public void Clear()
        {
            _Cache.Clear();
        }

        #endregion

        #region Private-Methods

        private DateTime Expiry()
        {
            return DateTime.UtcNow.AddSeconds(_TtlSeconds);
        }

        #endregion
    }
}

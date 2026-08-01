namespace Xeno.Server.Services
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Xeno.Core.Models.Telemetry;

    /// <summary>
    /// Thread-safe in-memory cache for latest persisted client telemetry.
    /// </summary>
    public sealed class ClientTelemetryLatestCache : IDisposable
    {
        private readonly ReaderWriterLockSlim _Lock = new ReaderWriterLockSlim();
        private readonly Dictionary<string, ServerClientTelemetrySampleRecord> _HostSamples = new Dictionary<string, ServerClientTelemetrySampleRecord>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ServerProviderTelemetrySampleRecord> _ProviderSamples = new Dictionary<string, ServerProviderTelemetrySampleRecord>(StringComparer.OrdinalIgnoreCase);
        private bool _Disposed;

        /// <summary>
        /// Add or replace the latest host telemetry sample for a tenant/client pair.
        /// </summary>
        /// <param name="sample">Persisted host telemetry sample.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="sample"/> is null.</exception>
        public void SetHost(ServerClientTelemetrySampleRecord sample)
        {
            ArgumentNullException.ThrowIfNull(sample);
            string key = HostKey(sample.TenantId, sample.ClientId);
            _Lock.EnterWriteLock();
            try
            {
                _HostSamples[key] = sample;
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Add or replace the latest provider telemetry sample for a tenant/client/provider pair.
        /// </summary>
        /// <param name="sample">Persisted provider telemetry sample.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="sample"/> is null.</exception>
        public void SetProvider(ServerProviderTelemetrySampleRecord sample)
        {
            ArgumentNullException.ThrowIfNull(sample);
            string key = ProviderKey(sample.TenantId, sample.ClientId, sample.ProviderName);
            _Lock.EnterWriteLock();
            try
            {
                _ProviderSamples[key] = sample;
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Read latest host telemetry for a tenant and optional client.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="clientId">Optional client identifier.</param>
        /// <returns>Cached latest host samples.</returns>
        public IReadOnlyList<ServerClientTelemetrySampleRecord> GetHost(string tenantId, string? clientId)
        {
            _Lock.EnterReadLock();
            try
            {
                return _HostSamples.Values
                    .Where(sample => MatchesTenantAndClient(sample.TenantId, sample.ClientId, tenantId, clientId))
                    .ToList();
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Read latest provider telemetry for a tenant and optional client.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        /// <param name="clientId">Optional client identifier.</param>
        /// <returns>Cached latest provider samples.</returns>
        public IReadOnlyList<ServerProviderTelemetrySampleRecord> GetProviders(string tenantId, string? clientId)
        {
            _Lock.EnterReadLock();
            try
            {
                return _ProviderSamples.Values
                    .Where(sample => MatchesTenantAndClient(sample.TenantId, sample.ClientId, tenantId, clientId))
                    .ToList();
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Read the latest host telemetry samples across all tenants and clients.
        /// </summary>
        /// <returns>All cached latest host samples.</returns>
        public IReadOnlyList<ServerClientTelemetrySampleRecord> GetAllHost()
        {
            _Lock.EnterReadLock();
            try
            {
                return _HostSamples.Values.ToList();
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Read the latest provider telemetry samples across all tenants and clients.
        /// </summary>
        /// <returns>All cached latest provider samples.</returns>
        public IReadOnlyList<ServerProviderTelemetrySampleRecord> GetAllProviders()
        {
            _Lock.EnterReadLock();
            try
            {
                return _ProviderSamples.Values.ToList();
            }
            finally
            {
                _Lock.ExitReadLock();
            }
        }

        /// <summary>
        /// Remove all cached samples for a tenant.
        /// </summary>
        /// <param name="tenantId">Tenant identifier.</param>
        public void ClearTenant(string tenantId)
        {
            _Lock.EnterWriteLock();
            try
            {
                foreach (string key in _HostSamples.Where(pair => String.Equals(pair.Value.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Key).ToList())
                {
                    _HostSamples.Remove(key);
                }

                foreach (string key in _ProviderSamples.Where(pair => String.Equals(pair.Value.TenantId, tenantId, StringComparison.OrdinalIgnoreCase)).Select(pair => pair.Key).ToList())
                {
                    _ProviderSamples.Remove(key);
                }
            }
            finally
            {
                _Lock.ExitWriteLock();
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            if (_Disposed) return;
            _Lock.Dispose();
            _Disposed = true;
        }

        private static bool MatchesTenantAndClient(string tenantId, string? sampleClientId, string requestedTenantId, string? requestedClientId)
        {
            if (!String.Equals(tenantId, requestedTenantId, StringComparison.OrdinalIgnoreCase)) return false;
            return String.IsNullOrWhiteSpace(requestedClientId) || String.Equals(sampleClientId, requestedClientId, StringComparison.OrdinalIgnoreCase);
        }

        private static string HostKey(string tenantId, string? clientId)
        {
            return tenantId + "|" + (clientId ?? String.Empty);
        }

        private static string ProviderKey(string tenantId, string? clientId, string providerName)
        {
            return tenantId + "|" + (clientId ?? String.Empty) + "|" + providerName;
        }
    }
}

namespace Test.Shared
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Xeno.Core.Database.Interfaces;
    using Xeno.Core.Models;

    /// <summary>
    /// In-memory <see cref="IAuditMethods"/> implementation for tests. Collects inserted records and
    /// supports the same tenant/user/id-scoped filtering as the database providers.
    /// </summary>
    public sealed class InMemoryAuditMethods : IAuditMethods
    {
        /// <summary>
        /// Records that have been inserted.
        /// </summary>
        public List<AuditRecord> Records { get; } = new List<AuditRecord>();

        /// <inheritdoc />
        public Task InsertAsync(AuditRecord record, CancellationToken token)
        {
            if (record != null && !Records.Any(item => item.Id == record.Id)) Records.Add(record);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task<AuditRecordPage> QueryAsync(AuditQuery query, CancellationToken token)
        {
            List<AuditRecord> matched = Filter(query).OrderByDescending(item => item.CreatedUtc).ToList();
            int total = matched.Count;
            if (query.Limit > 0) matched = matched.Skip(Math.Max(0, query.Offset)).Take(query.Limit).ToList();
            return Task.FromResult(new AuditRecordPage { Items = matched, Total = total });
        }

        /// <inheritdoc />
        public Task<int> CountAsync(AuditQuery query, CancellationToken token)
        {
            return Task.FromResult(Filter(query).Count());
        }

        /// <inheritdoc />
        public Task<int> DeleteAsync(AuditQuery query, CancellationToken token)
        {
            List<AuditRecord> matched = Filter(query).ToList();
            foreach (AuditRecord record in matched) Records.Remove(record);
            return Task.FromResult(matched.Count);
        }

        /// <inheritdoc />
        public Task<int> PruneOlderThanAsync(DateTime cutoffUtc, CancellationToken token)
        {
            List<AuditRecord> matched = Records.Where(item => item.CreatedUtc < cutoffUtc).ToList();
            foreach (AuditRecord record in matched) Records.Remove(record);
            return Task.FromResult(matched.Count);
        }

        private IEnumerable<AuditRecord> Filter(AuditQuery query)
        {
            IEnumerable<AuditRecord> results = Records;

            if (!String.IsNullOrEmpty(query.TenantId))
            {
                results = query.MatchTargetTenant
                    ? results.Where(item => String.Equals(item.TenantId, query.TenantId, StringComparison.Ordinal) || String.Equals(item.TargetTenantId, query.TenantId, StringComparison.Ordinal))
                    : results.Where(item => String.Equals(item.TenantId, query.TenantId, StringComparison.Ordinal));
            }

            if (!String.IsNullOrEmpty(query.UserId))
            {
                results = results.Where(item => String.Equals(item.PrincipalId, query.UserId, StringComparison.Ordinal) || String.Equals(item.TargetResourceId, query.UserId, StringComparison.Ordinal));
            }

            if (query.Ids != null && query.Ids.Count > 0)
            {
                results = results.Where(item => query.Ids.Contains(item.Id));
            }

            if (query.FromUtc.HasValue) results = results.Where(item => item.CreatedUtc >= query.FromUtc.Value);
            if (query.ToUtc.HasValue) results = results.Where(item => item.CreatedUtc < query.ToUtc.Value);

            return results;
        }
    }
}

namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Builds the provider health strip: one row per provider, one cell per bucket. A bucket is Healthy when every
    /// sample was healthy, Unhealthy when every sample failed, Degraded otherwise, and NoData without samples.
    /// Thread safe (stateless).
    /// </summary>
    public static class ProviderHealthBuilder
    {
        /// <summary>
        /// Build the strip.
        /// </summary>
        /// <param name="samples">Samples, in any order.</param>
        /// <param name="query">Query.</param>
        /// <returns>Strip.</returns>
        /// <exception cref="ArgumentNullException">samples or query is null.</exception>
        /// <exception cref="ArgumentException">The range or bucket size is not valid.</exception>
        public static ProviderHealthStrip Build(IEnumerable<ProviderHealthSample> samples, ProviderHealthQuery query)
        {
            ArgumentNullException.ThrowIfNull(samples);
            ArgumentNullException.ThrowIfNull(query);
            UsageRanges.Validate(query.FromUtc, query.ToUtc, query.BucketMinutes);
            int count = UsageRanges.BucketCount(query.FromUtc, query.ToUtc, query.BucketMinutes);
            TimeSpan size = TimeSpan.FromMinutes(query.BucketMinutes);
            ProviderHealthStrip strip = new ProviderHealthStrip { FromUtc = query.FromUtc, ToUtc = query.ToUtc, BucketMinutes = query.BucketMinutes };
            Dictionary<string, List<ProviderHealthSample>> byProvider = new Dictionary<string, List<ProviderHealthSample>>(StringComparer.Ordinal);
            foreach (ProviderHealthSample sample in samples)
            {
                if (sample == null || sample.TimestampUtc < query.FromUtc || sample.TimestampUtc >= query.ToUtc) continue;
                if (query.ProviderName != null && !String.Equals(sample.ProviderName, query.ProviderName, StringComparison.Ordinal)) continue;
                if (query.ClientId != null && !String.Equals(sample.ClientId, query.ClientId, StringComparison.Ordinal)) continue;
                string key = (sample.ClientId ?? String.Empty) + "\n" + sample.ProviderName;
                if (!byProvider.TryGetValue(key, out List<ProviderHealthSample>? list))
                {
                    list = new List<ProviderHealthSample>();
                    byProvider[key] = list;
                }

                list.Add(sample);
            }

            foreach (List<ProviderHealthSample> list in byProvider.Values.OrderBy(item => item[0].ProviderName, StringComparer.Ordinal).ThenBy(item => item[0].ClientId ?? String.Empty, StringComparer.Ordinal))
            {
                ProviderHealthRow row = new ProviderHealthRow { ProviderName = list[0].ProviderName, ClientId = list[0].ClientId };
                for (int i = 0; i < count; i++) row.Cells.Add(new ProviderHealthCell { StartUtc = query.FromUtc + TimeSpan.FromTicks(size.Ticks * i) });
                foreach (ProviderHealthSample sample in list)
                {
                    int index = (int)((sample.TimestampUtc - query.FromUtc).Ticks / size.Ticks);
                    if (index < 0 || index >= count) continue;
                    row.Cells[index].Samples++;
                    if (!sample.Healthy) row.Cells[index].Failures++;
                }

                int total = 0;
                int healthy = 0;
                foreach (ProviderHealthCell cell in row.Cells)
                {
                    total += cell.Samples;
                    healthy += cell.Samples - cell.Failures;
                    cell.Status = cell.Samples == 0 ? ProviderHealthStatusEnum.NoData
                        : cell.Failures == 0 ? ProviderHealthStatusEnum.Healthy
                        : cell.Failures == cell.Samples ? ProviderHealthStatusEnum.Unhealthy
                        : ProviderHealthStatusEnum.Degraded;
                }

                row.UptimePercent = total == 0 ? null : Math.Round(healthy * 100.0 / total, 2);
                foreach (ProviderHealthSample sample in list.OrderByDescending(item => item.TimestampUtc))
                {
                    if (sample.Healthy) break;
                    row.ConsecutiveFailures++;
                }

                strip.Providers.Add(row);
            }

            return strip;
        }
    }
}

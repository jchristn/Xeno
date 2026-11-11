namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Buckets connected-client samples. Each bucket takes its last sample, and an empty bucket stays empty so a server
    /// outage shows as a gap instead of a repeated count.
    /// </summary>
    public static class ConnectedClientHistoryBuilder
    {
        #region Public-Methods

        /// <summary>
        /// Build the history for a window.
        /// </summary>
        /// <param name="samples">Samples for one tenant.</param>
        /// <param name="fromUtc">Window start.</param>
        /// <param name="toUtc">Window end.</param>
        /// <param name="bucketMinutes">Bucket width in minutes.</param>
        /// <returns>History.</returns>
        /// <exception cref="ArgumentNullException">samples is null.</exception>
        /// <exception cref="ArgumentOutOfRangeException">bucketMinutes is not positive.</exception>
        public static ConnectedClientHistory Build(IEnumerable<ConnectedClientSample> samples, DateTime fromUtc, DateTime toUtc, int bucketMinutes)
        {
            ArgumentNullException.ThrowIfNull(samples);
            if (bucketMinutes < 1) throw new ArgumentOutOfRangeException(nameof(bucketMinutes));
            ConnectedClientHistory history = new ConnectedClientHistory { FromUtc = fromUtc, ToUtc = toUtc, BucketMinutes = bucketMinutes };
            TimeSpan width = TimeSpan.FromMinutes(bucketMinutes);
            for (DateTime start = fromUtc; start < toUtc; start = start.Add(width))
            {
                DateTime end = start.Add(width) < toUtc ? start.Add(width) : toUtc;
                history.Buckets.Add(new ConnectedClientHistoryBucket { StartUtc = start, EndUtc = end });
            }

            foreach (ConnectedClientSample sample in samples.OrderBy(item => item.SampledUtc))
            {
                if (sample.SampledUtc < fromUtc || sample.SampledUtc >= toUtc) continue;
                int index = (int)((sample.SampledUtc - fromUtc).Ticks / width.Ticks);
                if (index >= history.Buckets.Count) continue;
                history.Buckets[index].Connected = sample.Connected;
                history.Buckets[index].Total = sample.Total;
            }

            return history;
        }

        #endregion
    }
}

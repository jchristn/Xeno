namespace Xeno.Core.Chat
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// Builds <see cref="ChatAnalytics"/> from turn records. Shared by every chat store so the numbers agree.
    /// </summary>
    public static class ChatAnalyticsBuilder
    {
        /// <summary>
        /// Most buckets a range is split into; larger ranges get wider buckets.
        /// </summary>
        public const int MaximumBuckets = 500;

        /// <summary>
        /// Build analytics.
        /// </summary>
        /// <param name="turns">Turns that started in the range, with <see cref="ChatTurn.FeedbackRating"/> filled in.</param>
        /// <param name="fromUtc">Start, UTC.</param>
        /// <param name="toUtc">End, UTC. Must be after <paramref name="fromUtc"/>.</param>
        /// <param name="bucketSeconds">Requested bucket size in seconds; widened to stay within <see cref="MaximumBuckets"/>.</param>
        /// <returns>Analytics.</returns>
        /// <exception cref="ArgumentNullException">turns is null.</exception>
        /// <exception cref="ArgumentException">toUtc is not after fromUtc.</exception>
        public static ChatAnalytics Build(IEnumerable<ChatTurn> turns, DateTime fromUtc, DateTime toUtc, int bucketSeconds)
        {
            ArgumentNullException.ThrowIfNull(turns);
            if (toUtc <= fromUtc) throw new ArgumentException("The end of the range must be after its start.");
            double rangeSeconds = (toUtc - fromUtc).TotalSeconds;
            int seconds = Math.Max(1, bucketSeconds);
            if (rangeSeconds / seconds > MaximumBuckets) seconds = (int)Math.Ceiling(rangeSeconds / MaximumBuckets);
            ChatAnalytics analytics = new ChatAnalytics { FromUtc = fromUtc, ToUtc = toUtc, BucketSeconds = seconds, BucketMinutes = Math.Max(1, seconds / 60) };
            for (DateTime start = fromUtc; start < toUtc; start = start.AddSeconds(seconds))
            {
                analytics.Buckets.Add(new ChatAnalyticsBucket { StartUtc = start });
            }

            List<ChatTurn> list = turns.Where(turn => turn.CreatedUtc >= fromUtc && turn.CreatedUtc < toUtc && turn.Status != ChatTurnStatusEnum.Running).ToList();
            Dictionary<string, ChatModelBreakdown> models = new Dictionary<string, ChatModelBreakdown>(StringComparer.OrdinalIgnoreCase);
            Dictionary<string, List<double>> modelTimes = new Dictionary<string, List<double>>(StringComparer.OrdinalIgnoreCase);
            List<double> totals = new List<double>();
            List<double> firstTokens = new List<double>();
            foreach (ChatTurn turn in list)
            {
                bool failed = turn.Status != ChatTurnStatusEnum.Success;
                bool local = !failed && IsLocal(turn.Route);
                analytics.Turns++;
                if (failed) analytics.Errors++;
                else if (local) analytics.Local++;
                else analytics.Network++;
                if (turn.Fallback) analytics.Fallbacks++;
                analytics.PromptTokens += turn.PromptTokens ?? 0;
                analytics.CompletionTokens += turn.CompletionTokens ?? 0;
                analytics.ToolCalls += turn.ToolCallCount;
                if (turn.FeedbackRating == 1) analytics.ThumbsUp++;
                if (turn.FeedbackRating == -1) analytics.ThumbsDown++;
                if (!failed)
                {
                    totals.Add(turn.TotalMs);
                    if (turn.TimeToFirstTokenMs.HasValue) firstTokens.Add(turn.TimeToFirstTokenMs.Value);
                }

                int index = (int)Math.Floor((turn.CreatedUtc - fromUtc).TotalSeconds / seconds);
                if (index >= 0 && index < analytics.Buckets.Count)
                {
                    ChatAnalyticsBucket bucket = analytics.Buckets[index];
                    if (failed) bucket.Errors++;
                    else if (local) bucket.Local++;
                    else bucket.Network++;
                    if (turn.FeedbackRating == 1) bucket.ThumbsUp++;
                    if (turn.FeedbackRating == -1) bucket.ThumbsDown++;
                    bucket.CompletionTokens += turn.CompletionTokens ?? 0;
                }

                string model = String.IsNullOrWhiteSpace(turn.Model) ? "(none)" : turn.Model;
                if (!models.TryGetValue(model, out ChatModelBreakdown? breakdown))
                {
                    breakdown = new ChatModelBreakdown { Model = model };
                    models[model] = breakdown;
                    modelTimes[model] = new List<double>();
                }

                breakdown.Turns++;
                if (failed) breakdown.Errors++;
                else modelTimes[model].Add(turn.TotalMs);
                if (turn.FeedbackRating == 1) breakdown.ThumbsUp++;
                if (turn.FeedbackRating == -1) breakdown.ThumbsDown++;
                breakdown.CompletionTokens += turn.CompletionTokens ?? 0;
            }

            analytics.ErrorRate = analytics.Turns == 0 ? 0 : (double)analytics.Errors / analytics.Turns;
            int ratings = analytics.ThumbsUp + analytics.ThumbsDown;
            analytics.NegativeRate = ratings == 0 ? null : (double)analytics.ThumbsDown / ratings;
            analytics.P50Ms = Percentile(totals, 50);
            analytics.P95Ms = Percentile(totals, 95);
            analytics.P50TimeToFirstTokenMs = Percentile(firstTokens, 50);
            foreach (KeyValuePair<string, ChatModelBreakdown> pair in models) pair.Value.P50Ms = Percentile(modelTimes[pair.Key], 50);
            analytics.Models = models.Values.OrderByDescending(item => item.Turns).ThenBy(item => item.Model, StringComparer.OrdinalIgnoreCase).ToList();
            return analytics;
        }

        /// <summary>
        /// Nearest-rank percentile.
        /// </summary>
        /// <param name="values">Values.</param>
        /// <param name="percent">Percentile, 0 to 100.</param>
        /// <returns>Value, or null for no values.</returns>
        public static double? Percentile(IReadOnlyCollection<double> values, double percent)
        {
            if (values == null || values.Count == 0) return null;
            List<double> sorted = values.OrderBy(value => value).ToList();
            int rank = (int)Math.Ceiling(Math.Clamp(percent, 0, 100) / 100.0 * sorted.Count);
            return sorted[Math.Clamp(rank - 1, 0, sorted.Count - 1)];
        }

        /// <summary>
        /// True when a route names a provider on this machine.
        /// </summary>
        /// <param name="route">Route.</param>
        /// <returns>True for "local" and "local:&lt;provider&gt;".</returns>
        public static bool IsLocal(string? route)
        {
            return !String.IsNullOrEmpty(route) && (String.Equals(route, "local", StringComparison.OrdinalIgnoreCase) || route.StartsWith("local:", StringComparison.OrdinalIgnoreCase));
        }
    }
}

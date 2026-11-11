namespace Xeno.Core.Analytics
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Turns usage records into <see cref="UsageAnalytics"/>: gap-filled buckets, exact nearest-rank percentiles, top
    /// groups with Other, the token activity heatmap, and stage averages. One pass places each record in its bucket by
    /// index. Every analytics route uses this builder so every surface draws the same numbers. Thread safe (stateless).
    /// </summary>
    public static class UsageAnalyticsBuilder
    {
        #region Public-Members

        /// <summary>
        /// Group key for records whose group value is missing.
        /// </summary>
        public const string UnknownKey = "Unknown";

        /// <summary>
        /// Group key for everything outside the top groups.
        /// </summary>
        public const string OtherKey = "Other";

        /// <summary>
        /// Streaming shorter than this many milliseconds is not used for throughput; total time is used instead.
        /// </summary>
        public const double MinimumStreamingMs = 50;

        #endregion

        #region Public-Methods

        /// <summary>
        /// Stages each dataset can have, in display order.
        /// </summary>
        /// <param name="dataset">Dataset.</param>
        /// <returns>Stages.</returns>
        public static List<UsageStageEnum> StageOrder(UsageDatasetEnum dataset)
        {
            switch (dataset)
            {
                case UsageDatasetEnum.NetworkRequests:
                    return new List<UsageStageEnum> { UsageStageEnum.ServerOverhead, UsageStageEnum.RelayAndClient, UsageStageEnum.ProviderHeaders, UsageStageEnum.ProviderFirstToken, UsageStageEnum.Streaming };
                case UsageDatasetEnum.ServedRequests:
                    return new List<UsageStageEnum> { UsageStageEnum.ProviderHeaders, UsageStageEnum.ProviderFirstToken, UsageStageEnum.Streaming };
                case UsageDatasetEnum.LocalEndpoint:
                    return new List<UsageStageEnum> { UsageStageEnum.Queue, UsageStageEnum.ProviderFirstToken, UsageStageEnum.Streaming };
                default:
                    return new List<UsageStageEnum> { UsageStageEnum.ModelFirstToken, UsageStageEnum.ModelStreaming, UsageStageEnum.Tools, UsageStageEnum.Approval, UsageStageEnum.Other };
            }
        }

        /// <summary>
        /// Build analytics from records.
        /// </summary>
        /// <param name="dataset">Dataset the records come from.</param>
        /// <param name="records">Records, in any order. Records outside the range or not matching the filters are skipped.</param>
        /// <param name="query">Query.</param>
        /// <param name="options">Options, or null for defaults.</param>
        /// <returns>Analytics.</returns>
        /// <exception cref="ArgumentNullException">records or query is null.</exception>
        /// <exception cref="ArgumentException">The range or bucket size is not valid.</exception>
        public static UsageAnalytics Build(UsageDatasetEnum dataset, IEnumerable<UsageRecord> records, UsageAnalyticsQuery query, UsageAnalyticsOptions? options = null)
        {
            ArgumentNullException.ThrowIfNull(records);
            ArgumentNullException.ThrowIfNull(query);
            Accumulator accumulator = new Accumulator(dataset, query, options ?? new UsageAnalyticsOptions());
            foreach (UsageRecord record in records)
            {
                if (!accumulator.Add(record)) break;
            }

            return accumulator.Finish();
        }

        /// <summary>
        /// Build analytics from an asynchronous record stream.
        /// </summary>
        /// <param name="dataset">Dataset the records come from.</param>
        /// <param name="records">Records.</param>
        /// <param name="query">Query.</param>
        /// <param name="options">Options, or null for defaults.</param>
        /// <param name="token">Cancellation token.</param>
        /// <returns>Analytics.</returns>
        /// <exception cref="ArgumentNullException">records or query is null.</exception>
        /// <exception cref="ArgumentException">The range or bucket size is not valid.</exception>
        public static async Task<UsageAnalytics> BuildAsync(UsageDatasetEnum dataset, IAsyncEnumerable<UsageRecord> records, UsageAnalyticsQuery query, UsageAnalyticsOptions? options, CancellationToken token)
        {
            ArgumentNullException.ThrowIfNull(records);
            ArgumentNullException.ThrowIfNull(query);
            Accumulator accumulator = new Accumulator(dataset, query, options ?? new UsageAnalyticsOptions());
            await foreach (UsageRecord record in records.WithCancellation(token).ConfigureAwait(false))
            {
                token.ThrowIfCancellationRequested();
                if (!accumulator.Add(record)) break;
            }

            return accumulator.Finish();
        }

        /// <summary>
        /// Nearest-rank distribution of values.
        /// </summary>
        /// <param name="values">Values. The list is sorted in place.</param>
        /// <returns>Distribution.</returns>
        /// <exception cref="ArgumentNullException">values is null.</exception>
        public static UsageDistribution Distribution(List<double> values)
        {
            ArgumentNullException.ThrowIfNull(values);
            UsageDistribution distribution = new UsageDistribution { Count = values.Count };
            if (values.Count == 0) return distribution;
            values.Sort();
            distribution.Average = Math.Round(values.Average(), 3);
            distribution.P50 = Percentile(values, 50);
            distribution.P95 = Percentile(values, 95);
            distribution.P99 = Percentile(values, 99);
            distribution.Maximum = values[values.Count - 1];
            return distribution;
        }

        /// <summary>
        /// Nearest-rank percentile of sorted values.
        /// </summary>
        /// <param name="sorted">Values sorted ascending; not empty.</param>
        /// <param name="percent">Percentile, 0 to 100.</param>
        /// <returns>Value.</returns>
        /// <exception cref="ArgumentNullException">sorted is null.</exception>
        /// <exception cref="ArgumentException">sorted is empty.</exception>
        public static double Percentile(IReadOnlyList<double> sorted, double percent)
        {
            ArgumentNullException.ThrowIfNull(sorted);
            if (sorted.Count == 0) throw new ArgumentException("No values.", nameof(sorted));
            int rank = (int)Math.Ceiling(Math.Clamp(percent, 0, 100) / 100.0 * sorted.Count);
            return sorted[Math.Clamp(rank - 1, 0, sorted.Count - 1)];
        }

        /// <summary>
        /// Tokens per second for a record, or null when it cannot be measured: completion tokens over streaming time when
        /// it is at least <see cref="MinimumStreamingMs"/> (shorter streams are too brief to time), otherwise over total time.
        /// </summary>
        /// <param name="record">Record.</param>
        /// <returns>Tokens per second, or null.</returns>
        /// <exception cref="ArgumentNullException">record is null.</exception>
        public static double? Throughput(UsageRecord record)
        {
            ArgumentNullException.ThrowIfNull(record);
            if (!record.CompletionTokens.HasValue || record.CompletionTokens.Value <= 0) return null;
            double? ms = record.StreamingMs.HasValue && record.StreamingMs.Value >= MinimumStreamingMs ? record.StreamingMs : record.TotalMs;
            if (!ms.HasValue || ms.Value <= 0) return null;
            return Math.Round(record.CompletionTokens.Value / (ms.Value / 1000.0), 2);
        }

        /// <summary>
        /// Add a stage to a record from a formula result, clamping negative results to zero and counting them. Null
        /// results (missing inputs) add nothing.
        /// </summary>
        /// <param name="record">Record.</param>
        /// <param name="stage">Stage.</param>
        /// <param name="milliseconds">Formula result, or null when an input is missing.</param>
        /// <exception cref="ArgumentNullException">record is null.</exception>
        public static void AddStage(UsageRecord record, UsageStageEnum stage, double? milliseconds)
        {
            ArgumentNullException.ThrowIfNull(record);
            if (!milliseconds.HasValue) return;
            double value = milliseconds.Value;
            if (value < 0)
            {
                record.StageAnomalies++;
                value = 0;
            }

            record.Stages.Add(new UsageStageDuration { Stage = stage, Milliseconds = value });
        }

        /// <summary>
        /// Heatmap layout for a range.
        /// </summary>
        /// <param name="fromUtc">Start.</param>
        /// <param name="toUtc">End.</param>
        /// <returns>Layout.</returns>
        public static UsageHeatmapLayoutEnum HeatmapLayout(DateTime fromUtc, DateTime toUtc)
        {
            double minutes = (toUtc - fromUtc).TotalMinutes;
            if (minutes <= 120) return UsageHeatmapLayoutEnum.HourByMinute;
            if (minutes <= 32 * 24 * 60) return UsageHeatmapLayoutEnum.DayByHour;
            return UsageHeatmapLayoutEnum.WeekdayByHour;
        }

        #endregion

        #region Private-Classes

        private sealed class BucketWork
        {
            internal UsageBucket Bucket { get; } = new UsageBucket();

            internal List<double> Latency { get; } = new List<double>();

            internal List<double> TimeToFirstToken { get; } = new List<double>();

            internal List<double> Streaming { get; } = new List<double>();

            internal List<double> Throughput { get; } = new List<double>();

            internal Dictionary<string, UsageGroupValue> Groups { get; } = new Dictionary<string, UsageGroupValue>(StringComparer.Ordinal);

            internal Dictionary<UsageStageEnum, List<double>> Stages { get; } = new Dictionary<UsageStageEnum, List<double>>();
        }

        private sealed class GroupWork
        {
            internal UsageGroupTotal Total { get; } = new UsageGroupTotal();

            internal List<double> Latency { get; } = new List<double>();
        }

        private sealed class Accumulator
        {
            private readonly UsageDatasetEnum _Dataset;
            private readonly UsageAnalyticsQuery _Query;
            private readonly UsageAnalyticsOptions _Options;
            private readonly TimeSpan _BucketSize;
            private readonly BucketWork[] _Buckets;
            private readonly Dictionary<string, GroupWork> _Groups = new Dictionary<string, GroupWork>(StringComparer.Ordinal);
            private readonly Dictionary<UsageStageEnum, List<double>> _Stages = new Dictionary<UsageStageEnum, List<double>>();
            private readonly List<double> _Latency = new List<double>();
            private readonly List<double> _TimeToFirstToken = new List<double>();
            private readonly List<double> _Streaming = new List<double>();
            private readonly List<double> _Throughput = new List<double>();
            private readonly UsageTotals _Totals = new UsageTotals();
            private readonly UsageHeatmap? _Heatmap;
            private readonly Dictionary<int, UsageHeatmapCell> _Cells = new Dictionary<int, UsageHeatmapCell>();
            private readonly DateTime _LocalStartDate;
            private bool _FallbackKnown = false;
            private bool _CachedKnown = false;
            private long _Scanned = 0;
            private int _Anomalies = 0;
            private bool _Truncated = false;

            internal Accumulator(UsageDatasetEnum dataset, UsageAnalyticsQuery query, UsageAnalyticsOptions options)
            {
                UsageRanges.Validate(query.FromUtc, query.ToUtc, query.BucketMinutes);
                _Dataset = dataset;
                _Query = query;
                _Options = options;
                _BucketSize = TimeSpan.FromMinutes(query.BucketMinutes);
                int count = UsageRanges.BucketCount(query.FromUtc, query.ToUtc, query.BucketMinutes);
                _Buckets = new BucketWork[count];
                for (int i = 0; i < count; i++)
                {
                    BucketWork work = new BucketWork();
                    work.Bucket.BucketStartUtc = query.FromUtc + TimeSpan.FromTicks(_BucketSize.Ticks * i);
                    DateTime end = work.Bucket.BucketStartUtc + _BucketSize;
                    work.Bucket.BucketEndUtc = end > query.ToUtc ? query.ToUtc : end;
                    _Buckets[i] = work;
                }

                if (query.Sections.HasFlag(UsageSectionsEnum.Heatmap))
                {
                    UsageHeatmapLayoutEnum layout = HeatmapLayout(query.FromUtc, query.ToUtc);
                    _LocalStartDate = query.FromUtc.AddMinutes(query.UtcOffsetMinutes).Date;
                    int rows;
                    int columns;
                    if (layout == UsageHeatmapLayoutEnum.HourByMinute)
                    {
                        rows = Math.Max(1, (int)Math.Ceiling((query.ToUtc - query.FromUtc).TotalMinutes / 10.0 - 1e-9));
                        columns = 10;
                    }
                    else if (layout == UsageHeatmapLayoutEnum.DayByHour)
                    {
                        rows = (int)(query.ToUtc.AddMinutes(query.UtcOffsetMinutes).Date - _LocalStartDate).TotalDays + 1;
                        columns = 24;
                    }
                    else
                    {
                        rows = 7;
                        columns = 24;
                    }

                    _Heatmap = new UsageHeatmap { Layout = layout, Rows = rows, Columns = columns, UtcOffsetMinutes = query.UtcOffsetMinutes };
                }
            }

            internal bool Add(UsageRecord record)
            {
                if (record == null) return true;
                if (record.TimestampUtc < _Query.FromUtc || record.TimestampUtc >= _Query.ToUtc) return true;
                if (!Matches(record)) return true;
                if (_Scanned >= _Options.MaxRecords)
                {
                    _Truncated = true;
                    return false;
                }

                _Scanned++;
                int index = (int)((record.TimestampUtc - _Query.FromUtc).Ticks / _BucketSize.Ticks);
                if (index < 0 || index >= _Buckets.Length) return true;
                BucketWork work = _Buckets[index];
                UsageBucket bucket = work.Bucket;
                long prompt = record.PromptTokens ?? 0;
                long cached = Math.Min(prompt, record.CachedPromptTokens ?? 0);
                long completion = record.CompletionTokens ?? 0;
                long tokens = prompt + completion;
                _Anomalies += record.StageAnomalies;

                bucket.Requests++;
                _Totals.Requests++;
                if (record.Success)
                {
                    bucket.Succeeded++;
                    _Totals.Succeeded++;
                }
                else
                {
                    bucket.Failed++;
                    _Totals.Failed++;
                }

                if (record.Fallback.HasValue)
                {
                    _FallbackKnown = true;
                    if (record.Fallback.Value)
                    {
                        bucket.Fallbacks = (bucket.Fallbacks ?? 0) + 1;
                        _Totals.Fallbacks = (_Totals.Fallbacks ?? 0) + 1;
                    }
                }

                Measure(record.TotalMs, work.Latency, _Latency);
                Measure(record.TimeToFirstTokenMs, work.TimeToFirstToken, _TimeToFirstToken);
                Measure(record.StreamingMs, work.Streaming, _Streaming);
                Measure(Throughput(record), work.Throughput, _Throughput);

                if (record.CachedPromptTokens.HasValue)
                {
                    _CachedKnown = true;
                    bucket.CachedPromptTokens = (bucket.CachedPromptTokens ?? 0) + cached;
                    _Totals.CachedPromptTokens = (_Totals.CachedPromptTokens ?? 0) + cached;
                }

                bucket.PromptTokens += prompt - cached;
                bucket.CompletionTokens += completion;
                _Totals.PromptTokens += prompt;
                _Totals.CompletionTokens += completion;

                if (record.Rating.HasValue)
                {
                    if (record.Rating.Value > 0)
                    {
                        bucket.ThumbsUp++;
                        _Totals.ThumbsUp++;
                    }
                    else if (record.Rating.Value < 0)
                    {
                        bucket.ThumbsDown++;
                        _Totals.ThumbsDown++;
                    }
                }

                bucket.ToolCalls += record.ToolCalls;
                bucket.ToolFailures += record.ToolFailures;
                bucket.ToolDenied += record.ToolDenied;
                _Totals.ToolCalls += record.ToolCalls;
                _Totals.ToolFailures += record.ToolFailures;
                _Totals.ToolDenied += record.ToolDenied;

                if (_Query.Sections.HasFlag(UsageSectionsEnum.Groups))
                {
                    string key = GroupKey(record);
                    if (!work.Groups.TryGetValue(key, out UsageGroupValue? value))
                    {
                        value = new UsageGroupValue { Key = key };
                        work.Groups[key] = value;
                    }

                    value.Requests++;
                    value.Tokens += tokens;
                    if (!_Groups.TryGetValue(key, out GroupWork? group))
                    {
                        group = new GroupWork();
                        group.Total.Key = key;
                        _Groups[key] = group;
                    }

                    group.Total.Requests++;
                    if (!record.Success) group.Total.Failed++;
                    group.Total.PromptTokens += prompt;
                    group.Total.CompletionTokens += completion;
                    if (record.TotalMs.HasValue && record.TotalMs.Value >= 0) group.Latency.Add(record.TotalMs.Value);
                }

                if (_Query.Sections.HasFlag(UsageSectionsEnum.Stages))
                {
                    foreach (UsageStageDuration stage in record.Stages)
                    {
                        if (!work.Stages.TryGetValue(stage.Stage, out List<double>? list))
                        {
                            list = new List<double>();
                            work.Stages[stage.Stage] = list;
                        }

                        list.Add(stage.Milliseconds);
                        if (!_Stages.TryGetValue(stage.Stage, out List<double>? all))
                        {
                            all = new List<double>();
                            _Stages[stage.Stage] = all;
                        }

                        all.Add(stage.Milliseconds);
                    }
                }

                if (_Heatmap != null) AddHeat(record.TimestampUtc, tokens);
                return true;
            }

            internal UsageAnalytics Finish()
            {
                UsageAnalytics analytics = new UsageAnalytics
                {
                    Dataset = _Dataset,
                    FromUtc = _Query.FromUtc,
                    ToUtc = _Query.ToUtc,
                    BucketMinutes = _Query.BucketMinutes,
                    GroupBy = _Query.GroupBy,
                    GroupMetric = _Query.GroupMetric,
                    StageOrder = StageOrder(_Dataset),
                    StageAnomalies = _Anomalies,
                    RecordsScanned = _Scanned,
                    Truncated = _Truncated,
                    TruncatedReason = _Truncated ? "More than " + _Options.MaxRecords.ToString(System.Globalization.CultureInfo.InvariantCulture) + " records matched; the range was not read in full." : null
                };

                _Totals.ErrorRate = _Totals.Requests > 0 ? Math.Round((double)_Totals.Failed / _Totals.Requests, 4) : null;
                if (_FallbackKnown && !_Totals.Fallbacks.HasValue) _Totals.Fallbacks = 0;
                if (!_CachedKnown) _Totals.CachedPromptTokens = null;
                _Totals.Latency = Distribution(_Latency);
                _Totals.TimeToFirstToken = Distribution(_TimeToFirstToken);
                _Totals.Streaming = Distribution(_Streaming);
                _Totals.Throughput = Distribution(_Throughput);
                analytics.Totals = _Totals;

                List<string> top = new List<string>();
                bool folded = false;
                if (_Query.Sections.HasFlag(UsageSectionsEnum.Groups))
                {
                    int limit = _Query.TopN ?? Math.Clamp(_Options.DefaultTopN, 1, 20);
                    List<GroupWork> ranked = _Groups.Values
                        .OrderByDescending(item => _Query.GroupMetric == UsageGroupMetricEnum.Tokens ? item.Total.PromptTokens + item.Total.CompletionTokens : item.Total.Requests)
                        .ThenBy(item => item.Total.Key, StringComparer.Ordinal)
                        .ToList();
                    top = ranked.Where(item => item.Total.Key != OtherKey).Take(limit).Select(item => item.Total.Key).ToList();
                    HashSet<string> kept = new HashSet<string>(top, StringComparer.Ordinal);
                    GroupWork other = new GroupWork();
                    other.Total.Key = OtherKey;
                    foreach (GroupWork group in ranked)
                    {
                        group.Total.Latency = Distribution(group.Latency);
                        if (kept.Contains(group.Total.Key))
                        {
                            analytics.GroupTotals.Add(group.Total);
                            continue;
                        }

                        folded = true;
                        other.Total.Requests += group.Total.Requests;
                        other.Total.Failed += group.Total.Failed;
                        other.Total.PromptTokens += group.Total.PromptTokens;
                        other.Total.CompletionTokens += group.Total.CompletionTokens;
                        other.Latency.AddRange(group.Latency);
                    }

                    analytics.GroupTotals = analytics.GroupTotals.OrderBy(item => top.IndexOf(item.Key)).ToList();
                    if (folded)
                    {
                        other.Total.Latency = Distribution(other.Latency);
                        analytics.GroupTotals.Add(other.Total);
                    }

                    analytics.GroupKeys = new List<string>(top);
                    if (folded) analytics.GroupKeys.Add(OtherKey);
                }

                foreach (BucketWork work in _Buckets)
                {
                    UsageBucket bucket = work.Bucket;
                    if (_FallbackKnown && !bucket.Fallbacks.HasValue) bucket.Fallbacks = 0;
                    if (_CachedKnown && !bucket.CachedPromptTokens.HasValue) bucket.CachedPromptTokens = 0;
                    bucket.Latency = Distribution(work.Latency);
                    bucket.TimeToFirstToken = Distribution(work.TimeToFirstToken);
                    bucket.Streaming = Distribution(work.Streaming);
                    bucket.Throughput = Distribution(work.Throughput);
                    if (_Query.Sections.HasFlag(UsageSectionsEnum.Groups))
                    {
                        UsageGroupValue other = new UsageGroupValue { Key = OtherKey };
                        foreach (string key in top)
                        {
                            bucket.Groups.Add(work.Groups.TryGetValue(key, out UsageGroupValue? value) ? value : new UsageGroupValue { Key = key });
                        }

                        foreach (UsageGroupValue value in work.Groups.Values.Where(item => !top.Contains(item.Key)))
                        {
                            other.Requests += value.Requests;
                            other.Tokens += value.Tokens;
                        }

                        if (folded) bucket.Groups.Add(other);
                    }

                    foreach (UsageStageEnum stage in analytics.StageOrder)
                    {
                        if (!work.Stages.TryGetValue(stage, out List<double>? values)) continue;
                        bucket.Stages.Add(StageAverage(stage, values));
                    }

                    analytics.Buckets.Add(bucket);
                }

                foreach (UsageStageEnum stage in analytics.StageOrder)
                {
                    if (_Stages.TryGetValue(stage, out List<double>? values)) analytics.StageAverages.Add(StageAverage(stage, values));
                }

                if (_Heatmap != null)
                {
                    for (int row = 0; row < _Heatmap.Rows; row++)
                    {
                        for (int column = 0; column < _Heatmap.Columns; column++)
                        {
                            int key = row * _Heatmap.Columns + column;
                            UsageHeatmapCell cell = _Cells.TryGetValue(key, out UsageHeatmapCell? found) ? found : new UsageHeatmapCell { Row = row, Column = column, StartUtc = CellStart(row, column) };
                            _Heatmap.Cells.Add(cell);
                            if (cell.Tokens > _Heatmap.MaximumTokens) _Heatmap.MaximumTokens = cell.Tokens;
                        }
                    }

                    analytics.Heatmap = _Heatmap;
                }

                return analytics;
            }

            private static UsageStageAverage StageAverage(UsageStageEnum stage, List<double> values)
            {
                values.Sort();
                return new UsageStageAverage { Stage = stage, Count = values.Count, AverageMs = Math.Round(values.Average(), 3), P95Ms = Percentile(values, 95) };
            }

            private static void Measure(double? value, List<double> bucket, List<double> total)
            {
                if (!value.HasValue || value.Value < 0 || Double.IsNaN(value.Value) || Double.IsInfinity(value.Value)) return;
                bucket.Add(value.Value);
                total.Add(value.Value);
            }

            private bool Matches(UsageRecord record)
            {
                if (_Query.Model != null && !String.Equals(record.Model, _Query.Model, StringComparison.Ordinal)) return false;
                if (_Query.Provider != null && !String.Equals(record.Provider, _Query.Provider, StringComparison.Ordinal)) return false;
                if (_Query.ClientId != null && !String.Equals(record.ClientId, _Query.ClientId, StringComparison.Ordinal)) return false;
                if (_Query.UserId != null && !String.Equals(record.UserId, _Query.UserId, StringComparison.Ordinal)) return false;
                if (_Query.CredentialId != null && !String.Equals(record.CredentialId, _Query.CredentialId, StringComparison.Ordinal)) return false;
                return true;
            }

            private string GroupKey(UsageRecord record)
            {
                string? value;
                switch (_Query.GroupBy)
                {
                    case UsageGroupByEnum.Provider: value = record.Provider; break;
                    case UsageGroupByEnum.Route: value = RouteKey(record.Route); break;
                    case UsageGroupByEnum.Client: value = record.ClientId; break;
                    case UsageGroupByEnum.Tenant: value = record.TenantId; break;
                    case UsageGroupByEnum.User: value = record.UserId; break;
                    case UsageGroupByEnum.Credential: value = record.CredentialId; break;
                    default: value = record.Model; break;
                }

                return String.IsNullOrWhiteSpace(value) ? UnknownKey : value;
            }

            private static string? RouteKey(string? route)
            {
                if (String.IsNullOrWhiteSpace(route)) return null;
                if (route.StartsWith("local", StringComparison.OrdinalIgnoreCase) && !route.Contains('+', StringComparison.Ordinal)) return "local";
                return route;
            }

            private void AddHeat(DateTime timestampUtc, long tokens)
            {
                UsageHeatmap heatmap = _Heatmap!;
                int row;
                int column;
                DateTime local = timestampUtc.AddMinutes(_Query.UtcOffsetMinutes);
                if (heatmap.Layout == UsageHeatmapLayoutEnum.HourByMinute)
                {
                    int minute = (int)(timestampUtc - _Query.FromUtc).TotalMinutes;
                    row = minute / 10;
                    column = minute % 10;
                }
                else if (heatmap.Layout == UsageHeatmapLayoutEnum.DayByHour)
                {
                    row = (int)(local.Date - _LocalStartDate).TotalDays;
                    column = local.Hour;
                }
                else
                {
                    row = (int)local.DayOfWeek;
                    column = local.Hour;
                }

                if (row < 0 || row >= heatmap.Rows || column < 0 || column >= heatmap.Columns) return;
                int key = row * heatmap.Columns + column;
                if (!_Cells.TryGetValue(key, out UsageHeatmapCell? cell))
                {
                    cell = new UsageHeatmapCell { Row = row, Column = column, StartUtc = CellStart(row, column) };
                    _Cells[key] = cell;
                }

                cell.Tokens += tokens;
                cell.Requests++;
            }

            private DateTime CellStart(int row, int column)
            {
                UsageHeatmap heatmap = _Heatmap!;
                if (heatmap.Layout == UsageHeatmapLayoutEnum.HourByMinute) return _Query.FromUtc.AddMinutes(row * 10 + column);
                if (heatmap.Layout == UsageHeatmapLayoutEnum.DayByHour) return DateTime.SpecifyKind(_LocalStartDate.AddDays(row).AddHours(column).AddMinutes(-_Query.UtcOffsetMinutes), DateTimeKind.Utc);
                return DateTime.MinValue;
            }
        }

        #endregion
    }
}

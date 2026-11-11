import catalog from './chartCatalog.json';

// Builds every Usage chart from the analytics the backend computes, following the catalog XenoChartCatalog generates.
// The desktop and terminal apps do the same in XenoUsageCharts. Nothing here calculates a percentile or a bucket.

const charts = new Map(catalog.Charts.map((item) => [item.Id, item]));
const toneVariables = { Accent: 'var(--color-accent)', Success: 'var(--color-success)', Danger: 'var(--color-danger)', Info: 'var(--color-info)', Warning: 'var(--color-warning)' };

export const CHART_IDS = {
  outcome: 'usage.outcome', latency: 'usage.latency', ttft: 'usage.ttft', streaming: 'usage.streaming', throughput: 'usage.throughput',
  tokens: 'usage.tokens', groups: 'usage.groups', groupTotals: 'usage.groupTotals', heatmap: 'usage.heatmap', stages: 'usage.stages',
  stageAverages: 'usage.stageAverages', feedback: 'usage.feedback', tools: 'usage.tools', health: 'usage.health', requestTiming: 'request.timing'
};

export const OTHER_KEY = 'Other';
export const UNKNOWN_KEY = 'Unknown';

export function toneColor(tone) {
  if (tone === 'Neutral') return catalog.NeutralColor;
  if (toneVariables[tone]) return toneVariables[tone];
  const index = catalog.CategoryTones.indexOf(tone);
  return index >= 0 ? catalog.CategoryColors[index] : toneVariables.Accent;
}

function categoryColor(index) {
  return catalog.CategoryColors[index % catalog.CategoryColors.length];
}

export function usagePageChartIds(dataset) {
  return catalog.Charts
    .filter((item) => item.Id !== CHART_IDS.health && item.Id !== CHART_IDS.requestTiming)
    .filter((item) => !item.ChatOnly || dataset === 'Chat')
    .map((item) => item.Id);
}

function definition(id) {
  const found = charts.get(id);
  if (!found) throw new Error(`No chart ${id}`);
  return found;
}

function fixed(def, key, values, t) {
  const series = def.Series.find((item) => item.Key === key);
  return { key, label: t(series.LabelKey), color: toneColor(series.Tone), values };
}

function number(value) {
  return value === undefined || value === null ? null : Number(value);
}

export function groupKeyLabel(key, t) {
  if (key === OTHER_KEY) return t('usageGroupOther');
  if (key === UNKNOWN_KEY) return t('usageGroupUnknown');
  if (key === 'local') return t('chatRouteLocal');
  if (key === 'network') return t('chatRouteNetwork');
  return key;
}

function groupColor(key, index) {
  return key === OTHER_KEY || key === UNKNOWN_KEY ? catalog.NeutralColor : categoryColor(index);
}

function percentileSeries(def, distributions, t) {
  return ['p50', 'p95', 'p99', 'maximum'].map((key) => fixed(def, key, distributions.map((item) => number(item?.[key])), t));
}

export function buildUsageChart(id, analytics, t) {
  const def = definition(id);
  const buckets = (analytics?.buckets || []).map((item) => ({ ...item, startUtc: item.bucketStartUtc, endUtc: item.bucketEndUtc }));
  const totals = analytics?.totals || {};
  const base = { id, kind: def.Kind, title: t(def.TitleKey), unit: def.Unit, stacked: def.Stacked, additive: def.Additive, buckets, labels: buckets.map((item) => item.startUtc), bucketMinutes: analytics?.bucketMinutes, notice: analytics?.truncated ? analytics.truncatedReason : null };
  const fallbackKnown = totals.fallbacks !== undefined && totals.fallbacks !== null;
  const cachedKnown = totals.cachedPromptTokens !== undefined && totals.cachedPromptTokens !== null;
  switch (id) {
    case CHART_IDS.outcome: {
      const series = [
        fixed(def, 'succeeded', buckets.map((item) => Math.max(0, (item.succeeded || 0) - (item.fallbacks || 0))), t),
        fixed(def, 'fallback', buckets.map((item) => item.fallbacks || 0), t),
        fixed(def, 'failed', buckets.map((item) => item.failed || 0), t)
      ];
      if (!fallbackKnown) series.splice(1, 1);
      return { ...base, series, hasData: (totals.requests || 0) > 0, emptyKey: 'usageEmptyRequests' };
    }
    case CHART_IDS.latency:
      return { ...base, series: percentileSeries(def, buckets.map((item) => item.latency), t), hasData: buckets.some((item) => item.latency?.count > 0), emptyKey: 'usageEmptyTiming' };
    case CHART_IDS.ttft:
      return { ...base, series: percentileSeries(def, buckets.map((item) => item.timeToFirstToken), t), hasData: buckets.some((item) => item.timeToFirstToken?.count > 0), emptyKey: 'usageEmptyFirstToken' };
    case CHART_IDS.streaming:
      return { ...base, series: percentileSeries(def, buckets.map((item) => item.streaming), t), hasData: buckets.some((item) => item.streaming?.count > 0), emptyKey: 'usageEmptyStreaming' };
    case CHART_IDS.throughput:
      return { ...base, series: ['p50', 'p95'].map((key) => fixed(def, key, buckets.map((item) => number(item.throughput?.[key])), t)), hasData: buckets.some((item) => item.throughput?.count > 0), emptyKey: 'usageEmptyTokens' };
    case CHART_IDS.tokens: {
      const series = [
        fixed(def, 'prompt', buckets.map((item) => item.promptTokens || 0), t),
        fixed(def, 'cached', buckets.map((item) => item.cachedPromptTokens || 0), t),
        fixed(def, 'completion', buckets.map((item) => item.completionTokens || 0), t)
      ];
      if (!cachedKnown) series.splice(1, 1);
      return { ...base, series, hasData: (totals.promptTokens || 0) + (totals.completionTokens || 0) > 0, emptyKey: 'usageEmptyTokens' };
    }
    case CHART_IDS.groups: {
      const tokens = analytics?.groupMetric === 'Tokens';
      const keys = analytics?.groupKeys || [];
      const series = keys.map((key, index) => ({
        key,
        label: groupKeyLabel(key, t),
        color: groupColor(key, index),
        values: buckets.map((item) => {
          const found = (item.groups || []).find((value) => value.key === key);
          return found ? (tokens ? found.tokens || 0 : found.requests || 0) : 0;
        })
      }));
      return { ...base, title: t(tokens ? 'usageChartGroupsTokens' : 'usageChartGroupsRequests', { group: t(`usageGroupBy${analytics?.groupBy || 'Model'}`).toLowerCase() }), unit: tokens ? ' tok' : ' req', series, hasData: (totals.requests || 0) > 0, emptyKey: 'usageEmptyRequests', drillSeries: analytics?.groupBy === 'Model' };
    }
    case CHART_IDS.groupTotals: {
      const tokens = analytics?.groupMetric === 'Tokens';
      const groups = analytics?.groupTotals || [];
      const series = tokens
        ? [
          { key: 'prompt', label: t('usageSeriesPrompt'), color: toneColor('Info'), values: groups.map((item) => item.promptTokens || 0) },
          { key: 'completion', label: t('usageSeriesCompletion'), color: toneColor('Success'), values: groups.map((item) => item.completionTokens || 0) }
        ]
        : [
          { key: 'succeeded', label: t('usageSeriesSucceeded'), color: toneColor('Success'), values: groups.map((item) => (item.requests || 0) - (item.failed || 0)) },
          { key: 'failed', label: t('usageSeriesFailed'), color: toneColor('Danger'), values: groups.map((item) => item.failed || 0) }
        ];
      return { ...base, title: t(tokens ? 'usageChartGroupTotalsTokens' : 'usageChartGroupTotalsRequests', { group: t(`usageGroupBy${analytics?.groupBy || 'Model'}`).toLowerCase() }), unit: tokens ? ' tok' : ' req', labels: groups.map((item) => groupKeyLabel(item.key, t)), buckets: [], series, hasData: groups.length > 0, emptyKey: 'usageEmptyRequests' };
    }
    case CHART_IDS.heatmap: {
      const heatmap = analytics?.heatmap;
      if (!heatmap) return { ...base, labels: [], buckets: [], series: [], rowLabels: [], grid: [], hasData: false, emptyKey: 'usageEmptyTokens' };
      const grid = Array.from({ length: heatmap.rows }, (_, row) => (heatmap.cells || []).filter((cell) => cell.row === row).sort((a, b) => a.column - b.column));
      return { ...base, heatmap, grid, buckets: [], hasData: (heatmap.maximumTokens || 0) > 0, emptyKey: 'usageEmptyTokens', series: [] };
    }
    case CHART_IDS.stages: {
      const order = analytics?.stageOrder || [];
      const series = order.map((stage, index) => ({ key: stage, label: t(`usageStage${stage}`), color: categoryColor(index), values: buckets.map((item) => (item.stages || []).find((value) => value.stage === stage)?.averageMs || 0) }));
      return { ...base, series, hasData: (analytics?.stageAverages || []).length > 0, emptyKey: 'usageEmptyStages' };
    }
    case CHART_IDS.stageAverages: {
      const order = analytics?.stageOrder || [];
      const averages = order.map((stage) => (analytics?.stageAverages || []).find((item) => item.stage === stage)).filter(Boolean);
      return { ...base, labels: averages.map((item) => t(`usageStage${item.stage}`)), buckets: [], series: [fixed(def, 'average', averages.map((item) => item.averageMs || 0), t), fixed(def, 'p95', averages.map((item) => item.p95Ms || 0), t)], hasData: averages.length > 0, emptyKey: 'usageEmptyStages' };
    }
    case CHART_IDS.feedback:
      return { ...base, series: [fixed(def, 'up', buckets.map((item) => item.thumbsUp || 0), t), fixed(def, 'down', buckets.map((item) => item.thumbsDown || 0), t)], hasData: (totals.thumbsUp || 0) + (totals.thumbsDown || 0) > 0, emptyKey: 'usageEmptyFeedback' };
    case CHART_IDS.tools:
      return { ...base, series: [fixed(def, 'succeeded', buckets.map((item) => item.toolCalls || 0), t), fixed(def, 'failed', buckets.map((item) => item.toolFailures || 0), t), fixed(def, 'denied', buckets.map((item) => item.toolDenied || 0), t)], hasData: (totals.toolCalls || 0) + (totals.toolFailures || 0) + (totals.toolDenied || 0) > 0, emptyKey: 'usageEmptyTools' };
    default:
      throw new Error(`Not a Usage chart: ${id}`);
  }
}

export function formatChartValue(value, unit, locale) {
  if (value === null || value === undefined || Number.isNaN(value)) return '-';
  if (unit === ' ms') {
    if (value < 1000) return `${new Intl.NumberFormat(locale, { maximumFractionDigits: 0 }).format(value)} ms`;
    if (value < 60000) return `${new Intl.NumberFormat(locale, { maximumFractionDigits: 1, minimumFractionDigits: 1 }).format(value / 1000)} s`;
    return `${new Intl.NumberFormat(locale, { maximumFractionDigits: 1, minimumFractionDigits: 1 }).format(value / 60000)} min`;
  }
  return `${new Intl.NumberFormat(locale, { maximumFractionDigits: value >= 100 ? 0 : 1 }).format(value)}${unit || ''}`;
}

export function formatBucketLabel(startUtc, bucketMinutes, locale) {
  const date = new Date(startUtc);
  return bucketMinutes <= 15
    ? new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit' }).format(date)
    : new Intl.DateTimeFormat(locale, { month: 'numeric', day: 'numeric', hour: '2-digit', minute: '2-digit' }).format(date);
}

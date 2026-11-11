import { useMemo, useState } from 'react';
import { RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { refreshIntervals, useAutoRefresh } from '../charts/autoRefresh.jsx';
import TimeChart from '../charts/TimeChart.jsx';

const ranges = [
  { key: 'lastHour', buckets: 60 },
  { key: 'lastDay', buckets: 96 },
  { key: 'lastWeek', buckets: 84 },
  { key: 'lastMonth', buckets: 120 }
];

export default function ActivityChart({
  ariaLabel,
  failedLabel,
  loading = false,
  onRangeChange,
  onRefresh,
  range,
  showControls = true,
  successLabel,
  summary,
  xAxisLabel,
  yAxisLabel
}) {
  const { t } = useTranslation();
  const [localRange, setLocalRange] = useState('lastDay');
  const [autoRefreshSeconds, setAutoRefreshSeconds] = useState(15);
  const activeRange = range || localRange;
  const selectedRange = ranges.find((item) => item.key === activeRange) || ranges[1];
  const buckets = useMemo(() => buildBuckets(summary?.buckets, selectedRange), [summary?.buckets, selectedRange]);

  function updateRange(nextRange) {
    if (!range) setLocalRange(nextRange);
    onRangeChange?.(nextRange);
  }

  useAutoRefresh(onRefresh ? autoRefreshSeconds : 0, onRefresh);

  const successText = successLabel || t('success');
  const failedText = failedLabel || t('failed');
  const timeChart = {
    id: 'requests',
    kind: 'StackedColumns',
    title: ariaLabel || t('requests'),
    unit: '',
    stacked: true,
    buckets: buckets.map((bucket, index) => ({ ...bucket, startUtc: `${index}-${bucket.label}` })),
    series: [
      { key: 'failed', label: failedText, color: 'var(--color-danger)', values: buckets.map((bucket) => bucket.failed) },
      { key: 'success', label: successText, color: 'var(--color-success)', values: buckets.map((bucket) => bucket.success) }
    ]
  };

  return (
    <div className="activity-chart-panel">
      {showControls || onRefresh ? (
        <div className="chart-controls" aria-label={t('chartRange')}>
          {showControls ? ranges.map((item) => (
              <button
                className={item.key === activeRange ? 'active' : ''}
                key={item.key}
                disabled={loading}
                onClick={() => updateRange(item.key)}
                title={t('tooltipChartRangeOption', { range: t(item.key) })}
                type="button"
              >
                {t(item.key)}
              </button>
            )) : null}
          <ChartRefreshControls
            autoRefreshSeconds={autoRefreshSeconds}
            disabled={!onRefresh || loading}
            onRefresh={onRefresh}
            setAutoRefreshSeconds={setAutoRefreshSeconds}
            t={t}
          />
        </div>
      ) : null}
      <div className="chart-canvas">
        <TimeChart chart={timeChart} height={525} svgClassName="activity-chart" xAxisLabel={xAxisLabel || t('time')} yAxisLabel={yAxisLabel || t('requestCount')} />
        {loading ? <ChartLoadingOverlay label={t('loading')} /> : null}
      </div>
      <div className="chart-legend">
        <span><i className="success-swatch" />{successText}</span>
        <span><i className="failed-swatch" />{failedText}</span>
      </div>
    </div>
  );
}

function ChartLoadingOverlay({ label }) {
  return (
    <div aria-live="polite" className="chart-loading-overlay" role="status">
      <div aria-hidden="true" className="table-loading-spinner" />
      <strong>{label}</strong>
    </div>
  );
}

function ChartRefreshControls({ autoRefreshSeconds, disabled, onRefresh, setAutoRefreshSeconds, t }) {
  if (!onRefresh) return null;
  return (
    <label className="chart-refresh" title={t('tooltipAutoRefresh')}>
      <span>{t('autoRefresh')}</span>
      <select
        disabled={disabled}
        value={autoRefreshSeconds}
        onChange={(event) => setAutoRefreshSeconds(Number(event.target.value))}
        title={t('tooltipAutoRefresh')}
      >
        {refreshIntervals.map((seconds) => (
          <option key={seconds} value={seconds}>{seconds === 0 ? t('none') : `${seconds}s`}</option>
        ))}
      </select>
      <button className="pagination-button" disabled={disabled} onClick={onRefresh} title={t('tooltipManualRefresh')} type="button">
        <RefreshCw size={14} />
      </button>
    </label>
  );
}

function buildBuckets(rawBuckets, selectedRange) {
  const source = Array.isArray(rawBuckets) && rawBuckets.length > 0 ? rawBuckets : [];
  const normalized = source.slice(-selectedRange.buckets).map((bucket, index) => normalizeBucket(bucket, selectedRange, index, source.length));
  const missing = Math.max(0, selectedRange.buckets - normalized.length);
  const padding = Array.from({ length: missing }, (_, index) => ({
        label: fallbackLabel(selectedRange.key, index, selectedRange.buckets),
        success: 0,
        failed: 0
      }));

  return [...padding, ...normalized].slice(-selectedRange.buckets);
}

function normalizeBucket(bucket, selectedRange, index, count) {
  const total = Number(bucket.count ?? bucket.Count ?? 0);
  const failed = Number(bucket.failed ?? bucket.Failed ?? bucket.errors ?? bucket.Errors ?? 0);
  const explicitSuccess = bucket.success ?? bucket.Success;
  const success = explicitSuccess === undefined || explicitSuccess === null
    ? Math.max(0, total - failed)
    : Number(explicitSuccess);

  return {
    failed: Number.isFinite(failed) ? failed : 0,
    label: bucketLabel(bucket, selectedRange, index, count),
    success: Number.isFinite(success) ? success : 0
  };
}

function bucketLabel(bucket, selectedRange, index, count) {
  const label = bucket.label ?? bucket.Label;
  if (label) return String(label);

  const start = bucket.startUtc ?? bucket.StartUtc;
  if (start) {
    const date = new Date(start);
    if (!Number.isNaN(date.getTime())) {
      if (selectedRange.key === 'lastHour') {
        return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
      }

      if (selectedRange.key === 'lastDay') {
        return date.toLocaleTimeString([], { hour: '2-digit' });
      }

      return date.toLocaleDateString([], { month: 'short', day: 'numeric' });
    }
  }

  return fallbackLabel(selectedRange.key, index, count);
}

function fallbackLabel(range, index, count) {
  if (range === 'lastHour') return `${Math.max(0, count - index - 1)}m`;
  if (range === 'lastDay') {
    const totalMinutes = Math.max(0, count - index - 1) * 15;
    const hours = Math.floor(totalMinutes / 60);
    const minutes = totalMinutes % 60;
    return `${hours}h${minutes ? ` ${minutes}m` : ''}`;
  }
  if (range === 'lastWeek' || range === 'lastMonth') return dateFallbackLabel(range, index, count);
  return `${index + 1}`;
}

function dateFallbackLabel(range, index, count) {
  const durationMs = range === 'lastWeek'
    ? 7 * 24 * 60 * 60 * 1000
    : 30 * 24 * 60 * 60 * 1000;
  const bucketMs = durationMs / Math.max(1, count);
  const timestamp = Date.now() - durationMs + index * bucketMs + bucketMs / 2;
  return new Date(timestamp).toLocaleDateString([], { month: 'short', day: 'numeric' });
}

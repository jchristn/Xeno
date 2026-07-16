import { useEffect, useMemo, useRef, useState } from 'react';
import { RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';

const refreshIntervals = [0, 15, 30, 60, 180, 300];
const ranges = [
  { key: 'lastHour', buckets: 60 },
  { key: 'lastDay', buckets: 96 },
  { key: 'lastWeek', buckets: 84 },
  { key: 'lastMonth', buckets: 90 }
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
  const canvasRef = useRef(null);
  const refreshRef = useRef(onRefresh);
  const [chartWidth, setChartWidth] = useState(760);
  const [localRange, setLocalRange] = useState('lastDay');
  const [hovered, setHovered] = useState(null);
  const [autoRefreshSeconds, setAutoRefreshSeconds] = useState(15);
  const activeRange = range || localRange;
  const selectedRange = ranges.find((item) => item.key === activeRange) || ranges[1];
  const buckets = useMemo(() => buildBuckets(summary?.buckets, selectedRange), [summary?.buckets, selectedRange]);
  const max = Math.max(1, ...buckets.map((bucket) => bucket.success + bucket.failed));
  const axisMax = axisMaximum(max);

  function updateRange(nextRange) {
    if (!range) setLocalRange(nextRange);
    onRangeChange?.(nextRange);
  }

  useEffect(() => {
    refreshRef.current = onRefresh;
  }, [onRefresh]);

  useEffect(() => {
    if (!autoRefreshSeconds || !refreshRef.current) return undefined;
    const interval = window.setInterval(() => {
      refreshRef.current?.();
    }, autoRefreshSeconds * 1000);
    return () => window.clearInterval(interval);
  }, [autoRefreshSeconds]);

  useEffect(() => {
    const element = canvasRef.current;
    if (!element) return undefined;

    function updateWidth() {
      setChartWidth(Math.max(360, Math.round(element.clientWidth || 760)));
    }

    updateWidth();
    const observer = new ResizeObserver(updateWidth);
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  const chart = {
    height: 525,
    width: chartWidth,
    left: 58,
    right: 28,
    top: 28,
    bottom: 58
  };
  chart.plotWidth = chart.width - chart.left - chart.right;
  chart.plotHeight = chart.height - chart.top - chart.bottom;
  chart.baseY = chart.top + chart.plotHeight;
  const barSlot = chart.plotWidth / Math.max(1, buckets.length);
  const barWidth = Math.max(2, Math.min(14, barSlot - 2));
  const yTicks = buildYAxisTicks(axisMax);
  const xTicks = buildXAxisTicks(buckets, chart.left, chart.plotWidth);
  const successText = successLabel || t('success');
  const failedText = failedLabel || t('failed');

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
      <div className="chart-canvas" ref={canvasRef}>
        <svg className="activity-chart" viewBox={`0 0 ${chart.width} ${chart.height}`} role="img" aria-label={ariaLabel || t('requests')}>
          <line x1={chart.left} y1={chart.top} x2={chart.left} y2={chart.baseY} stroke="var(--color-border)" />
          <line x1={chart.left} y1={chart.baseY} x2={chart.left + chart.plotWidth} y2={chart.baseY} stroke="var(--color-border)" />
          {yTicks.map((tick) => {
            const y = chart.baseY - tick.ratio * chart.plotHeight;
            return (
              <g key={tick.value}>
                <line x1={chart.left} y1={y} x2={chart.left + chart.plotWidth} y2={y} stroke="var(--color-border)" strokeDasharray="4 6" opacity="0.72" />
                <text className="axis-label" x={chart.left - 10} y={y + 4} textAnchor="end">{tick.value}</text>
              </g>
            );
          })}
          <text className="axis-title" transform={`translate(16 ${chart.top + chart.plotHeight / 2}) rotate(-90)`}>{yAxisLabel || t('requestCount')}</text>
          <text className="axis-title" x={chart.left + chart.plotWidth / 2} y={chart.height - 12} textAnchor="middle">{xAxisLabel || t('time')}</text>
          {buckets.map((bucket, index) => {
            const x = chart.left + index * barSlot + (barSlot - barWidth) / 2;
            const successHeight = (bucket.success / axisMax) * chart.plotHeight;
            const failedHeight = (bucket.failed / axisMax) * chart.plotHeight;
            const failedY = chart.baseY - failedHeight;
            const successY = chart.baseY - failedHeight - successHeight;
            const tooltipY = Math.min(successY, failedY) + (successHeight + failedHeight) / 2;
            return (
              <g
                key={`${bucket.label}-${index}`}
                onBlur={() => setHovered(null)}
                onFocus={() => setHovered({ bucket, ...tooltipFromChartPoint(x + barWidth / 2, tooltipY, chart, canvasRef.current) })}
                onMouseLeave={() => setHovered(null)}
                onMouseMove={(event) => setHovered({ bucket, ...tooltipFromPointer(event, canvasRef.current) })}
                tabIndex={0}
              >
                <title>{`${bucket.label}: ${successText} ${bucket.success}, ${failedText} ${bucket.failed}. ${t('tooltipChartRange')}`}</title>
                <rect x={x} y={failedY} width={barWidth} height={failedHeight} rx="3" fill="var(--color-danger)" />
                <rect x={x} y={successY} width={barWidth} height={successHeight} rx="3" fill="var(--color-success)" />
              </g>
            );
          })}
          {xTicks.map((tick) => (
            <text className="axis-label" key={tick.key} x={tick.x} y={chart.baseY + 18} textAnchor="middle">{tick.label}</text>
          ))}
        </svg>
        {hovered ? (
          <div className="chart-tooltip" style={{ left: `${hovered.x}px`, top: `${hovered.y}px`, transform: hovered.transform }}>
            <strong>{hovered.bucket.label}</strong>
            <span>{successText}: {hovered.bucket.success}</span>
            <span>{failedText}: {hovered.bucket.failed}</span>
            <span>{t('total')}: {hovered.bucket.success + hovered.bucket.failed}</span>
          </div>
        ) : null}
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

function buildXAxisTicks(buckets, left, plotWidth) {
  const count = buckets.length;
  if (count === 0) return [];

  const tickCount = Math.min(8, count);
  if (tickCount === 1) {
    return [{ key: 'x-0', label: buckets[0]?.label || '', x: left + plotWidth / 2 }];
  }

  return Array.from({ length: tickCount }, (_, tickIndex) => {
    const ratio = tickIndex / (tickCount - 1);
    const bucketIndex = Math.min(count - 1, Math.max(0, Math.round(ratio * (count - 1))));
    return {
      key: `x-${tickIndex}-${bucketIndex}`,
      label: buckets[bucketIndex]?.label || '',
      x: left + ratio * plotWidth
    };
  });
}

function axisMaximum(max) {
  if (!Number.isFinite(max) || max <= 1) return 1;
  return Math.max(4, Math.ceil(max / 4) * 4);
}

function buildYAxisTicks(max) {
  if (!Number.isFinite(max) || max <= 1) return [{ value: 1, ratio: 1 }, { value: 0, ratio: 0 }];
  return [1, 0.75, 0.5, 0.25, 0].map((ratio) => ({
    ratio,
    value: Math.round(max * ratio)
  }));
}

function tooltipFromPointer(event, element) {
  if (!element) return { x: 0, y: 0, transform: 'translate(14px, -50%)' };

  const bounds = element.getBoundingClientRect();
  return tooltipPosition(event.clientX - bounds.left, event.clientY - bounds.top, bounds.width, bounds.height);
}

function tooltipFromChartPoint(x, y, chart, element) {
  const width = element?.clientWidth || chart.width;
  const height = element?.clientHeight || chart.height;
  return tooltipPosition((x / chart.width) * width, (y / chart.height) * height, width, height);
}

function tooltipPosition(x, y, width, height) {
  const horizontalPadding = 18;
  const verticalPadding = 48;
  const tooltipWidth = 188;
  const clampedX = Math.min(Math.max(x, horizontalPadding), Math.max(horizontalPadding, width - horizontalPadding));
  const clampedY = Math.min(Math.max(y, verticalPadding), Math.max(verticalPadding, height - verticalPadding));
  const transform = clampedX > width - tooltipWidth ? 'translate(calc(-100% - 14px), -50%)' : 'translate(14px, -50%)';

  return { x: clampedX, y: clampedY, transform };
}

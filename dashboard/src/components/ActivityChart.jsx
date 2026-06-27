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

export default function ActivityChart({ onRangeChange, onRefresh, range, showControls = true, summary }) {
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
  const yTicks = buildYAxisTicks(max);

  return (
    <div className="activity-chart-panel">
      {showControls || onRefresh ? (
        <div className="chart-controls" aria-label={t('chartRange')}>
          {showControls ? ranges.map((item) => (
              <button
                className={item.key === activeRange ? 'active' : ''}
                key={item.key}
                onClick={() => updateRange(item.key)}
                title={t('tooltipChartRangeOption', { range: t(item.key) })}
                type="button"
              >
                {t(item.key)}
              </button>
            )) : null}
          <ChartRefreshControls
            autoRefreshSeconds={autoRefreshSeconds}
            disabled={!onRefresh}
            onRefresh={onRefresh}
            setAutoRefreshSeconds={setAutoRefreshSeconds}
            t={t}
          />
        </div>
      ) : null}
      <div className="chart-canvas" ref={canvasRef}>
        <svg className="activity-chart" viewBox={`0 0 ${chart.width} ${chart.height}`} role="img" aria-label={t('requests')}>
          <line x1={chart.left} y1={chart.top} x2={chart.left} y2={chart.baseY} stroke="var(--color-border)" />
          <line x1={chart.left} y1={chart.baseY} x2={chart.left + chart.plotWidth} y2={chart.baseY} stroke="var(--color-border)" />
          {yTicks.map((tick) => {
            const y = chart.baseY - (tick / max) * chart.plotHeight;
            return (
              <g key={tick}>
                <line x1={chart.left} y1={y} x2={chart.left + chart.plotWidth} y2={y} stroke="var(--color-border)" strokeDasharray="4 6" opacity="0.72" />
                <text className="axis-label" x={chart.left - 10} y={y + 4} textAnchor="end">{tick}</text>
              </g>
            );
          })}
          <text className="axis-title" transform={`translate(16 ${chart.top + chart.plotHeight / 2}) rotate(-90)`}>{t('requestCount')}</text>
          <text className="axis-title" x={chart.left + chart.plotWidth / 2} y={chart.height - 12} textAnchor="middle">{t('time')}</text>
          {buckets.map((bucket, index) => {
            const x = chart.left + index * barSlot + (barSlot - barWidth) / 2;
            const successHeight = (bucket.success / max) * chart.plotHeight;
            const failedHeight = (bucket.failed / max) * chart.plotHeight;
            const failedY = chart.baseY - failedHeight;
            const successY = chart.baseY - failedHeight - successHeight;
            const showTick = shouldShowTick(index, buckets.length);
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
                <title>{`${bucket.label}: ${t('success')} ${bucket.success}, ${t('failed')} ${bucket.failed}. ${t('tooltipChartRange')}`}</title>
                <rect x={x} y={failedY} width={barWidth} height={failedHeight} rx="3" fill="var(--color-danger)" />
                <rect x={x} y={successY} width={barWidth} height={successHeight} rx="3" fill="var(--color-success)" />
                {showTick ? <text className="axis-label" x={x + barWidth / 2} y={chart.baseY + 18} textAnchor="middle">{bucket.label}</text> : null}
              </g>
            );
          })}
        </svg>
        {hovered ? (
          <div className="chart-tooltip" style={{ left: `${hovered.x}px`, top: `${hovered.y}px`, transform: hovered.transform }}>
            <strong>{hovered.bucket.label}</strong>
            <span>{t('success')}: {hovered.bucket.success}</span>
            <span>{t('failed')}: {hovered.bucket.failed}</span>
            <span>{t('total')}: {hovered.bucket.success + hovered.bucket.failed}</span>
          </div>
        ) : null}
      </div>
      <div className="chart-legend">
        <span><i className="success-swatch" />{t('success')}</span>
        <span><i className="failed-swatch" />{t('failed')}</span>
      </div>
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
  if (range === 'lastWeek') return `D${Math.max(1, index + 1)}`;
  if (range === 'lastMonth') return `${Math.max(1, index + 1)}`;
  return `${index + 1}`;
}

function shouldShowTick(index, count) {
  const step = Math.max(1, Math.ceil(count / 8));
  return index % step === 0 || index === count - 1;
}

function buildYAxisTicks(max) {
  if (!Number.isFinite(max) || max <= 1) return [1, 0];
  const tickCount = Math.min(5, Math.max(2, Math.ceil(max) + 1));
  const step = Math.max(1, Math.ceil(max / (tickCount - 1)));
  const ticks = [];
  for (let tick = 0; tick <= max; tick += step) {
    ticks.push(tick);
  }
  if (!ticks.includes(max)) ticks.push(max);
  return [...new Set(ticks)].sort((left, right) => right - left).slice(0, 5);
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

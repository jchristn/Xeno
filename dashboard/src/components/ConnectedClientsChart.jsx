import { useEffect, useMemo, useRef, useState } from 'react';
import { RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';

const sampleStorageKey = 'xeno.connectedClients.samples';
const refreshIntervals = [0, 15, 30, 60, 180, 300];
const ranges = [
  { key: 'lastHour', durationMs: 60 * 60 * 1000, buckets: 60 },
  { key: 'lastDay', durationMs: 24 * 60 * 60 * 1000, buckets: 96 },
  { key: 'lastWeek', durationMs: 7 * 24 * 60 * 60 * 1000, buckets: 84 },
  { key: 'lastMonth', durationMs: 30 * 24 * 60 * 60 * 1000, buckets: 90 }
];

export default function ConnectedClientsChart({ connected, onRefresh, sampleVersion, total }) {
  const { t } = useTranslation();
  const canvasRef = useRef(null);
  const refreshRef = useRef(onRefresh);
  const [chartWidth, setChartWidth] = useState(760);
  const [hovered, setHovered] = useState(null);
  const [range, setRange] = useState('lastDay');
  const [autoRefreshSeconds, setAutoRefreshSeconds] = useState(15);
  const [samples, setSamples] = useState(loadSamples);
  const selectedRange = ranges.find((item) => item.key === range) || ranges[1];
  const now = Date.now();
  const buckets = useMemo(
    () => buildBuckets(samples, connected, total, selectedRange, now),
    [connected, now, samples, selectedRange, total]
  );
  const max = Math.max(1, total || 0, ...buckets.map((bucket) => bucket.connected));

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

  useEffect(() => {
    if (!sampleVersion) return;
    setSamples((current) => {
      const next = pruneSamples([
        ...current,
        { connected: Number(connected || 0), total: Number(total || 0), ts: Date.now() }
      ]);
      saveSamples(next);
      return next;
    });
  }, [connected, sampleVersion, total]);

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
  const yTicks = buildYAxisTicks(max);
  const barSlot = chart.plotWidth / Math.max(1, buckets.length);
  const barWidth = Math.max(2, Math.min(14, barSlot - 2));

  return (
    <div className="activity-chart-panel">
      <div className="chart-controls" aria-label={t('chartRange')}>
        {ranges.map((item) => (
          <button
            className={item.key === range ? 'active' : ''}
            key={item.key}
            onClick={() => setRange(item.key)}
            title={t('tooltipChartRangeOption', { range: t(item.key) })}
            type="button"
          >
            {t(item.key)}
          </button>
        ))}
        <label className="chart-refresh" title={t('tooltipAutoRefresh')}>
          <span>{t('autoRefresh')}</span>
          <select
            disabled={!onRefresh}
            value={autoRefreshSeconds}
            onChange={(event) => setAutoRefreshSeconds(Number(event.target.value))}
            title={t('tooltipAutoRefresh')}
          >
            {refreshIntervals.map((seconds) => (
              <option key={seconds} value={seconds}>{seconds === 0 ? t('none') : `${seconds}s`}</option>
            ))}
          </select>
          <button className="pagination-button" disabled={!onRefresh} onClick={onRefresh} title={t('tooltipManualRefresh')} type="button">
            <RefreshCw size={14} />
          </button>
        </label>
      </div>
      <div className="chart-canvas" ref={canvasRef}>
        <svg className="activity-chart" viewBox={`0 0 ${chart.width} ${chart.height}`} role="img" aria-label={t('connectedClients')}>
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
          <text className="axis-title" transform={`translate(16 ${chart.top + chart.plotHeight / 2}) rotate(-90)`}>{t('connectedNodeCount')}</text>
          <text className="axis-title" x={chart.left + chart.plotWidth / 2} y={chart.height - 12} textAnchor="middle">{t('time')}</text>
          {buckets.map((bucket, index) => {
            const x = chart.left + index * barSlot + (barSlot - barWidth) / 2;
            const height = (bucket.connected / max) * chart.plotHeight;
            const y = chart.baseY - height;
            const showTick = shouldShowTick(index, buckets.length);
            return (
              <g
                key={`${bucket.start}-${index}`}
                onBlur={() => setHovered(null)}
                onFocus={() => setHovered({ bucket, ...tooltipFromChartPoint(x + barWidth / 2, y + height / 2, chart, canvasRef.current) })}
                onMouseLeave={() => setHovered(null)}
                onMouseMove={(event) => setHovered({ bucket, ...tooltipFromPointer(event, canvasRef.current) })}
                tabIndex={0}
              >
                <title>{`${bucket.label}: ${t('connectedClients')} ${bucket.connected}. ${t('tooltipChartRange')}`}</title>
                <rect x={x} y={chart.top} width={barWidth} height={chart.plotHeight} fill="transparent" />
                <rect x={x} y={y} width={barWidth} height={height} rx="3" fill="var(--color-info)" />
                {showTick ? <text className="axis-label" x={x + barWidth / 2} y={chart.baseY + 18} textAnchor="middle">{bucket.label}</text> : null}
              </g>
            );
          })}
        </svg>
        {hovered ? (
          <div className="chart-tooltip" style={{ left: `${hovered.x}px`, top: `${hovered.y}px`, transform: hovered.transform }}>
            <strong>{hovered.bucket.label}</strong>
            <span>{t('connectedClients')}: {hovered.bucket.connected}</span>
            <span>{t('clients')}: {hovered.bucket.total}</span>
          </div>
        ) : null}
      </div>
      <div className="chart-legend">
        <span><i className="info-swatch" />{t('connectedClients')}</span>
      </div>
    </div>
  );
}

function loadSamples() {
  try {
    const parsed = JSON.parse(window.localStorage.getItem(sampleStorageKey) || '[]');
    return pruneSamples(Array.isArray(parsed) ? parsed : []);
  } catch {
    return [];
  }
}

function saveSamples(samples) {
  try {
    window.localStorage.setItem(sampleStorageKey, JSON.stringify(samples));
  } catch {
    // Local storage can be unavailable in hardened browsers.
  }
}

function pruneSamples(samples) {
  const cutoff = Date.now() - ranges[ranges.length - 1].durationMs;
  return samples
    .filter((sample) => Number.isFinite(Number(sample.ts)) && Number(sample.ts) >= cutoff)
    .map((sample) => ({
      connected: Number(sample.connected || 0),
      total: Number(sample.total || 0),
      ts: Number(sample.ts)
    }))
    .slice(-500);
}

function buildBuckets(samples, connected, total, selectedRange, now) {
  const rangeStart = now - selectedRange.durationMs;
  const bucketMs = selectedRange.durationMs / selectedRange.buckets;
  const source = pruneSamples([
    ...samples,
    { connected: Number(connected || 0), total: Number(total || 0), ts: now }
  ]).filter((sample) => sample.ts >= rangeStart && sample.ts <= now);

  return Array.from({ length: selectedRange.buckets }, (_, index) => {
    const start = rangeStart + index * bucketMs;
    const end = index === selectedRange.buckets - 1 ? now + 1 : start + bucketMs;
    const bucketSamples = source
      .filter((sample) => sample.ts >= start && sample.ts < end)
      .sort((left, right) => left.ts - right.ts);
    const latest = bucketSamples[bucketSamples.length - 1];
    return {
      connected: latest ? latest.connected : 0,
      label: formatTime(start, selectedRange.key),
      start,
      total: latest ? latest.total : Number(total || 0)
    };
  });
}

function formatTime(timestamp, range) {
  const date = new Date(timestamp);
  if (range === 'lastHour') {
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
  if (range === 'lastDay') {
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }
  return date.toLocaleDateString([], { month: 'short', day: 'numeric' });
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

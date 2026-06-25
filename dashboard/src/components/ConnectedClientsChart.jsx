import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

const sampleStorageKey = 'xeno.connectedClients.samples';
const ranges = [
  { key: 'lastHour', durationMs: 60 * 60 * 1000, buckets: 12 },
  { key: 'lastDay', durationMs: 24 * 60 * 60 * 1000, buckets: 24 },
  { key: 'lastWeek', durationMs: 7 * 24 * 60 * 60 * 1000, buckets: 14 },
  { key: 'lastMonth', durationMs: 30 * 24 * 60 * 60 * 1000, buckets: 30 }
];

export default function ConnectedClientsChart({ connected, onRefresh, sampleVersion, total }) {
  const { t } = useTranslation();
  const canvasRef = useRef(null);
  const refreshRef = useRef(onRefresh);
  const [chartWidth, setChartWidth] = useState(760);
  const [hovered, setHovered] = useState(null);
  const [range, setRange] = useState('lastDay');
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
    const interval = window.setInterval(() => {
      refreshRef.current?.();
    }, 15000);
    return () => window.clearInterval(interval);
  }, []);

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
  const yTicks = [max, Math.round(max / 2), 0];
  const barSlot = chart.plotWidth / Math.max(1, buckets.length);
  const barWidth = Math.max(5, barSlot - 6);

  return (
    <div className="activity-chart-panel">
      <div className="chart-controls" aria-label={t('chartRange')}>
        {ranges.map((item) => (
          <button
            className={item.key === range ? 'active' : ''}
            key={item.key}
            onClick={() => setRange(item.key)}
            type="button"
          >
            {t(item.key)}
          </button>
        ))}
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
                onFocus={() => setHovered({ bucket, x: x + barWidth / 2, y })}
                onMouseLeave={() => setHovered(null)}
                onMouseMove={() => setHovered({ bucket, x: x + barWidth / 2, y })}
                tabIndex={0}
              >
                <title>{`${bucket.label}: ${bucket.connected}`}</title>
                <rect x={x} y={chart.top} width={barWidth} height={chart.plotHeight} fill="transparent" />
                <rect x={x} y={y} width={barWidth} height={height} rx="3" fill="var(--color-info)" />
                {showTick ? <text className="axis-label" x={x + barWidth / 2} y={chart.baseY + 18} textAnchor="middle">{bucket.label}</text> : null}
              </g>
            );
          })}
        </svg>
        {hovered ? (
          <div className="chart-tooltip" style={{ left: `${(hovered.x / chart.width) * 100}%`, top: `${(hovered.y / chart.height) * 100}%` }}>
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
  if (count <= 12) return true;
  if (count <= 24) return index % 4 === 0 || index === count - 1;
  return index % 5 === 0 || index === count - 1;
}

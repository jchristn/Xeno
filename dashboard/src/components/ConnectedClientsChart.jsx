import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

const sampleStorageKey = 'xeno.connectedClients.samples';
const ranges = [
  { key: 'lastHour', durationMs: 60 * 60 * 1000 },
  { key: 'lastDay', durationMs: 24 * 60 * 60 * 1000 },
  { key: 'lastWeek', durationMs: 7 * 24 * 60 * 60 * 1000 },
  { key: 'lastMonth', durationMs: 30 * 24 * 60 * 60 * 1000 }
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
  const rangeStart = now - selectedRange.durationMs;
  const visibleSamples = useMemo(
    () => normalizeSamples(samples, connected, total, rangeStart, now),
    [connected, now, rangeStart, samples, total]
  );
  const max = Math.max(1, total || 0, ...visibleSamples.map((sample) => sample.connected));

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
  const points = visibleSamples.map((sample) => {
    const x = chart.left + ((sample.ts - rangeStart) / selectedRange.durationMs) * chart.plotWidth;
    const y = chart.baseY - (sample.connected / max) * chart.plotHeight;
    return { ...sample, x, y };
  });
  const linePath = points.map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x.toFixed(2)} ${point.y.toFixed(2)}`).join(' ');
  const areaPath = points.length > 0
    ? `${linePath} L ${points[points.length - 1].x.toFixed(2)} ${chart.baseY} L ${points[0].x.toFixed(2)} ${chart.baseY} Z`
    : '';

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
          <text className="axis-label" x={chart.left} y={chart.baseY + 20} textAnchor="start">{formatTime(rangeStart, selectedRange.key)}</text>
          <text className="axis-label" x={chart.left + chart.plotWidth / 2} y={chart.baseY + 20} textAnchor="middle">{formatTime(rangeStart + selectedRange.durationMs / 2, selectedRange.key)}</text>
          <text className="axis-label" x={chart.left + chart.plotWidth} y={chart.baseY + 20} textAnchor="end">{formatTime(now, selectedRange.key)}</text>
          {areaPath ? <path d={areaPath} fill="var(--color-info-soft)" opacity="0.78" /> : null}
          {linePath ? <path d={linePath} fill="none" stroke="var(--color-info)" strokeLinecap="round" strokeLinejoin="round" strokeWidth="3" /> : null}
          {points.map((point, index) => (
            <circle
              cx={point.x}
              cy={point.y}
              fill="var(--color-info)"
              key={`${point.ts}-${index}`}
              onBlur={() => setHovered(null)}
              onFocus={() => setHovered(point)}
              onMouseLeave={() => setHovered(null)}
              onMouseMove={() => setHovered(point)}
              r="4"
              tabIndex={0}
            >
              <title>{`${formatTime(point.ts, selectedRange.key)}: ${point.connected}`}</title>
            </circle>
          ))}
        </svg>
        {hovered ? (
          <div className="chart-tooltip" style={{ left: `${(hovered.x / chart.width) * 100}%`, top: `${(hovered.y / chart.height) * 100}%` }}>
            <strong>{formatTime(hovered.ts, selectedRange.key)}</strong>
            <span>{t('connectedClients')}: {hovered.connected}</span>
            <span>{t('clients')}: {hovered.total}</span>
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

function normalizeSamples(samples, connected, total, rangeStart, now) {
  const visible = samples.filter((sample) => sample.ts >= rangeStart && sample.ts <= now);
  if (visible.length === 0) {
    return [
      { connected: Number(connected || 0), total: Number(total || 0), ts: rangeStart },
      { connected: Number(connected || 0), total: Number(total || 0), ts: now }
    ];
  }

  if (visible.length === 1) {
    return [
      { ...visible[0], ts: rangeStart },
      visible[0]
    ];
  }

  return visible;
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

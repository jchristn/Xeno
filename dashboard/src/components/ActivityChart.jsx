import { useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

const ranges = [
  { key: 'lastHour', buckets: 12 },
  { key: 'lastDay', buckets: 24 },
  { key: 'lastWeek', buckets: 14 },
  { key: 'lastMonth', buckets: 30 }
];

export default function ActivityChart({ summary }) {
  const { t } = useTranslation();
  const [range, setRange] = useState('lastDay');
  const [hovered, setHovered] = useState(null);
  const selectedRange = ranges.find((item) => item.key === range) || ranges[1];
  const buckets = useMemo(() => buildBuckets(summary?.buckets, selectedRange), [summary?.buckets, selectedRange]);
  const max = Math.max(1, ...buckets.map((bucket) => bucket.success + bucket.failed));

  const chart = {
    height: 525,
    width: 760,
    left: 58,
    right: 28,
    top: 28,
    bottom: 58
  };
  chart.plotWidth = chart.width - chart.left - chart.right;
  chart.plotHeight = chart.height - chart.top - chart.bottom;
  chart.baseY = chart.top + chart.plotHeight;
  const barSlot = chart.plotWidth / Math.max(1, buckets.length);
  const barWidth = Math.max(5, barSlot - 6);
  const yTicks = [max, Math.round(max / 2), 0];

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
      <div className="chart-canvas">
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
            return (
              <g
                key={`${bucket.label}-${index}`}
                onBlur={() => setHovered(null)}
                onFocus={() => setHovered({ bucket, x: x + barWidth / 2, y: Math.min(successY, failedY) })}
                onMouseLeave={() => setHovered(null)}
                onMouseMove={() => setHovered({ bucket, x: x + barWidth / 2, y: Math.min(successY, failedY) })}
                tabIndex={0}
              >
                <title>{`${bucket.label}: ${t('success')} ${bucket.success}, ${t('failed')} ${bucket.failed}`}</title>
                <rect x={x} y={failedY} width={barWidth} height={failedHeight} rx="3" fill="var(--color-danger)" />
                <rect x={x} y={successY} width={barWidth} height={successHeight} rx="3" fill="var(--color-success)" />
                {showTick ? <text className="axis-label" x={x + barWidth / 2} y={chart.baseY + 18} textAnchor="middle">{bucket.label}</text> : null}
              </g>
            );
          })}
        </svg>
        {hovered ? (
          <div className="chart-tooltip" style={{ left: `${(hovered.x / chart.width) * 100}%`, top: `${(hovered.y / chart.height) * 100}%` }}>
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

function buildBuckets(rawBuckets, selectedRange) {
  const source = Array.isArray(rawBuckets) && rawBuckets.length > 0
    ? rawBuckets
    : Array.from({ length: selectedRange.buckets }, (_, index) => ({
        label: fallbackLabel(selectedRange.key, index, selectedRange.buckets),
        success: index % 4,
        failed: index % 5 === 0 ? 1 : 0
      }));

  return source.slice(-selectedRange.buckets).map((bucket, index) => ({
    failed: Number(bucket.failed ?? bucket.Failed ?? 0),
    label: String(bucket.label ?? bucket.Label ?? fallbackLabel(selectedRange.key, index, source.length)),
    success: Number(bucket.success ?? bucket.Success ?? 0)
  }));
}

function fallbackLabel(range, index, count) {
  if (range === 'lastHour') return `${index * 5}m`;
  if (range === 'lastWeek') return `D${Math.max(1, index + 1)}`;
  if (range === 'lastMonth') return `${Math.max(1, index + 1)}`;
  return `${Math.max(0, 24 - count + index)}h`;
}

function shouldShowTick(index, count) {
  if (count <= 12) return true;
  if (count <= 24) return index % 4 === 0 || index === count - 1;
  return index % 5 === 0 || index === count - 1;
}

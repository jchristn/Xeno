import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import ChartTooltip from './ChartTooltip.jsx';
import useChartWidth from './useChartWidth.js';
import { formatBucketLabel, formatChartValue } from './usageCharts.js';

const DEFAULT_HEIGHT = 200;
const LEFT = 58;
const TOP = 8;

// Time charts: stacked columns and percentile lines, one slot per bucket. Clicking a slot, or pressing Enter on it,
// drills down. Every time chart in the web dashboards draws through this component. A bucket's label is its own label
// when it has one, otherwise its start time.
export default function TimeChart({ chart, height: HEIGHT = DEFAULT_HEIGHT, onDrill, svgClassName, xAxisLabel, yAxisLabel }) {
  const { i18n, t } = useTranslation();
  const [frameRef, width] = useChartWidth();
  const [tip, setTip] = useState(null);
  const locale = i18n.resolvedLanguage || i18n.language;
  const count = Math.max(1, chart.buckets.length);
  const BOTTOM = xAxisLabel ? 40 : 20;
  const left = yAxisLabel ? LEFT + 16 : LEFT;
  const plotWidth = Math.max(40, width - left);
  const plotHeight = HEIGHT - BOTTOM - TOP;
  const bucketLabel = (bucket) => bucket.label || formatBucketLabel(bucket.startUtc, chart.bucketMinutes, locale);
  const slot = plotWidth / count;
  const lines = chart.kind === 'Lines';
  const totals = chart.buckets.map((_, index) => (lines
    ? Math.max(0, ...chart.series.map((series) => series.values[index] ?? 0))
    : chart.series.reduce((sum, series) => sum + Math.max(0, series.values[index] || 0), 0)));
  const maximum = Math.max(lines ? 0.001 : 1, ...totals);
  const y = (value) => TOP + plotHeight - (Math.min(value, maximum) / maximum) * plotHeight;
  const labelEvery = Math.max(1, Math.ceil(count / Math.max(2, Math.floor(plotWidth / 90))));

  function show(event, index) {
    setTip({
      x: event.clientX,
      y: event.clientY,
      title: bucketLabel(chart.buckets[index]),
      lines: chart.series.map((series) => ({ label: series.label, color: series.color, value: formatChartValue(series.values[index], chart.unit, locale) }))
    });
  }

  function showAt(event, index) {
    const bounds = event.currentTarget.getBoundingClientRect();
    show({ clientX: bounds.left + bounds.width / 2, clientY: bounds.top + bounds.height / 2 }, index);
  }

  function keyDrill(event, index) {
    if (event.key === 'Enter' && onDrill) drill(event, index, null);
  }

  function drill(event, index, seriesKey) {
    event.stopPropagation();
    if (onDrill) onDrill(chart.buckets[index], chart.drillSeries ? seriesKey : null);
  }

  // Dashed segments joining readings across empty buckets, so sparse data reads as one line with visible gaps.
  function bridgePath(values) {
    let path = '';
    let previous = -1;
    values.forEach((value, index) => {
      if (value === null || value === undefined) return;
      if (previous >= 0 && index - previous > 1) {
        path += `M${(left + previous * slot + slot / 2).toFixed(1)} ${y(values[previous]).toFixed(1)} L${(left + index * slot + slot / 2).toFixed(1)} ${y(value).toFixed(1)} `;
      }
      previous = index;
    });
    return path;
  }

  function linePath(values) {
    let path = '';
    let drawing = false;
    values.forEach((value, index) => {
      if (value === null || value === undefined) {
        drawing = false;
        return;
      }
      const x = left + index * slot + slot / 2;
      path += `${drawing ? 'L' : 'M'}${x.toFixed(1)} ${y(value).toFixed(1)} `;
      drawing = true;
    });
    return path;
  }

  return (
    <div className="usage-time-chart" ref={frameRef}>
      <svg aria-label={chart.title} className={svgClassName} height={HEIGHT} role="img" viewBox={`0 0 ${width} ${HEIGHT}`} width={width}>
        {yAxisLabel ? <text className="axis-title" transform={`translate(12 ${TOP + plotHeight / 2}) rotate(-90)`}>{yAxisLabel}</text> : null}
        {xAxisLabel ? <text className="axis-title" textAnchor="middle" x={left + plotWidth / 2} y={HEIGHT - 4}>{xAxisLabel}</text> : null}
        {[1, 0.5, 0].map((ratio) => (
          <g key={ratio}>
            <line stroke="var(--color-border)" strokeDasharray={ratio === 0 ? undefined : '3 4'} x1={left} x2={width} y1={TOP + plotHeight * (1 - ratio)} y2={TOP + plotHeight * (1 - ratio)} />
            <text fill="var(--color-muted)" fontSize="10" textAnchor="end" x={left - 6} y={TOP + plotHeight * (1 - ratio) + 3}>{formatChartValue(maximum * ratio, chart.unit, locale)}</text>
          </g>
        ))}
        {lines ? chart.series.slice().reverse().map((series) => (
          <g key={series.key}>
            <path className="usage-bridge" d={bridgePath(series.values)} fill="none" stroke={series.color} strokeDasharray="2 4" strokeOpacity="0.8" strokeWidth={series.key === 'p50' ? 2 : 1.25} />
            <path d={linePath(series.values)} fill="none" stroke={series.color} strokeLinejoin="round" strokeWidth={series.key === 'p50' ? 2.5 : 1.5} />
            {series.values.map((value, index) => {
              // Every reading gets a dot, so a lone value and the ends of a dashed bridge are easy to see.
              if (value === null || value === undefined) return null;
              return <circle className="usage-point" cx={left + index * slot + slot / 2} cy={y(value)} fill={series.color} key={chart.buckets[index].startUtc} r={series.key === 'p50' ? 3 : 2.25} />;
            })}
          </g>
        )) : chart.buckets.map((bucket, index) => {
          let top = TOP + plotHeight;
          const barWidth = Math.max(1, Math.min(slot * 0.78, 42));
          return (
            <g key={bucket.startUtc}>
              {chart.series.map((series) => {
                const value = Math.max(0, series.values[index] || 0);
                if (!value) return null;
                const height = (value / maximum) * plotHeight;
                top -= height;
                return <rect data-series={series.key} fill={series.color} height={height} key={series.key} rx={barWidth > 6 ? 1.5 : 0} width={barWidth} x={left + index * slot + (slot - barWidth) / 2} y={top} />;
              })}
            </g>
          );
        })}
        {chart.buckets.map((bucket, index) => (
          <g key={`slot-${bucket.startUtc}`}>
            <rect aria-label={t('usageBucketAria', { when: bucketLabel(bucket) })} className="usage-slot" data-bucket-index={index} fill="transparent" height={plotHeight} onBlur={() => setTip(null)} onClick={(event) => drill(event, index, null)} onFocus={(event) => showAt(event, index)} onKeyDown={(event) => keyDrill(event, index)} tabIndex={lines ? 0 : undefined} onMouseLeave={() => setTip(null)} onMouseMove={(event) => show(event, index)} style={{ cursor: onDrill ? 'pointer' : undefined, pointerEvents: lines ? 'all' : 'none' }} width={slot} x={left + index * slot} y={TOP} />
            {index % labelEvery === 0 ? <text fill="var(--color-muted)" fontSize="10" x={left + index * slot + 2} y={TOP + plotHeight + 14}>{bucketLabel(bucket)}</text> : null}
          </g>
        ))}
        {!lines ? chart.buckets.map((bucket, index) => (
          <rect aria-label={t('usageBucketAria', { when: bucketLabel(bucket) })} className="usage-hover" fill="transparent" height={plotHeight} key={`hover-${bucket.startUtc}`} onBlur={() => setTip(null)} onFocus={(event) => showAt(event, index)} onKeyDown={(event) => keyDrill(event, index)} tabIndex={0} onClick={(event) => drill(event, index, topSeriesAt(chart, index, event, maximum, plotHeight, TOP))} onMouseLeave={() => setTip(null)} onMouseMove={(event) => show(event, index)} style={{ cursor: onDrill ? 'pointer' : undefined }} width={slot} x={left + index * slot} y={TOP} />
        )) : null}
      </svg>
      <ChartTooltip tip={tip} />
    </div>
  );
}

// The series under the pointer in a stacked column, from the pointer's height in the plot.
function topSeriesAt(chart, index, event, maximum, plotHeight, top) {
  const bounds = event.currentTarget.ownerSVGElement?.getBoundingClientRect();
  if (!bounds) return null;
  const fromBottom = top + plotHeight - (event.clientY - bounds.top);
  let running = 0;
  for (const series of chart.series) {
    running += (Math.max(0, series.values[index] || 0) / maximum) * plotHeight;
    if (fromBottom <= running) return series.key;
  }
  return null;
}

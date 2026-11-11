import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';
import ChartCard from './ChartCard.jsx';
import { toneColor } from './usageCharts.js';
import { usageWindow } from '../usage/usageRanges.js';

function field(bucket, name) {
  const found = bucket[name] ?? bucket[name.charAt(0).toUpperCase() + name.slice(1)];
  return found === undefined || found === null || !Number.isFinite(Number(found)) ? null : Number(found);
}

function startOf(bucket) {
  return Date.parse(String(bucket.startUtc ?? bucket.StartUtc ?? ''));
}

// One telemetry metric over time as average, minimum, and maximum lines, like XenoCharts.HostMetric and ProviderMetric.
export function buildRollupChart(id, title, unit, rollups, window, t) {
  const fromMs = Date.parse(window.fromUtc);
  const toMs = Date.parse(window.toUtc);
  const bucketMs = window.bucketMinutes * 60000;
  const count = Math.max(1, Math.ceil((toMs - fromMs) / bucketMs));
  const buckets = Array.from({ length: count }, (_, index) => ({
    startUtc: new Date(fromMs + index * bucketMs).toISOString(),
    endUtc: new Date(Math.min(toMs, fromMs + (index + 1) * bucketMs)).toISOString()
  }));
  const values = { average: Array(count).fill(null), minimum: Array(count).fill(null), maximum: Array(count).fill(null) };
  (Array.isArray(rollups) ? rollups : []).forEach((bucket) => {
    const startMs = startOf(bucket);
    if (!Number.isFinite(startMs) || startMs + bucketMs <= fromMs) return;
    const index = Math.max(0, Math.floor((startMs - fromMs) / bucketMs));
    if (index >= count) return;
    Object.keys(values).forEach((key) => { values[key][index] = field(bucket, key); });
  });
  return {
    id,
    kind: 'Lines',
    title,
    unit,
    bucketMinutes: window.bucketMinutes,
    buckets,
    series: [
      { key: 'average', label: t('average'), color: toneColor('Info'), values: values.average },
      { key: 'minimum', label: t('minimum'), color: toneColor('Neutral'), values: values.minimum },
      { key: 'maximum', label: t('maximum'), color: toneColor('Warning'), values: values.maximum }
    ],
    hasData: values.average.some((value) => value !== null),
    emptyKey: 'telemetryChartEmpty'
  };
}

export default function RollupChart({ id, metric, range, rollups, unit }) {
  const { t } = useTranslation();
  const chart = useMemo(() => buildRollupChart(id, `${t(metric)}, ${t(range).toLowerCase()}`, unit, rollups, usageWindow(range), t), [id, metric, range, rollups, t, unit]);
  return <ChartCard chart={chart} />;
}

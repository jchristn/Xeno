import { useTranslation } from 'react-i18next';
import EmptyState from '../components/EmptyState.jsx';
import TimeChart from '../charts/TimeChart.jsx';

export { chatRanges } from './chatRanges.js';

// Chat turns per time bucket, stacked by where they ran, with failures on top. Used by both dashboards.
export default function ChatActivityChart({ buckets, showLocal = true }) {
  const { t } = useTranslation();
  const total = buckets.reduce((sum, item) => sum + item.local + item.network + item.errors, 0);
  // Without a local series (the server dashboard), every answered turn counts as network so no bar goes unlabeled.
  const series = showLocal
    ? [
      { key: 'local', label: t('chatRouteLocal'), color: 'var(--color-success)', values: buckets.map((bucket) => bucket.local) },
      { key: 'network', label: t('chatRouteNetwork'), color: 'var(--color-info)', values: buckets.map((bucket) => bucket.network) },
      { key: 'errors', label: t('errors'), color: 'var(--color-danger)', values: buckets.map((bucket) => bucket.errors) }
    ]
    : [
      { key: 'network', label: t('chatRouteNetwork'), color: 'var(--color-info)', values: buckets.map((bucket) => bucket.network + bucket.local) },
      { key: 'errors', label: t('errors'), color: 'var(--color-danger)', values: buckets.map((bucket) => bucket.errors) }
    ];
  const chart = {
    id: 'chat.activity',
    kind: 'StackedColumns',
    title: t('chatActivitySummary', { total }),
    unit: '',
    stacked: true,
    bucketMinutes: buckets.length > 1 ? (new Date(buckets[1].startUtc) - new Date(buckets[0].startUtc)) / 60000 : 60,
    buckets,
    series
  };
  return (
    <div className="route-mix-chart chat-activity-chart">
      {total === 0 ? <EmptyState message={t('chatNoActivity')} /> : (
        <>
          <TimeChart chart={chart} height={288} />
          <div className="chart-legend">
            {series.map((item) => <span key={item.key}><i style={{ background: item.color }} />{item.label}</span>)}
          </div>
        </>
      )}
    </div>
  );
}

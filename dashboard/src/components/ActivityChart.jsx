import { useTranslation } from 'react-i18next';

export default function ActivityChart({ summary }) {
  const { t } = useTranslation();
  const buckets = summary?.buckets || Array.from({ length: 24 }, (_, index) => ({ label: String(index), success: index % 4, failed: index % 3 === 0 ? 1 : 0 }));
  const max = Math.max(1, ...buckets.map((bucket) => bucket.success + bucket.failed));

  return (
    <svg className="activity-chart" viewBox="0 0 720 220" role="img" aria-label={t('requests')}>
      {buckets.map((bucket, index) => {
        const x = 20 + index * (680 / buckets.length);
        const width = Math.max(4, 680 / buckets.length - 4);
        const successHeight = (bucket.success / max) * 170;
        const failedHeight = (bucket.failed / max) * 170;
        return (
          <g key={index}>
            <rect x={x} y={200 - failedHeight} width={width} height={failedHeight} fill="var(--color-danger)" />
            <rect x={x} y={200 - failedHeight - successHeight} width={width} height={successHeight} fill="var(--color-success)" />
          </g>
        );
      })}
      <line x1="16" y1="200" x2="704" y2="200" stroke="var(--color-border)" />
    </svg>
  );
}

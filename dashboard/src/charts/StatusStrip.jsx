import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import ChartTooltip from './ChartTooltip.jsx';
import { formatBucketLabel } from './usageCharts.js';

const glyphs = { Healthy: '', Degraded: '/', Unhealthy: '×', NoData: '' };

// Provider health: a row of cells per provider. Status shows as a color and a mark, so color is never the only signal.
export default function StatusStrip({ strip, onCell }) {
  const { i18n, t } = useTranslation();
  const [tip, setTip] = useState(null);
  const locale = i18n.resolvedLanguage || i18n.language;
  const providers = strip?.providers || [];
  const hasData = providers.some((row) => (row.cells || []).some((cell) => cell.status !== 'NoData'));
  if (!hasData) return <div className="usage-empty">{t('usageEmptyHealth')}</div>;
  return (
    <div aria-label={t('usageChartHealth')} className="usage-strip" role="img">
      {providers.map((row) => (
        <div className="usage-strip-row" key={`${row.clientId || ''}-${row.providerName}`}>
          <span className="usage-strip-label" title={row.providerName}>
            {row.providerName}
            {row.uptimePercent !== undefined && row.uptimePercent !== null ? <small>{t('usageUptime', { value: new Intl.NumberFormat(locale, { maximumFractionDigits: 1 }).format(row.uptimePercent) })}</small> : null}
          </span>
          <div className="usage-strip-cells">
            {(row.cells || []).map((cell) => (
              <i
                className={`usage-strip-cell ${String(cell.status || 'NoData').toLowerCase()}`}
                data-status={cell.status}
                key={cell.startUtc}
                onClick={onCell ? () => onCell(row, cell) : undefined}
                onMouseLeave={() => setTip(null)}
                onMouseMove={(event) => setTip({ x: event.clientX, y: event.clientY, title: `${row.providerName} ${formatBucketLabel(cell.startUtc, strip.bucketMinutes, locale)}`, lines: [{ label: t(`usageStatus${cell.status === 'NoData' ? 'NoData' : cell.status}`), value: t('usageStatusSamples', { samples: cell.samples || 0, failures: cell.failures || 0 }) }] })}
              >
                {glyphs[cell.status] || ''}
              </i>
            ))}
          </div>
        </div>
      ))}
      <div className="chart-legend">
        {['Healthy', 'Degraded', 'Unhealthy', 'NoData'].map((status) => (
          <span key={status}><i className={`usage-strip-cell ${status.toLowerCase()}`}>{glyphs[status]}</i>{t(`usageStatus${status}`)}</span>
        ))}
      </div>
      <ChartTooltip tip={tip} />
    </div>
  );
}

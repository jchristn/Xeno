import { useTranslation } from 'react-i18next';
import { formatChartValue } from './usageCharts.js';

// One bar per label. Stacked series share a bar; otherwise each series gets its own thinner bar in the row.
export default function HorizontalBarChart({ chart }) {
  const { i18n } = useTranslation();
  const locale = i18n.resolvedLanguage || i18n.language;
  const totals = chart.labels.map((_, row) => (chart.stacked
    ? chart.series.reduce((sum, series) => sum + Math.max(0, series.values[row] || 0), 0)
    : Math.max(0, ...chart.series.map((series) => series.values[row] || 0))));
  const maximum = Math.max(1e-9, ...totals);
  return (
    <div aria-label={chart.title} className="usage-bars" role="img">
      {chart.labels.map((label, row) => (
        <div className="usage-bar-row" key={label}>
          <span className="usage-bar-label" title={label}>{label}</span>
          <div className={`usage-bar-track${chart.stacked ? '' : ' grouped'}`}>
            {chart.series.map((series) => {
              const value = Math.max(0, series.values[row] || 0);
              return <i key={series.key} style={{ background: series.color, width: `${(value / maximum) * 100}%` }} title={`${series.label}: ${formatChartValue(value, chart.unit, locale)}`} />;
            })}
          </div>
          <span className="usage-bar-value">{chart.stacked ? formatChartValue(totals[row], chart.unit, locale) : chart.series.map((series) => formatChartValue(series.values[row] || 0, chart.unit, locale)).join(' / ')}</span>
        </div>
      ))}
    </div>
  );
}

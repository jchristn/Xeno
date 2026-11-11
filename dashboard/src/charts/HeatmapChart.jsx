import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import ChartTooltip from './ChartTooltip.jsx';
import { formatChartValue } from './usageCharts.js';

function weekdayLabel(row, locale) {
  // 2023-01-01 was a Sunday, matching row 0.
  return new Intl.DateTimeFormat(locale, { weekday: 'short', timeZone: 'UTC' }).format(new Date(Date.UTC(2023, 0, 1 + row)));
}

function rowLabel(heatmap, row, cells, locale) {
  if (heatmap.layout === 'WeekdayByHour') return weekdayLabel(row, locale);
  const first = cells[0];
  if (!first) return '';
  const date = new Date(first.startUtc);
  return heatmap.layout === 'HourByMinute'
    ? new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit' }).format(date)
    : new Intl.DateTimeFormat(locale, { weekday: 'short', month: 'numeric', day: 'numeric' }).format(date);
}

function columnLabel(heatmap, column) {
  return heatmap.layout === 'HourByMinute' ? `:${column}` : String(column).padStart(2, '0');
}

// Token activity: a grid of cells shaded by tokens. The backend has already placed each cell for the viewer's time zone.
export default function HeatmapChart({ chart }) {
  const { i18n, t } = useTranslation();
  const [tip, setTip] = useState(null);
  const locale = i18n.resolvedLanguage || i18n.language;
  const { heatmap, grid } = chart;
  const maximum = Math.max(1, heatmap.maximumTokens || 0);
  const every = heatmap.columns > 12 ? 3 : 1;
  return (
    <div aria-label={chart.title} className="usage-heatmap" role="img" style={{ '--heat-columns': heatmap.columns }}>
      <div className="usage-heat-layout">{t(`usageHeatmap${heatmap.layout}`)}</div>
      {grid.map((cells, row) => (
        <div className="usage-heat-row" key={row}>
          <span className="usage-heat-label">{rowLabel(heatmap, row, cells, locale)}</span>
          {cells.map((cell) => (
            <i
              className={`usage-heat-cell${cell.tokens > 0 ? ' active' : ''}`}
              data-tokens={cell.tokens}
              key={cell.column}
              onMouseLeave={() => setTip(null)}
              onMouseMove={(event) => setTip({ x: event.clientX, y: event.clientY, title: `${rowLabel(heatmap, row, cells, locale)} ${columnLabel(heatmap, cell.column)}`, lines: [{ label: t('usageSeriesTokens'), value: formatChartValue(cell.tokens, '', locale) }, { label: t('usageSeriesRequests'), value: formatChartValue(cell.requests, '', locale) }] })}
              style={{ opacity: cell.tokens > 0 ? 0.2 + 0.8 * Math.min(1, cell.tokens / maximum) : 1 }}
            />
          ))}
        </div>
      ))}
      <div className="usage-heat-row usage-heat-axis">
        <span className="usage-heat-label" />
        {Array.from({ length: heatmap.columns }, (_, column) => <span key={column}>{column % every === 0 ? columnLabel(heatmap, column) : ''}</span>)}
      </div>
      <ChartTooltip tip={tip} />
    </div>
  );
}

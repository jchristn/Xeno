import { useTranslation } from 'react-i18next';
import HeatmapChart from './HeatmapChart.jsx';
import HorizontalBarChart from './HorizontalBarChart.jsx';
import TimeChart from './TimeChart.jsx';

// One chart with its title, legend, and empty state.
export default function ChartCard({ chart, onDrill }) {
  const { t } = useTranslation();
  return (
    <section className="workspace-card usage-chart-card" data-chart={chart.id}>
      <div className="workspace-card-header"><div><h2>{chart.title}</h2></div></div>
      <div className="workspace-card-body">
        {!chart.hasData ? <div className="usage-empty">{t(chart.emptyKey)}</div> : (
          <>
            {chart.kind === 'HorizontalBars' ? <HorizontalBarChart chart={chart} /> : chart.kind === 'Heatmap' ? <HeatmapChart chart={chart} /> : <TimeChart chart={chart} onDrill={onDrill} />}
            {chart.series.length ? (
              <div className="chart-legend">
                {chart.series.map((series) => <span key={series.key}><i style={{ background: series.color }} />{series.label}</span>)}
              </div>
            ) : null}
          </>
        )}
      </div>
    </section>
  );
}

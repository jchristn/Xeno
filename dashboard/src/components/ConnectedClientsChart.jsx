import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';
import ChartCard from '../charts/ChartCard.jsx';
import TimeChart from '../charts/TimeChart.jsx';
import { usageWindow } from '../usage/usageRanges.js';

// Connected clients over time from the samples the server stores each minute. A bucket with no sample is left empty
// because the server was not running then.
function useConnectedChart(load, range, refreshSignal) {
  const { t } = useTranslation();
  const [history, setHistory] = useState(null);

  useEffect(() => {
    let alive = true;
    load(usageWindow(range))
      .then((next) => { if (alive) setHistory(next); })
      .catch(() => { if (alive) setHistory(null); });
    return () => { alive = false; };
  }, [load, range, refreshSignal]);

  return useMemo(() => {
    const buckets = Array.isArray(history?.buckets) ? history.buckets : [];
    return {
      id: 'connected.clients',
      kind: 'StackedColumns',
      title: t('connectedClients'),
      unit: '',
      stacked: true,
      bucketMinutes: history?.bucketMinutes || 15,
      buckets,
      series: [{ key: 'connected', label: t('connectedClients'), color: 'var(--color-info)', values: buckets.map((item) => item.connected ?? null) }],
      hasData: buckets.some((item) => item.total !== undefined && item.total !== null),
      emptyKey: 'connectedClientsEmpty'
    };
  }, [history, t]);
}

export default function ConnectedClientsChart({ load, range, refreshSignal }) {
  const { t } = useTranslation();
  const chart = useConnectedChart(load, range, refreshSignal);
  return (
    <div className="activity-chart-panel">
      <div className="chart-canvas">
        {chart.hasData
          ? <TimeChart chart={chart} height={525} svgClassName="activity-chart" xAxisLabel={t('time')} yAxisLabel={t('connectedNodeCount')} />
          : <div className="usage-empty">{t(chart.emptyKey)}</div>}
      </div>
      <div className="chart-legend">
        <span><i className="info-swatch" />{t('connectedClients')}</span>
      </div>
    </div>
  );
}

export function ConnectedClientsCard({ load, range, refreshSignal }) {
  return <ChartCard chart={useConnectedChart(load, range, refreshSignal)} />;
}

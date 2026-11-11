import { useEffect, useMemo, useState } from 'react';
import { Activity, CircleAlert, Gauge, MessageSquare, RefreshCw, Timer, Zap } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { AutoRefreshSelect, useAutoRefresh } from '../charts/autoRefresh.jsx';
import ChartCard from '../charts/ChartCard.jsx';
import { ConnectedClientsCard } from '../components/ConnectedClientsChart.jsx';
import { buildUsageChart, formatChartValue, groupKeyLabel, OTHER_KEY, UNKNOWN_KEY, usagePageChartIds } from '../charts/usageCharts.js';
import { useUsageApi } from './UsageApiContext.js';
import { usageRanges, usageWindow } from './usageRanges.js';

// The Usage page in both web dashboards. It asks the backend for finished analytics and draws them; nothing is
// computed here. onDrill(dataset, { fromUtc, toUtc, model }) opens the matching list for a bucket.
export default function UsagePage({ onDrill, refreshSignal }) {
  const { i18n, t } = useTranslation();
  const api = useUsageApi();
  const locale = i18n.resolvedLanguage || i18n.language;
  const [dataset, setDataset] = useState(api.datasets[0]);
  const [range, setRange] = useState('lastDay');
  const [groupBy, setGroupBy] = useState('Model');
  const [groupMetric, setGroupMetric] = useState('Requests');
  const [model, setModel] = useState('');
  const [provider, setProvider] = useState('');
  const [clientId, setClientId] = useState('');
  const [userId, setUserId] = useState('');
  const [analytics, setAnalytics] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');
  const [loadedAt, setLoadedAt] = useState(null);
  const [autoRefreshSeconds, setAutoRefreshSeconds] = useState(15);
  const [autoRefreshSignal, setAutoRefreshSignal] = useState(0);
  const groupings = api.groupingsFor ? api.groupingsFor(dataset) : api.groupings;

  useEffect(() => {
    if (!groupings.includes(groupBy)) setGroupBy('Model');
  }, [dataset]);

  useEffect(() => {
    load();
  }, [dataset, range, groupBy, groupMetric, model, provider, clientId, userId, refreshSignal]);

  useAutoRefresh(autoRefreshSeconds, () => {
    load();
    setAutoRefreshSignal((current) => current + 1);
  });

  async function load() {
    setLoading(true);
    try {
      setError('');
      const query = { ...usageWindow(range), groupBy, groupMetric, model, provider, clientId, userId, utcOffsetMinutes: -new Date().getTimezoneOffset() };
      setAnalytics(await api.getUsage(dataset, query));
      setLoadedAt(new Date());
    } catch (err) {
      setError(err.status === 403 ? t('usageForbidden') : err.message);
      setAnalytics(null);
    } finally {
      setLoading(false);
    }
  }

  const chartIds = usagePageChartIds(dataset);
  const charts = useMemo(() => (analytics ? chartIds.map((id) => buildUsageChart(id, analytics, t)) : []), [analytics, dataset, t]);
  const totals = analytics?.totals;
  const noTraffic = analytics && !totals?.requests;
  const filtered = !!(model || provider || clientId || userId);

  function drill(bucket, seriesKey) {
    if (!onDrill) return;
    const series = seriesKey && seriesKey !== OTHER_KEY && seriesKey !== UNKNOWN_KEY ? seriesKey : null;
    onDrill(dataset, { fromUtc: bucket.startUtc, toUtc: bucket.endUtc, model: series || model || null });
  }

  const ms = (value) => formatChartValue(value, ' ms', locale);
  const count = (value) => new Intl.NumberFormat(locale).format(value ?? 0);
  return (
    <div className="view-stack usage-page">
      <div className="usage-toolbar">
        <div className="segmented-control" role="group" aria-label={t('usageDataset')}>
          {api.datasets.map((item) => <button className={item === dataset ? 'active' : ''} key={item} onClick={() => setDataset(item)} title={t(`usageDataset${item}Help`)} type="button">{t(`usageDataset${item}`)}</button>)}
        </div>
        <div className="time-range-button-row" role="group" aria-label={t('usageRange')}>
          {Object.keys(usageRanges).map((item) => <button className={item === range ? 'active' : ''} key={item} onClick={() => setRange(item)} title={t('tooltipChartRangeOption', { range: t(item) })} type="button">{t(item)}</button>)}
        </div>
        <button aria-label={t('refresh')} className="icon-button" onClick={load} title={t('tooltipManualRefresh')} type="button"><RefreshCw size={16} /></button>
        <AutoRefreshSelect onChange={setAutoRefreshSeconds} seconds={autoRefreshSeconds} />
        {loadedAt ? <span className="usage-loaded">{t('usageLoadedAt', { time: new Intl.DateTimeFormat(locale, { timeStyle: 'medium' }).format(loadedAt) })}</span> : null}
      </div>
      <div className="usage-filters">
        <label>{t('usageGroupByLabel')}<select onChange={(event) => setGroupBy(event.target.value)} value={groupBy}>{groupings.map((item) => <option key={item} value={item}>{t(`usageGroupBy${item}`)}</option>)}</select></label>
        <label>{t('usageCountLabel')}<select onChange={(event) => setGroupMetric(event.target.value)} value={groupMetric}><option value="Requests">{t('usageSeriesRequests')}</option><option value="Tokens">{t('usageSeriesTokens')}</option></select></label>
        <label>{t('model')}<input onChange={(event) => setModel(event.target.value)} placeholder={t('usageAny')} value={model} /></label>
        <label>{t('provider')}<input onChange={(event) => setProvider(event.target.value)} placeholder={t('usageAny')} value={provider} /></label>
        {api.showPrincipalFilters ? <label>{t('usageClient')}<input onChange={(event) => setClientId(event.target.value)} placeholder={t('usageAny')} value={clientId} /></label> : null}
        {api.showPrincipalFilters && api.canReviewTenant ? <label>{t('usageUser')}<input onChange={(event) => setUserId(event.target.value)} placeholder={t('usageAny')} value={userId} /></label> : null}
      </div>
      {error ? <div className="settings-restart-banner error-state"><CircleAlert size={18} /><span>{error}</span><button className="action-button secondary" onClick={load} type="button">{t('usageRetry')}</button></div> : null}
      {analytics?.truncated ? <div className="settings-restart-banner"><CircleAlert size={18} /><span>{analytics.truncatedReason}</span></div> : null}
      {loading && !analytics ? <div className="usage-empty">{t('usageLoading')}</div> : null}
      {analytics ? (
        <>
          <div className="metric-grid kpi-grid usage-kpis">
            <Kpi icon={<Activity size={18} />} label={t(dataset === 'Chat' ? 'chatTurns' : 'usageSeriesRequests')} subtext={t('usageKpiFailed', { count: count(totals.failed) })} tone={totals.failed ? 'danger' : 'neutral'} value={count(totals.requests)} />
            <Kpi icon={<CircleAlert size={18} />} label={t('usageKpiErrorRate')} subtext={totals.fallbacks != null ? t('usageKpiFallbacks', { count: count(totals.fallbacks) }) : ''} value={totals.errorRate != null ? new Intl.NumberFormat(locale, { style: 'percent', maximumFractionDigits: 1 }).format(totals.errorRate) : '-'} />
            <Kpi icon={<Timer size={18} />} label={t('usageKpiMedian')} subtext={t('usageKpiP95', { value: ms(totals.latency?.p95) })} value={ms(totals.latency?.p50)} />
            <Kpi icon={<Zap size={18} />} label={t('usageKpiFirstToken')} subtext={t('usageSeriesMedian')} value={ms(totals.timeToFirstToken?.p50)} />
            <Kpi icon={<MessageSquare size={18} />} label={t('usageSeriesTokens')} subtext={t('usageKpiTokens', { prompt: count(totals.promptTokens), completion: count(totals.completionTokens) })} value={count((totals.promptTokens || 0) + (totals.completionTokens || 0))} />
            <Kpi icon={<Gauge size={18} />} label={t('usageChartThroughput')} subtext={t('usageSeriesMedian')} value={formatChartValue(totals.throughput?.p50, ' tok/s', locale)} />
          </div>
          {noTraffic ? <div className="usage-empty usage-empty-page">{t(filtered ? 'usageEmptyFiltered' : 'usageEmptyRange')}</div> : null}
          <div className="usage-grid">
            {charts.map((chart) => <ChartCard chart={chart} key={chart.id} onDrill={onDrill ? drill : undefined} />)}
            {api.getConnectedHistory ? <ConnectedClientsCard load={api.getConnectedHistory} range={range} refreshSignal={`${refreshSignal}-${autoRefreshSignal}`} /> : null}
          </div>
          {analytics.groupTotals?.length ? (
            <section className="workspace-card usage-groups">
              <div className="workspace-card-header"><div><h2>{t('usageByGroupTitle', { group: t(`usageGroupBy${analytics.groupBy}`).toLowerCase() })}</h2></div></div>
              <div className="table-scroll"><table className="data-table">
                <thead><tr><th>{t(`usageGroupBy${analytics.groupBy}`)}</th><th>{t('usageSeriesRequests')}</th><th>{t('usageSeriesFailed')}</th><th>{t('usageSeriesPrompt')}</th><th>{t('usageSeriesCompletion')}</th><th>{t('usageSeriesMedian')}</th><th>{t('usageSeriesP95')}</th></tr></thead>
                <tbody>{analytics.groupTotals.map((item) => (
                  <tr key={item.key}>
                    <td>{groupKeyLabel(item.key, t)}</td>
                    <td>{count(item.requests)}</td>
                    <td>{count(item.failed)}</td>
                    <td>{count(item.promptTokens)}</td>
                    <td>{count(item.completionTokens)}</td>
                    <td>{ms(item.latency?.p50)}</td>
                    <td>{ms(item.latency?.p95)}</td>
                  </tr>
                ))}</tbody>
              </table></div>
            </section>
          ) : null}
        </>
      ) : null}
    </div>
  );
}

function Kpi({ icon, label, subtext, tone = 'neutral', value }) {
  return (
    <div className={`metric-card ${tone}`}>
      <div className="metric-icon">{icon}</div>
      <div className="metric-copy">
        <span>{label}</span>
        <strong>{value}</strong>
        {subtext ? <small>{subtext}</small> : null}
      </div>
    </div>
  );
}

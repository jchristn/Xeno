import { useEffect, useState } from 'react';
import { Activity, CircleAlert, Gauge, MessageSquare, RefreshCw, ThumbsDown, Timer, Trash2, Zap } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { DataTable, KpiGrid, Metric, Panel, TimeRangeButtons } from '../LocalDashboardApp.jsx';
import { chatAnalytics, deleteHistory, getTurn, queryHistory, rangeWindow } from './chatApi.js';
import { RouteChip, seconds } from '../../chat/ChatPage.jsx';
import ChatActivityChart from '../../chat/ChatActivityChart.jsx';
import TimingBars, { chatTurnStages } from '../../charts/TimingBars.jsx';

const statusOptions = ['', 'Success', 'Error', 'Cancelled'];
const routeOptions = ['', 'local', 'network'];
const ratingOptions = ['', '1', '-1', '0'];
const surfaceOptions = ['', 'Web', 'Desktop', 'Terminal', 'Cli', 'Mcp'];

export default function ChatHistoryPage({ drill, openConversation, setModal }) {
  const { t } = useTranslation();
  const [range, setRange] = useState('lastDay');
  const [filters, setFilters] = useState({ model: '', route: '', status: '', rating: '', surface: '' });
  const [analytics, setAnalytics] = useState(null);
  const [turns, setTurns] = useState([]);
  const [total, setTotal] = useState(0);
  const [error, setError] = useState('');

  useEffect(() => {
    load();
  }, [range, filters, drill]);

  async function load() {
    try {
      setError('');
      const window = rangeWindow(range);
      const [analyticsResponse, page] = await Promise.all([
        chatAnalytics(window.fromUtc, window.toUtc, window.bucketSeconds),
        queryHistory({ ...filters, model: drill?.model || filters.model, fromUtc: drill?.fromUtc || window.fromUtc, toUtc: drill?.toUtc || window.toUtc, page: 1, pageSize: 500 })
      ]);
      setAnalytics(analyticsResponse);
      setTurns(Array.isArray(page.items) ? page.items : []);
      setTotal(page.total || 0);
    } catch (err) {
      setError(err.message);
    }
  }

  function setFilter(key, value) {
    setFilters({ ...filters, [key]: value });
  }

  async function openTurn(row) {
    try {
      const turn = await getTurn(row.id);
      setModal({
        title: t('chatTurnDetails'),
        json: turn,
        size: 'large',
        body: <TurnDetails onOpenConversation={() => { setModal(null); openConversation(turn.conversationId); }} turn={turn} />
      });
    } catch (err) {
      setError(err.message);
    }
  }

  function clearMatching() {
    const window = rangeWindow(range);
    setModal({
      title: t('chatClearHistory'),
      body: <p>{t('chatClearHistoryMessage', { count: total })}</p>,
      actions: <button className="primary-button chat-danger" onClick={async () => {
        await deleteHistory({ ...filters, fromUtc: window.fromUtc, toUtc: window.toUtc });
        setModal(null);
        load();
      }} title={t('delete')} type="button">{t('delete')}</button>,
      size: 'small'
    });
  }

  const modelOptions = ['', ...(analytics?.models || []).map((item) => item.model)];
  if (filters.model && !modelOptions.includes(filters.model)) modelOptions.push(filters.model);

  return (
    <>
      {error ? <div className="settings-restart-banner error-state"><CircleAlert size={18} /><span>{error}</span></div> : null}
      <div className="chat-toolbar">
        <TimeRangeButtons range={range} setRange={setRange} />
        <FilterSelect label={t('model')} onChange={(value) => setFilter('model', value)} options={modelOptions.map((value) => ({ value, label: value || t('chatAllModels') }))} value={filters.model} />
        <FilterSelect label={t('route')} onChange={(value) => setFilter('route', value)} options={routeOptions.map((value) => ({ value, label: value ? t(value === 'local' ? 'chatRouteLocal' : 'chatRouteNetwork') : t('chatAllRoutes') }))} value={filters.route} />
        <FilterSelect label={t('status')} onChange={(value) => setFilter('status', value)} options={statusOptions.map((value) => ({ value, label: value ? t(`chatStatus${value}`) : t('chatAllStatuses') }))} value={filters.status} />
        <FilterSelect label={t('chatRating')} onChange={(value) => setFilter('rating', value)} options={ratingOptions.map((value) => ({ value, label: ratingLabel(value, t) }))} value={filters.rating} />
        <FilterSelect label={t('chatSurface')} onChange={(value) => setFilter('surface', value)} options={surfaceOptions.map((value) => ({ value, label: value ? t(`chatSurface${value}`) : t('chatAllSurfaces') }))} value={filters.surface} />
        <button className="icon-button" onClick={load} title={t('tooltipRefresh')} type="button"><RefreshCw size={16} /></button>
      </div>
      <KpiGrid>
        <Metric icon={<MessageSquare size={18} />} label={t('chatTurns')} subtext={t('chatLocalNetworkSplit', { local: analytics?.local ?? 0, network: analytics?.network ?? 0 })} value={(analytics?.turns ?? 0).toLocaleString()} />
        <Metric icon={<Timer size={18} />} label={t('chatMedianTime')} subtext={analytics?.p95Ms != null ? t('chatP95Value', { value: seconds(analytics.p95Ms) }) : t('chatNoData')} value={analytics?.p50Ms != null ? seconds(analytics.p50Ms) : '-'} />
        <Metric icon={<Zap size={18} />} label={t('chatTimeToFirstToken')} subtext={t('chatMedian')} value={analytics?.p50TimeToFirstTokenMs != null ? seconds(analytics.p50TimeToFirstTokenMs) : '-'} />
        <Metric icon={<ThumbsDown size={18} />} label={t('chatNegativeRate')} subtext={t('chatRatingsSplit', { up: analytics?.thumbsUp ?? 0, down: analytics?.thumbsDown ?? 0 })} tone={analytics?.negativeRate > 0.25 ? 'warning' : 'neutral'} value={analytics?.negativeRate != null ? `${Math.round(analytics.negativeRate * 100)}%` : '-'} />
      </KpiGrid>
      <KpiGrid>
        <Metric icon={<CircleAlert size={18} />} label={t('errors')} subtext={t('chatErrorRateValue', { value: Math.round((analytics?.errorRate ?? 0) * 100) })} tone={analytics?.errors ? 'danger' : 'neutral'} value={(analytics?.errors ?? 0).toLocaleString()} />
        <Metric icon={<Activity size={18} />} label={t('networkFallbacks')} subtext={t('networkFallbacksHelp')} tone={analytics?.fallbacks ? 'warning' : 'neutral'} value={(analytics?.fallbacks ?? 0).toLocaleString()} />
        <Metric icon={<Gauge size={18} />} label={t('chatTokens')} subtext={t('chatPromptTokensValue', { count: analytics?.promptTokens ?? 0 })} value={(analytics?.completionTokens ?? 0).toLocaleString()} />
        <Metric icon={<Activity size={18} />} label={t('chatToolCalls')} subtext={t('chatToolCallsHelp')} value={(analytics?.toolCalls ?? 0).toLocaleString()} />
      </KpiGrid>
      <Panel title={t('chatActivity')}>
        <ChatActivityChart buckets={analytics?.buckets || []} />
      </Panel>
      {analytics?.models?.length ? (
        <DataTable
          columns={['chatColModel', 'chatColTurns', 'chatColErrors', 'chatColMedianTime', 'chatColThumbsUp', 'chatColThumbsDown', 'chatColTokens']}
          rows={analytics.models.map((item) => ({ id: `model-${item.model}`, chatColModel: item.model, chatColTurns: item.turns, chatColErrors: item.errors, chatColMedianTime: item.p50Ms != null ? seconds(item.p50Ms) : '-', chatColThumbsUp: item.thumbsUp, chatColThumbsDown: item.thumbsDown, chatColTokens: item.completionTokens }))}
          title={t('chatByModel')}
        />
      ) : null}
      <DataTable
        actions={(row) => [{ icon: <MessageSquare size={14} />, key: 'open', label: t('chatOpenConversation'), onSelect: () => openConversation(row.conversationId), tooltip: t('chatOpenConversation') }]}
        columns={['chatColStarted', 'chatColConversation', 'chatColModel', 'chatColWent', 'chatColOutcome', 'chatColTime', 'chatColRating', 'chatColSurface']}
        headerActions={<button className="action-button" disabled={total === 0} onClick={clearMatching} title={t('chatClearHistory')} type="button"><Trash2 size={16} />{t('chatClearHistory')}</button>}
        onRefresh={load}
        onRowClick={openTurn}
        rowActionLabel={t('details')}
        rows={turns.map((item) => ({
          ...item,
          chatColStarted: new Date(item.createdUtc).toLocaleString([], { month: 'numeric', day: 'numeric', hour: '2-digit', minute: '2-digit' }),
          chatColConversation: item.conversationTitle || t('chatDeletedConversation'),
          chatColModel: item.model,
          chatColWent: routeLabel(item.route, t) + (item.fallback ? ` (${t('fallback')})` : ''),
          chatColOutcome: t(`chatStatus${item.status}`, item.status),
          chatColTime: seconds(item.totalMs),
          chatColFirstToken: item.timeToFirstTokenMs != null ? seconds(item.timeToFirstTokenMs) : '-',
          chatColTokens: item.completionTokens ?? '-',
          chatColTools: item.toolCallCount || 0,
          chatColRating: item.feedbackRating === 1 ? t('chatThumbsUp') : item.feedbackRating === -1 ? t('chatThumbsDown') : '',
          chatColSurface: t(`chatSurface${item.surface}`, item.surface)
        }))}
        title={t('chatTurnsTitle', { count: total })}
      />
    </>
  );
}

function FilterSelect({ label, onChange, options, value }) {
  return (
    <label className="chat-filter" title={label}>
      <span>{label}</span>
      <select aria-label={label} onChange={(event) => onChange(event.target.value)} title={label} value={value}>
        {options.map((option) => <option key={option.value || 'all'} value={option.value}>{option.label}</option>)}
      </select>
    </label>
  );
}

function TurnDetails({ onOpenConversation, turn }) {
  const { t } = useTranslation();
  const rows = [
    [t('chatConversation'), turn.conversationTitle || t('chatDeletedConversation')],
    [t('model'), turn.model],
    [t('route'), <RouteChip fallback={turn.fallback} key="route" route={turn.route} />],
    [t('status'), t(`chatStatus${turn.status}`, turn.status)],
    [t('chatErrorLabel'), turn.error || '-'],
    [t('chatTotalTime'), seconds(turn.totalMs)],
    [t('chatTimeToFirstToken'), turn.timeToFirstTokenMs != null ? seconds(turn.timeToFirstTokenMs) : '-'],
    [t('chatTokens'), `${turn.promptTokens ?? '-'} / ${turn.completionTokens ?? '-'}`],
    [t('chatModelCalls'), turn.modelCalls],
    [t('chatToolCalls'), `${turn.toolCallCount} (${seconds(turn.toolMs)})`],
    [t('chatRating'), ratingLabel(String(turn.feedbackRating ?? '0'), t)],
    [t('chatSurface'), t(`chatSurface${turn.surface}`, turn.surface)],
    [t('chatStarted'), new Date(turn.createdUtc).toLocaleString()]
  ];
  return (
    <div className="details-stack">
      <table className="data-table compact-record">
        <tbody>{rows.map(([label, value]) => <tr key={label}><th>{label}</th><td>{value}</td></tr>)}</tbody>
      </table>
      <h3>{t('usageChartRequestTiming')}</h3>
      <TimingBars stages={chatTurnStages(turn)} />
      {turn.toolCalls?.length ? (
        <div className="chat-turn-tools">
          <h3>{t('chatToolCalls')}</h3>
          {turn.toolCalls.map((call) => (
            <details key={call.id}>
              <summary><code>{call.toolName}</code> · {t(`chatApproval${call.approval}`, call.approval)} · {call.success ? t('chatToolDone') : t('chatToolFailed')} · {seconds(call.durationMs)}</summary>
              <pre>{call.arguments}</pre>
              {call.result ? <pre>{call.result}</pre> : null}
              {call.error ? <pre>{call.error}</pre> : null}
            </details>
          ))}
        </div>
      ) : null}
      {turn.conversationTitle ? <div className="button-row"><button className="primary-button" onClick={onOpenConversation} title={t('chatOpenConversation')} type="button"><MessageSquare size={15} />{t('chatOpenConversation')}</button></div> : null}
    </div>
  );
}

export function ratingLabel(value, t) {
  if (value === '1') return t('chatThumbsUp');
  if (value === '-1') return t('chatThumbsDown');
  if (value === '0') return t('chatNotRated');
  return t('chatAllRatings');
}

export function routeLabel(route, t) {
  if (!route) return '-';
  if (route.startsWith('local:')) return t('chatRouteLocalProvider', { provider: route.slice(6) });
  if (route === 'network') return t('chatRouteNetwork');
  if (route === 'unavailable') return t('chatRouteUnavailable');
  return route;
}

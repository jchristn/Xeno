import { useEffect, useMemo, useState } from 'react';
import { CircleAlert, MessageSquare, RefreshCw, ThumbsDown, ThumbsUp, Timer } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import Modal from '../components/Modal.jsx';
import EmptyState from '../components/EmptyState.jsx';
import ChatActivityChart from './ChatActivityChart.jsx';
import ChatPage, { RouteChip, seconds } from './ChatPage.jsx';
import { ChatApiContext } from './ChatApiContext.js';
import { createServerChatApi } from './serverChatApi.js';
import { useSearchParams } from 'react-router-dom';
import DrillBanner from '../usage/DrillBanner.jsx';
import TimingBars, { chatTurnStages } from '../charts/TimingBars.jsx';

import { chatRanges } from './chatRanges.js';

// Chat in the server dashboard: the shared chat page talking to the Xeno server, which answers with network models
// and bills the signed-in user like any network request.
export function ServerChatView({ api }) {
  const chatApi = useMemo(() => createServerChatApi(api), [api]);
  const [conversationId, setConversationId] = useState(null);
  const [modal, setModal] = useState(null);
  return (
    <ChatApiContext.Provider value={chatApi}>
      <div className="workspace-chat-host">
        <ChatPage conversationId={conversationId} setConversationId={setConversationId} setModal={setModal} />
      </div>
      <Modal actions={modal?.actions} onClose={() => setModal(null)} open={!!modal} size={modal?.size || 'large'} title={modal?.title || ''}>
        {modal?.body}
      </Modal>
    </ChatApiContext.Provider>
  );
}

// Chat history and ratings in the server dashboard. Tenant admins can switch to every user in the tenant.
export function ServerChatReviewView({ api, canReviewTenant }) {
  const { t } = useTranslation();
  const [searchParams, setSearchParams] = useSearchParams();
  const drill = searchParams.get('fromUtc') && searchParams.get('toUtc') ? { fromUtc: searchParams.get('fromUtc'), toUtc: searchParams.get('toUtc'), model: searchParams.get('model') } : null;
  const [selectedTurn, setSelectedTurn] = useState(null);
  const chatApi = useMemo(() => createServerChatApi(api), [api]);
  const [range, setRange] = useState('lastWeek');
  const [scope, setScope] = useState('mine');
  const [rating, setRating] = useState('');
  const [analytics, setAnalytics] = useState(null);
  const [turns, setTurns] = useState([]);
  const [feedback, setFeedback] = useState([]);
  const [error, setError] = useState('');

  useEffect(() => {
    load();
  }, [range, scope, rating, searchParams]);

  async function load() {
    try {
      setError('');
      const { hours, bucketSeconds } = chatRanges[range] || chatRanges.lastDay;
      const toUtc = new Date().toISOString();
      const fromUtc = new Date(Date.now() - hours * 3600 * 1000).toISOString();
      const shared = { fromUtc, toUtc, scope: scope === 'tenant' ? 'tenant' : '' };
      const [analyticsResponse, turnPage, feedbackPage] = await Promise.all([
        chatApi.chatAnalytics({ ...shared, bucketSeconds }),
        chatApi.queryHistory({ ...shared, ...(drill ? { fromUtc: drill.fromUtc, toUtc: drill.toUtc, model: drill.model || '' } : {}), rating, pageSize: 200 }),
        chatApi.queryFeedback({ ...shared, rating, pageSize: 200 })
      ]);
      setAnalytics(analyticsResponse);
      setTurns(turnPage.items || []);
      setFeedback(feedbackPage.items || []);
    } catch (err) {
      setError(err.message);
    }
  }

  const negative = analytics?.negativeRate != null ? `${Math.round(analytics.negativeRate * 100)}%` : '-';
  return (
    <div className="chat-review">
      <DrillBanner drill={drill} onClear={() => setSearchParams({})} />
      <Modal onClose={() => setSelectedTurn(null)} open={!!selectedTurn} size="large" title={t('chatTurnDetails')}>
        {selectedTurn ? <div className="details-stack"><h3>{t('usageChartRequestTiming')}</h3><TimingBars stages={chatTurnStages(selectedTurn)} /></div> : null}
      </Modal>
      {error ? <div className="settings-restart-banner error-state"><CircleAlert size={18} /><span>{error}</span></div> : null}
      <div className="chat-toolbar">
        <div className="time-range-button-row">
          {Object.keys(chatRanges).map((key) => <button className={range === key ? 'active' : ''} key={key} onClick={() => setRange(key)} title={t(key)} type="button">{t(key)}</button>)}
        </div>
        {canReviewTenant ? (
          <div className="time-range-button-row" role="group" aria-label={t('chatScope')}>
            <button className={scope === 'mine' ? 'active' : ''} onClick={() => setScope('mine')} title={t('chatScopeMine')} type="button">{t('chatScopeMine')}</button>
            <button className={scope === 'tenant' ? 'active' : ''} onClick={() => setScope('tenant')} title={t('chatScopeTenant')} type="button">{t('chatScopeTenant')}</button>
          </div>
        ) : null}
        <div className="time-range-button-row" role="group" aria-label={t('chatRating')}>
          {[['', t('chatAllRatings')], ['1', t('chatThumbsUp')], ['-1', t('chatThumbsDown')]].map(([value, label]) => (
            <button className={rating === value ? 'active' : ''} key={value || 'all'} onClick={() => setRating(value)} title={label} type="button">{label}</button>
          ))}
        </div>
        <button className="icon-button" onClick={load} title={t('tooltipRefresh')} type="button"><RefreshCw size={16} /></button>
      </div>
      <div className="kpi-strip"><div className="metric-grid kpi-grid">
        <ReviewMetric icon={<MessageSquare size={18} />} label={t('chatTurns')} value={(analytics?.turns ?? 0).toLocaleString()} subtext={t('chatErrorRateValue', { value: Math.round((analytics?.errorRate ?? 0) * 100) })} />
        <ReviewMetric icon={<Timer size={18} />} label={t('chatMedianTime')} value={analytics?.p50Ms != null ? seconds(analytics.p50Ms) : '-'} subtext={analytics?.p95Ms != null ? t('chatP95Value', { value: seconds(analytics.p95Ms) }) : t('chatNoData')} />
        <ReviewMetric icon={<ThumbsUp size={18} />} label={t('chatThumbsUp')} tone="success" value={(analytics?.thumbsUp ?? 0).toLocaleString()} />
        <ReviewMetric icon={<ThumbsDown size={18} />} label={t('chatNegativeRate')} tone={analytics?.negativeRate > 0.25 ? 'warning' : 'neutral'} value={negative} subtext={t('chatRatingsSplit', { up: analytics?.thumbsUp ?? 0, down: analytics?.thumbsDown ?? 0 })} />
      </div></div>
      <section className="workspace-card chat-review-card">
        <div className="workspace-card-header"><div><h2>{t('chatActivity')}</h2></div></div>
        <ChatActivityChart showLocal={false} buckets={(analytics?.buckets || []).map((bucket) => ({ ...bucket, local: bucket.local || 0, network: bucket.network || 0, errors: bucket.errors || 0 }))} />
      </section>
      {analytics?.models?.length ? (
        <section className="workspace-card chat-review-card">
          <div className="workspace-card-header"><div><h2>{t('chatByModel')}</h2></div></div>
          <div className="table-scroll"><table className="data-table">
            <thead><tr><th>{t('chatColModel')}</th><th>{t('chatColTurns')}</th><th>{t('chatColErrors')}</th><th>{t('chatColMedianTime')}</th><th>{t('chatColThumbsUp')}</th><th>{t('chatColThumbsDown')}</th><th>{t('chatColTokens')}</th></tr></thead>
            <tbody>{analytics.models.map((item) => (
              <tr key={item.model}>
                <td>{item.model}</td>
                <td>{item.turns}</td>
                <td>{item.errors}</td>
                <td>{item.p50Ms != null ? seconds(item.p50Ms) : '-'}</td>
                <td>{item.thumbsUp}</td>
                <td>{item.thumbsDown}</td>
                <td>{(item.completionTokens ?? 0).toLocaleString()}</td>
              </tr>
            ))}</tbody>
          </table></div>
        </section>
      ) : null}
      <section className="workspace-card chat-review-card">
        <div className="workspace-card-header"><div><h2>{t('chatFeedbackTitle', { count: feedback.length })}</h2></div></div>
        {feedback.length === 0 ? <EmptyState message={t('chatNoFeedback')} /> : (
          <div className="table-scroll"><table className="data-table">
            <thead><tr><th>{t('chatColRated')}</th><th>{t('chatColRating')}</th><th>{t('chatColComment')}</th><th>{t('chatColModel')}</th><th>{t('chatColConversation')}</th><th>{t('chatColExcerpt')}</th></tr></thead>
            <tbody>{feedback.map((item) => (
              <tr key={item.id}>
                <td>{new Date(item.updatedUtc).toLocaleString()}</td>
                <td>{item.rating > 0 ? t('chatThumbsUp') : t('chatThumbsDown')}</td>
                <td className="chat-prewrap">{item.comment || ''}</td>
                <td>{item.model || '-'}</td>
                <td>{item.conversationTitle || t('chatDeletedConversation')}</td>
                <td>{(item.excerpt || '').slice(0, 120)}</td>
              </tr>
            ))}</tbody>
          </table></div>
        )}
      </section>
      <section className="workspace-card chat-review-card">
        <div className="workspace-card-header"><div><h2>{t('chatTurnsTitle', { count: turns.length })}</h2></div></div>
        {turns.length === 0 ? <EmptyState message={t('chatNoActivity')} /> : (
          <div className="table-scroll"><table className="data-table">
            <thead><tr><th>{t('chatColStarted')}</th><th>{t('chatColConversation')}</th><th>{t('chatColModel')}</th><th>{t('chatColWent')}</th><th>{t('chatColOutcome')}</th><th>{t('chatColTime')}</th><th>{t('chatColTokens')}</th><th>{t('chatColRating')}</th></tr></thead>
            <tbody>{turns.map((item) => (
              <tr className="clickable-row" key={item.id} onClick={() => setSelectedTurn(item)} title={t('details')}>
                <td>{new Date(item.createdUtc).toLocaleString()}</td>
                <td>{item.conversationTitle || t('chatDeletedConversation')}</td>
                <td>{item.model}</td>
                <td><RouteChip fallback={item.fallback} route={item.route} />{item.provider ? <span className="chat-muted"> {item.provider}</span> : null}</td>
                <td>{t(`chatStatus${item.status}`, item.status)}{item.error ? <div className="chat-muted">{item.error}</div> : null}</td>
                <td>{seconds(item.totalMs)}</td>
                <td>{item.completionTokens ?? '-'}</td>
                <td>{item.feedbackRating === 1 ? t('chatThumbsUp') : item.feedbackRating === -1 ? t('chatThumbsDown') : ''}</td>
              </tr>
            ))}</tbody>
          </table></div>
        )}
      </section>
    </div>
  );
}

function ReviewMetric({ icon, label, subtext, tone = 'neutral', value }) {
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

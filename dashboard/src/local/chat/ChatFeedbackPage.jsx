import { useEffect, useState } from 'react';
import { CircleAlert, MessageSquare, MessageSquareText, RefreshCw, ThumbsDown, ThumbsUp } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { DataTable, KpiGrid, Metric, TimeRangeButtons } from '../LocalDashboardApp.jsx';
import { chatAnalytics, getConversation, queryFeedback, rangeWindow } from './chatApi.js';
import { Markdown, RouteChip, seconds } from '../../chat/ChatPage.jsx';
import { routeLabel } from './ChatHistoryPage.jsx';

export default function ChatFeedbackPage({ openConversation, setModal }) {
  const { t } = useTranslation();
  const [range, setRange] = useState('lastWeek');
  const [rating, setRating] = useState('');
  const [commentsOnly, setCommentsOnly] = useState(false);
  const [items, setItems] = useState([]);
  const [total, setTotal] = useState(0);
  const [analytics, setAnalytics] = useState(null);
  const [error, setError] = useState('');

  useEffect(() => {
    load();
  }, [range, rating, commentsOnly]);

  async function load() {
    try {
      setError('');
      const window = rangeWindow(range);
      const [page, analyticsResponse] = await Promise.all([
        queryFeedback({ rating, hasComment: commentsOnly ? 'true' : '', fromUtc: window.fromUtc, toUtc: window.toUtc, page: 1, pageSize: 500 }),
        chatAnalytics(window.fromUtc, window.toUtc, window.bucketSeconds)
      ]);
      setItems(Array.isArray(page.items) ? page.items : []);
      setTotal(page.total || 0);
      setAnalytics(analyticsResponse);
    } catch (err) {
      setError(err.message);
    }
  }

  function open(row) {
    setModal({
      title: row.ratingValue > 0 ? t('chatGoodAnswer') : t('chatBadAnswer'),
      json: row,
      size: 'large',
      body: (
        <div className="details-stack">
          <table className="data-table compact-record">
            <tbody>
              <tr><th>{t('chatRating')}</th><td>{row.ratingValue > 0 ? t('chatThumbsUp') : t('chatThumbsDown')}</td></tr>
              <tr><th>{t('chatFeedbackComment')}</th><td className="chat-prewrap">{row.comment || '-'}</td></tr>
              <tr><th>{t('chatConversation')}</th><td>{row.conversationTitle || t('chatDeletedConversation')}</td></tr>
              <tr><th>{t('model')}</th><td>{row.model || '-'}</td></tr>
              <tr><th>{t('route')}</th><td><RouteChip route={row.route} /></td></tr>
              <tr><th>{t('chatTotalTime')}</th><td>{row.totalMs != null ? seconds(row.totalMs) : '-'}</td></tr>
              <tr><th>{t('chatTokens')}</th><td>{row.completionTokens ?? '-'}</td></tr>
              <tr><th>{t('chatRated')}</th><td>{new Date(row.updatedUtc).toLocaleString()}</td></tr>
            </tbody>
          </table>
          {row.excerpt ? <div><h3>{t('chatAnswerExcerpt')}</h3><RatedAnswer row={row} /></div> : null}
          {row.conversationTitle ? <div className="button-row"><button className="primary-button" onClick={() => { setModal(null); openConversation(row.conversationId); }} title={t('chatOpenConversation')} type="button"><MessageSquare size={15} />{t('chatOpenConversation')}</button></div> : null}
        </div>
      )
    });
  }

  const ratings = (analytics?.thumbsUp ?? 0) + (analytics?.thumbsDown ?? 0);
  return (
    <>
      {error ? <div className="settings-restart-banner error-state"><CircleAlert size={18} /><span>{error}</span></div> : null}
      <div className="chat-toolbar">
        <TimeRangeButtons range={range} setRange={setRange} />
        <div className="time-range-button-row" role="group" aria-label={t('chatRating')}>
          {[['', t('chatAllRatings')], ['1', t('chatThumbsUp')], ['-1', t('chatThumbsDown')]].map(([value, label]) => (
            <button className={rating === value ? 'active' : ''} key={value || 'all'} onClick={() => setRating(value)} title={label} type="button">{label}</button>
          ))}
        </div>
        <label className="toggle-field" title={t('chatCommentsOnly')}><input checked={commentsOnly} onChange={(event) => setCommentsOnly(event.target.checked)} title={t('chatCommentsOnly')} type="checkbox" />{t('chatCommentsOnly')}</label>
        <button className="icon-button" onClick={load} title={t('tooltipRefresh')} type="button"><RefreshCw size={16} /></button>
      </div>
      <KpiGrid>
        <Metric icon={<ThumbsUp size={18} />} label={t('chatThumbsUp')} tone="success" value={(analytics?.thumbsUp ?? 0).toLocaleString()} />
        <Metric icon={<ThumbsDown size={18} />} label={t('chatThumbsDown')} tone={analytics?.thumbsDown ? 'danger' : 'neutral'} value={(analytics?.thumbsDown ?? 0).toLocaleString()} />
        <Metric icon={<CircleAlert size={18} />} label={t('chatNegativeRate')} subtext={t('chatRatedAnswers', { count: ratings })} tone={analytics?.negativeRate > 0.25 ? 'warning' : 'neutral'} value={analytics?.negativeRate != null ? `${Math.round(analytics.negativeRate * 100)}%` : '-'} />
        <Metric icon={<MessageSquareText size={18} />} label={t('chatRatedShare')} subtext={t('chatRatedShareHelp')} value={analytics?.turns ? `${Math.round((ratings / analytics.turns) * 100)}%` : '-'} />
      </KpiGrid>
      <DataTable
        actions={(row) => [{ icon: <MessageSquare size={14} />, key: 'open', label: t('chatOpenConversation'), onSelect: () => openConversation(row.conversationId), tooltip: t('chatOpenConversation') }]}
        columns={['chatColRated', 'chatColRating', 'chatColComment', 'chatColModel', 'chatColWent', 'chatColConversation', 'chatColExcerpt']}
        onRefresh={load}
        onRowClick={open}
        rowActionLabel={t('details')}
        rows={items.map((item) => ({
          ...item,
          chatColRated: new Date(item.updatedUtc).toLocaleString(),
          chatColRating: item.rating > 0 ? t('chatThumbsUp') : t('chatThumbsDown'),
          ratingValue: item.rating,
          chatColComment: item.comment || '',
          chatColModel: item.model || '-',
          chatColWent: routeLabel(item.route, t),
          chatColConversation: item.conversationTitle || t('chatDeletedConversation'),
          chatColExcerpt: truncate(item.excerpt, 120)
        }))}
        title={t('chatFeedbackTitle', { count: total })}
      />
    </>
  );
}

function truncate(value, length) {
  if (!value) return '';
  return value.length <= length ? value : `${value.slice(0, length - 1)}…`;
}

// The rated answer rendered as Markdown. The feedback row holds only the first 240 characters, so the whole answer is
// read from its conversation; a deleted conversation falls back to the excerpt.
function RatedAnswer({ row }) {
  const [text, setText] = useState(null);

  useEffect(() => {
    let active = true;
    setText(null);
    if (!row.conversationId || !row.conversationTitle) {
      setText(row.excerpt || '');
      return undefined;
    }
    getConversation(row.conversationId)
      .then((conversation) => {
        const message = (conversation?.messages || []).find((item) => item.id === row.messageId);
        if (active) setText(message?.content || row.excerpt || '');
      })
      .catch(() => {
        if (active) setText(row.excerpt || '');
      });
    return () => {
      active = false;
    };
  }, [row.conversationId, row.messageId]);

  return <div className="chat-excerpt">{text === null ? null : <Markdown text={text} />}</div>;
}

import { lazy, Suspense, useEffect, useMemo, useRef, useState } from 'react';
import { Bot, Brain, Check, FolderOpen, Info, ListChecks, Loader2, Radio, ChevronDown, ChevronRight, CircleAlert, MessageSquarePlus, Pencil, Pin, PinOff, Search, Send, Settings2, Square, ThumbsDown, ThumbsUp, Trash2, Wrench, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import ActionMenu from '../components/ActionMenu.jsx';
import CopyButton from '../components/CopyButton.jsx';
import EmptyState from '../components/EmptyState.jsx';
import { useChatApi } from './ChatApiContext.js';
import { pickWaitMessage } from './chatWaitMessages.js';

const MarkdownPreview = lazy(() => import('../components/MarkdownPreview.jsx'));
const lastModelKey = 'xeno.chat.lastModel';

export default function ChatPage({ conversationId, setConversationId, setModal }) {
  const { t } = useTranslation();
  const chat = useChatApi();
  const [conversations, setConversations] = useState([]);
  const [search, setSearch] = useState('');
  const [selecting, setSelecting] = useState(false);
  const [selected, setSelected] = useState(() => new Set());
  const [conversation, setConversation] = useState(null);
  const [catalog, setCatalog] = useState(null);
  const [input, setInput] = useState('');
  const [live, setLive] = useState(null);
  const skipLoadRef = useRef(null);
  // Wait quips picked this page session, so the last 20 are not repeated.
  const recentWaitMessages = useRef([]);
  // Tools for the next new chat: null follows the model's tool support, true or false is the user's choice.
  const [newChatTools, setNewChatTools] = useState(null);
  // Working directory for the next new chat; empty uses the client's default (the home folder).
  const [newChatDirectory, setNewChatDirectory] = useState('');
  const [error, setError] = useState('');
  const [selectedModel, setSelectedModel] = useState(() => readLastModel());
  const [readiness, setReadiness] = useState({});
  const [streaming, setStreaming] = useState(() => readStreaming());
  const readinessRef = useRef({});
  const inputRef = useRef(null);
  const listRef = useRef(null);
  const abortRef = useRef(null);
  const sending = !!live;

  const models = useMemo(() => chatModels(catalog), [catalog]);
  const fallbackModel = models.local[0]?.name || models.network[0]?.name || '';
  const activeModel = conversation?.model || (selectedModel && (models.all.has(selectedModel) || !catalog) ? selectedModel : '') || fallbackModel;
  const activeReadiness = readiness[activeModel];

  useEffect(() => {
    if (catalog && activeModel) prepare(activeModel);
  }, [catalog, activeModel]);

  // A model that cannot use tools never gets them, so a conversation with tools on is switched off rather than
  // telling the model about tools it will not see.
  const toolsUnsupported = !!chat.supportsTools && activeReadiness?.status === 'ready' && activeReadiness.route !== 'network' && activeReadiness.supportsTools === false;
  const toolsOn = conversation ? !!conversation.toolsEnabled : !toolsUnsupported && (newChatTools ?? !!activeReadiness?.supportsTools);
  useEffect(() => {
    if (!toolsUnsupported || sending || !conversation?.id || !conversation.toolsEnabled) return;
    const id = conversation.id;
    chat.updateConversation(id, { toolsEnabled: false })
      .then((updated) => setConversation((value) => (value && value.id === id ? { ...value, toolsEnabled: updated.toolsEnabled } : value)))
      .catch((err) => setError(err.message));
  }, [toolsUnsupported, sending, conversation?.id, conversation?.toolsEnabled]);

  useEffect(() => {
    const element = inputRef.current;
    if (!element) return;
    element.style.height = '';
    if (element.scrollHeight > element.clientHeight) element.style.height = `${Math.min(200, element.scrollHeight)}px`;
  }, [input]);

  useEffect(() => {
    chat.listChatModels().then(setCatalog).catch(() => setCatalog({ entries: [], networkMessage: '' }));
  }, []);

  useEffect(() => {
    loadList(search);
  }, [search]);

  useEffect(() => {
    if (!conversationId) {
      if (!sending) setConversation(null);
      return;
    }
    if (skipLoadRef.current === conversationId) {
      skipLoadRef.current = null;
      return;
    }
    loadConversation(conversationId);
  }, [conversationId]);

  useEffect(() => {
    const element = listRef.current;
    if (element) element.scrollTop = element.scrollHeight;
  }, [conversation?.messages?.length, live?.text, live?.thinking, live?.toolCalls?.length]);

  useEffect(() => () => abortRef.current?.abort(), []);

  async function loadList(text) {
    try {
      const items = await chat.listConversations(text);
      setConversations(Array.isArray(items) ? items : []);
    } catch (err) {
      setError(err.message);
    }
  }

  async function loadConversation(id) {
    try {
      setError('');
      setConversation(await chat.getConversation(id));
    } catch (err) {
      setError(err.message);
      setConversation(null);
    }
  }

  // Load the model where it runs and learn what it can do. Each model is prepared once per page; a failure can be retried.
  async function prepare(model) {
    const known = readinessRef.current[model];
    if (known && known.status !== 'error') return known.promise || known;
    const promise = (async () => {
      try {
        const result = await chat.prepareModel(model);
        const entry = { status: result.route !== 'unavailable' && (result.loaded || result.route === 'network') ? 'ready' : 'error', supportsTools: !!result.supportsTools, message: result.message || '', route: result.route };
        readinessRef.current = { ...readinessRef.current, [model]: entry };
        setReadiness(readinessRef.current);
        return entry;
      } catch (err) {
        const entry = { status: 'error', supportsTools: false, message: err.message };
        readinessRef.current = { ...readinessRef.current, [model]: entry };
        setReadiness(readinessRef.current);
        return entry;
      }
    })();
    readinessRef.current = { ...readinessRef.current, [model]: { status: 'loading', promise } };
    setReadiness(readinessRef.current);
    return promise;
  }

  function toolsDefault(model) {
    if (!chat.supportsTools) return false;
    if (newChatTools !== null) return newChatTools && readinessRef.current[model]?.supportsTools !== false;
    return !!readinessRef.current[model]?.supportsTools;
  }

  function toggleStreaming() {
    const next = !streaming;
    setStreaming(next);
    writeStreaming(next);
  }

  async function newChat() {
    try {
      const created = await chat.createConversation({ model: activeModel || null, surface: chat.surface || 'Web', toolsEnabled: toolsDefault(activeModel), workingDirectory: newChatDirectory || undefined });
      await loadList(search);
      setConversationId(created.id);
    } catch (err) {
      setError(err.message);
    }
  }

  async function chooseModel(model) {
    setSelectedModel(model);
    setNewChatTools(null);
    writeLastModel(model);
    const current = conversation;
    try {
      if (current) {
        const updated = await chat.updateConversation(current.id, { model });
        setConversation((value) => (value && value.id === current.id ? { ...value, model: updated.model } : value));
        loadList(search);
      }

      const ready = await prepare(model);
      // A conversation with no messages yet follows the new model's tool support.
      if (current && !(current.messages || []).length && chat.supportsTools) {
        const tools = !!ready?.supportsTools;
        const updated = await chat.updateConversation(current.id, { toolsEnabled: tools });
        setConversation((value) => (value && value.id === current.id ? { ...value, toolsEnabled: updated.toolsEnabled } : value));
      }
    } catch (err) {
      setError(err.message);
    }
  }

  async function toggleTools() {
    if (!conversation) {
      setNewChatTools(!toolsOn);
      return;
    }
    if (!conversation.id) return;
    try {
      const updated = await chat.updateConversation(conversation.id, { toolsEnabled: !conversation.toolsEnabled });
      setConversation({ ...conversation, toolsEnabled: updated.toolsEnabled });
    } catch (err) {
      setError(err.message);
    }
  }

  async function showTools() {
    try {
      const tools = await chat.listChatTools();
      setModal({
        title: t('chatToolsTitle'),
        body: (
          <div className="details-stack">
            <div className="empty-state">{t('chatToolsHelp')}</div>
            {groupTools(Array.isArray(tools) ? tools : []).map(([group, items]) => (
              <div key={group}>
                <h3 className="chat-tools-group">{group} <span className="chat-muted">({items.length})</span></h3>
                <table className="data-table compact-record">
                  <tbody>
                    {items.map((tool) => (
                      <tr key={tool.name}>
                        <th><code>{tool.name}</code><div className="chat-muted">{tool.mutating ? t('chatToolAsksFirst') : t('chatToolReadOnly')}</div></th>
                        <td>{tool.description}</td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            ))}
          </div>
        )
      });
    } catch (err) {
      setError(err.message);
    }
  }

  async function send(event) {
    event?.preventDefault();
    const content = input.trim();
    if (!content || sending) return;
    if (!activeModel) {
      setError(t('chatChooseModel'));
      return;
    }

    let target = conversation;
    // Show the prompt and the waiting answer right away, before a new conversation exists on the server.
    const pendingUser = { id: `pending-${Date.now()}`, role: 'User', content, createdUtc: new Date().toISOString() };
    setInput('');
    setLive({ turnId: null, text: '', thinking: '', toolCalls: [], error: '' });
    setConversation((current) => (current
      ? { ...current, messages: [...(current.messages || []), pendingUser] }
      : { id: null, model: activeModel, title: '', toolsEnabled: toolsDefault(activeModel), messages: [pendingUser] }));
    try {
      setError('');
      if (!target) {
        const created = await chat.createConversation({ model: activeModel, surface: chat.surface || 'Web', toolsEnabled: toolsDefault(activeModel), workingDirectory: newChatDirectory || undefined });
        target = { ...created, messages: [pendingUser] };
        // The page already holds this conversation, so selecting it must not reload it over the pending prompt.
        skipLoadRef.current = target.id;
        setConversation(target);
        setConversationId(target.id);
      }

      writeLastModel(activeModel);
      const controller = new AbortController();
      abortRef.current = controller;
      await chat.sendMessage(target.id, { content, model: activeModel, stream: streaming, surface: chat.surface || 'Web' }, (item) => applyEvent(item), controller.signal);
    } catch (err) {
      if (err.name !== 'AbortError') setError(err.message);
    } finally {
      abortRef.current = null;
      setLive(null);
      if (target) await loadConversation(target.id);
      loadList(search);
    }
  }

  function applyEvent(item) {
    setLive((current) => {
      if (!current) return current;
      const kind = String(item.kind || '').toLowerCase();
      if (kind === 'turn') return { ...current, turnId: item.turnId };
      if (kind === 'chunk') return { ...current, text: current.text + (item.text || '') };
      if (kind === 'thinking') return { ...current, thinking: current.thinking + (item.text || '') };
      if (kind === 'error') return { ...current, error: item.text || t('chatTurnFailed'), failure: item.failure || null };
      if (kind === 'toolcall' && item.toolCall) {
        const others = current.toolCalls.filter((call) => call.id !== item.toolCall.id);
        return { ...current, toolCalls: [...others, item.toolCall] };
      }
      return current;
    });
  }

  async function stop() {
    if (live?.turnId) {
      try {
        await chat.cancelTurn(live.turnId);
        return;
      } catch {
        // Fall through and drop the connection.
      }
    }
    abortRef.current?.abort();
  }

  async function answerTool(call, allow) {
    if (!live?.turnId) return;
    try {
      await chat.approveToolCall(live.turnId, call.id, allow);
    } catch (err) {
      setError(err.message);
    }
  }

  function rename(item) {
    setModal({
      title: t('chatRename'),
      body: (
        <form className="form-grid" id="chat-rename-form" onSubmit={async (event) => {
          event.preventDefault();
          const value = new FormData(event.currentTarget).get('chatTitle');
          await chat.updateConversation(item.id, { title: value });
          setModal(null);
          await loadList(search);
          if (conversation?.id === item.id) loadConversation(item.id);
        }}>
          <label className="form-field span-full">
            <span>{t('chatTitle')}</span>
            <input autoFocus defaultValue={item.title} maxLength={200} name="title" title={t('chatRename')} />
          </label>
        </form>
      ),
      actions: <button className="primary-button" form="chat-rename-form" title={t('save')} type="submit">{t('save')}</button>,
      size: 'small'
    });
  }

  function editWorkingDirectory() {
    const current = conversation?.id ? conversation.workingDirectory || '' : newChatDirectory;
    setModal({
      title: t('chatWorkingDirectory'),
      body: (
        <form className="form-grid" id="chat-directory-form" onSubmit={async (event) => {
          event.preventDefault();
          const value = String(new FormData(event.currentTarget).get('workingDirectory') || '').trim();
          try {
            if (conversation?.id) {
              const updated = await chat.updateConversation(conversation.id, { workingDirectory: value });
              setConversation((item) => (item && item.id === conversation.id ? { ...item, workingDirectory: updated.workingDirectory } : item));
            } else {
              setNewChatDirectory(value);
            }
            setModal(null);
          } catch (err) {
            setError(err.message);
            setModal(null);
          }
        }}>
          <div className="empty-state span-full">{t('chatWorkingDirectoryHelp')}</div>
          <label className="form-field span-full">
            <span>{t('chatWorkingDirectory')}</span>
            <input autoFocus defaultValue={current} name="workingDirectory" placeholder={t('chatWorkingDirectoryPlaceholder')} title={t('tooltipChatWorkingDirectory')} />
          </label>
        </form>
      ),
      actions: <button className="primary-button" form="chat-directory-form" title={t('save')} type="submit">{t('save')}</button>
    });
  }

  function editSystemPrompt() {
    if (!conversation?.id) return;
    setModal({
      title: t('chatSystemPrompt'),
      body: (
        <form className="form-grid" id="chat-system-form" onSubmit={async (event) => {
          event.preventDefault();
          const value = new FormData(event.currentTarget).get('systemPrompt');
          await chat.updateConversation(conversation.id, { systemPrompt: value || '' });
          setModal(null);
          loadConversation(conversation.id);
        }}>
          <div className="empty-state span-full">{t('chatSystemPromptHelp')}</div>
          <label className="form-field span-full">
            <span>{t('chatSystemPrompt')}</span>
            <textarea defaultValue={conversation.systemPrompt || ''} name="systemPrompt" rows={8} title={t('chatSystemPrompt')} />
          </label>
        </form>
      ),
      actions: <button className="primary-button" form="chat-system-form" title={t('save')} type="submit">{t('save')}</button>
    });
  }

  function remove(item) {
    setModal({
      title: t('chatDeleteTitle'),
      body: <p>{t('chatDeleteMessage', { title: item.title })}</p>,
      actions: <button className="primary-button chat-danger" onClick={async () => {
        await chat.deleteConversation(item.id);
        setModal(null);
        if (conversation?.id === item.id) setConversationId(null);
        loadList(search);
      }} title={t('delete')} type="button">{t('delete')}</button>,
      size: 'small'
    });
  }

  function toggleSelected(id) {
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }

  function stopSelecting() {
    setSelecting(false);
    setSelected(new Set());
  }

  function removeSelected() {
    const ids = conversations.filter((item) => selected.has(item.id)).map((item) => item.id);
    if (ids.length === 0) return;
    setModal({
      title: t('chatDeleteManyTitle'),
      body: <p>{t('chatDeleteManyMessage', { count: ids.length })}</p>,
      actions: <button className="primary-button chat-danger" onClick={async () => {
        for (const id of ids) {
          await chat.deleteConversation(id);
        }
        setModal(null);
        if (conversation && ids.includes(conversation.id)) setConversationId(null);
        stopSelecting();
        loadList(search);
      }} title={t('chatDeleteSelectedTooltip')} type="button">{t('chatDeleteSelected', { count: ids.length })}</button>,
      size: 'small'
    });
  }

  async function togglePin(item) {
    await chat.updateConversation(item.id, { pinned: !item.pinned });
    loadList(search);
  }

  function rate(message, rating) {
    setModal({
      title: rating > 0 ? t('chatGoodAnswer') : t('chatBadAnswer'),
      body: (
        <form className="form-grid" id="chat-feedback-form" onSubmit={async (event) => {
          event.preventDefault();
          const comment = new FormData(event.currentTarget).get('comment');
          await chat.setFeedback(message.id, rating, comment);
          setModal(null);
          loadConversation(message.conversationId);
        }}>
          <div className="empty-state span-full">{rating > 0 ? t('chatFeedbackUpHelp') : t('chatFeedbackDownHelp')}</div>
          <label className="form-field span-full">
            <span>{t('chatFeedbackComment')}</span>
            <textarea autoFocus defaultValue={message.feedback?.rating === rating ? message.feedback?.comment || '' : ''} maxLength={4000} name="comment" rows={5} title={t('chatFeedbackComment')} />
          </label>
        </form>
      ),
      actions: (
        <div className="button-row">
          {message.feedback ? (
            <button className="action-button" onClick={async () => {
              await chat.deleteFeedback(message.id);
              setModal(null);
              loadConversation(message.conversationId);
            }} title={t('chatRemoveRating')} type="button">{t('chatRemoveRating')}</button>
          ) : null}
          <button className="primary-button" form="chat-feedback-form" title={t('chatSendFeedback')} type="submit">{t('chatSendFeedback')}</button>
        </div>
      ),
      size: 'small'
    });
  }

  const messages = (conversation?.messages || []).filter((message) => message.role !== 'Tool' && message.role !== 'System');
  const toolResults = new Map((conversation?.messages || []).filter((message) => message.role === 'Tool').map((message) => [message.toolCallId, message.content]));

  return (
    <div className="chat-layout">
      <aside className="chat-sidebar" aria-label={t('chatConversations')}>
        <button className="primary-button chat-new-button" onClick={newChat} title={t('tooltipChatNew')} type="button"><MessageSquarePlus size={16} />{t('chatNew')}</button>
        <div className="chat-search-row">
          <label className="search-box chat-search" title={t('tooltipChatSearch')}>
            <Search size={15} />
            <input aria-label={t('search')} onChange={(event) => setSearch(event.target.value)} placeholder={t('chatSearch')} title={t('tooltipChatSearch')} value={search} />
          </label>
          <button aria-pressed={selecting} className={`icon-button${selecting ? ' active' : ''}`} disabled={conversations.length === 0 && !selecting} onClick={() => (selecting ? stopSelecting() : setSelecting(true))} title={selecting ? t('chatSelectDone') : t('chatSelectTooltip')} type="button"><ListChecks size={16} /></button>
        </div>
        {selecting ? (
          <div className="chat-select-bar">
            <span>{t('chatSelectedCount', { count: selected.size })}</span>
            <button className="action-button" onClick={() => setSelected(selected.size === conversations.length ? new Set() : new Set(conversations.map((item) => item.id)))} title={t('chatSelectAllTooltip')} type="button">{selected.size === conversations.length && conversations.length > 0 ? t('chatSelectNone') : t('chatSelectAll')}</button>
            <button className="action-button chat-stop" disabled={selected.size === 0} onClick={removeSelected} title={t('chatDeleteSelectedTooltip')} type="button"><Trash2 size={14} />{t('chatDeleteSelected', { count: selected.size })}</button>
          </div>
        ) : null}
        <div className="chat-conversation-list">
          {conversations.length === 0 ? <p className="chat-muted">{search ? t('chatNoMatches') : t('chatNoConversations')}</p> : null}
          {conversations.map((item) => (
            <div className={`chat-conversation-item${item.id === conversation?.id ? ' active' : ''}${selecting && selected.has(item.id) ? ' selected' : ''}`} key={item.id}>
              {selecting ? (
                <input aria-label={t('chatSelectOne', { title: item.title })} checked={selected.has(item.id)} className="chat-select-check" onChange={() => toggleSelected(item.id)} title={t('chatSelectOne', { title: item.title })} type="checkbox" />
              ) : null}
              <button className="chat-conversation-open" onClick={() => (selecting ? toggleSelected(item.id) : setConversationId(item.id))} title={item.title} type="button">
                <span className="chat-conversation-title">{item.pinned ? <Pin size={12} /> : null}{item.title}</span>
                <span className="chat-conversation-meta">{item.model || t('chatNoModel')} · {relativeTime(item.updatedUtc, t)}{item.surface && item.surface !== 'Web' ? ` · ${t(`chatSurface${item.surface}`, item.surface)}` : ''}</span>
              </button>
              <ActionMenu items={[
                { icon: <Pencil size={14} />, key: 'rename', label: t('chatRename'), onSelect: () => rename(item), tooltip: t('chatRename') },
                { icon: item.pinned ? <PinOff size={14} /> : <Pin size={14} />, key: 'pin', label: item.pinned ? t('chatUnpin') : t('chatPin'), onSelect: () => togglePin(item), tooltip: item.pinned ? t('chatUnpin') : t('chatPin') },
                { divider: true, key: 'divider' },
                { destructive: true, icon: <Trash2 size={14} />, key: 'delete', label: t('delete'), onSelect: () => remove(item), tooltip: t('chatDeleteTitle') }
              ]} />
            </div>
          ))}
        </div>
      </aside>

      <section className="chat-main">
        <header className="chat-header">
          <div className="chat-header-title">
            <h2 title={conversation?.title || t('chatNew')}>{conversation?.title || t('chatNew')}</h2>
            {conversation?.systemPrompt ? <span className="status-chip neutral" title={conversation.systemPrompt}>{t('chatHasSystemPrompt')}</span> : null}
            {chat.supportsTools ? (
              <button className="chat-directory" disabled={sending || (conversation && !conversation.id)} onClick={editWorkingDirectory} title={t('tooltipChatWorkingDirectory')} type="button">
                <FolderOpen size={14} /><span>{(conversation?.id ? conversation.workingDirectory : newChatDirectory) || t('chatWorkingDirectoryDefault')}</span>
              </button>
            ) : null}
          </div>
          <div className="chat-header-actions">
            <label className="chat-model-picker" title={t('tooltipChatModel')}>
              <Bot size={15} />
              <select aria-label={t('model')} disabled={sending} onChange={(event) => chooseModel(event.target.value)} title={t('tooltipChatModel')} value={activeModel}>
                {!activeModel ? <option value="">{t('chatChooseModel')}</option> : null}
                {models.local.length ? <optgroup label={t('chatModelsThisMachine')}>{models.local.map((item) => <option key={`l-${item.name}`} value={item.name}>{item.name}</option>)}</optgroup> : null}
                {models.network.length ? <optgroup label={t('chatModelsNetwork')}>{models.network.map((item) => <option key={`n-${item.name}`} value={item.name}>{item.name}</option>)}</optgroup> : null}
                {activeModel && !models.all.has(activeModel) ? <option value={activeModel}>{activeModel}</option> : null}
              </select>
            </label>
            {activeModel ? <ModelStatus readiness={activeReadiness} /> : null}
            <button aria-pressed={streaming} className={`action-button chat-tools-toggle${streaming ? ' on' : ''}`} disabled={sending} onClick={toggleStreaming} title={streaming ? t('tooltipChatStreamingOn') : t('tooltipChatStreamingOff')} type="button"><Radio size={15} />{streaming ? t('chatStreamingOn') : t('chatStreamingOff')}</button>
            {chat.supportsTools ? <button aria-pressed={toolsOn} className={`action-button chat-tools-toggle${toolsOn ? ' on' : ''}`} disabled={(conversation && !conversation.id) || sending || toolsUnsupported} onClick={toggleTools} title={toolsUnsupported ? t('tooltipChatToolsUnsupported') : toolsOn ? t('tooltipChatToolsOn') : t('tooltipChatToolsOff')} type="button"><Wrench size={15} />{toolsOn ? t('chatToolsOn') : t('chatToolsOff')}</button> : null}
            {chat.supportsTools ? <button className="icon-button" onClick={showTools} title={t('chatToolsTitle')} type="button"><ListChecks size={16} /></button> : null}
            <button className="icon-button" disabled={!conversation} onClick={editSystemPrompt} title={t('chatSystemPrompt')} type="button"><Settings2 size={16} /></button>
            {conversation ? <button className="icon-button" onClick={() => rename(conversation)} title={t('chatRename')} type="button"><Pencil size={16} /></button> : null}
          </div>
        </header>
        {catalog?.networkMessage && models.network.length === 0 ? <div className="chat-notice"><CircleAlert size={15} /><span>{t('chatNetworkUnavailable', { message: catalog.networkMessage })}</span></div> : null}
        {error ? <div className="chat-notice danger" role="alert"><CircleAlert size={15} /><span>{error}</span><button className="icon-button small" onClick={() => setError('')} title={t('dismiss')} type="button"><X size={14} /></button></div> : null}

        <div className="chat-messages" ref={listRef}>
          {messages.length === 0 && !live ? (
            <div className="chat-empty">
              <Bot size={30} />
              <h3>{t('chatEmptyTitle')}</h3>
              <p>{activeModel ? t('chatEmptyBody', { model: activeModel }) : t('chatChooseModel')}</p>
            </div>
          ) : null}
          {messages.map((message) => (
            message.role === 'User'
              ? <UserMessage key={message.id} message={message} />
              : <AssistantMessage key={message.id} message={message} onRate={rate} toolResults={toolResults} />
          ))}
          {live ? <LiveAnswer live={live} model={activeModel} onAnswerTool={answerTool} recentWaitMessages={recentWaitMessages} /> : null}
        </div>

        <form className="chat-composer" onSubmit={send}>
          <div className="chat-composer-row">
            <textarea
              aria-label={t('chatMessage')}
              disabled={sending}
              onChange={(event) => setInput(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter' && !event.shiftKey && !event.nativeEvent.isComposing) send(event);
              }}
              placeholder={activeModel ? t('chatPlaceholder', { model: activeModel }) : t('chatChooseModel')}
              ref={inputRef}
              rows={1}
              title={t('tooltipChatMessage')}
              value={input}
            />
            {sending ? (
              <button className="action-button chat-send chat-stop" onClick={stop} title={t('tooltipChatStop')} type="button"><Square size={15} />{t('chatStop')}</button>
            ) : (
              <button className="primary-button chat-send" disabled={!input.trim() || !activeModel} title={t('tooltipChatSend')} type="submit"><Send size={15} />{t('chatSend')}</button>
            )}
          </div>
          <p className="chat-disclaimer">{t('chatDisclaimer')}</p>
        </form>
      </section>
    </div>
  );
}

function ModelStatus({ readiness }) {
  const { t } = useTranslation();
  const status = readiness?.status || 'loading';
  const label = status === 'loading' ? t('chatModelLoading') : status === 'error' ? t('chatModelNotReady') : t('chatModelReady');
  const tone = status === 'loading' ? 'neutral' : status === 'error' ? 'warning' : 'success';
  return (
    <span className={`status-chip ${tone} chat-model-status`} title={readiness?.message || label}>
      {status === 'loading' ? <Loader2 className="chat-spin" size={12} /> : null}{label}
    </span>
  );
}

// The (i) next to every answer: where it ran and how long and how many tokens each part took.
function ChatInfo({ message }) {
  const { t } = useTranslation();
  const stats = message.stats || {};
  const ms = (value) => (value == null ? '-' : seconds(value));
  const count = (value) => (value == null ? '-' : Number(value).toLocaleString());
  const rows = [
    [t('chatInfoRoute'), [routeText(stats.route || message.route, t), stats.provider && !(stats.route || '').includes(stats.provider) ? stats.provider : ''].filter(Boolean).join(' · ') || '-'],
    [t('model'), stats.model || message.model || '-'],
    [t('chatInfoStreaming'), stats.streamed === false ? t('chatOff') : t('chatOn')],
    [t('chatTimeToFirstToken'), ms(stats.timeToFirstTokenMs ?? message.timeToFirstTokenMs)],
    [t('chatInfoStreamingTime'), ms(stats.streamingMs)],
    [t('chatInfoModelTime'), ms(stats.modelMs ?? message.totalMs)],
    [t('chatInfoToolTime'), stats.toolCalls ? `${ms(stats.toolMs)} (${t('chatInfoCalls', { count: stats.toolCalls })})` : t('chatInfoNone')],
    [t('chatModelCalls'), count(stats.modelCalls ?? 1)],
    [t('chatInfoTurnTime'), ms(stats.turnMs)],
    [t('chatInfoPromptTokens'), `${count(stats.promptTokens ?? message.promptTokens)}${stats.cachedPromptTokens != null ? ` (${t('chatInfoCached', { count: stats.cachedPromptTokens })})` : ''}`],
    [t('chatInfoCompletionTokens'), `${count(stats.completionTokens ?? message.completionTokens)}${stats.reasoningTokens != null ? ` (${t('chatInfoReasoning', { count: stats.reasoningTokens })})` : ''}`]
  ];
  if ((stats.modelCalls || 1) > 1) rows.push([t('chatInfoTurnTokens'), `${count(stats.turnPromptTokens)} / ${count(stats.turnCompletionTokens)}`]);
  rows.push([t('chatInfoTokensPerSecond'), stats.tokensPerSecond != null ? Number(stats.tokensPerSecond).toFixed(1) : '-']);
  return (
    <span aria-label={t('chatInfoTitle')} className="chat-info" role="button" tabIndex={0} title={rows.map(([label, value]) => `${label}: ${value}`).join('\n')}>
      <Info size={14} />
      <span className="chat-info-pop" role="tooltip">
        <strong>{t('chatInfoTitle')}</strong>
        <table><tbody>{rows.map(([label, value]) => <tr key={label}><th>{label}</th><td>{value}</td></tr>)}</tbody></table>
      </span>
    </span>
  );
}

function routeText(route, t) {
  if (!route) return '';
  if (route.startsWith('local:')) return t('chatRouteLocalProvider', { provider: route.slice(6) });
  if (route === 'network') return t('chatRouteNetwork');
  return route;
}

function UserMessage({ message }) {
  return (
    <div className="chat-message user">
      <div className="chat-bubble">{message.content}</div>
    </div>
  );
}

function AssistantMessage({ message, onRate, toolResults }) {
  const { t } = useTranslation();
  const rating = message.feedback?.rating || 0;
  return (
    <div className="chat-message assistant">
      <div className="chat-avatar"><Bot size={16} /></div>
      <div className="chat-answer">
        <div className="chat-answer-bubble">
          {message.thinking ? <Thinking text={message.thinking} /> : null}
          {(message.toolCalls || []).map((call) => <ToolCallRow call={{ ...call, result: call.result ?? toolResults.get(call.callId) }} key={call.id} />)}
          {message.content ? <Markdown text={message.content} /> : null}
          {message.failure ? <FailureNote failure={message.failure} partial={!!(message.content || message.thinking || (message.toolCalls || []).length)} /> : message.incomplete ? <div className="chat-incomplete">{t('chatIncomplete')}</div> : null}
        </div>
        <div className="chat-answer-footer">
          <div className="chat-meta">
            <ChatInfo message={message} />
            <RouteChip route={message.route} fallback={message.fallback} />
            {message.model ? <span title={t('model')}>{message.model}</span> : null}
            {message.timeToFirstTokenMs != null ? <span title={t('chatTimeToFirstToken')}>{t('chatFirstTokenValue', { value: seconds(message.timeToFirstTokenMs) })}</span> : null}
            {message.totalMs != null ? <span title={t('chatTotalTime')}>{seconds(message.totalMs)}</span> : null}
            {message.completionTokens != null ? <span title={t('chatTokens')}>{t('chatTokensValue', { count: message.completionTokens })}</span> : null}
          </div>
          {message.content ? (
            <div className="chat-actions">
              <CopyButton label={t('copy')} tooltip={t('chatCopyAnswer')} value={message.content} />
              <button aria-pressed={rating === 1} className={`icon-button small${rating === 1 ? ' rated-up' : ''}`} onClick={() => onRate(message, 1)} title={t('chatGoodAnswer')} type="button"><ThumbsUp size={14} /></button>
              <button aria-pressed={rating === -1} className={`icon-button small${rating === -1 ? ' rated-down' : ''}`} onClick={() => onRate(message, -1)} title={t('chatBadAnswer')} type="button"><ThumbsDown size={14} /></button>
            </div>
          ) : null}
        </div>
        {message.feedback?.comment ? <div className="chat-feedback-note" title={t('chatFeedbackComment')}>{message.feedback.comment}</div> : null}
      </div>
    </div>
  );
}

// Why the model did not answer, shown in the conversation in place of (or after) the answer. Every chat surface shows
// the same headline and reason (ChatFailure in Xeno.Core.Chat).
export function FailureNote({ failure, partial }) {
  const { t } = useTranslation();
  const kind = failure?.kind || 'Failed';
  const partialKey = partial && (kind === 'Timeout' || kind === 'Stopped') ? 'Partial' : '';
  const headline = t(`chatFailure${kind}${partialKey}`, { defaultValue: t('chatFailureFailed') });
  const reason = String(failure?.reason || '').trim();
  return (
    <div className={`chat-failure${kind === 'Stopped' ? ' stopped' : ''}`} data-failure-kind={kind} role="status">
      <CircleAlert size={15} />
      <span><strong>{headline}</strong>{reason ? <>: {reason}</> : null}</span>
    </div>
  );
}

function LiveAnswer({ live, model, onAnswerTool, recentWaitMessages }) {
  const { t } = useTranslation();
  const waiting = !live.text && !live.thinking && live.toolCalls.length === 0 && !live.error;
  // A short quip under the bubble while nothing has arrived yet, changed every 6 seconds.
  const [waitMessage, setWaitMessage] = useState(() => pickWaitMessage(recentWaitMessages));
  useEffect(() => {
    if (!waiting) return undefined;
    const timer = setInterval(() => setWaitMessage(pickWaitMessage(recentWaitMessages)), 6000);
    return () => clearInterval(timer);
  }, [waiting]);
  return (
    <div className="chat-message assistant live" aria-live="polite">
      <div className="chat-avatar"><Bot size={16} /></div>
      <div className="chat-answer">
        <div className="chat-answer-bubble">
          {live.thinking ? <Thinking open text={live.thinking} /> : null}
          {live.toolCalls.map((call) => <ToolCallRow call={call} key={call.id} onAnswer={onAnswerTool} />)}
          {live.text ? <Markdown text={live.text} /> : null}
          {waiting ? <div className="chat-typing" title={t('chatWaiting', { model })}><span /><span /><span /></div> : null}
          {live.error ? <FailureNote failure={live.failure || { kind: 'Failed', reason: live.error }} partial={!!(live.text || live.thinking || live.toolCalls.length)} /> : null}
        </div>
        {waiting && waitMessage ? <span className="chat-wait-label" key={waitMessage} title={waitMessage}>{waitMessage}</span> : null}
      </div>
    </div>
  );
}

function Thinking({ open = false, text }) {
  const { t } = useTranslation();
  const [expanded, setExpanded] = useState(open);
  return (
    <div className="chat-thinking">
      <button onClick={() => setExpanded(!expanded)} title={t('chatThinking')} type="button">
        {expanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}<Brain size={14} />{t('chatThinking')}
      </button>
      {expanded ? <div className="chat-thinking-body">{text}</div> : null}
    </div>
  );
}

function ToolCallRow({ call, onAnswer }) {
  const { t } = useTranslation();
  const [expanded, setExpanded] = useState(false);
  const approval = call.approval || 'Auto';
  const pending = approval === 'Pending';
  const state = pending ? t('chatToolWaiting') : call.success ? t('chatToolDone') : call.error ? t('chatToolFailed') : t('chatToolRunning');
  const tone = pending ? 'warning' : call.success ? 'success' : call.error ? 'danger' : 'neutral';
  return (
    <div className={`chat-tool ${tone}`}>
      <button className="chat-tool-head" onClick={() => setExpanded(!expanded)} title={t('chatToolDetails')} type="button">
        {expanded ? <ChevronDown size={14} /> : <ChevronRight size={14} />}
        <Wrench size={14} />
        <code>{call.toolName}</code>
        <span className={`status-chip ${tone}`}>{state}</span>
        {approval === 'Allowed' || approval === 'Denied' || approval === 'TimedOut' ? <span className="chat-muted">{t(`chatApproval${approval}`)}</span> : null}
        {call.durationMs ? <span className="chat-muted">{seconds(call.durationMs)}</span> : null}
      </button>
      {pending && onAnswer ? (
        <div className="chat-tool-approval">
          <span>{t('chatToolApprovalPrompt')}</span>
          <button className="primary-button small" onClick={() => onAnswer(call, true)} title={t('chatAllow')} type="button"><Check size={14} />{t('chatAllow')}</button>
          <button className="action-button small" onClick={() => onAnswer(call, false)} title={t('chatDeny')} type="button"><X size={14} />{t('chatDeny')}</button>
        </div>
      ) : null}
      {expanded ? (
        <div className="chat-tool-body">
          <div><strong>{t('chatToolArguments')}</strong><pre>{pretty(call.arguments)}</pre></div>
          {call.result ? <div><strong>{t('chatToolResult')}</strong><pre>{pretty(call.result)}</pre></div> : null}
          {call.error ? <div><strong>{t('chatErrorLabel')}</strong><pre>{call.error}</pre></div> : null}
        </div>
      ) : null}
    </div>
  );
}

// Tools grouped for display in the order they are offered: Xeno, Files, Processes, Web, Planning, Memory, then MCP servers.
function groupTools(tools) {
  const groups = new Map();
  for (const tool of tools) {
    const group = tool.group || 'Xeno';
    if (!groups.has(group)) groups.set(group, []);
    groups.get(group).push(tool);
  }
  return [...groups.entries()];
}

export function RouteChip({ fallback, route }) {
  const { t } = useTranslation();
  if (!route) return null;
  const local = route === 'local' || route.startsWith('local:');
  const provider = local && route.includes(':') ? route.slice(route.indexOf(':') + 1) : '';
  const label = local ? (provider ? t('chatRouteLocalProvider', { provider }) : t('chatRouteLocal')) : route === 'network' ? t('chatRouteNetwork') : route;
  return <span className={`status-chip ${local ? 'success' : route === 'network' ? 'info' : 'danger'}`} title={fallback ? t('chatRouteFallback') : route}>{label}{fallback ? ` · ${t('fallback')}` : ''}</span>;
}

export function Markdown({ text }) {
  return (
    <Suspense fallback={<div className="chat-plain">{text}</div>}>
      <div className="chat-markdown"><MarkdownPreview text={text} /></div>
    </Suspense>
  );
}

function chatModels(catalog) {
  const entries = Array.isArray(catalog?.entries) ? catalog.entries : [];
  const usable = entries.filter((item) => !isEmbeddingModel(item));
  const local = usable.filter((item) => item.servedLocally);
  const network = usable.filter((item) => !item.servedLocally && item.onNetwork);
  return { local, network, all: new Set(usable.map((item) => item.name)) };
}

function isEmbeddingModel(item) {
  if (item.supportsEmbeddings && !item.supportsCompletions) return true;
  const name = String(item.name || '').toLowerCase().split('/').pop();
  return name.includes('embed') || /^(all-)?minilm|^bge[-:]|^gte[-:]|^(multilingual-)?e5[-:]|rerank/.test(name);
}

function readStreaming() {
  try {
    return window.localStorage.getItem('xeno.chat.streaming') !== 'off';
  } catch {
    return true;
  }
}

function writeStreaming(on) {
  try {
    window.localStorage.setItem('xeno.chat.streaming', on ? 'on' : 'off');
  } catch {
    // Storage may be unavailable; the choice still applies on this page.
  }
}

function readLastModel() {
  try {
    return window.localStorage.getItem(lastModelKey) || '';
  } catch {
    return '';
  }
}

function writeLastModel(model) {
  try {
    window.localStorage.setItem(lastModelKey, model);
  } catch {
    // Storage may be unavailable; the choice still applies to this conversation.
  }
}

export function seconds(ms) {
  if (ms == null) return '';
  return ms < 1000 ? `${Math.round(ms)} ms` : `${(ms / 1000).toFixed(ms < 10000 ? 1 : 0)} s`;
}

function pretty(value) {
  if (!value) return '';
  try {
    return JSON.stringify(JSON.parse(value), null, 2);
  } catch {
    return value;
  }
}

export function relativeTime(value, t) {
  const time = Date.parse(value);
  if (Number.isNaN(time)) return '';
  const minutes = Math.round((Date.now() - time) / 60000);
  if (minutes < 1) return t('chatJustNow');
  if (minutes < 60) return t('chatMinutesAgo', { count: minutes });
  const hours = Math.round(minutes / 60);
  if (hours < 24) return t('chatHoursAgo', { count: hours });
  return new Date(time).toLocaleDateString();
}

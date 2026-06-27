import { useEffect, useMemo, useState } from 'react';
import { Github, Moon, Plus, RefreshCw, Sun, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import CopyButton from '../components/CopyButton.jsx';
import LanguageSelector from '../components/LanguageSelector.jsx';
import Modal from '../components/Modal.jsx';
import { jsonBody, localApi } from './localApi.js';

const pages = [
  { id: 'home', labelKey: 'home', tooltipKey: 'tooltipLocalNavHome' },
  { id: 'providers', labelKey: 'localProviders', tooltipKey: 'tooltipLocalNavProviders' },
  { id: 'models', labelKey: 'advertisedModels', tooltipKey: 'tooltipLocalNavModels' },
  { id: 'requests', labelKey: 'requests', tooltipKey: 'tooltipLocalNavRequests' },
  { id: 'logs', labelKey: 'localLogs', tooltipKey: 'tooltipLocalNavLogs' },
  { id: 'settings', labelKey: 'settings', tooltipKey: 'tooltipLocalNavSettings' }
];

export default function LocalDashboardApp() {
  const { t } = useTranslation();
  const [page, setPage] = useState('home');
  const [theme, setTheme] = useState(document.documentElement.getAttribute('data-theme') || 'light');
  const [state, setState] = useState(null);
  const [statistics, setStatistics] = useState(null);
  const [summary, setSummary] = useState([]);
  const [providers, setProviders] = useState([]);
  const [models, setModels] = useState([]);
  const [requests, setRequests] = useState([]);
  const [logs, setLogs] = useState([]);
  const [database, setDatabase] = useState(null);
  const [range, setRange] = useState('hour');
  const [providerFilter, setProviderFilter] = useState('');
  const [logSeverity, setLogSeverity] = useState('all');
  const [logTail, setLogTail] = useState(200);
  const [autoRefreshMs, setAutoRefreshMs] = useState(15000);
  const [modal, setModal] = useState(null);
  const [error, setError] = useState('');

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
  }, [theme]);

  useEffect(() => {
    refresh();
    if (autoRefreshMs === 0) return undefined;
    const timer = window.setInterval(refresh, autoRefreshMs);
    return () => window.clearInterval(timer);
  }, [page, range, logSeverity, logTail, autoRefreshMs]);

  async function refresh() {
    try {
      setError('');
      if (page === 'home') {
        const [stateResponse, statisticsResponse, summaryResponse] = await Promise.all([
          localApi('/api/v1/state'),
          localApi('/api/v1/statistics'),
          localApi(`/api/v1/requests/summary?range=${encodeURIComponent(range)}`)
        ]);
        setState(stateResponse);
        setStatistics(statisticsResponse);
        setSummary(summaryResponse.buckets || []);
      } else if (page === 'providers') {
        const response = await localApi('/api/v1/providers');
        setProviders(response.providers || []);
      } else if (page === 'models') {
        const response = await localApi('/api/v1/models');
        setModels(response.models || []);
      } else if (page === 'requests') {
        const [historyResponse, summaryResponse] = await Promise.all([
          localApi('/api/v1/requests'),
          localApi(`/api/v1/requests/summary?range=${encodeURIComponent(range)}`)
        ]);
        setRequests(historyResponse.entries || []);
        setSummary(summaryResponse.buckets || []);
      } else if (page === 'logs') {
        const response = await localApi(`/api/v1/logs?tail=${logTail}&severity=${logSeverity}`);
        setLogs(response.lines || []);
      } else if (page === 'settings') {
        const [stateResponse, databaseResponse] = await Promise.all([
          localApi('/api/v1/settings'),
          localApi('/api/v1/database')
        ]);
        setState(stateResponse);
        setDatabase(databaseResponse);
      }
    } catch (err) {
      setError(err.message);
    }
  }

  function navLabel(value) {
    const pageDefinition = pages.find((item) => item.id === value);
    return pageDefinition ? t(pageDefinition.labelKey) : title(value, t);
  }

  return (
    <div className="app-shell local-dashboard-shell">
      <aside className="sidebar">
        <div className="brand-block">
          <strong>{t('localDashboardTitle')}</strong>
          <span>{t('client')}</span>
        </div>
        <nav className="nav-list">
          {pages.map((item) => (
            <button className={page === item.id ? 'active' : ''} key={item.id} onClick={() => setPage(item.id)} title={t(item.tooltipKey)} type="button">
              {t(item.labelKey)}
            </button>
          ))}
        </nav>
      </aside>
      <main className="workspace">
        <header className="topbar">
          <div>
            <h1>{navLabel(page)}</h1>
            {state?.dashboardUrl ? <span className="topbar-server-url">{state.dashboardUrl}<CopyButton label={t('copyServerUrl')} value={state.dashboardUrl} /></span> : null}
          </div>
          <div className="topbar-actions">
            <LanguageSelector />
            <a aria-label={t('github')} className="icon-button" href="https://github.com/jchristn/Xeno" rel="noreferrer" target="_blank" title={t('tooltipGitHub')}>
              <Github size={17} />
            </a>
            <button className="icon-button" onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')} title={t('tooltipToggleTheme')} type="button">
              {theme === 'dark' ? <Sun size={17} /> : <Moon size={17} />}
            </button>
            <button className="icon-button" onClick={refresh} title={t('tooltipManualRefresh')} type="button">
              <RefreshCw size={17} />
            </button>
          </div>
        </header>
        {error ? <div className="empty-state error-state">{error}</div> : null}
        {page === 'home' ? <Home range={range} setRange={setRange} state={state} statistics={statistics} summary={summary} /> : null}
        {page === 'providers' ? <Providers autoRefreshMs={autoRefreshMs} providers={providers} refresh={refresh} setAutoRefreshMs={setAutoRefreshMs} setModal={setModal} /> : null}
        {page === 'models' ? <Models autoRefreshMs={autoRefreshMs} models={models} providerFilter={providerFilter} providers={providers} refresh={refresh} setAutoRefreshMs={setAutoRefreshMs} setModal={setModal} setProviderFilter={setProviderFilter} /> : null}
        {page === 'requests' ? <Requests autoRefreshMs={autoRefreshMs} range={range} refresh={refresh} requests={requests} setAutoRefreshMs={setAutoRefreshMs} setModal={setModal} setRange={setRange} summary={summary} /> : null}
        {page === 'logs' ? <Logs logSeverity={logSeverity} logTail={logTail} logs={logs} setLogSeverity={setLogSeverity} setLogTail={setLogTail} /> : null}
        {page === 'settings' ? <Settings database={database} refresh={refresh} state={state} /> : null}
      </main>
      <Modal
        actions={modal?.actions}
        headerActions={modal?.json ? <CopyButton label={t('copyJson')} value={JSON.stringify(modal.json, null, 2)} /> : null}
        onClose={() => setModal(null)}
        open={!!modal}
        title={modal?.title || ''}
      >
        {modal?.body}
      </Modal>
    </div>
  );
}

function Home({ range, setRange, state, statistics, summary }) {
  const { t } = useTranslation();
  return (
    <>
      {state?.settings?.localDashboard?.host === '0.0.0.0' ? <div className="empty-state">{t('localBindWarning')}</div> : null}
      <div className="metric-grid">
        <Metric label={t('connected')} value={state?.connection?.isConnected ? t('yes') : t('no')} />
        <Metric label={t('registered')} value={state?.connection?.isRegistered ? t('yes') : t('no')} />
        <Metric label={t('requests')} value={statistics?.requests || 0} />
        <Metric label={t('failures')} value={statistics?.failures || 0} />
      </div>
      <Panel title={t('requestsOverTime')} actions={<RangeSelector range={range} setRange={setRange} />}>
        <BarChart buckets={summary} />
      </Panel>
      <Panel title={t('connection')}>
        <RecordTable fields={['clientId', 'tenantId', 'webSocketUrl', 'lastDisconnectReason']} record={state?.connection || {}} />
      </Panel>
    </>
  );
}

function Providers({ autoRefreshMs, providers, refresh, setAutoRefreshMs, setModal }) {
  const { t } = useTranslation();

  async function remove(provider) {
    setModal(confirmModal(
      t,
      t('deleteProviderTitle'),
      t('deleteProviderMessage', { name: provider.name }),
      async () => {
        await localApi(`/api/v1/providers/${encodeURIComponent(provider.name)}`, { method: 'DELETE' });
        refresh();
        setModal(null);
      }
    ));
  }

  async function test(provider) {
    const response = await localApi(`/api/v1/providers/${encodeURIComponent(provider.name)}/test`, { method: 'POST', body: '{}' });
    setModal(messageModal(t, t('providerTestTitle'), response.success ? t('providerReachable') : t('providerTestFailed', { detail: response.error || response.statusCode || t('unknown') }), response));
  }

  return (
    <Panel title={t('providerEndpoints')} actions={<button className="primary-button" onClick={() => setModal(providerModal(t, null, refresh))} title={t('tooltipCreateLocalProvider')} type="button"><Plus size={16} />{t('createProvider')}</button>}>
      <DataTable
        actions={(row) => (
          <>
            <button className="action-button secondary" onClick={() => test(row)} title={t('tooltipTestLocalProvider')} type="button">{t('test')}</button>
            <button className="action-button secondary" onClick={() => setModal(providerModal(t, row, refresh))} title={t('tooltipEditLocalProvider')} type="button">{t('edit')}</button>
            <button className="action-button danger" onClick={() => remove(row)} title={t('tooltipDeleteLocalProvider')} type="button"><Trash2 size={16} /></button>
          </>
        )}
        autoRefreshMs={autoRefreshMs}
        columns={['name', 'apiType', 'endpoint', 'timeoutMs', 'credential']}
        onRefresh={refresh}
        onRowClick={(row) => setModal(providerModal(t, row, refresh))}
        rows={providers}
        setAutoRefreshMs={setAutoRefreshMs}
      />
    </Panel>
  );
}

function Models({ autoRefreshMs, models, providerFilter, providers, refresh, setAutoRefreshMs, setModal, setProviderFilter }) {
  const { t } = useTranslation();
  const providerNames = useMemo(() => [...new Set(models.map((model) => model.provider).filter(Boolean))], [models]);
  const rows = providerFilter ? models.filter((model) => model.provider === providerFilter) : models;

  async function remove(model) {
    setModal(confirmModal(
      t,
      t('deleteModelTitle'),
      t('deleteModelMessage', { name: model.name }),
      async () => {
        await localApi(`/api/v1/models/${encodeURIComponent(model.provider)}/${encodeURIComponent(model.name)}`, { method: 'DELETE' });
        refresh();
        setModal(null);
      }
    ));
  }

  return (
    <Panel
      title={t('advertisedModels')}
      actions={(
        <>
          <label className="form-field inline compact-field" title={t('tooltipLocalModelProviderFilter')}>
            <span>{t('provider')}</span>
            <select value={providerFilter} onChange={(event) => setProviderFilter(event.target.value)} title={t('tooltipLocalModelProviderFilter')}>
              <option value="">{t('all')}</option>
              {providerNames.map((name) => <option key={name} value={name}>{name}</option>)}
            </select>
          </label>
          <button className="primary-button" onClick={() => setModal(modelModal(t, null, providers, refresh))} title={t('tooltipCreateLocalModel')} type="button"><Plus size={16} />{t('createModel')}</button>
        </>
      )}
    >
      <DataTable
        actions={(row) => (
          <>
            <button className="action-button secondary" onClick={() => setModal(modelModal(t, row, providers, refresh))} title={t('tooltipEditLocalModel')} type="button">{t('edit')}</button>
            <button className="action-button danger" onClick={() => remove(row)} title={t('tooltipDeleteLocalModel')} type="button"><Trash2 size={16} /></button>
          </>
        )}
        autoRefreshMs={autoRefreshMs}
        columns={['name', 'provider', 'apiType', 'supportsCompletions', 'supportsEmbeddings']}
        onRefresh={refresh}
        onRowClick={(row) => setModal(modelModal(t, row, providers, refresh))}
        rows={rows}
        setAutoRefreshMs={setAutoRefreshMs}
      />
    </Panel>
  );
}

function Requests({ autoRefreshMs, range, refresh, requests, setAutoRefreshMs, setModal, setRange, summary }) {
  const { t } = useTranslation();

  async function clear() {
    setModal(confirmModal(
      t,
      t('clearHistoryTitle'),
      t('clearLocalHistoryMessage'),
      async () => {
        await localApi('/api/v1/requests/bulk', { method: 'POST', body: '{}' });
        refresh();
        setModal(null);
      }
    ));
  }

  async function remove(row) {
    setModal(confirmModal(
      t,
      t('deleteRequestTitle'),
      t('deleteRequestMessage', { id: row.id }),
      async () => {
        await localApi(`/api/v1/requests/${encodeURIComponent(row.id)}`, { method: 'DELETE' });
        refresh();
        setModal(null);
      }
    ));
  }

  async function details(row) {
    const entry = await localApi(`/api/v1/requests/${encodeURIComponent(row.id)}`);
    setModal({ title: t('requestDetails'), json: entry, body: <RequestDetails entry={entry} /> });
  }

  return (
    <>
      <Panel title={t('requestsOverTime')} actions={<RangeSelector range={range} setRange={setRange} />}>
        <BarChart buckets={summary} />
      </Panel>
      <Panel title={t('requestHistory')} actions={<button className="action-button danger" onClick={clear} title={t('tooltipClearLocalHistory')} type="button"><Trash2 size={16} />{t('clear')}</button>}>
        <DataTable
          actions={(row) => (
            <>
              <button className="action-button secondary" onClick={() => details(row)} title={t('tooltipOpenLocalRequestDetails')} type="button">{t('details')}</button>
              <button className="action-button danger" onClick={() => remove(row)} title={t('tooltipDeleteLocalRequest')} type="button"><Trash2 size={16} /></button>
            </>
          )}
          autoRefreshMs={autoRefreshMs}
          columns={['id', 'receivedUtc', 'method', 'path', 'modelName', 'providerName', 'statusCode', 'outcome']}
          onRefresh={refresh}
          onRowClick={details}
          rows={requests}
          setAutoRefreshMs={setAutoRefreshMs}
        />
      </Panel>
    </>
  );
}

function Logs({ logSeverity, logTail, logs, setLogSeverity, setLogTail }) {
  const { t } = useTranslation();
  return (
    <Panel
      title={t('clientLogs')}
      actions={(
        <>
          <label className="form-field inline compact-field" title={t('tooltipLocalLogSeverity')}>
            <span>{t('severity')}</span>
            <select value={logSeverity} onChange={(event) => setLogSeverity(event.target.value)} title={t('tooltipLocalLogSeverity')}>
              {['all', 'debug', 'info', 'warn', 'error'].map((item) => <option key={item} value={item}>{title(item, t)}</option>)}
            </select>
          </label>
          <label className="form-field inline compact-field" title={t('tooltipLocalLogTail')}>
            <span>{t('tail')}</span>
            <select value={logTail} onChange={(event) => setLogTail(Number(event.target.value))} title={t('tooltipLocalLogTail')}>
              {[50, 100, 200, 500].map((count) => <option key={count} value={count}>{count}</option>)}
            </select>
          </label>
        </>
      )}
    >
      <pre className="json-preview"><code>{logs.join('\n')}</code></pre>
    </Panel>
  );
}

function Settings({ database, refresh, state }) {
  const { t } = useTranslation();

  async function save(event) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    await localApi('/api/v1/settings', {
      method: 'PUT',
      body: jsonBody({
        enabled: form.get('enabled') === 'on',
        host: form.get('host'),
        port: Number(form.get('port')),
        openBrowserOnStart: form.get('openBrowserOnStart') === 'on',
        requireLocalToken: form.get('requireLocalToken') === 'on',
        database: { filename: form.get('filename') },
        historyRetention: {
          maxEntries: Number(form.get('maxEntries')),
          maxBodyBytes: Number(form.get('maxBodyBytes')),
          persistBodies: form.get('persistBodies') === 'on'
        }
      })
    });
    refresh();
  }

  const settings = state?.settings?.localDashboard || {};
  return (
    <>
      <Panel title={t('localDashboardSettings')}>
        <div className="empty-state">{t('localSettingsRestartNotice')}</div>
        <form className="form-grid" onSubmit={save}>
          <Toggle checked={settings.enabled} label={t('enabled')} name="enabled" tooltip={t('tooltipLocalSettingEnabled')} />
          <Field defaultValue={settings.host} label={t('host')} name="host" tooltip={t('tooltipLocalSettingHost')} />
          <Field defaultValue={settings.port} label={t('port')} name="port" tooltip={t('tooltipLocalSettingPort')} type="number" />
          <Toggle checked={settings.requireLocalToken} label={t('requireLocalToken')} name="requireLocalToken" tooltip={t('tooltipLocalSettingToken')} />
          <Toggle checked={settings.openBrowserOnStart} label={t('openBrowserOnStart')} name="openBrowserOnStart" tooltip={t('tooltipLocalSettingOpenBrowser')} />
          <Field defaultValue={settings.database?.filename} label={t('sqliteFilename')} name="filename" tooltip={t('tooltipLocalSettingSqlite')} />
          <Field defaultValue={settings.historyRetention?.maxEntries} label={t('maxHistoryEntries')} name="maxEntries" tooltip={t('tooltipLocalSettingMaxEntries')} type="number" />
          <Field defaultValue={settings.historyRetention?.maxBodyBytes} label={t('maxBodyBytes')} name="maxBodyBytes" tooltip={t('tooltipLocalSettingMaxBodyBytes')} type="number" />
          <Toggle checked={settings.historyRetention?.persistBodies} label={t('persistBodies')} name="persistBodies" tooltip={t('tooltipLocalSettingPersistBodies')} />
          <div className="button-row"><button className="primary-button" title={t('tooltipSaveSettings')} type="submit">{t('saveSettings')}</button></div>
        </form>
      </Panel>
      <Panel title={t('databaseStatus')} actions={<CopyButton label={t('copyJson')} value={JSON.stringify(database || {}, null, 2)} />}>
        <RecordTable fields={['status', 'resolvedFilename', 'sizeBytes', 'requests', 'oldestRequestUtc', 'newestRequestUtc']} record={database || {}} />
      </Panel>
    </>
  );
}

function providerModal(t, provider, refresh) {
  async function save(event) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    await localApi(provider ? `/api/v1/providers/${encodeURIComponent(provider.name)}` : '/api/v1/providers', {
      method: provider ? 'PUT' : 'POST',
      body: jsonBody({
        name: form.get('name'),
        apiType: form.get('apiType'),
        endpoint: form.get('endpoint'),
        timeoutMs: Number(form.get('timeoutMs')),
        credential: form.get('credential') || null,
        clearCredential: form.get('clearCredential') === 'on'
      })
    });
    refresh();
  }

  return {
    title: provider ? t('editProvider') : t('createProvider'),
    json: provider || {},
    body: (
      <form className="form-grid" id="provider-form" onSubmit={save}>
        <Field defaultValue={provider?.name || ''} label={t('name')} name="name" tooltip={t('tooltipLocalProviderName')} />
        <Field defaultValue={provider?.apiType || 'Ollama'} label={t('apiType')} name="apiType" tooltip={t('tooltipLocalProviderApiType')} />
        <Field defaultValue={provider?.endpoint || ''} label={t('endpoint')} name="endpoint" tooltip={t('tooltipLocalProviderEndpoint')} />
        <Field defaultValue={provider?.timeoutMs || 300000} label={t('timeoutMs')} name="timeoutMs" tooltip={t('tooltipLocalProviderTimeout')} type="number" />
        <Field defaultValue="" label={t('credential')} name="credential" tooltip={t('tooltipLocalProviderCredential')} />
        <Toggle checked={false} label={t('clearCredential')} name="clearCredential" tooltip={t('tooltipLocalProviderClearCredential')} />
      </form>
    ),
    actions: <button className="primary-button" form="provider-form" title={t('tooltipSaveLocalProvider')} type="submit">{t('save')}</button>
  };
}

function modelModal(t, model, providers, refresh) {
  async function save(event) {
    event.preventDefault();
    const form = new FormData(event.currentTarget);
    await localApi(model ? `/api/v1/models/${encodeURIComponent(model.provider)}/${encodeURIComponent(model.name)}` : '/api/v1/models', {
      method: model ? 'PUT' : 'POST',
      body: jsonBody({
        provider: form.get('provider'),
        name: form.get('name'),
        supportsCompletions: form.get('supportsCompletions') === 'on',
        supportsEmbeddings: form.get('supportsEmbeddings') === 'on'
      })
    });
    refresh();
  }

  return {
    title: model ? t('editModel') : t('createModel'),
    json: model || {},
    body: (
      <form className="form-grid" id="model-form" onSubmit={save}>
        <label className="form-field" title={t('tooltipLocalModelProvider')}>
          <span>{t('provider')}</span>
          <select defaultValue={model?.provider || providers[0]?.name || ''} disabled={!!model} name="provider" title={t('tooltipLocalModelProvider')}>
            {providers.map((provider) => <option key={provider.name} value={provider.name}>{provider.name}</option>)}
          </select>
        </label>
        <Field defaultValue={model?.name || ''} label={t('model')} name="name" tooltip={t('tooltipLocalModelName')} />
        <Toggle checked={model ? model.supportsCompletions : true} label={t('supportsCompletions')} name="supportsCompletions" tooltip={t('tooltipLocalModelCompletions')} />
        <Toggle checked={model ? model.supportsEmbeddings : false} label={t('supportsEmbeddings')} name="supportsEmbeddings" tooltip={t('tooltipLocalModelEmbeddings')} />
      </form>
    ),
    actions: <button className="primary-button" form="model-form" title={t('tooltipSaveLocalModel')} type="submit">{t('save')}</button>
  };
}

function confirmModal(t, titleText, message, onConfirm) {
  async function confirm() {
    await onConfirm();
  }

  return {
    title: titleText,
    body: <div className="empty-state">{message}</div>,
    actions: <button className="action-button danger" onClick={confirm} title={t('tooltipConfirmDestructiveAction')} type="button">{t('confirm')}</button>
  };
}

function messageModal(t, titleText, message, json) {
  return {
    title: titleText,
    json,
    body: <div className="empty-state">{message}</div>
  };
}

function DataTable({ actions, autoRefreshMs = 0, columns, compact = false, onRefresh, onRowClick, rows, setAutoRefreshMs }) {
  const { t } = useTranslation();
  const [draftFilters, setDraftFilters] = useState({});
  const [filters, setFilters] = useState({});
  const [page, setPage] = useState(1);
  const pageSize = 10;
  const filteredRows = useMemo(() => rows.filter((row) => columns.every((column) => {
    const filter = String(filters[column] || '').trim().toLowerCase();
    if (!filter) return true;
    return String(row[column] || '').toLowerCase().includes(filter);
  })), [columns, filters, rows]);
  const pageCount = Math.max(1, Math.ceil(filteredRows.length / pageSize));
  const safePage = Math.min(page, pageCount);
  const pageRows = filteredRows.slice((safePage - 1) * pageSize, safePage * pageSize);

  function applyFilter(column) {
    setFilters({ ...filters, [column]: draftFilters[column] || '' });
    setPage(1);
  }

  function updateAutoRefresh(event) {
    if (!setAutoRefreshMs) return;
    setAutoRefreshMs(Number(event.target.value));
  }

  return (
    <>
      {!compact ? (
        <div className="table-controls">
          <div className="button-row">
            {onRefresh ? <button className="icon-button" onClick={onRefresh} title={t('tooltipManualRefresh')} type="button"><RefreshCw size={16} /></button> : null}
            {setAutoRefreshMs ? (
              <label className="form-field inline compact-field" title={t('tooltipAutoRefresh')}>
                <span>{t('autoRefresh')}</span>
                <select onChange={updateAutoRefresh} title={t('tooltipAutoRefresh')} value={autoRefreshMs}>
                  <option value={0}>{t('none')}</option>
                  <option value={15000}>15s</option>
                  <option value={30000}>30s</option>
                  <option value={60000}>60s</option>
                  <option value={180000}>180s</option>
                  <option value={300000}>300s</option>
                </select>
              </label>
            ) : null}
          </div>
          <div className="button-row">
            <button className="action-button secondary" disabled={safePage <= 1} onClick={() => setPage(Math.max(1, safePage - 1))} title={t('tooltipPreviousPage')} type="button">{t('previousPage')}</button>
            <span className="table-page-label">{t('pageOf', { page: safePage, total: pageCount })}</span>
            <button className="action-button secondary" disabled={safePage >= pageCount} onClick={() => setPage(Math.min(pageCount, safePage + 1))} title={t('tooltipNextPage')} type="button">{t('nextPage')}</button>
          </div>
        </div>
      ) : null}
      <div className="table-scroll">
        <table className="data-table">
          <thead>
            <tr>{columns.map((column) => <th key={column}>{title(column, t)}</th>)}{actions ? <th>{t('actions')}</th> : null}</tr>
            {!compact ? (
              <tr className="table-filter-row">
                {columns.map((column) => (
                  <th key={column}>
                    <input
                      onBlur={() => applyFilter(column)}
                      onChange={(event) => setDraftFilters({ ...draftFilters, [column]: event.target.value })}
                      onKeyDown={(event) => {
                        if (event.key === 'Enter') applyFilter(column);
                      }}
                      placeholder={t('filterColumn', { column: title(column, t) })}
                      title={t('tooltipLocalColumnFilter', { column: title(column, t) })}
                      type="text"
                      value={draftFilters[column] || ''}
                    />
                  </th>
                ))}
                {actions ? <th /> : null}
              </tr>
            ) : null}
          </thead>
          <tbody>
            {pageRows.length === 0 ? <tr><td colSpan={columns.length + (actions ? 1 : 0)}>{t('emptyTableMessage')}</td></tr> : pageRows.map((row) => (
              <tr className={onRowClick ? 'clickable-row' : ''} key={row.id || `${row.provider || ''}:${row.name || row.modelName || row.endpoint}`} onClick={onRowClick ? () => onRowClick(row) : undefined}>
                {columns.map((column) => <td key={column}>{copyColumn(column, row[column]) ? <span className="copyable-value"><code>{String(row[column] || '-')}</code><CopyButton label={t('copyValue')} value={String(row[column] || '')} /></span> : display(row[column], t)}</td>)}
                {actions ? <td className="table-actions" onClick={(event) => event.stopPropagation()}>{actions(row)}</td> : null}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </>
  );
}

function RecordTable({ fields, record }) {
  const { t } = useTranslation();
  return <DataTable columns={['field', 'value']} compact rows={fields.map((field) => ({ field: title(field, t), value: record[field] }))} />;
}

function RequestDetails({ entry }) {
  const { t } = useTranslation();
  return (
    <div className="details-stack">
      <RecordTable fields={['id', 'tenantId', 'clientId', 'requestorIp', 'providerName', 'apiType', 'modelName', 'outcome', 'statusCode']} record={entry} />
      {['requestHeaders', 'requestBody', 'responseHeaders', 'responseBody', 'telemetry', 'timeline'].map((field) => (
        <details key={field} open>
          <summary>{title(field, t)} <CopyButton label={t('copyValue')} value={typeof entry[field] === 'string' ? entry[field] : JSON.stringify(entry[field] || {}, null, 2)} /></summary>
          <pre className="json-preview"><code>{typeof entry[field] === 'string' ? entry[field] : JSON.stringify(entry[field] || {}, null, 2)}</code></pre>
        </details>
      ))}
    </div>
  );
}

function Panel({ actions, children, title: panelTitle }) {
  return <section className="workspace-card"><div className="panel-heading"><h2>{panelTitle}</h2><div className="button-row">{actions}</div></div>{children}</section>;
}

function Metric({ label, value }) {
  return <div className="metric-card"><span>{label}</span><strong>{value}</strong></div>;
}

function RangeSelector({ range, setRange }) {
  const { t } = useTranslation();
  return (
    <label className="form-field inline compact-field" title={t('tooltipChartRange')}>
      <span>{t('range')}</span>
      <select value={range} onChange={(event) => setRange(event.target.value)} title={t('tooltipChartRange')}>
        <option value="hour">{t('lastHour')}</option>
        <option value="day">{t('lastDay')}</option>
        <option value="week">{t('lastWeek')}</option>
        <option value="month">{t('lastMonth')}</option>
      </select>
    </label>
  );
}

function BarChart({ buckets }) {
  const max = Math.max(1, ...buckets.map((bucket) => bucket.total || 0));
  return (
    <svg className="activity-chart" role="img" viewBox="0 0 640 220">
      {buckets.map((bucket, index) => {
        const width = 640 / Math.max(1, buckets.length);
        const height = Math.max(2, ((bucket.total || 0) / max) * 180);
        const x = index * width;
        return <rect fill="var(--color-accent)" height={height} key={bucket.startUtc || index} rx="2" width={Math.max(2, width - 3)} x={x} y={200 - height}><title>{`${bucket.startUtc}: ${bucket.total || 0}`}</title></rect>;
      })}
    </svg>
  );
}

function Field({ defaultValue, label, name, tooltip, type = 'text' }) {
  return <label className="form-field" title={tooltip}><span>{label}</span><input defaultValue={defaultValue || ''} name={name} title={tooltip} type={type} /></label>;
}

function Toggle({ checked, label, name, tooltip }) {
  return <label className="toggle-field" title={tooltip}><input defaultChecked={!!checked} name={name} title={tooltip} type="checkbox" />{label}</label>;
}

function display(value, t) {
  if (typeof value === 'boolean') return value ? t('yes') : t('no');
  if (value === null || value === undefined || value === '') return '-';
  return String(value);
}

function copyColumn(column, value) {
  const normalized = column.toLowerCase();
  return normalized.includes('id')
    || normalized.includes('url')
    || normalized.includes('path')
    || column === 'name'
    || column === 'provider'
    || column === 'resolvedFilename'
    || column === 'endpoint'
    || (column === 'value' && value !== null && value !== undefined && value !== '');
}

function title(value, t) {
  const key = String(value || '');
  if (typeof t === 'function') {
    const translated = t(key);
    if (translated !== key) return translated;
  }

  return key.replace(/([A-Z])/g, ' $1').replace(/^./, (letter) => letter.toUpperCase()).trim();
}

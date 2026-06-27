import { useEffect, useMemo, useState } from 'react';
import {
  Activity,
  BrainCircuit,
  ChevronRight,
  CircleAlert,
  FileJson,
  Gauge,
  History,
  Moon,
  Pencil,
  Play,
  Plus,
  RefreshCw,
  Search,
  Server,
  Settings as SettingsIcon,
  Sun,
  Terminal,
  Trash2,
  Zap
} from 'lucide-react';
import { useTranslation } from 'react-i18next';
import ActionMenu from '../components/ActionMenu.jsx';
import ActivityChart from '../components/ActivityChart.jsx';
import CopyButton from '../components/CopyButton.jsx';
import CopyableValue from '../components/CopyableValue.jsx';
import EmptyState from '../components/EmptyState.jsx';
import LanguageSelector from '../components/LanguageSelector.jsx';
import Modal from '../components/Modal.jsx';
import Pagination from '../components/Pagination.jsx';
import { jsonBody, localApi } from './localApi.js';

const pages = [
  { id: 'home', descriptionKey: 'localHomeDescription', icon: Activity, labelKey: 'home', tooltipKey: 'tooltipLocalNavHome' },
  { id: 'providers', descriptionKey: 'localProvidersDescription', icon: Server, labelKey: 'localProviders', tooltipKey: 'tooltipLocalNavProviders' },
  { id: 'models', descriptionKey: 'localModelsDescription', icon: BrainCircuit, labelKey: 'advertisedModels', tooltipKey: 'tooltipLocalNavModels' },
  { id: 'requests', descriptionKey: 'localRequestsDescription', icon: History, labelKey: 'requests', tooltipKey: 'tooltipLocalNavRequests' },
  { id: 'logs', descriptionKey: 'localLogsDescription', icon: Terminal, labelKey: 'localLogs', tooltipKey: 'tooltipLocalNavLogs' },
  { id: 'settings', descriptionKey: 'localSettingsDescription', icon: SettingsIcon, labelKey: 'settings', tooltipKey: 'tooltipLocalNavSettings' }
];

const interactiveRowClickSelector = [
  'button',
  'a',
  'input',
  'select',
  'textarea',
  'label',
  '[role="button"]',
  '[data-row-click-ignore="true"]'
].join(', ');

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
  const [range, setRange] = useState('lastHour');
  const [providerFilter, setProviderFilter] = useState('');
  const [logSeverity, setLogSeverity] = useState('all');
  const [logTail, setLogTail] = useState(200);
  const [modal, setModal] = useState(null);
  const [error, setError] = useState('');

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
  }, [theme]);

  useEffect(() => {
    refresh();
  }, [page, range, logSeverity, logTail]);

  async function refresh() {
    try {
      setError('');
      if (page === 'home') {
        const [stateResponse, statisticsResponse, summaryResponse] = await Promise.all([
          localApi('/api/v1/state'),
          localApi('/api/v1/statistics'),
          localApi(`/api/v1/requests/summary?range=${encodeURIComponent(localApiRange(range))}`)
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
          localApi(`/api/v1/requests/summary?range=${encodeURIComponent(localApiRange(range))}`)
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

  function currentPage() {
    return pages.find((item) => item.id === page) || pages[0];
  }

  const selectedPage = currentPage();

  return (
    <div className="app-shell local-dashboard-shell">
      <aside className="sidebar">
        <div className="sidebar-brand">
          <img className="sidebar-logo" src="/logo.png" alt={t('localDashboardTitle')} />
          <div>
            <strong>{t('localDashboardTitle')}</strong>
            <span>{t('client')}</span>
          </div>
        </div>
        <nav className="sidebar-nav" aria-label={t('dashboard')}>
          <div className="sidebar-group">
            <div className="sidebar-group-label">{t('observe')}</div>
            {pages.slice(0, 4).map((item) => (
              <SidebarButton item={item} key={item.id} page={page} setPage={setPage} />
            ))}
          </div>
          <div className="sidebar-group">
            <div className="sidebar-group-label">{t('configuration')}</div>
            {pages.slice(4).map((item) => (
              <SidebarButton item={item} key={item.id} page={page} setPage={setPage} />
            ))}
          </div>
        </nav>
        <div className="sidebar-footer">
          <span>{t('version')}</span>
          <strong>v0.1.0</strong>
        </div>
      </aside>
      <div className="content-shell">
        <header className="topbar">
          <div className="topbar-title">
            <h1>{navLabel(page)}</h1>
            <p>{t(selectedPage.descriptionKey)}</p>
          </div>
          <div className="topbar-actions">
            {state?.dashboardUrl ? (
              <span className="topbar-server-url" title={t('tooltipServerUrl')}>
                <Server size={15} />
                <span>{state.dashboardUrl}</span>
                <CopyButton label={t('copyServerUrl')} tooltip={t('tooltipCopyServerUrl')} value={state.dashboardUrl} />
              </span>
            ) : null}
            <LanguageSelector />
            <a aria-label={t('github')} className="icon-button" href="https://github.com/jchristn/Xeno" rel="noreferrer" target="_blank" title={t('tooltipGitHub')}>
              <GitHubMark size={17} />
            </a>
            <button className="icon-button" onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')} title={t('tooltipToggleTheme')} type="button">
              {theme === 'dark' ? <Sun size={17} /> : <Moon size={17} />}
            </button>
            <button className="icon-button" onClick={refresh} title={t('tooltipManualRefresh')} type="button">
              <RefreshCw size={17} />
            </button>
          </div>
        </header>
        <main className="workspace">
          {error ? <div className="settings-restart-banner error-state"><CircleAlert size={18} /><span>{error}</span></div> : null}
          {page === 'home' ? <Home range={range} refresh={refresh} setRange={setRange} state={state} statistics={statistics} summary={summary} /> : null}
          {page === 'providers' ? <Providers providers={providers} refresh={refresh} setModal={setModal} /> : null}
          {page === 'models' ? <Models models={models} providerFilter={providerFilter} providers={providers} refresh={refresh} setModal={setModal} setProviderFilter={setProviderFilter} /> : null}
          {page === 'requests' ? <Requests range={range} refresh={refresh} requests={requests} setModal={setModal} setRange={setRange} summary={summary} /> : null}
          {page === 'logs' ? <Logs logSeverity={logSeverity} logTail={logTail} logs={logs} setLogSeverity={setLogSeverity} setLogTail={setLogTail} /> : null}
          {page === 'settings' ? <Settings database={database} refresh={refresh} state={state} /> : null}
        </main>
      </div>
      <Modal
        actions={modal?.actions}
        headerActions={modal?.json ? <CopyButton label={t('copyJson')} value={JSON.stringify(modal.json, null, 2)} /> : null}
        onClose={() => setModal(null)}
        open={!!modal}
        size={modal?.size || 'large'}
        title={modal?.title || ''}
      >
        {modal?.body}
      </Modal>
    </div>
  );
}

function SidebarButton({ item, page, setPage }) {
  const { t } = useTranslation();
  const Icon = item.icon;
  return (
    <button className={`sidebar-link ${page === item.id ? 'active' : ''}`} onClick={() => setPage(item.id)} title={t(item.tooltipKey)} type="button">
      <Icon size={17} />
      <span>{t(item.labelKey)}</span>
      <ChevronRight className="sidebar-link-arrow" size={14} />
    </button>
  );
}

function GitHubMark({ size = 17 }) {
  return (
    <svg
      aria-hidden="true"
      fill="currentColor"
      height={size}
      viewBox="0 0 16 16"
      width={size}
    >
      <path d="M8 0C3.58 0 0 3.58 0 8c0 3.54 2.29 6.53 5.47 7.59.4.07.55-.17.55-.38 0-.19-.01-.82-.01-1.49-2.01.37-2.53-.49-2.69-.94-.09-.23-.48-.94-.82-1.13-.28-.15-.68-.52-.01-.53.63-.01 1.08.58 1.23.82.72 1.21 1.87.87 2.33.66.07-.52.28-.87.51-1.07-1.78-.2-3.64-.89-3.64-3.95 0-.87.31-1.59.82-2.15-.08-.2-.36-1.02.08-2.12 0 0 .67-.21 2.2.82A7.65 7.65 0 0 1 8 3.87c.68 0 1.36.09 2 .26 1.53-1.04 2.2-.82 2.2-.82.44 1.1.16 1.92.08 2.12.51.56.82 1.27.82 2.15 0 3.07-1.87 3.75-3.65 3.95.29.25.54.73.54 1.48 0 1.07-.01 1.93-.01 2.2 0 .21.15.46.55.38A8.01 8.01 0 0 0 16 8c0-4.42-3.58-8-8-8Z" />
    </svg>
  );
}

function Home({ range, refresh, setRange, state, statistics, summary }) {
  const { t } = useTranslation();
  return (
    <>
      {state?.settings?.localDashboard?.host === '0.0.0.0' ? <div className="settings-restart-banner"><CircleAlert size={18} /><span>{t('localBindWarning')}</span></div> : null}
      <div className="metric-grid">
        <Metric icon={<Zap size={18} />} label={t('connected')} tone={state?.connection?.isConnected ? 'success' : 'danger'} value={state?.connection?.isConnected ? t('yes') : t('no')} />
        <Metric icon={<Server size={18} />} label={t('registered')} tone={state?.connection?.isRegistered ? 'success' : 'warning'} value={state?.connection?.isRegistered ? t('yes') : t('no')} />
        <Metric icon={<History size={18} />} label={t('requests')} value={statistics?.requests || 0} />
        <Metric icon={<Gauge size={18} />} label={t('failures')} tone={statistics?.failures ? 'danger' : 'neutral'} value={statistics?.failures || 0} />
      </div>
      <Panel className="chart-panel-card" title={t('requestsOverTime')} actions={<button className="icon-button" onClick={refresh} title={t('tooltipManualRefresh')} type="button"><RefreshCw size={16} /></button>}>
        <ActivityChart onRangeChange={setRange} range={range} summary={{ buckets: summary }} />
      </Panel>
      <Panel title={t('connection')}>
        <RecordTable fields={['clientId', 'tenantId', 'webSocketUrl', 'lastDisconnectReason']} record={state?.connection || {}} />
      </Panel>
    </>
  );
}

function Providers({ providers, refresh, setModal }) {
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
    <DataTable
      actions={(row) => [
        { icon: <Play size={14} />, key: 'test', label: t('test'), onSelect: () => test(row), tooltip: t('tooltipTestLocalProvider') },
        { divider: true, key: 'divider-delete' },
        { destructive: true, icon: <Trash2 size={14} />, key: 'delete', label: t('delete'), onSelect: () => remove(row), tooltip: t('tooltipDeleteLocalProvider') }
      ]}
      columns={['name', 'apiType', 'endpoint', 'timeoutMs', 'credential']}
      headerActions={<button className="primary-button" onClick={() => setModal(providerModal(t, null, refresh))} title={t('tooltipCreateLocalProvider')} type="button"><Plus size={16} />{t('createProvider')}</button>}
      onRefresh={refresh}
      onRowClick={(row) => setModal(providerModal(t, row, refresh))}
      rowActionLabel={t('edit')}
      rows={providers}
      title={t('providerEndpoints')}
    />
  );
}

function Models({ models, providerFilter, providers, refresh, setModal, setProviderFilter }) {
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
    <DataTable
      actions={(row) => [
        { destructive: true, icon: <Trash2 size={14} />, key: 'delete', label: t('delete'), onSelect: () => remove(row), tooltip: t('tooltipDeleteLocalModel') }
      ]}
      columns={['name', 'provider', 'apiType', 'supportsCompletions', 'supportsEmbeddings']}
      headerActions={(
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
      onRefresh={refresh}
      onRowClick={(row) => setModal(modelModal(t, row, providers, refresh))}
      rowActionLabel={t('edit')}
      rows={rows}
      title={t('advertisedModels')}
    />
  );
}

function Requests({ range, refresh, requests, setModal, setRange, summary }) {
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
    setModal({ title: t('requestDetails'), json: entry, size: 'request-details', body: <RequestDetails entry={entry} /> });
  }

  return (
    <>
      <Panel className="chart-panel-card" title={t('requestsOverTime')} actions={<button className="icon-button" onClick={refresh} title={t('tooltipManualRefresh')} type="button"><RefreshCw size={16} /></button>}>
        <ActivityChart onRangeChange={setRange} range={range} summary={{ buckets: summary }} />
      </Panel>
      <DataTable
      actions={(row) => [
          { destructive: true, icon: <Trash2 size={14} />, key: 'delete', label: t('delete'), onSelect: () => remove(row), tooltip: t('tooltipDeleteLocalRequest') }
        ]}
        columns={['id', 'receivedUtc', 'method', 'path', 'modelName', 'providerName', 'statusCode', 'outcome']}
        headerActions={<button className="action-button danger icon-only" onClick={clear} title={t('tooltipClearLocalHistory')} type="button"><Trash2 size={16} /><span className="sr-only">{t('clear')}</span></button>}
        onRefresh={refresh}
        onRowClick={details}
        rows={requests}
        title={t('requestHistory')}
      />
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

function DataTable({ actions, columns, compact = false, headerActions, onRefresh, onRowClick, rowActionLabel, rows, title: tableTitle }) {
  const { t } = useTranslation();
  const [query, setQuery] = useState('');
  const [draftFilters, setDraftFilters] = useState({});
  const [filters, setFilters] = useState({});
  const [jsonRow, setJsonRow] = useState(null);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const safeRows = Array.isArray(rows) ? rows : [];
  const filteredRows = useMemo(() => safeRows.filter((row) => {
    const normalizedQuery = query.trim().toLowerCase();
    const matchesQuery = !normalizedQuery || columns.some((column) => String(row[column] ?? '').toLowerCase().includes(normalizedQuery));
    if (!matchesQuery) return false;
    return columns.every((column) => {
      const filter = String(filters[column] || '').trim().toLowerCase();
      if (!filter) return true;
      return String(row[column] || '').toLowerCase().includes(filter);
    });
  }), [columns, filters, query, safeRows]);
  const pageCount = Math.max(1, Math.ceil(filteredRows.length / pageSize));
  const safePage = Math.min(page, pageCount);
  const pageRows = filteredRows.slice((safePage - 1) * pageSize, safePage * pageSize);

  function applyFilter(column) {
    setFilters({ ...filters, [column]: draftFilters[column] || '' });
    setPage(1);
  }

  function handleRowClick(event, row) {
    if (!onRowClick) return;
    const target = event.target instanceof Element ? event.target : event.currentTarget;
    if (target.closest(interactiveRowClickSelector)) return;
    onRowClick(row);
  }

  function rowActions(row) {
    const items = [];
    if (onRowClick) {
      items.push({
        icon: <Pencil size={14} />,
        key: 'open',
        label: rowActionLabel || t('details'),
        onSelect: () => onRowClick(row),
        tooltip: t('tooltipOpenLocalRequestDetails')
      });
    }

    items.push({
      icon: <FileJson size={14} />,
      key: 'json',
      label: t('viewJson'),
      onSelect: () => setJsonRow(row),
      tooltip: t('tooltipViewJson')
    });

    const customItems = actions ? actions(row) : [];
    if (customItems.length > 0) {
      items.push({ divider: true, key: 'custom-divider' });
      items.push(...customItems);
    }

    return items;
  }

  const table = (
    <>
      {!compact ? <Pagination currentPage={safePage} onPageChange={setPage} onPageSizeChange={setPageSize} onRefresh={onRefresh} pageSize={pageSize} totalItems={filteredRows.length} /> : null}
      <div className="table-scroll">
        <table className="data-table">
          <thead>
            <tr>{columns.map((column) => <th key={column}>{title(column, t)}</th>)}{!compact ? <th className="actions-cell">{t('actions')}</th> : null}</tr>
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
                <th />
              </tr>
            ) : null}
          </thead>
          <tbody>
            {pageRows.length === 0 ? (
              <tr>
                <td className="empty-cell" colSpan={columns.length + (compact ? 0 : 1)}>
                  <EmptyState message={t('emptyTableMessage')} title={t('noData')} />
                </td>
              </tr>
            ) : pageRows.map((row) => (
              <tr className={onRowClick ? 'clickable-row' : ''} key={row.id || `${row.provider || ''}:${row.name || row.modelName || row.endpoint}`} onClick={(event) => handleRowClick(event, row)}>
                {columns.map((column) => <td key={column}>{copyColumn(column, row[column]) ? <CopyableValue label={t('copyValue')} value={String(row[column] || '')} /> : display(row[column], t)}</td>)}
                {!compact ? <td className="actions-cell"><ActionMenu items={rowActions(row)} /></td> : null}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {jsonRow ? (
        <Modal headerActions={<CopyButton label={t('copyJson')} value={JSON.stringify(jsonRow, null, 2)} />} onClose={() => setJsonRow(null)} open title={t('recordJson')}>
          <pre className="json-preview"><code>{JSON.stringify(jsonRow, null, 2)}</code></pre>
        </Modal>
      ) : null}
    </>
  );

  if (compact) return table;

  return (
    <section className="workspace-card data-panel">
      <div className="workspace-card-header">
        <div>
          <h2>{tableTitle}</h2>
        </div>
        <div className="table-toolbar">
          <label className="search-box" title={t('tooltipSearchTable')}>
            <Search size={15} />
            <input aria-label={t('search')} onChange={(event) => setQuery(event.target.value)} placeholder={t('search')} title={t('tooltipSearchTable')} value={query} />
          </label>
          <span className="row-count" title={t('showingRange', { start: filteredRows.length === 0 ? 0 : 1, end: filteredRows.length, total: filteredRows.length })}>{filteredRows.length} {t('rows')}</span>
          {headerActions}
        </div>
      </div>
      {table}
    </section>
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

function Panel({ actions, children, className = '', title: panelTitle }) {
  return (
    <section className={`workspace-card ${className}`.trim()}>
      <div className="workspace-card-header">
        <div>
          <h2>{panelTitle}</h2>
        </div>
        {actions ? <div className="workspace-card-actions">{actions}</div> : null}
      </div>
      <div className="workspace-card-body">{children}</div>
    </section>
  );
}

function Metric({ icon, label, tone = 'neutral', value }) {
  return (
    <div className={`metric-card ${tone}`}>
      <div className="metric-icon">{icon}</div>
      <div className="metric-copy">
        <span>{label}</span>
        <strong>{value}</strong>
      </div>
    </div>
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

function localApiRange(range) {
  if (range === 'lastDay') return 'day';
  if (range === 'lastWeek') return 'week';
  if (range === 'lastMonth') return 'month';
  return 'hour';
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

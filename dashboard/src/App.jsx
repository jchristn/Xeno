import { useEffect, useMemo, useState } from 'react';
import { BrowserRouter, NavLink, Route, Routes, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Activity,
  ArrowDown,
  ArrowUp,
  ArrowUpDown,
  BarChart3,
  BrainCircuit,
  ChevronRight,
  CircleDot,
  Cpu,
  Download,
  Gauge,
  Github,
  Globe2,
  History,
  KeyRound,
  Layers3,
  LockKeyhole,
  Network,
  LogOut,
  Moon,
  Pencil,
  Play,
  RefreshCw,
  Search,
  Server,
  Settings as SettingsIcon,
  Shield,
  ShieldCheck,
  Sun,
  Trash2,
  Upload,
  Zap
} from 'lucide-react';
import ApiClient from './utils/api.js';
import ActionMenu from './components/ActionMenu.jsx';
import ConfirmModal from './components/ConfirmModal.jsx';
import CopyButton from './components/CopyButton.jsx';
import CopyableValue from './components/CopyableValue.jsx';
import EmptyState from './components/EmptyState.jsx';
import LanguageSelector from './components/LanguageSelector.jsx';
import ActivityChart from './components/ActivityChart.jsx';
import Modal from './components/Modal.jsx';
import Pagination from './components/Pagination.jsx';

const api = new ApiClient();

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

const navGroups = [
  {
    key: 'operate',
    links: [
      { to: '/dashboard/home', key: 'home', icon: Gauge },
      { to: '/dashboard/requests', key: 'requests', icon: History },
      { to: '/dashboard/explorer', key: 'explorer', icon: Globe2 },
      { to: '/dashboard/analytics', key: 'analytics', icon: BarChart3 }
    ]
  },
  {
    key: 'control',
    links: [
      { to: '/dashboard/model-runners', key: 'modelRunners', icon: Network }
    ]
  },
  {
    key: 'administration',
    links: [
      { to: '/dashboard/tenants', key: 'tenants', icon: Layers3 },
      { to: '/dashboard/users', key: 'users', icon: ShieldCheck },
      { to: '/dashboard/credentials', key: 'credentials', icon: KeyRound },
      { to: '/dashboard/security', key: 'security', icon: LockKeyhole },
      { to: '/dashboard/settings', key: 'settings', icon: SettingsIcon }
    ]
  }
];

const pageMeta = {
  '/dashboard/home': { title: 'home', description: 'homeDescription' },
  '/dashboard/requests': { title: 'requests', description: 'requestsDescription' },
  '/dashboard/explorer': { title: 'explorer', description: 'explorerDescription' },
  '/dashboard/tenants': { title: 'tenants', description: 'tenantsDescription' },
  '/dashboard/users': { title: 'users', description: 'usersDescription' },
  '/dashboard/credentials': { title: 'credentials', description: 'credentialsDescription' },
  '/dashboard/security': { title: 'security', description: 'securityDescription' },
  '/dashboard/model-runners': { title: 'modelRunners', description: 'modelRunnersDescription' },
  '/dashboard/clients': { title: 'modelRunners', description: 'modelRunnersDescription' },
  '/dashboard/analytics': { title: 'analytics', description: 'analyticsDescription' },
  '/dashboard/settings': { title: 'settings', description: 'settingsDescription' }
};

function Shell({ onLogout, onToggleTheme, theme }) {
  const { t } = useTranslation();
  const location = useLocation();
  const meta = pageMeta[location.pathname] || pageMeta['/dashboard/home'];

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <div className="sidebar-brand">
          <img className="sidebar-logo" src="/logo.png" alt={t('appTitle')} />
          <div>
            <strong>{t('appTitle')}</strong>
          </div>
        </div>
        <div className="release-badge">
          <CircleDot size={12} />
          <span>{t('alphaRelease')}</span>
        </div>
        <nav className="sidebar-nav" aria-label={t('dashboard')}>
          {navGroups.map((group) => (
            <div className="sidebar-group" key={group.key}>
              <div className="sidebar-group-label">{t(group.key)}</div>
              {group.links.map((link) => {
                const Icon = link.icon;
                return (
                  <NavLink className="sidebar-link" key={link.to} to={link.to}>
                    <Icon size={17} />
                    <span>{t(link.key)}</span>
                    <ChevronRight className="sidebar-link-arrow" size={14} />
                  </NavLink>
                );
              })}
            </div>
          ))}
        </nav>
        <div className="sidebar-footer">
          <span>{t('version')}</span>
          <strong>v0.1.0</strong>
        </div>
      </aside>

      <div className="content-shell">
        <header className="topbar">
          <div className="topbar-title">
            <h1>{t(meta.title)}</h1>
            <p>{t(meta.description)}</p>
          </div>
          <div className="topbar-actions">
            <span className="topbar-server-url" title={api.baseUrl}>
              <Server size={15} />
              <span>{api.baseUrl}</span>
              <CopyButton label={t('copyServerUrl')} value={api.baseUrl} />
            </span>
            <LanguageSelector />
            <a
              aria-label={t('github')}
              className="icon-button"
              href="https://github.com/jchristn/Xeno"
              rel="noreferrer"
              target="_blank"
              title={t('github')}
            >
              <Github size={17} />
            </a>
            <button aria-label={t('toggleTheme')} className="icon-button" onClick={onToggleTheme} title={t('toggleTheme')} type="button">
              {theme === 'dark' ? <Sun size={17} /> : <Moon size={17} />}
            </button>
            <button aria-label={t('logout')} className="icon-button danger" onClick={onLogout} title={t('logout')} type="button">
              <LogOut size={17} />
            </button>
          </div>
        </header>

        <main className="workspace">
          <Routes>
            <Route index element={<HomeView />} />
            <Route path="home" element={<HomeView />} />
            <Route path="requests" element={<RequestHistoryView />} />
            <Route path="explorer" element={<ApiExplorerView />} />
            <Route path="tenants" element={<TenantsView />} />
            <Route path="users" element={<UsersView />} />
            <Route path="credentials" element={<CredentialsView />} />
            <Route path="security" element={<SecurityView />} />
            <Route path="clients" element={<ModelRunnersView />} />
            <Route path="model-runners" element={<ModelRunnersView />} />
            <Route path="analytics" element={<AnalyticsView />} />
            <Route path="settings" element={<SettingsView />} />
            <Route path="*" element={<HomeView />} />
          </Routes>
        </main>
      </div>
    </div>
  );
}

function LoginView({ onToggleTheme, theme }) {
  const { t } = useTranslation();

  return (
    <div className="login-view">
      <div className="login-shell">
        <section className="login-brand-panel">
          <img className="login-brand-logo" src="/logo.png" alt={t('appTitle')} />
          <span className="eyebrow">{t('alphaRelease')}</span>
          <h1>{t('appTitle')}</h1>
          <p>{t('loginSubtitle')}</p>
          <div className="login-signal-grid">
            <span><Cpu size={16} />{t('advertisedHardware')}</span>
            <span><BrainCircuit size={16} />{t('distributedModels')}</span>
            <span><ShieldCheck size={16} />{t('credentialGated')}</span>
          </div>
        </section>

        <section className="login-card">
          <div className="login-card-actions">
            <a aria-label={t('github')} className="icon-button" href="https://github.com/jchristn/Xeno" rel="noreferrer" target="_blank" title={t('github')}>
              <Github size={17} />
            </a>
            <button aria-label={t('toggleTheme')} className="icon-button" onClick={onToggleTheme} title={t('toggleTheme')} type="button">
              {theme === 'dark' ? <Sun size={17} /> : <Moon size={17} />}
            </button>
          </div>
          <div className="login-card-header">
            <img className="login-logo" src="/logo.png" alt={t('appTitle')} />
            <div>
              <span className="eyebrow">{t('dashboard')}</span>
              <h2>{t('signInToDashboard')}</h2>
            </div>
          </div>
          <button className="primary-button" onClick={() => { window.location.href = '/dashboard/home'; }} type="button">
            <Shield size={16} />
            <span>{t('login')}</span>
          </button>
          <LanguageSelector />
          <div className="default-credentials">
            <span>{t('defaultCredentials')}</span>
            <code>{t('defaultUsername')}</code>
            <code>{t('defaultPassword')}</code>
          </div>
        </section>
      </div>
    </div>
  );
}

function HomeView() {
  const { t } = useTranslation();
  const analytics = useResource('/api/v1/analytics/overview', { total: 0, success: 0, failures: 0, failureRate: 0 });
  const summary = useResource('/api/v1/requesthistory/summary', { buckets: [] });
  const clients = useResource('/api/v1/clients', []);
  const endpoints = useResource('/api/v1/modelrunnerendpoints', []);
  const models = useResource('/api/v1/modeldefinitions', []);
  const connected = clients.data.filter((client) => value(client, 'connected') === true).length;
  const activeEndpoints = endpoints.data.filter((endpoint) => isHealthyState(value(endpoint, 'serviceState'))).length;
  const completionModels = models.data.filter((model) => value(model, 'supportsCompletions') === true).length;

  function refreshAll() {
    analytics.refresh();
    summary.refresh();
    clients.refresh();
    endpoints.refresh();
    models.refresh();
  }

  return (
    <div className="view-stack">
      <div className="metric-grid">
        <MetricCard icon={<History size={18} />} label={t('requestVolume')} value={formatNumber(analytics.data.total)} subtext={t('total')} />
        <MetricCard icon={<Zap size={18} />} label={t('success')} value={formatNumber(analytics.data.success)} subtext={formatPercent(1 - Number(analytics.data.failureRate || 0))} tone="success" />
        <MetricCard icon={<Shield size={18} />} label={t('failed')} value={formatNumber(analytics.data.failures)} subtext={formatPercent(Number(analytics.data.failureRate || 0))} tone="danger" />
        <MetricCard icon={<Network size={18} />} label={t('connectedClients')} value={formatNumber(connected)} subtext={t('clients')} tone="info" />
        <MetricCard icon={<Server size={18} />} label={t('activeEndpoints')} value={formatNumber(activeEndpoints)} subtext={t('endpoints')} tone="success" />
        <MetricCard icon={<BrainCircuit size={18} />} label={t('activeModels')} value={formatNumber(completionModels)} subtext={t('models')} tone="neutral" />
      </div>

      <Panel
        actions={<IconButton iconOnly onClick={refreshAll} label={t('refresh')} icon={<RefreshCw size={16} />} />}
        title={t('requestActivity')}
      >
        <ActivityChart summary={summary.data} />
      </Panel>

      <DataTable
        columns={['name', 'provider', 'serviceState', 'inFlightRequests', 'maxParallelRequests']}
        onRefresh={endpoints.refresh}
        rows={endpoints.data}
        subtitle={t('endpointSubtitle')}
        title={t('endpoints')}
      />
    </div>
  );
}

function RequestHistoryView() {
  const { t } = useTranslation();
  const history = useResource('/api/v1/requesthistory', []);
  const summary = useResource('/api/v1/requesthistory/summary', { buckets: [] });
  const [selected, setSelected] = useState(null);
  const [confirmClear, setConfirmClear] = useState(false);
  const bucketTotals = summarizeBuckets(summary.data.buckets || []);

  async function clearHistory() {
    await api.post('/api/v1/requesthistory/bulk', {});
    history.refresh();
    summary.refresh();
    setConfirmClear(false);
  }

  return (
    <div className="view-stack">
      <div className="metric-grid compact">
        <MetricCard icon={<Activity size={18} />} label={t('success')} value={formatNumber(bucketTotals.success)} tone="success" />
        <MetricCard icon={<Shield size={18} />} label={t('failed')} value={formatNumber(bucketTotals.failed)} tone="danger" />
        <MetricCard icon={<Gauge size={18} />} label={t('total')} value={formatNumber(bucketTotals.total)} />
      </div>
      <Panel
        actions={(
          <div className="button-row">
            <IconButton iconOnly onClick={() => { history.refresh(); summary.refresh(); }} label={t('refresh')} icon={<RefreshCw size={16} />} />
            <IconButton iconOnly onClick={() => setConfirmClear(true)} label={t('clear')} icon={<Trash2 size={16} />} variant="danger" />
          </div>
        )}
        title={t('requestActivity')}
      >
        <ActivityChart summary={summary.data} />
      </Panel>
      <DataTable
        columns={['statusCode', 'method', 'path', 'model', 'durationMs', 'createdUtc']}
        onRowClick={setSelected}
        onRefresh={() => { history.refresh(); summary.refresh(); }}
        rows={history.data}
        subtitle={t('requestHistorySubtitle')}
        title={t('requests')}
      />
      {selected && <EditRecordModal title={t('editRequest')} data={selected} onClose={() => setSelected(null)} />}
      <ConfirmModal
        confirmLabel={t('clear')}
        destructive
        message={t('clearHistoryMessage')}
        onCancel={() => setConfirmClear(false)}
        onConfirm={clearHistory}
        open={confirmClear}
        title={t('clearHistoryTitle')}
      />
    </div>
  );
}

function ApiExplorerView() {
  const { t } = useTranslation();
  const operations = [
    { key: 'chatCompletions', method: 'POST', path: '/v1/chat/completions' },
    { key: 'completions', method: 'POST', path: '/v1/completions' },
    { key: 'embeddings', method: 'POST', path: '/v1/embeddings' },
    { key: 'listModels', method: 'GET', path: '/v1/models' }
  ];
  const [operation, setOperation] = useState('chatCompletions');
  const [model, setModel] = useState('llama3.2:latest');
  const [prompt, setPrompt] = useState('hello');
  const [input, setInput] = useState('hello');
  const [maxTokens, setMaxTokens] = useState(128);
  const [request, setRequest] = useState(null);
  const [response, setResponse] = useState(null);
  const selected = operations.find((item) => item.key === operation) || operations[0];

  function buildRequestBody() {
    if (operation === 'chatCompletions') {
      return { model, messages: [{ role: 'user', content: prompt }], stream: false, max_tokens: Number(maxTokens) || 128 };
    }
    if (operation === 'completions') {
      return { model, prompt, stream: false, max_tokens: Number(maxTokens) || 128 };
    }
    if (operation === 'embeddings') {
      return { model, input };
    }
    return null;
  }

  async function execute() {
    const body = buildRequestBody();
    setRequest({ method: selected.method, path: selected.path, body });
    const data = selected.method === 'GET'
      ? await api.get(selected.path)
      : await api.post(selected.path, body);
    setResponse(data);
  }

  return (
    <div className="view-stack">
      <Panel
        actions={<IconButton onClick={execute} label={t('execute')} icon={<Play size={16} />} variant="primary" />}
        title={t('requestBuilder')}
      >
        <div className="form-stack">
          <div className="segmented-control" role="tablist" aria-label={t('operation')}>
            {operations.map((item) => (
              <button
                aria-selected={item.key === operation}
                className={item.key === operation ? 'active' : ''}
                key={item.key}
                onClick={() => setOperation(item.key)}
                role="tab"
                type="button"
              >
                {t(item.key)}
              </button>
            ))}
          </div>
          <div className="route-preview">
            <span className="status-chip neutral">{selected.method}</span>
            <code>{selected.path}</code>
            <span className="muted-copy">{api.baseUrl}</span>
          </div>
          {operation !== 'listModels' ? (
            <div className="form-grid two">
              <label className="form-field">
                <span>{t('model')}</span>
                <input value={model} onChange={(event) => setModel(event.target.value)} />
              </label>
              {operation !== 'embeddings' ? (
                <label className="form-field">
                  <span>{t('maxTokens')}</span>
                  <input min="1" type="number" value={maxTokens} onChange={(event) => setMaxTokens(event.target.value)} />
                </label>
              ) : null}
            </div>
          ) : null}
          {operation === 'embeddings' ? (
            <label className="form-field">
              <span>{t('input')}</span>
              <textarea rows={5} value={input} onChange={(event) => setInput(event.target.value)} />
            </label>
          ) : operation !== 'listModels' ? (
            <label className="form-field">
              <span>{t('prompt')}</span>
              <textarea rows={5} value={prompt} onChange={(event) => setPrompt(event.target.value)} />
            </label>
          ) : null}
        </div>
      </Panel>
      <div className="workspace-grid explorer-grid">
        <JsonPanelInline title={t('request')} data={request || { method: selected.method, path: selected.path, body: buildRequestBody() }} />
        <JsonPanelInline title={t('response')} data={response || { status: t('noData') }} />
      </div>
    </div>
  );
}

function SecurityView() {
  const { t } = useTranslation();
  const roles = useResource('/api/v1/roles', []);
  const permissions = useResource('/api/v1/permissions', []);
  const effective = useResource('/api/v1/effective-permissions', []);
  const audit = useResource('/api/v1/audit', []);

  return (
    <div className="view-stack">
      <div className="metric-grid compact">
        <MetricCard icon={<LockKeyhole size={18} />} label={t('roles')} value={formatNumber(roles.data.length)} />
        <MetricCard icon={<ShieldCheck size={18} />} label={t('permissions')} value={formatNumber(permissions.data.length)} />
        <MetricCard icon={<Activity size={18} />} label={t('audit')} value={formatNumber(audit.data.length)} tone="info" />
      </div>
      <DataTable title={t('roles')} rows={roles.data} columns={['name', 'builtIn', 'active']} onRefresh={roles.refresh} />
      <DataTable title={t('permissions')} rows={permissions.data} columns={['resourceType', 'operationType', 'permit']} onRefresh={permissions.refresh} />
      <DataTable title={t('effectivePermissions')} rows={effective.data} columns={['resourceType', 'operationType', 'permit', 'sourceRole']} onRefresh={effective.refresh} />
      <DataTable
        columns={['createdUtc', 'resourceType', 'operationType', 'reasonCode', 'statusCode']}
        onRefresh={audit.refresh}
        rows={audit.data}
        subtitle={t('auditSubtitle')}
        title={t('audit')}
      />
    </div>
  );
}

function TenantsView() {
  const { t } = useTranslation();
  const tenants = useResource('/api/v1/tenants', []);
  const [selected, setSelected] = useState(null);

  return (
    <div className="view-stack">
      <DataTable
        columns={['id', 'name', 'active', 'createdUtc', 'lastUpdateUtc']}
        onRowClick={setSelected}
        onRefresh={tenants.refresh}
        rows={tenants.data}
        subtitle={t('tenantsTableSubtitle')}
        title={t('tenants')}
      />
      {selected && <EditRecordModal title={t('editTenant')} data={selected} onClose={() => setSelected(null)} />}
    </div>
  );
}

function UsersView() {
  const { t } = useTranslation();
  const users = useResource('/api/v1/users', []);
  const [selected, setSelected] = useState(null);

  return (
    <div className="view-stack">
      <DataTable
        columns={['id', 'tenantId', 'username', 'email', 'isAdmin', 'isTenantAdmin', 'active']}
        onRowClick={setSelected}
        onRefresh={users.refresh}
        rows={users.data}
        subtitle={t('usersTableSubtitle')}
        title={t('users')}
      />
      {selected && <EditRecordModal title={t('editUser')} data={selected} onClose={() => setSelected(null)} />}
    </div>
  );
}

function CredentialsView() {
  const { t } = useTranslation();
  const credentials = useResource('/api/v1/credentials', []);
  const [selected, setSelected] = useState(null);
  const [created, setCreated] = useState(null);

  async function createCredential() {
    const result = await api.post('/api/v1/credentials', {});
    setCreated(result);
    credentials.refresh();
  }

  return (
    <div className="view-stack">
      <div className="toolbar align-end">
        <IconButton onClick={createCredential} label={t('createCredential')} icon={<KeyRound size={16} />} variant="primary" />
      </div>
      {created ? <JsonPanelInline title={t('newCredential')} data={created} /> : null}
      <DataTable
        columns={['id', 'tenantId', 'userId', 'accessKey', 'secretKey', 'active']}
        onRowClick={setSelected}
        onRefresh={credentials.refresh}
        rows={credentials.data}
        subtitle={t('credentialsTableSubtitle')}
        title={t('credentials')}
      />
      {selected && <EditRecordModal title={t('editCredential')} data={selected} onClose={() => setSelected(null)} />}
    </div>
  );
}

function ModelRunnersView() {
  const { t } = useTranslation();
  const clients = useResource('/api/v1/clients', []);
  const endpoints = useResource('/api/v1/modelrunnerendpoints', []);
  const models = useResource('/api/v1/modeldefinitions', []);
  const connected = clients.data.filter((client) => value(client, 'connected') === true).length;

  return (
    <div className="view-stack">
      <div className="metric-grid compact">
        <MetricCard icon={<Network size={18} />} label={t('connectedClients')} value={`${connected} / ${clients.data.length}`} tone={connected ? 'success' : 'warning'} />
        <MetricCard icon={<Server size={18} />} label={t('endpoints')} value={formatNumber(endpoints.data.length)} />
        <MetricCard icon={<BrainCircuit size={18} />} label={t('models')} value={formatNumber(models.data.length)} />
      </div>
      <DataTable title={t('modelRunners')} rows={clients.data} columns={['name', 'connected', 'lastHeartbeatUtc']} onRefresh={clients.refresh} />
      <DataTable title={t('endpoints')} rows={endpoints.data} columns={['name', 'provider', 'serviceState', 'maxParallelRequests']} onRefresh={endpoints.refresh} />
      <DataTable title={t('models')} rows={models.data} columns={['name', 'supportsCompletions', 'supportsEmbeddings']} onRefresh={models.refresh} />
    </div>
  );
}

function AnalyticsView() {
  const { t } = useTranslation();
  const analytics = useResource('/api/v1/analytics/overview', { total: 0, success: 0, failures: 0, failureRate: 0 });
  const summary = useResource('/api/v1/requesthistory/summary', { buckets: [] });
  const history = useResource('/api/v1/requesthistory', []);
  const metrics = useTextResource('/metrics', '');
  const byModel = groupByCount(history.data, 'model');
  const byPath = groupByCount(history.data, 'path');

  return (
    <div className="view-stack">
      <div className="metric-grid compact">
        <MetricCard icon={<Activity size={18} />} label={t('total')} value={formatNumber(analytics.data.total)} />
        <MetricCard icon={<Zap size={18} />} label={t('success')} value={formatNumber(analytics.data.success)} tone="success" />
        <MetricCard icon={<Shield size={18} />} label={t('failed')} value={formatNumber(analytics.data.failures)} tone="danger" />
        <MetricCard icon={<Gauge size={18} />} label={t('failureRate')} value={formatPercent(Number(analytics.data.failureRate || 0))} tone={Number(analytics.data.failureRate || 0) > 0 ? 'warning' : 'success'} />
      </div>
      <Panel
        actions={<IconButton iconOnly onClick={() => { analytics.refresh(); summary.refresh(); history.refresh(); metrics.refresh(); }} label={t('refresh')} icon={<RefreshCw size={16} />} />}
        title={t('requestActivity')}
      >
        <ActivityChart summary={summary.data} />
      </Panel>
      <div className="workspace-grid">
        <DataTable title={t('requestsByModel')} rows={byModel} columns={['name', 'count', 'success', 'failed']} onRefresh={history.refresh} />
        <DataTable title={t('requestsByPath')} rows={byPath} columns={['name', 'count', 'success', 'failed']} onRefresh={history.refresh} />
      </div>
      <Panel
        actions={<IconButton iconOnly onClick={metrics.refresh} label={t('refresh')} icon={<RefreshCw size={16} />} />}
        title={t('prometheusMetrics')}
      >
        <pre className="metrics-output">{metrics.data || t('noData')}</pre>
      </Panel>
    </div>
  );
}

function SettingsView() {
  const { t } = useTranslation();
  const settings = useResource('/api/v1/settings', defaultServerSettings());
  const [draft, setDraft] = useState(defaultServerSettings());
  const [configText, setConfigText] = useState('');
  const [backup, setBackup] = useState('');
  const [status, setStatus] = useState('');

  useEffect(() => {
    const normalized = normalizeSettings(unwrapSettings(settings.data));
    setDraft(normalized);
    setConfigText(JSON.stringify(normalized, null, 2));
  }, [settings.data]);

  function updateSetting(section, key, inputValue) {
    setDraft((current) => ({
      ...current,
      [section]: {
        ...current[section],
        [key]: inputValue
      }
    }));
  }

  function updateListSetting(section, key, inputValue) {
    updateSetting(section, key, inputValue.split(/\r?\n|,/).map((item) => item.trim()).filter(Boolean));
  }

  async function saveSettings() {
    const result = await api.post('/api/v1/settings', draft);
    const normalized = normalizeSettings(unwrapSettings(result));
    setDraft(normalized);
    setConfigText(JSON.stringify(normalized, null, 2));
    settings.refresh();
    setStatus(t('settingsSaved'));
  }

  async function saveJsonSettings() {
    const parsed = JSON.parse(configText || '{}');
    const result = await api.post('/api/v1/settings', parsed);
    const normalized = normalizeSettings(unwrapSettings(result));
    setDraft(normalized);
    setConfigText(JSON.stringify(normalized, null, 2));
    settings.refresh();
    setStatus(t('settingsSaved'));
  }

  async function downloadBackup() {
    setBackup(JSON.stringify(await api.get('/api/v1/backup'), null, 2));
    setStatus(t('backupDownloaded'));
  }

  async function restoreBackup() {
    await api.post('/api/v1/restore', JSON.parse(backup || '{}'));
    setStatus(t('restoreComplete'));
  }

  return (
    <div className="view-stack">
      <div className="toolbar align-end">
        <LanguageSelector />
        <IconButton iconOnly onClick={settings.refresh} label={t('refresh')} icon={<RefreshCw size={16} />} />
        <IconButton onClick={saveSettings} label={t('saveSettings')} icon={<Upload size={16} />} variant="primary" />
      </div>
      {status ? <p className="muted-copy">{status}</p> : null}
      <div className="workspace-grid settings-grid">
        <Panel title={t('restSettings')}>
          <div className="form-grid two">
            <label className="form-field">
              <span>{t('hostname')}</span>
              <input value={draft.Rest.Hostname} onChange={(event) => updateSetting('Rest', 'Hostname', event.target.value)} />
            </label>
            <label className="form-field">
              <span>{t('port')}</span>
              <input min="1" type="number" value={draft.Rest.Port} onChange={(event) => updateSetting('Rest', 'Port', Number(event.target.value))} />
            </label>
            <label className="toggle-field">
              <input checked={draft.Rest.Ssl} onChange={(event) => updateSetting('Rest', 'Ssl', event.target.checked)} type="checkbox" />
              <span>{t('ssl')}</span>
            </label>
            <label className="toggle-field">
              <input checked={draft.Rest.RequireAuthentication} onChange={(event) => updateSetting('Rest', 'RequireAuthentication', event.target.checked)} type="checkbox" />
              <span>{t('requireAuthentication')}</span>
            </label>
          </div>
        </Panel>
        <Panel title={t('corsSettings')}>
          <div className="form-stack">
            <label className="toggle-field">
              <input checked={draft.Cors.Enabled} onChange={(event) => updateSetting('Cors', 'Enabled', event.target.checked)} type="checkbox" />
              <span>{t('enabled')}</span>
            </label>
            <label className="form-field">
              <span>{t('allowedOrigins')}</span>
              <textarea rows={4} value={listToText(draft.Cors.AllowedOrigins)} onChange={(event) => updateListSetting('Cors', 'AllowedOrigins', event.target.value)} />
            </label>
            <div className="form-grid two">
              <label className="form-field">
                <span>{t('allowedHeaders')}</span>
                <textarea rows={4} value={listToText(draft.Cors.AllowedHeaders)} onChange={(event) => updateListSetting('Cors', 'AllowedHeaders', event.target.value)} />
              </label>
              <label className="form-field">
                <span>{t('allowedMethods')}</span>
                <textarea rows={4} value={listToText(draft.Cors.AllowedMethods)} onChange={(event) => updateListSetting('Cors', 'AllowedMethods', event.target.value)} />
              </label>
            </div>
          </div>
        </Panel>
        <Panel title={t('webSocketSettings')}>
          <div className="form-grid two">
            <label className="toggle-field">
              <input checked={draft.WebSocket.Enabled} onChange={(event) => updateSetting('WebSocket', 'Enabled', event.target.checked)} type="checkbox" />
              <span>{t('enabled')}</span>
            </label>
            <label className="form-field">
              <span>{t('path')}</span>
              <input value={draft.WebSocket.Path} onChange={(event) => updateSetting('WebSocket', 'Path', event.target.value)} />
            </label>
            <label className="form-field">
              <span>{t('heartbeatIntervalMs')}</span>
              <input min="1000" type="number" value={draft.WebSocket.HeartbeatIntervalMs} onChange={(event) => updateSetting('WebSocket', 'HeartbeatIntervalMs', Number(event.target.value))} />
            </label>
            <label className="form-field">
              <span>{t('requestTimeoutMs')}</span>
              <input min="1000" type="number" value={draft.WebSocket.RequestTimeoutMs} onChange={(event) => updateSetting('WebSocket', 'RequestTimeoutMs', Number(event.target.value))} />
            </label>
          </div>
        </Panel>
        <Panel title={t('databaseSettings')}>
          <div className="form-grid two">
            <label className="form-field">
              <span>{t('provider')}</span>
              <select value={draft.Database.Type} onChange={(event) => updateSetting('Database', 'Type', event.target.value)}>
                {['Sqlite', 'Postgresql', 'SqlServer', 'Mysql'].map((item) => <option key={item} value={item}>{item}</option>)}
              </select>
            </label>
            <label className="form-field">
              <span>{t('filename')}</span>
              <input value={draft.Database.Filename} onChange={(event) => updateSetting('Database', 'Filename', event.target.value)} />
            </label>
            <label className="form-field">
              <span>{t('server')}</span>
              <input value={draft.Database.Server || ''} onChange={(event) => updateSetting('Database', 'Server', event.target.value)} />
            </label>
            <label className="form-field">
              <span>{t('port')}</span>
              <input min="0" type="number" value={draft.Database.Port} onChange={(event) => updateSetting('Database', 'Port', Number(event.target.value))} />
            </label>
            <label className="form-field">
              <span>{t('databaseName')}</span>
              <input value={draft.Database.DatabaseName || ''} onChange={(event) => updateSetting('Database', 'DatabaseName', event.target.value)} />
            </label>
            <label className="form-field">
              <span>{t('username')}</span>
              <input value={draft.Database.Username || ''} onChange={(event) => updateSetting('Database', 'Username', event.target.value)} />
            </label>
            <label className="form-field">
              <span>{t('password')}</span>
              <input type="password" value={draft.Database.Password || ''} onChange={(event) => updateSetting('Database', 'Password', event.target.value)} />
            </label>
            <label className="form-field">
              <span>{t('connectionString')}</span>
              <input value={draft.Database.ConnectionString || ''} onChange={(event) => updateSetting('Database', 'ConnectionString', event.target.value)} />
            </label>
          </div>
        </Panel>
        <Panel title={t('loggingSettings')}>
          <div className="form-grid two">
            <label className="toggle-field">
              <input checked={draft.Logging.ConsoleLogging} onChange={(event) => updateSetting('Logging', 'ConsoleLogging', event.target.checked)} type="checkbox" />
              <span>{t('consoleLogging')}</span>
            </label>
            <label className="toggle-field">
              <input checked={draft.Logging.FileLogging} onChange={(event) => updateSetting('Logging', 'FileLogging', event.target.checked)} type="checkbox" />
              <span>{t('fileLogging')}</span>
            </label>
            <label className="form-field">
              <span>{t('logDirectory')}</span>
              <input value={draft.Logging.LogDirectory} onChange={(event) => updateSetting('Logging', 'LogDirectory', event.target.value)} />
            </label>
            <label className="form-field">
              <span>{t('logFilename')}</span>
              <input value={draft.Logging.LogFilename} onChange={(event) => updateSetting('Logging', 'LogFilename', event.target.value)} />
            </label>
          </div>
        </Panel>
        <Panel title={t('backupRestore')}>
          <div className="button-row">
            <IconButton onClick={downloadBackup} label={t('backup')} icon={<Download size={16} />} />
            <IconButton onClick={restoreBackup} label={t('restore')} icon={<Upload size={16} />} variant="primary" />
          </div>
          <textarea className="backup-editor" value={backup} onChange={(event) => setBackup(event.target.value)} aria-label={t('backup')} />
        </Panel>
      </div>
      <Panel
        actions={<IconButton onClick={saveJsonSettings} label={t('saveJson')} icon={<Upload size={16} />} variant="primary" />}
        title={t('rawConfigJson')}
      >
        <textarea className="backup-editor config-editor" value={configText} onChange={(event) => setConfigText(event.target.value)} aria-label={t('rawConfigJson')} />
      </Panel>
    </div>
  );
}

function useResource(path, fallback) {
  const [data, setData] = useState(fallback);
  const [version, setVersion] = useState(0);

  useEffect(() => {
    let alive = true;
    api.get(path)
      .then((result) => { if (alive) setData(result); })
      .catch(() => { if (alive) setData(fallback); });
    return () => { alive = false; };
  }, [path, version]);

  return { data, refresh: () => setVersion((current) => current + 1) };
}

function useTextResource(path, fallback) {
  const [data, setData] = useState(fallback);
  const [version, setVersion] = useState(0);

  useEffect(() => {
    let alive = true;
    api.text(path)
      .then((result) => { if (alive) setData(result); })
      .catch(() => { if (alive) setData(fallback); });
    return () => { alive = false; };
  }, [path, version]);

  return { data, refresh: () => setVersion((current) => current + 1) };
}

function defaultServerSettings() {
  return {
    Rest: { Hostname: '0.0.0.0', Port: 9000, Ssl: false, RequireAuthentication: false },
    Cors: {
      Enabled: true,
      AllowedOrigins: ['http://localhost:9100'],
      AllowedHeaders: ['Authorization', 'Content-Type', 'x-token', 'x-tenant-guid'],
      AllowedMethods: ['GET', 'POST', 'PUT', 'DELETE', 'OPTIONS']
    },
    WebSocket: { Enabled: true, Path: '/ws/v1/clients', HeartbeatIntervalMs: 15000, RequestTimeoutMs: 120000 },
    Database: { Type: 'Sqlite', Filename: 'data/xeno.db', Server: '', Port: 0, DatabaseName: '', Username: '', Password: '', ConnectionString: '' },
    Logging: { ConsoleLogging: true, FileLogging: true, LogDirectory: 'logs', LogFilename: 'xeno.log' }
  };
}

function unwrapSettings(input) {
  return input?.settings || input?.Settings || input || {};
}

function normalizeSettings(input) {
  const defaults = defaultServerSettings();
  const source = input || {};
  return {
    ...source,
    Rest: { ...defaults.Rest, ...(source.Rest || source.rest || {}) },
    Cors: { ...defaults.Cors, ...(source.Cors || source.cors || {}) },
    WebSocket: { ...defaults.WebSocket, ...(source.WebSocket || source.webSocket || source.websocket || {}) },
    Database: { ...defaults.Database, ...(source.Database || source.database || {}) },
    Logging: { ...defaults.Logging, ...(source.Logging || source.logging || {}) }
  };
}

function listToText(input) {
  return Array.isArray(input) ? input.join('\n') : '';
}

function MetricCard({ icon, label, subtext, tone = 'neutral', value }) {
  return (
    <div className={`metric-card ${tone}`}>
      <div className="metric-icon">{icon}</div>
      <div className="metric-copy">
        <span>{label}</span>
        <strong>{value ?? '-'}</strong>
        {subtext ? <small>{subtext}</small> : null}
      </div>
    </div>
  );
}

function Panel({ actions, children, eyebrow, title }) {
  return (
    <section className="workspace-card">
      <div className="workspace-card-header">
        <div>
          {eyebrow ? <span className="eyebrow">{eyebrow}</span> : null}
          <h2>{title}</h2>
        </div>
        {actions ? <div className="workspace-card-actions">{actions}</div> : null}
      </div>
      <div className="workspace-card-body">{children}</div>
    </section>
  );
}

function IconButton({ icon, iconOnly = false, label, onClick, variant = 'secondary' }) {
  return (
    <button className={`action-button ${variant} ${iconOnly ? 'icon-only' : ''}`} onClick={onClick} title={label} type="button">
      {icon}
      <span className={iconOnly ? 'sr-only' : ''}>{label}</span>
    </button>
  );
}

function DataTable({ actions, columns, onRefresh, onRowClick, rows, subtitle, title }) {
  const { t } = useTranslation();
  const [query, setQuery] = useState('');
  const [columnFilters, setColumnFilters] = useState({});
  const [sort, setSort] = useState({ key: '', direction: 'asc' });
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const safeRows = Array.isArray(rows) ? rows : [];
  const normalizedColumns = columns.map((column) => (typeof column === 'string' ? { key: column } : column));
  const filteredRows = useMemo(() => {
    const normalizedQuery = query.trim().toLowerCase();
    return safeRows.filter((row) => {
      const matchesGlobal = !normalizedQuery || normalizedColumns.some((column) => {
        const raw = column.searchValue ? column.searchValue(row) : value(row, column.key);
        return String(raw ?? '').toLowerCase().includes(normalizedQuery);
      });
      if (!matchesGlobal) return false;

      return normalizedColumns.every((column) => {
        const filter = String(columnFilters[column.key] || '').trim().toLowerCase();
        if (!filter) return true;
        const raw = column.searchValue ? column.searchValue(row) : value(row, column.key);
        return String(raw ?? '').toLowerCase().includes(filter);
      });
    });
  }, [columnFilters, normalizedColumns, query, safeRows]);
  const sortedRows = useMemo(() => {
    if (!sort.key) return filteredRows;
    const column = normalizedColumns.find((item) => item.key === sort.key);
    return [...filteredRows].sort((left, right) => {
      const leftValue = column?.sortValue ? column.sortValue(left) : value(left, sort.key);
      const rightValue = column?.sortValue ? column.sortValue(right) : value(right, sort.key);
      if (leftValue === undefined || leftValue === null) return 1;
      if (rightValue === undefined || rightValue === null) return -1;
      const comparison = String(leftValue).localeCompare(String(rightValue), undefined, { numeric: true, sensitivity: 'base' });
      return sort.direction === 'asc' ? comparison : -comparison;
    });
  }, [filteredRows, normalizedColumns, sort]);
  const totalPages = Math.max(1, Math.ceil(sortedRows.length / pageSize));
  const displayPage = Math.min(currentPage, totalPages);
  const pagedRows = sortedRows.slice((displayPage - 1) * pageSize, displayPage * pageSize);
  const hasColumnFilters = normalizedColumns.some((column) => column.filterable !== false);

  useEffect(() => {
    setCurrentPage(1);
  }, [columnFilters, pageSize, query]);

  function handleSort(column) {
    if (column.sortable === false) return;
    setSort((current) => {
      if (current.key !== column.key) return { key: column.key, direction: 'asc' };
      if (current.direction === 'asc') return { key: column.key, direction: 'desc' };
      return { key: '', direction: 'asc' };
    });
  }

  function renderSortIcon(column) {
    if (column.sortable === false) return null;
    if (sort.key !== column.key) return <ArrowUpDown size={13} />;
    return sort.direction === 'asc' ? <ArrowUp size={13} /> : <ArrowDown size={13} />;
  }

  function handleRowClick(event, row) {
    if (!onRowClick) return;
    const target = event.target instanceof Element ? event.target : event.currentTarget;
    if (target.closest(interactiveRowClickSelector)) return;
    onRowClick(row);
  }

  return (
    <section className="workspace-card data-panel">
      <div className="workspace-card-header">
        <div>
          <span className="eyebrow">{t('dataSet')}</span>
          <h2>{title}</h2>
          {subtitle ? <p>{subtitle}</p> : null}
        </div>
        <div className="table-toolbar">
          <label className="search-box">
            <Search size={15} />
            <input aria-label={t('search')} onChange={(event) => setQuery(event.target.value)} placeholder={t('search')} value={query} />
          </label>
          <span className="row-count">{filteredRows.length} {t('rows')}</span>
          {actions}
        </div>
      </div>
      <Pagination
        currentPage={displayPage}
        onPageChange={setCurrentPage}
        onPageSizeChange={setPageSize}
        onRefresh={onRefresh}
        pageSize={pageSize}
        totalItems={sortedRows.length}
      />
      <div className="table-scroll">
        <table className="data-table">
          <thead>
            <tr>
              {normalizedColumns.map((column) => (
                <th className={column.sortable === false ? '' : 'sortable'} key={column.key} onClick={() => handleSort(column)}>
                  <span className="th-content">
                    <span>{t(column.label || column.key, column.label || column.key)}</span>
                    {renderSortIcon(column)}
                  </span>
                </th>
              ))}
              {onRowClick ? <th className="actions-cell">{t('actions')}</th> : null}
            </tr>
            {hasColumnFilters ? (
              <tr className="table-filter-row">
                {normalizedColumns.map((column) => (
                  <th key={`${column.key}-filter`}>
                    {column.filterable === false ? <span /> : (
                      <input
                        aria-label={t('filterColumn', { column: t(column.label || column.key, column.label || column.key) })}
                        onChange={(event) => setColumnFilters((current) => ({ ...current, [column.key]: event.target.value }))}
                        onClick={(event) => event.stopPropagation()}
                        placeholder={t('filter')}
                        value={columnFilters[column.key] || ''}
                      />
                    )}
                  </th>
                ))}
                {onRowClick ? <th /> : null}
              </tr>
            ) : null}
          </thead>
          <tbody>
            {pagedRows.length === 0 && (
              <tr>
                <td className="empty-cell" colSpan={normalizedColumns.length + (onRowClick ? 1 : 0)}>
                  <EmptyState message={t('emptyTableMessage')} title={t('noData')} />
                </td>
              </tr>
            )}
            {pagedRows.map((row, index) => (
              <tr className={onRowClick ? 'clickable-row' : ''} key={value(row, 'id') || value(row, 'guid') || index} onClick={(event) => handleRowClick(event, row)}>
                {normalizedColumns.map((column) => (
                  <td key={column.key}>
                    <CellValue columnKey={column.key} value={column.render ? column.render(row) : value(row, column.key)} />
                  </td>
                ))}
                {onRowClick ? (
                  <td className="actions-cell">
                    <ActionMenu
                      items={[{
                        icon: <Pencil size={14} />,
                        key: 'edit',
                        label: t('edit'),
                        onSelect: () => onRowClick(row)
                      }]}
                    />
                  </td>
                ) : null}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}

function CellValue({ columnKey, value: cell }) {
  const { t } = useTranslation();
  if (cell === undefined || cell === null || cell === '') return <span className="muted-cell">-</span>;
  if (isReactNode(cell)) return cell;
  if (isIdentifierColumn(columnKey)) return <CopyableValue label={t('copyValue')} value={cell} />;
  if (typeof cell === 'boolean') return <StatusChip tone={cell ? 'success' : 'danger'} value={cell ? t('yes') : t('no')} />;
  if (isStatusColumn(columnKey)) return <StatusChip tone={toneForValue(cell)} value={String(cell)} />;
  if (Array.isArray(cell)) return <span className="array-cell">{cell.join(', ')}</span>;
  if (typeof cell === 'object') return <code className="code-cell">{JSON.stringify(cell)}</code>;
  return <span>{String(cell)}</span>;
}

function StatusRow({ label, tone = 'neutral', value }) {
  return (
    <div className="status-row">
      <span>{label}</span>
      <StatusChip tone={tone} value={value} />
    </div>
  );
}

function StatusChip({ tone = 'neutral', value }) {
  return <span className={`status-chip ${tone}`}>{value}</span>;
}

function EditRecordModal({ title, data, onClose }) {
  const { t } = useTranslation();
  const [draft, setDraft] = useState(data || {});
  const entries = Object.entries(draft || {});

  function updateField(key, inputValue) {
    setDraft((current) => ({ ...current, [key]: inputValue }));
  }

  return (
    <Modal
      actions={<button className="action-button secondary" onClick={onClose} type="button">{t('close')}</button>}
      headerActions={<CopyButton label={t('copyJson')} value={stringifyJson(draft)} />}
      onClose={onClose}
      open
      subtitle={t('editRecordSubtitle')}
      title={title}
    >
      <div className="edit-record-grid">
        {entries.map(([key, fieldValue]) => (
          <label className="form-field" key={key}>
            <span>{t(key, key)}</span>
            {isIdentifierColumn(key) ? (
              <CopyableValue label={t('copyValue')} value={fieldValue} />
            ) : typeof fieldValue === 'boolean' ? (
              <select value={String(fieldValue)} onChange={(event) => updateField(key, event.target.value === 'true')}>
                <option value="true">{t('yes')}</option>
                <option value="false">{t('no')}</option>
              </select>
            ) : fieldValue && typeof fieldValue === 'object' ? (
              <JsonBlock data={fieldValue} />
            ) : (
              <input value={fieldValue ?? ''} onChange={(event) => updateField(key, event.target.value)} />
            )}
          </label>
        ))}
      </div>
    </Modal>
  );
}

function JsonPanelInline({ title, data }) {
  const { t } = useTranslation();
  return (
    <Panel actions={<CopyButton label={t('copyJson')} value={stringifyJson(data)} />} eyebrow={t('json')} title={title}>
      <JsonBlock data={data} />
    </Panel>
  );
}

function JsonBlock({ data }) {
  return (
    <div className="json-viewer">
      <JsonNode data={data} />
    </div>
  );
}

function JsonNode({ data, fieldKey = '' }) {
  if (data === null || data === undefined) return <span className="json-null">null</span>;
  if (Array.isArray(data)) {
    return (
      <div className="json-array">
        {data.length === 0 ? <span className="json-null">[]</span> : data.map((item, index) => (
          <div className="json-row" key={index}>
            <span className="json-key">{index}</span>
            <JsonNode data={item} fieldKey={fieldKey} />
          </div>
        ))}
      </div>
    );
  }
  if (typeof data === 'object') {
    const entries = Object.entries(data);
    return (
      <div className="json-object">
        {entries.length === 0 ? <span className="json-null">{'{}'}</span> : entries.map(([key, value]) => (
          <div className="json-row" key={key}>
            <span className="json-key">{key}</span>
            <JsonNode data={value} fieldKey={key} />
          </div>
        ))}
      </div>
    );
  }
  if (isIdentifierColumn(fieldKey)) return <CopyableValue value={data} />;
  if (typeof data === 'boolean') return <StatusChip tone={data ? 'success' : 'danger'} value={String(data)} />;
  if (typeof data === 'number') return <span className="json-number">{data}</span>;
  return <span className="json-string">{String(data)}</span>;
}

function stringifyJson(data) {
  return JSON.stringify(data ?? {}, null, 2);
}

function value(row, key) {
  if (!row) return undefined;
  const pascal = key.charAt(0).toUpperCase() + key.slice(1);
  return row[key] ?? row[pascal];
}

function formatNumber(input) {
  const number = Number(input || 0);
  return Number.isFinite(number) ? number.toLocaleString() : '0';
}

function formatPercent(input) {
  const number = Number(input || 0);
  return `${Math.max(0, Math.min(100, number * 100)).toFixed(1)}%`;
}

function summarizeBuckets(buckets) {
  return buckets.reduce((summary, bucket) => {
    const success = Number(bucket.success || bucket.Success || 0);
    const failed = Number(bucket.failed || bucket.Failed || 0);
    return {
      failed: summary.failed + failed,
      success: summary.success + success,
      total: summary.total + success + failed
    };
  }, { failed: 0, success: 0, total: 0 });
}

function groupByCount(rows, key) {
  const groups = new Map();
  (Array.isArray(rows) ? rows : []).forEach((row) => {
    const groupName = String(value(row, key) || 'unknown');
    const statusCode = Number(value(row, 'statusCode') || 0);
    const current = groups.get(groupName) || { name: groupName, count: 0, success: 0, failed: 0 };
    current.count += 1;
    if (statusCode >= 200 && statusCode < 400) current.success += 1;
    else current.failed += 1;
    groups.set(groupName, current);
  });
  return [...groups.values()].sort((left, right) => right.count - left.count);
}

function isHealthyState(state) {
  const text = String(state || '').toLowerCase();
  return ['active', 'available', 'healthy', 'normal', 'ready', 'running'].includes(text);
}

function isStatusColumn(key) {
  const lower = key.toLowerCase();
  return lower.includes('active')
    || lower.includes('connected')
    || lower.includes('decision')
    || lower.includes('permit')
    || lower.includes('state')
    || lower.includes('status')
    || lower.includes('success')
    || lower.includes('supports');
}

function isIdentifierColumn(key) {
  const lower = String(key || '').toLowerCase();
  return lower === 'id'
    || lower === 'guid'
    || lower === 'identifier'
    || lower.endsWith('id')
    || lower.endsWith('guid')
    || lower.endsWith('key');
}

function toneForValue(input) {
  const text = String(input || '').toLowerCase();
  if (/^2\d\d$/.test(text) || ['allow', 'allowed', 'enabled', 'healthy', 'normal', 'ready', 'success', 'true'].includes(text)) return 'success';
  if (/^4\d\d$/.test(text) || /^5\d\d$/.test(text) || ['deny', 'denied', 'disabled', 'error', 'failed', 'false', 'quarantined'].includes(text)) return 'danger';
  if (['draining', 'pending', 'warning'].includes(text)) return 'warning';
  return 'neutral';
}

function isReactNode(input) {
  return input && typeof input === 'object' && '$$typeof' in input;
}

function AppRoutes() {
  const [theme, setTheme] = useState(() => window.localStorage.getItem('xeno.theme') || 'light');

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    window.localStorage.setItem('xeno.theme', theme);
  }, [theme]);

  function toggleTheme() {
    setTheme((current) => current === 'dark' ? 'light' : 'dark');
  }

  function logout() {
    window.location.href = '/';
  }

  return (
    <Routes>
      <Route path="/" element={<LoginView onToggleTheme={toggleTheme} theme={theme} />} />
      <Route path="/dashboard/*" element={<Shell onLogout={logout} onToggleTheme={toggleTheme} theme={theme} />} />
    </Routes>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <AppRoutes />
    </BrowserRouter>
  );
}

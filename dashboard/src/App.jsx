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
  FileClock,
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
  Eye,
  Play,
  RefreshCw,
  Route as RouteIcon,
  Search,
  Server,
  Settings as SettingsIcon,
  Shield,
  ShieldCheck,
  Sun,
  Trash2,
  Upload,
  Workflow,
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
      { to: '/dashboard/clients', key: 'clients', icon: Network },
      { to: '/dashboard/vmrs', key: 'vmrs', icon: BrainCircuit },
      { to: '/dashboard/policies', key: 'policies', icon: RouteIcon },
      { to: '/dashboard/reservations', key: 'reservations', icon: FileClock }
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
  '/dashboard/home': { title: 'home', kicker: 'controlPlane', description: 'homeDescription' },
  '/dashboard/requests': { title: 'requests', kicker: 'observe', description: 'requestsDescription' },
  '/dashboard/explorer': { title: 'explorer', kicker: 'operate', description: 'explorerDescription' },
  '/dashboard/tenants': { title: 'tenants', kicker: 'administration', description: 'tenantsDescription' },
  '/dashboard/users': { title: 'users', kicker: 'administration', description: 'usersDescription' },
  '/dashboard/credentials': { title: 'credentials', kicker: 'administration', description: 'credentialsDescription' },
  '/dashboard/security': { title: 'security', kicker: 'administration', description: 'securityDescription' },
  '/dashboard/clients': { title: 'clients', kicker: 'control', description: 'clientsDescription' },
  '/dashboard/vmrs': { title: 'vmrs', kicker: 'control', description: 'vmrsDescription' },
  '/dashboard/policies': { title: 'policies', kicker: 'control', description: 'policiesDescription' },
  '/dashboard/reservations': { title: 'reservations', kicker: 'control', description: 'reservationsDescription' },
  '/dashboard/analytics': { title: 'analytics', kicker: 'observe', description: 'analyticsDescription' },
  '/dashboard/settings': { title: 'settings', kicker: 'administration', description: 'settingsDescription' }
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
            <span>{t('controlPlane')}</span>
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
            <span className="eyebrow">{t(meta.kicker)}</span>
            <h1>{t(meta.title)}</h1>
            <p>{t(meta.description)}</p>
          </div>
          <div className="topbar-actions">
            <span className="server-badge" title={api.baseUrl}>
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
            <Route path="clients" element={<ClientsView />} />
            <Route path="vmrs" element={<VmrsView />} />
            <Route path="policies" element={<PoliciesView />} />
            <Route path="reservations" element={<ReservationsView />} />
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
      <section className="hero-band">
        <div>
          <span className="eyebrow">{t('systemStatus')}</span>
          <h2>{t('homeHeroTitle')}</h2>
          <p>{t('homeHeroText')}</p>
        </div>
        <div className="hero-status-grid">
          <StatusRow label={t('serverHealth')} value={t('available')} tone="success" />
          <StatusRow label={t('websocketGateway')} value={t('credentialGated')} tone="neutral" />
          <StatusRow label={t('routingEngine')} value={t('conductorParity')} tone="success" />
        </div>
      </section>

      <div className="metric-grid">
        <MetricCard icon={<History size={18} />} label={t('requestVolume')} value={formatNumber(analytics.data.total)} subtext={t('total')} />
        <MetricCard icon={<Zap size={18} />} label={t('success')} value={formatNumber(analytics.data.success)} subtext={formatPercent(1 - Number(analytics.data.failureRate || 0))} tone="success" />
        <MetricCard icon={<Shield size={18} />} label={t('failed')} value={formatNumber(analytics.data.failures)} subtext={formatPercent(Number(analytics.data.failureRate || 0))} tone="danger" />
        <MetricCard icon={<Network size={18} />} label={t('connectedClients')} value={formatNumber(connected)} subtext={t('clients')} tone="info" />
        <MetricCard icon={<Server size={18} />} label={t('activeEndpoints')} value={formatNumber(activeEndpoints)} subtext={t('endpoints')} tone="success" />
        <MetricCard icon={<BrainCircuit size={18} />} label={t('activeModels')} value={formatNumber(completionModels)} subtext={t('models')} tone="neutral" />
      </div>

      <div className="workspace-grid wide-left">
        <Panel
          actions={<IconButton onClick={refreshAll} label={t('refresh')} icon={<RefreshCw size={16} />} />}
          eyebrow={t('observe')}
          title={t('requestActivity')}
        >
          <ActivityChart summary={summary.data} />
        </Panel>
        <Panel eyebrow={t('control')} title={t('routingSurface')}>
          <div className="status-list">
            <StatusRow label={t('clients')} value={formatNumber(clients.data.length)} tone={connected > 0 ? 'success' : 'warning'} />
            <StatusRow label={t('endpoints')} value={`${activeEndpoints} / ${endpoints.data.length}`} tone={activeEndpoints > 0 ? 'success' : 'warning'} />
            <StatusRow label={t('models')} value={formatNumber(models.data.length)} tone={models.data.length > 0 ? 'success' : 'neutral'} />
            <StatusRow label={t('database')} value={t('configured')} tone="neutral" />
          </div>
        </Panel>
      </div>

      <DataTable
        columns={['name', 'provider', 'serviceState', 'inFlightRequests', 'maxParallelRequests']}
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
            <IconButton onClick={history.refresh} label={t('refresh')} icon={<RefreshCw size={16} />} />
            <IconButton onClick={() => setConfirmClear(true)} label={t('clear')} icon={<Trash2 size={16} />} variant="danger" />
          </div>
        )}
        eyebrow={t('observe')}
        title={t('requestActivity')}
      >
        <ActivityChart summary={summary.data} />
      </Panel>
      <DataTable
        columns={['statusCode', 'method', 'path', 'model', 'durationMs', 'createdUtc']}
        onRowClick={setSelected}
        rows={history.data}
        subtitle={t('requestHistorySubtitle')}
        title={t('requests')}
      />
      {selected && <JsonPanel title={t('details')} data={selected} onClose={() => setSelected(null)} />}
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
  const spec = useResource('/openapi.json', null);
  const [selectedPath, setSelectedPath] = useState('/v1/chat/completions');
  const [response, setResponse] = useState(null);
  const paths = Object.keys(spec.data?.paths || {});
  const pathOptions = [selectedPath, ...paths.filter((path) => path !== selectedPath)];

  async function execute() {
    const data = selectedPath.includes('/embeddings')
      ? await api.post('/v1/embeddings', { model: 'text-embedding-3-small', input: 'hello' })
      : await api.post('/v1/chat/completions', { model: 'llama3.2:latest', messages: [{ role: 'user', content: 'hello' }] });
    setResponse(data);
  }

  return (
    <div className="view-stack">
      <div className="workspace-grid explorer-grid">
        <Panel
          actions={<IconButton onClick={execute} label={t('execute')} icon={<Play size={16} />} variant="primary" />}
          eyebrow={t('openapiLoaded')}
          title={t('requestBuilder')}
        >
          <div className="form-stack">
            <label className="form-field">
              <span>{t('path')}</span>
              <select value={selectedPath} onChange={(event) => setSelectedPath(event.target.value)}>
                {pathOptions.map((path) => <option key={path}>{path}</option>)}
              </select>
            </label>
            <div className="route-preview">
              <span className="status-chip neutral">POST</span>
              <code>{selectedPath}</code>
            </div>
          </div>
        </Panel>
        <Panel eyebrow={t('details')} title={t('apiSurface')}>
          <div className="status-list">
            <StatusRow label={t('paths')} value={formatNumber(paths.length)} tone={paths.length ? 'success' : 'warning'} />
            <StatusRow label={t('serverUrl')} value={api.baseUrl} tone="neutral" />
          </div>
        </Panel>
      </div>
      <JsonPanelInline title={response ? t('response') : t('openapiDocument')} data={response || spec.data || { status: t('noData') }} />
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
      <div className="workspace-grid">
        <DataTable title={t('roles')} rows={roles.data} columns={['name', 'builtIn', 'active']} />
        <DataTable title={t('permissions')} rows={permissions.data} columns={['resourceType', 'operationType', 'permit']} />
        <DataTable title={t('effectivePermissions')} rows={effective.data} columns={['resourceType', 'operationType', 'permit', 'sourceRole']} />
      </div>
      <DataTable
        columns={['createdUtc', 'resourceType', 'operationType', 'reasonCode', 'statusCode']}
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
      <Panel
        actions={<IconButton onClick={tenants.refresh} label={t('refresh')} icon={<RefreshCw size={16} />} />}
        eyebrow={t('administration')}
        title={t('tenantManagement')}
      >
        <div className="status-list horizontal">
          <StatusRow label={t('tenants')} value={formatNumber(tenants.data.length)} tone="neutral" />
          <StatusRow label={t('tenantIsolation')} value={t('enabled')} tone="success" />
        </div>
      </Panel>
      <DataTable
        columns={['id', 'name', 'active', 'createdUtc', 'lastUpdateUtc']}
        onRowClick={setSelected}
        rows={tenants.data}
        subtitle={t('tenantsTableSubtitle')}
        title={t('tenants')}
      />
      {selected && <JsonPanel title={t('tenantDetails')} data={selected} onClose={() => setSelected(null)} />}
    </div>
  );
}

function UsersView() {
  const { t } = useTranslation();
  const users = useResource('/api/v1/users', []);
  const [selected, setSelected] = useState(null);

  return (
    <div className="view-stack">
      <Panel
        actions={<IconButton onClick={users.refresh} label={t('refresh')} icon={<RefreshCw size={16} />} />}
        eyebrow={t('administration')}
        title={t('userManagement')}
      >
        <div className="status-list horizontal">
          <StatusRow label={t('users')} value={formatNumber(users.data.length)} tone="neutral" />
          <StatusRow label={t('passwordLogin')} value={t('dashboardOnly')} tone="warning" />
        </div>
      </Panel>
      <DataTable
        columns={['id', 'tenantId', 'username', 'email', 'isAdmin', 'isTenantAdmin', 'active']}
        onRowClick={setSelected}
        rows={users.data}
        subtitle={t('usersTableSubtitle')}
        title={t('users')}
      />
      {selected && <JsonPanel title={t('userDetails')} data={selected} onClose={() => setSelected(null)} />}
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
      <Panel
        actions={<IconButton onClick={createCredential} label={t('createCredential')} icon={<KeyRound size={16} />} variant="primary" />}
        eyebrow={t('administration')}
        title={t('credentialManagement')}
      >
        <div className="status-list horizontal">
          <StatusRow label={t('credentials')} value={formatNumber(credentials.data.length)} tone="neutral" />
          <StatusRow label={t('websocketGateway')} value={t('credentialGated')} tone="success" />
        </div>
      </Panel>
      {created ? <JsonPanelInline title={t('newCredential')} data={created} /> : null}
      <DataTable
        columns={['id', 'tenantId', 'userId', 'accessKey', 'secretKey', 'active']}
        onRowClick={setSelected}
        rows={credentials.data}
        subtitle={t('credentialsTableSubtitle')}
        title={t('credentials')}
      />
      {selected && <JsonPanel title={t('credentialDetails')} data={selected} onClose={() => setSelected(null)} />}
    </div>
  );
}

function ClientsView() {
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
      <div className="workspace-grid">
        <DataTable title={t('clients')} rows={clients.data} columns={['name', 'connected', 'lastHeartbeatUtc']} />
        <DataTable title={t('endpoints')} rows={endpoints.data} columns={['name', 'provider', 'serviceState', 'maxParallelRequests']} />
        <DataTable title={t('models')} rows={models.data} columns={['name', 'supportsCompletions', 'supportsEmbeddings']} />
      </div>
    </div>
  );
}

function VmrsView() {
  const { t } = useTranslation();
  const vmrs = useResource('/api/v1/virtualmodelrunners', []);
  const effective = useResource('/api/v1/virtualmodelrunners/effective', null);
  const [loadModel, setLoadModel] = useState('llama3.2:latest');
  const [loadResult, setLoadResult] = useState(null);
  const [routing, setRouting] = useState(null);

  async function load() {
    setLoadResult(await api.post('/api/v1/virtualmodelrunners/load-model', { model: loadModel }));
    effective.refresh();
  }

  async function explain() {
    setRouting(await api.post('/api/v1/virtualmodelrunners/explain-routing', { model: loadModel }));
  }

  return (
    <div className="view-stack">
      <Panel
        actions={<IconButton onClick={explain} label={t('explainRouting')} icon={<Play size={16} />} variant="primary" />}
        eyebrow={t('requestRouting')}
        title={t('vmrWorkbench')}
      >
        <div className="toolbar">
          <label className="form-field inline">
            <span>{t('model')}</span>
            <input value={loadModel} onChange={(event) => setLoadModel(event.target.value)} />
          </label>
          <IconButton onClick={load} label={t('loadModel')} icon={<Upload size={16} />} />
        </div>
      </Panel>
      <DataTable title={t('vmrs')} rows={vmrs.data} columns={['name', 'loadBalancingMode', 'strictMode', 'active']} />
      <div className="workspace-grid">
        <JsonPanelInline title={t('effectiveConfig')} data={effective.data} />
        <JsonPanelInline title={t('routingDecision')} data={routing || loadResult || {}} />
      </div>
    </div>
  );
}

function PoliciesView() {
  const { t } = useTranslation();
  const loadPolicies = useResource('/api/v1/loadbalancingpolicies', []);
  const accessPolicies = useResource('/api/v1/modelaccesspolicies', []);
  const metrics = useResource('/api/v1/loadbalancingpolicies/metrics-catalog', []);
  const [evaluation, setEvaluation] = useState(null);

  async function evaluate() {
    setEvaluation(await api.post('/api/v1/modelaccesspolicies/evaluate', { model: 'llama3.2:latest' }));
  }

  return (
    <div className="view-stack">
      <Panel
        actions={<IconButton onClick={evaluate} label={t('evaluate')} icon={<Play size={16} />} variant="primary" />}
        eyebrow={t('policyControls')}
        title={t('policyWorkbench')}
      >
        <div className="status-list horizontal">
          <StatusRow label={t('loadBalancingPolicies')} value={formatNumber(loadPolicies.data.length)} tone="neutral" />
          <StatusRow label={t('modelAccessPolicies')} value={formatNumber(accessPolicies.data.length)} tone="neutral" />
        </div>
      </Panel>
      <div className="workspace-grid">
        <DataTable title={t('loadBalancingPolicies')} rows={loadPolicies.data} columns={['name', 'rankingMetric', 'preferLower', 'active']} />
        <DataTable title={t('modelAccessPolicies')} rows={accessPolicies.data} columns={['name', 'mode', 'defaultDecision', 'active']} />
        <JsonPanelInline title={t('metricsCatalog')} data={metrics.data} />
        <JsonPanelInline title={t('evaluation')} data={evaluation || {}} />
      </div>
    </div>
  );
}

function ReservationsView() {
  const { t } = useTranslation();
  const reservations = useResource('/api/v1/virtualmodelrunnerreservations', []);
  const effective = useResource('/api/v1/virtualmodelrunnerreservations/effective', {});

  return (
    <div className="view-stack">
      <div className="metric-grid compact">
        <MetricCard icon={<FileClock size={18} />} label={t('reservationWindows')} value={formatNumber(reservations.data.length)} />
        <MetricCard icon={<Workflow size={18} />} label={t('effectiveAccess')} value={effective.data ? t('available') : t('noData')} tone={effective.data ? 'success' : 'neutral'} />
      </div>
      <div className="workspace-grid">
        <DataTable title={t('reservations')} rows={reservations.data} columns={['name', 'startUtc', 'endUtc', 'active']} />
        <JsonPanelInline title={t('effectiveAccess')} data={effective.data} />
      </div>
    </div>
  );
}

function AnalyticsView() {
  const { t } = useTranslation();
  const analytics = useResource('/api/v1/analytics/overview', {});
  const metrics = useTextResource('/metrics', '');

  return (
    <div className="view-stack">
      <div className="metric-grid compact">
        <MetricCard icon={<Activity size={18} />} label={t('total')} value={formatNumber(analytics.data.total)} />
        <MetricCard icon={<Zap size={18} />} label={t('success')} value={formatNumber(analytics.data.success)} tone="success" />
        <MetricCard icon={<Shield size={18} />} label={t('failed')} value={formatNumber(analytics.data.failures)} tone="danger" />
      </div>
      <div className="workspace-grid">
        <JsonPanelInline title={t('overview')} data={analytics.data} />
        <Panel
          actions={<IconButton onClick={() => { analytics.refresh(); metrics.refresh(); }} label={t('refresh')} icon={<RefreshCw size={16} />} />}
          eyebrow={t('observe')}
          title={t('prometheusMetrics')}
        >
          <pre className="metrics-output">{metrics.data || t('noData')}</pre>
        </Panel>
      </div>
    </div>
  );
}

function SettingsView() {
  const { t } = useTranslation();
  const [backup, setBackup] = useState('');
  const [status, setStatus] = useState('');

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
      <div className="workspace-grid settings-grid">
        <Panel eyebrow={t('configuration')} title={t('connection')}>
          <div className="status-list">
            <StatusRow label={t('serverUrl')} value={api.baseUrl} tone="neutral" />
            <StatusRow label={t('language')} value={<LanguageSelector />} tone="neutral" />
          </div>
        </Panel>
        <Panel eyebrow={t('administration')} title={t('backupRestore')}>
          <div className="button-row">
            <IconButton onClick={downloadBackup} label={t('backup')} icon={<Download size={16} />} />
            <IconButton onClick={restoreBackup} label={t('restore')} icon={<Upload size={16} />} variant="primary" />
          </div>
          {status ? <p className="muted-copy">{status}</p> : null}
        </Panel>
      </div>
      <textarea className="backup-editor" value={backup} onChange={(event) => setBackup(event.target.value)} aria-label={t('backup')} />
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

function IconButton({ icon, label, onClick, variant = 'secondary' }) {
  return (
    <button className={`action-button ${variant}`} onClick={onClick} title={label} type="button">
      {icon}
      <span>{label}</span>
    </button>
  );
}

function DataTable({ actions, columns, onRowClick, rows, subtitle, title }) {
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
                        icon: <Eye size={14} />,
                        key: 'view',
                        label: t('viewDetails'),
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
      <Pagination
        currentPage={displayPage}
        onPageChange={setCurrentPage}
        onPageSizeChange={setPageSize}
        pageSize={pageSize}
        totalItems={sortedRows.length}
      />
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

function JsonPanel({ title, data, onClose }) {
  const { t } = useTranslation();
  return (
    <Modal
      actions={<button className="action-button secondary" onClick={onClose} type="button">{t('close')}</button>}
      headerActions={<CopyButton label={t('copyJson')} value={stringifyJson(data)} />}
      onClose={onClose}
      open
      subtitle={t('details')}
      title={title}
    >
      <JsonBlock data={data} />
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

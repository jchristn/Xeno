import { useEffect, useMemo, useRef, useState } from 'react';
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
  FileJson,
  Gauge,
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
  Plus,
  RefreshCw,
  Search,
  Server,
  Settings as SettingsIcon,
  Shield,
  ShieldCheck,
  Square,
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
import ConnectedClientsChart from './components/ConnectedClientsChart.jsx';
import EmptyState from './components/EmptyState.jsx';
import LanguageSelector from './components/LanguageSelector.jsx';
import ActivityChart from './components/ActivityChart.jsx';
import Modal from './components/Modal.jsx';
import Pagination from './components/Pagination.jsx';

const api = new ApiClient();
const requestHistoryEndpoint = '/v1.0/api/request-history';
const requestHistorySummaryEndpoint = `${requestHistoryEndpoint}/summary`;
const defaultOpenAiPrompt = 'Hello! Can you give me a brief explanation of how an inverted index works?';
const defaultOpenAiSystemPrompt = 'You are a helpful and concise assistant.';
const defaultEmbeddingInputText = [
  'The quick brown fox jumps over the lazy dog.',
  'Artificial intelligence is transforming software development.',
  'How do I cook a perfect medium-rare steak?'
].join('\n');

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
      { to: '/dashboard/clients/advertised-models', key: 'advertisedModels', icon: BrainCircuit }
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
  '/dashboard/clients': { title: 'clients', description: 'clientsDescription' },
  '/dashboard/clients/advertised-models': { title: 'advertisedModels', description: 'advertisedModelsDescription' },
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
                  <NavLink className={`sidebar-link ${link.nested ? 'nested' : ''}`.trim()} end key={link.to} to={link.to}>
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
              <GitHubMark size={17} />
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
            <Route path="clients/advertised-models" element={<AdvertisedModelsView />} />
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
              <GitHubMark size={17} />
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
            <code>{t('defaultAccessKey')}</code>
          </div>
        </section>
      </div>
    </div>
  );
}

function HomeView() {
  const { t } = useTranslation();
  const analytics = useResource('/api/v1/analytics/overview', { total: 0, success: 0, failures: 0, failureRate: 0 });
  const summary = useResource(requestHistorySummaryEndpoint, { buckets: [] });
  const clients = useResource('/api/v1/clients', []);
  const clientModels = flattenClientModels(clients.data);
  const connected = clients.data.filter((client) => value(client, 'connected') === true).length;
  const advertisedModelCount = new Set(clientModels.map((model) => String(value(model, 'name') || '').toLowerCase()).filter(Boolean)).size;
  const completionModels = clientModels.filter((model) => value(model, 'supportsCompletions') === true).length;

  function refreshAll() {
    analytics.refresh();
    summary.refresh();
    clients.refresh();
  }

  return (
    <div className="view-stack">
      <div className="metric-grid">
        <MetricCard icon={<History size={18} />} label={t('requestVolume')} value={formatNumber(analytics.data.total)} subtext={t('total')} />
        <MetricCard icon={<Zap size={18} />} label={t('success')} value={formatNumber(analytics.data.success)} subtext={formatPercent(1 - Number(analytics.data.failureRate || 0))} tone="success" />
        <MetricCard icon={<Shield size={18} />} label={t('failed')} value={formatNumber(analytics.data.failures)} subtext={formatPercent(Number(analytics.data.failureRate || 0))} tone="danger" />
        <MetricCard icon={<Network size={18} />} label={t('connectedClients')} value={formatNumber(connected)} subtext={t('clients')} tone="info" />
        <MetricCard icon={<BrainCircuit size={18} />} label={t('advertisedModels')} value={formatNumber(advertisedModelCount)} subtext={t('models')} tone="success" />
        <MetricCard icon={<Server size={18} />} label={t('completionModels')} value={formatNumber(completionModels)} subtext={t('models')} tone="neutral" />
      </div>

      <Panel
        actions={<IconButton iconOnly onClick={refreshAll} label={t('refresh')} icon={<RefreshCw size={16} />} />}
        className="chart-panel-card"
        title={t('requestActivity')}
      >
        <ActivityChart summary={summary.data} />
      </Panel>

      <Panel
        actions={<IconButton iconOnly onClick={refreshAll} label={t('refresh')} icon={<RefreshCw size={16} />} />}
        className="chart-panel-card"
        title={t('connectedClients')}
      >
        <ConnectedClientsChart
          connected={connected}
          onRefresh={refreshAll}
          sampleVersion={clients.updatedAt}
          total={clients.data.length}
        />
      </Panel>
    </div>
  );
}

function RequestHistoryView() {
  const { t } = useTranslation();
  const history = useResource(requestHistoryEndpoint, []);
  const summary = useResource(requestHistorySummaryEndpoint, { buckets: [] });
  const [selected, setSelected] = useState(null);
  const [confirmClear, setConfirmClear] = useState(false);
  const bucketTotals = summarizeBuckets(summary.data.buckets || []);

  async function clearHistory() {
    await api.post(`${requestHistoryEndpoint}/bulk`, {});
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
        className="chart-panel-card"
        title={t('requestActivity')}
      >
        <ActivityChart summary={summary.data} />
      </Panel>
      <DataTable
        columns={['statusCode', 'method', 'path', 'model', 'selectedClientId', 'durationMs', 'createdUtc']}
        onDelete={(row) => {
          if (recordId(selected) === recordId(row)) setSelected(null);
          return deleteRecord(requestHistoryEndpoint, row);
        }}
        onRowClick={setSelected}
        onRefresh={() => { history.refresh(); summary.refresh(); }}
        rows={history.data}
        subtitle={t('requestHistorySubtitle')}
        title={t('requests')}
      />
      {selected && <EditRecordModal title={t('requestDetails')} data={selected} onClose={() => setSelected(null)} />}
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
  const openApi = useResource('/openapi.json', { paths: {} });
  const models = useResource('/v1/models', { data: [] });
  const operations = useMemo(() => buildApiOperations(openApi.data), [openApi.data]);
  const tags = useMemo(() => ['all', ...new Set(operations.map((item) => item.tag).filter(Boolean))], [operations]);
  const modelOptions = useMemo(() => extractModelOptions(models.data), [models.data]);
  const [tag, setTag] = useState('all');
  const [operationKey, setOperationKey] = useState('');
  const [model, setModel] = useState('');
  const [pathValues, setPathValues] = useState({});
  const [queryText, setQueryText] = useState('{}');
  const [bodyText, setBodyText] = useState('{}');
  const [response, setResponse] = useState(null);
  const [isSending, setIsSending] = useState(false);
  const abortControllerRef = useRef(null);
  const visibleOperations = tag === 'all' ? operations : operations.filter((item) => item.tag === tag);
  const selected = operations.find((item) => item.key === operationKey) || visibleOperations[0] || operations[0];
  const pathParameters = selected ? extractPathParameters(selected.path) : [];

  useEffect(() => {
    if (operationKey || operations.length === 0) return;
    const preferred = operations.find((item) => item.path === '/v1/chat/completions') || operations[0];
    setOperationKey(preferred.key);
  }, [operationKey, operations]);

  useEffect(() => {
    if (model || modelOptions.length === 0) return;
    const nextModel = modelOptions[0];
    setModel(nextModel);
    if (selected && operationUsesModel(selected)) {
      setBodyText(formatJson(exampleBodyForOperation(selected, nextModel)));
    }
  }, [model, modelOptions, selected?.key]);

  useEffect(() => {
    if (!selected) return;
    const nextModel = modelForOperation(selected, model);
    if (nextModel !== model) setModel(nextModel);
    setPathValues(Object.fromEntries(extractPathParameters(selected.path).map((name) => [name, ''])));
    setQueryText(formatJson(defaultQueryForOperation(selected)));
    setBodyText(formatJson(exampleBodyForOperation(selected, nextModel)));
    setResponse(null);
  }, [selected?.key]);

  function updateOperation(nextKey) {
    setOperationKey(nextKey);
  }

  function updateTag(nextTag) {
    setTag(nextTag);
    const nextOperations = nextTag === 'all' ? operations : operations.filter((item) => item.tag === nextTag);
    if (nextOperations.length > 0) setOperationKey(nextOperations[0].key);
  }

  async function execute() {
    if (!selected || isSending) return;

    const queryResult = parseJsonForExplorer(queryText, {});
    if (!queryResult.ok) {
      setResponse({ body: { error: t('invalidJson'), field: t('queryParameters'), details: queryResult.error }, error: t('invalidJson'), headers: {}, ok: false, rawBody: '', status: 0, statusText: 'Invalid JSON' });
      return;
    }

    const bodyResult = selected.method === 'GET'
      ? { ok: true, value: undefined }
      : parseJsonForExplorer(bodyText, {});
    if (!bodyResult.ok) {
      setResponse({ body: { error: t('invalidJson'), field: t('requestBody'), details: bodyResult.error }, error: t('invalidJson'), headers: {}, ok: false, rawBody: '', status: 0, statusText: 'Invalid JSON' });
      return;
    }

    const query = queryResult.value;
    const requestPath = buildExplorerPath(selected.path, pathValues, query);
    const body = bodyResult.value;
    setResponse(null);

    if (abortControllerRef.current) abortControllerRef.current.abort();
    const abortController = new AbortController();
    abortControllerRef.current = abortController;
    setIsSending(true);

    try {
      const result = await api.request(requestPath, {
        body,
        expectStream: Boolean(body && typeof body === 'object' && body.stream === true),
        method: selected.method,
        onProgress: (progress) => setResponse(progress),
        signal: abortController.signal
      });
      setResponse(result);
    } catch (error) {
      const cancelled = error instanceof Error && error.name === 'AbortError';
      setResponse({
        body: { error: cancelled ? t('requestCancelled') : (error instanceof Error ? error.message : String(error)) },
        error: cancelled ? t('requestCancelled') : (error instanceof Error ? error.message : String(error)),
        headers: {},
        ok: false,
        rawBody: '',
        status: 0,
        statusText: cancelled ? 'Cancelled' : 'Client Error',
        timing: {}
      });
    } finally {
      setIsSending(false);
      abortControllerRef.current = null;
    }
  }

  function stopRequest() {
    abortControllerRef.current?.abort();
  }

  function clearExplorerResponse() {
    setResponse(null);
  }

  return (
    <div className="view-stack">
      <Panel
        actions={(
          <div className="button-row">
            <IconButton disabled={!selected || isSending} onClick={execute} label={isSending ? t('sending') : t('execute')} icon={<Play size={16} />} variant="primary" />
            {isSending ? <IconButton onClick={stopRequest} label={t('stop')} icon={<Square size={16} />} variant="danger" /> : null}
          </div>
        )}
        title={t('requestBuilder')}
      >
        {operations.length === 0 ? (
          <EmptyState message={t('openApiUnavailable')} title={t('noData')} />
        ) : (
          <div className="form-stack">
            <div className="form-grid two">
              <label className="form-field">
                <span>{t('endpoints')}</span>
                <select value={tag} onChange={(event) => updateTag(event.target.value)}>
                  {tags.map((item) => <option key={item} value={item}>{item === 'all' ? t('allEndpoints') : item}</option>)}
                </select>
              </label>
              <label className="form-field">
                <span>{t('operation')}</span>
                <select className="explorer-operation-select" value={selected?.key || ''} onChange={(event) => updateOperation(event.target.value)}>
                  {visibleOperations.map((item) => (
                    <option key={item.key} value={item.key}>
                      {item.method} {item.path} - {item.summary}
                    </option>
                  ))}
                </select>
              </label>
            </div>

            {selected ? (
              <>
                <div className="route-preview">
                  <span className={`status-chip ${methodTone(selected.method)}`}>{selected.method}</span>
                  <code>{selected.path}</code>
                  <span className="muted-copy">{selected.summary}</span>
                </div>
                {selected.description ? <p className="explorer-description">{selected.description}</p> : null}

                {pathParameters.length > 0 ? (
                  <div className="form-grid two">
                    {pathParameters.map((name) => (
                      <label className="form-field" key={name}>
                        <span>{t('pathParameter')}: {name}</span>
                        <input
                          value={pathValues[name] || ''}
                          onChange={(event) => setPathValues((current) => ({ ...current, [name]: event.target.value }))}
                        />
                      </label>
                    ))}
                  </div>
                ) : null}

                <div className="form-grid two">
                  <label className="form-field">
                    <span>{t('queryParameters')}</span>
                    <textarea className="explorer-json-editor" rows={10} value={queryText} onChange={(event) => setQueryText(event.target.value)} />
                  </label>
                  <label className="form-field">
                    <span>{t('requestBody')}</span>
                    <textarea
                      className="explorer-json-editor"
                      disabled={selected.method === 'GET'}
                      rows={10}
                      value={selected.method === 'GET' ? t('noRequestBody') : bodyText}
                      onChange={(event) => setBodyText(event.target.value)}
                    />
                  </label>
                </div>
              </>
            ) : null}
          </div>
        )}
      </Panel>
      <div className="api-explorer-results">
        <ExplorerResponsePanel isSending={isSending} onClear={clearExplorerResponse} response={response} />
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
  const [selectedRole, setSelectedRole] = useState(null);
  const [selectedPermission, setSelectedPermission] = useState(null);
  const [creatingRole, setCreatingRole] = useState(false);
  const [creatingPermission, setCreatingPermission] = useState(false);

  async function saveRole(roleId, payload) {
    const result = roleId
      ? await api.put(`/api/v1/roles/${encodeURIComponent(roleId)}`, payload)
      : await api.post('/api/v1/roles', payload);
    if (result?.error) throw new Error(result.message || result.error);
    setSelectedRole(result);
    setCreatingRole(false);
    roles.refresh();
    effective.refresh();
  }

  async function savePermission(permissionId, payload) {
    const result = permissionId
      ? await api.put(`/api/v1/permissions/${encodeURIComponent(permissionId)}`, payload)
      : await api.post('/api/v1/permissions', payload);
    if (result?.error) throw new Error(result.message || result.error);
    setSelectedPermission(result);
    setCreatingPermission(false);
    permissions.refresh();
    effective.refresh();
  }

  return (
    <div className="view-stack">
      <div className="metric-grid compact">
        <MetricCard icon={<LockKeyhole size={18} />} label={t('roles')} value={formatNumber(roles.data.length)} />
        <MetricCard icon={<ShieldCheck size={18} />} label={t('permissions')} value={formatNumber(permissions.data.length)} />
        <MetricCard icon={<Activity size={18} />} label={t('audit')} value={formatNumber(audit.data.length)} tone="info" />
      </div>
      <DataTable
        title={t('roles')}
        rows={roles.data}
        columns={['id', 'tenantId', 'name', 'builtIn', 'active']}
        onDelete={(row) => {
          if (recordId(selectedRole) === recordId(row)) setSelectedRole(null);
          return deleteRecord('/api/v1/roles', row);
        }}
        onRefresh={roles.refresh}
        onRowClick={setSelectedRole}
        rowActionLabel={t('edit')}
        actions={<IconButton onClick={() => setCreatingRole(true)} label={t('createRole')} icon={<Plus size={16} />} variant="primary" />}
      />
      <DataTable
        title={t('permissions')}
        rows={permissions.data}
        columns={['id', 'tenantId', 'resourceType', 'operationType', 'permit', 'active']}
        onDelete={(row) => {
          if (recordId(selectedPermission) === recordId(row)) setSelectedPermission(null);
          return deleteRecord('/api/v1/permissions', row);
        }}
        onRefresh={permissions.refresh}
        onRowClick={setSelectedPermission}
        rowActionLabel={t('edit')}
        actions={<IconButton onClick={() => setCreatingPermission(true)} label={t('createPermission')} icon={<Plus size={16} />} variant="primary" />}
      />
      <DataTable title={t('effectivePermissions')} rows={effective.data} columns={['resourceType', 'operationType', 'permit', 'sourceRole']} onRefresh={effective.refresh} />
      <DataTable
        columns={['createdUtc', 'resourceType', 'operationType', 'reasonCode', 'statusCode']}
        onDelete={(row) => deleteRecord('/api/v1/audit', row)}
        onRefresh={audit.refresh}
        rows={audit.data}
        subtitle={t('auditSubtitle')}
        title={t('audit')}
      />
      {creatingRole && <RoleEditModal data={null} mode="create" onClose={() => setCreatingRole(false)} onSave={saveRole} permissions={permissions.data} />}
      {selectedRole && <RoleEditModal data={selectedRole} onClose={() => setSelectedRole(null)} onSave={saveRole} permissions={permissions.data} />}
      {creatingPermission && <PermissionEditModal data={null} mode="create" onClose={() => setCreatingPermission(false)} onSave={savePermission} />}
      {selectedPermission && <PermissionEditModal data={selectedPermission} onClose={() => setSelectedPermission(null)} onSave={savePermission} />}
    </div>
  );
}

function TenantsView() {
  const { t } = useTranslation();
  const tenants = useResource('/api/v1/tenants', []);
  const [selected, setSelected] = useState(null);
  const [creating, setCreating] = useState(false);

  async function saveTenant(tenantId, payload) {
    const result = tenantId
      ? await api.put(`/api/v1/tenants/${encodeURIComponent(tenantId)}`, payload)
      : await api.post('/api/v1/tenants', payload);
    if (result?.error) throw new Error(result.message || result.error);
    setSelected(result);
    setCreating(false);
    tenants.refresh();
  }

  return (
    <div className="view-stack">
      <DataTable
        columns={['id', 'name', 'active', 'createdUtc', 'lastUpdateUtc']}
        onDelete={(row) => {
          if (recordId(selected) === recordId(row)) setSelected(null);
          return deleteRecord('/api/v1/tenants', row);
        }}
        onRowClick={setSelected}
        onRefresh={tenants.refresh}
        rows={tenants.data}
        rowActionLabel={t('edit')}
        subtitle={t('tenantsTableSubtitle')}
        title={t('tenants')}
        actions={<IconButton onClick={() => setCreating(true)} label={t('createTenant')} icon={<Plus size={16} />} variant="primary" />}
      />
      {creating && <TenantEditModal data={null} mode="create" onClose={() => setCreating(false)} onSave={saveTenant} />}
      {selected && <TenantEditModal data={selected} onClose={() => setSelected(null)} onSave={saveTenant} />}
    </div>
  );
}

function UsersView() {
  const { t } = useTranslation();
  const users = useResource('/api/v1/users', []);
  const [selected, setSelected] = useState(null);
  const [creating, setCreating] = useState(false);

  async function saveUser(userId, payload) {
    const result = userId
      ? await api.put(`/api/v1/users/${encodeURIComponent(userId)}`, payload)
      : await api.post('/api/v1/users', payload);
    if (result?.error) throw new Error(result.message || result.error);
    setSelected(result);
    setCreating(false);
    users.refresh();
  }

  return (
    <div className="view-stack">
      <DataTable
        columns={['id', 'tenantId', 'username', 'email', 'isAdmin', 'isTenantAdmin', 'active']}
        onDelete={(row) => {
          if (recordId(selected) === recordId(row)) setSelected(null);
          return deleteRecord('/api/v1/users', row);
        }}
        onRowClick={setSelected}
        onRefresh={users.refresh}
        rows={users.data}
        rowActionLabel={t('edit')}
        subtitle={t('usersTableSubtitle')}
        title={t('users')}
        actions={<IconButton onClick={() => setCreating(true)} label={t('createUser')} icon={<Plus size={16} />} variant="primary" />}
      />
      {creating && <UserEditModal data={null} mode="create" onClose={() => setCreating(false)} onSave={saveUser} />}
      {selected && <UserEditModal data={selected} onClose={() => setSelected(null)} onSave={saveUser} />}
    </div>
  );
}

function CredentialsView() {
  const { t } = useTranslation();
  const credentials = useResource('/api/v1/credentials', []);
  const [selected, setSelected] = useState(null);
  const [creating, setCreating] = useState(false);
  const [created, setCreated] = useState(null);

  async function saveCredential(credentialId, payload) {
    const result = credentialId
      ? await api.put(`/api/v1/credentials/${encodeURIComponent(credentialId)}`, payload)
      : await api.post('/api/v1/credentials', payload);
    if (result?.error) throw new Error(result.message || result.error);
    if (credentialId) setSelected(result);
    else {
      setCreated(result);
      setSelected(null);
      setCreating(false);
    }
    credentials.refresh();
  }

  return (
    <div className="view-stack">
      <div className="toolbar align-end">
        <IconButton onClick={() => setCreating(true)} label={t('createCredential')} icon={<KeyRound size={16} />} variant="primary" />
      </div>
      {created ? <JsonPanelInline title={t('newCredential')} data={created} /> : null}
      <DataTable
        columns={['id', 'tenantId', 'userId', 'accessKey', 'secretKey', 'active']}
        onDelete={(row) => {
          if (recordId(selected) === recordId(row)) setSelected(null);
          return deleteRecord('/api/v1/credentials', row);
        }}
        onRowClick={setSelected}
        onRefresh={credentials.refresh}
        rows={credentials.data}
        rowActionLabel={t('edit')}
        subtitle={t('credentialsTableSubtitle')}
        title={t('credentials')}
      />
      {creating && <CredentialEditModal data={null} mode="create" onClose={() => setCreating(false)} onSave={saveCredential} />}
      {selected && <CredentialEditModal data={selected} onClose={() => setSelected(null)} onSave={saveCredential} />}
    </div>
  );
}

function ClientsView() {
  const { t } = useTranslation();
  const clients = useResource('/api/v1/clients', []);
  const [selectedClient, setSelectedClient] = useState(null);
  const clientModels = flattenClientModels(clients.data);
  const connected = clients.data.filter((client) => value(client, 'connected') === true).length;
  const advertisedModelCount = new Set(clientModels.map((model) => String(value(model, 'name') || '').toLowerCase()).filter(Boolean)).size;

  return (
    <div className="view-stack">
      <div className="metric-grid compact">
        <MetricCard icon={<Network size={18} />} label={t('connectedClients')} value={`${connected} / ${clients.data.length}`} tone={connected ? 'success' : 'warning'} />
        <MetricCard icon={<BrainCircuit size={18} />} label={t('advertisedModels')} value={formatNumber(advertisedModelCount)} />
        <MetricCard icon={<Server size={18} />} label={t('completionModels')} value={formatNumber(clientModels.filter((model) => value(model, 'supportsCompletions') === true).length)} />
      </div>
      <DataTable
        columns={['id', 'name', 'connected', 'serviceState', 'inFlightRequests', 'maxParallelRequests', 'lastHeartbeatUtc']}
        onDelete={(row) => {
          if (recordId(selectedClient) === recordId(row)) setSelectedClient(null);
          return deleteRecord('/api/v1/clients', row);
        }}
        onRefresh={clients.refresh}
        onRowClick={setSelectedClient}
        rows={clients.data}
        subtitle={t('clientsTableSubtitle')}
        title={t('clients')}
      />
      {selectedClient && <EditRecordModal title={t('clientDetails')} data={selectedClient} onClose={() => setSelectedClient(null)} />}
    </div>
  );
}

function AdvertisedModelsView() {
  const { t } = useTranslation();
  const clients = useResource('/api/v1/clients', []);
  const [selectedClientId, setSelectedClientId] = useState('all');
  const [selectedModel, setSelectedModel] = useState(null);
  const clientModels = flattenClientModels(clients.data);
  const filteredModels = selectedClientId === 'all'
    ? clientModels
    : clientModels.filter((model) => String(value(model, 'clientId') || '') === selectedClientId);
  const completionModelCount = filteredModels.filter((model) => value(model, 'supportsCompletions') === true).length;
  const embeddingModelCount = filteredModels.filter((model) => value(model, 'supportsEmbeddings') === true).length;

  useEffect(() => {
    if (selectedClientId === 'all') return;
    const exists = clients.data.some((client) => String(value(client, 'id') || '') === selectedClientId);
    if (!exists) setSelectedClientId('all');
  }, [clients.data, selectedClientId]);

  return (
    <div className="view-stack">
      <div className="metric-grid compact">
        <MetricCard icon={<BrainCircuit size={18} />} label={t('advertisedModels')} value={formatNumber(filteredModels.length)} />
        <MetricCard icon={<Server size={18} />} label={t('completionModels')} value={formatNumber(completionModelCount)} />
        <MetricCard icon={<Cpu size={18} />} label={t('embeddingModels')} value={formatNumber(embeddingModelCount)} tone="info" />
      </div>
      <div className="toolbar table-controls">
        <label className="form-field inline compact-field">
          <span>{t('clientFilter')}</span>
          <select value={selectedClientId} onChange={(event) => setSelectedClientId(event.target.value)}>
            <option value="all">{t('allClients')}</option>
            {clients.data.map((client) => {
              const clientId = String(value(client, 'id') || '');
              return (
                <option key={clientId || value(client, 'name')} value={clientId}>
                  {value(client, 'name') || clientId}
                </option>
              );
            })}
          </select>
        </label>
      </div>
      <DataTable
        columns={['clientName', 'clientId', 'name', 'provider', 'source', 'supportsCompletions', 'supportsEmbeddings', 'lastAdvertisedUtc']}
        onDelete={(row) => {
          if (recordId(selectedModel) === recordId(row)) setSelectedModel(null);
          return deleteClientModel(row);
        }}
        onRefresh={clients.refresh}
        onRowClick={setSelectedModel}
        rows={filteredModels}
        subtitle={t('advertisedModelsTableSubtitle')}
        title={t('advertisedModels')}
      />
      {selectedModel && <EditRecordModal title={t('modelDetails')} data={selectedModel} onClose={() => setSelectedModel(null)} />}
    </div>
  );
}

function AnalyticsView() {
  const { t } = useTranslation();
  const analytics = useResource('/api/v1/analytics/overview', { total: 0, success: 0, failures: 0, failureRate: 0 });
  const summary = useResource(requestHistorySummaryEndpoint, { buckets: [] });
  const history = useResource(requestHistoryEndpoint, []);
  const metrics = useTextResource('/metrics', '');
  const byModel = groupByCount(history.data, 'model');
  const byPath = groupByCount(history.data, 'path');

  function refreshAnalytics() {
    analytics.refresh();
    summary.refresh();
    history.refresh();
    metrics.refresh();
  }

  return (
    <div className="view-stack">
      <div className="metric-grid compact">
        <MetricCard icon={<Activity size={18} />} label={t('total')} value={formatNumber(analytics.data.total)} />
        <MetricCard icon={<Zap size={18} />} label={t('success')} value={formatNumber(analytics.data.success)} tone="success" />
        <MetricCard icon={<Shield size={18} />} label={t('failed')} value={formatNumber(analytics.data.failures)} tone="danger" />
        <MetricCard icon={<Gauge size={18} />} label={t('failureRate')} value={formatPercent(Number(analytics.data.failureRate || 0))} tone={Number(analytics.data.failureRate || 0) > 0 ? 'warning' : 'success'} />
      </div>
      <Panel
        actions={<IconButton iconOnly onClick={refreshAnalytics} label={t('refresh')} icon={<RefreshCw size={16} />} />}
        title={t('requestActivity')}
      >
        <ActivityChart summary={summary.data} />
      </Panel>
      <div className="workspace-grid">
        <DataTable title={t('requestsByModel')} rows={byModel} columns={['name', 'count', 'success', 'failed']} onDelete={(row) => deleteRequestHistoryGroup({ model: value(row, 'name') })} onRefresh={refreshAnalytics} />
        <DataTable title={t('requestsByPath')} rows={byPath} columns={['name', 'count', 'success', 'failed']} onDelete={(row) => deleteRequestHistoryGroup({ path: value(row, 'name') })} onRefresh={refreshAnalytics} />
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
    setStatus(settingSaveRequiresRestart(result) ? t('settingsSavedRestartRequired') : t('settingsSaved'));
  }

  async function saveJsonSettings() {
    const parsed = JSON.parse(configText || '{}');
    const result = await api.post('/api/v1/settings', parsed);
    const normalized = normalizeSettings(unwrapSettings(result));
    setDraft(normalized);
    setConfigText(JSON.stringify(normalized, null, 2));
    settings.refresh();
    setStatus(settingSaveRequiresRestart(result) ? t('settingsSavedRestartRequired') : t('settingsSaved'));
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
      <div className="settings-restart-banner">
        <StatusChip tone="warning" value={t('restartRequired')} />
        <span>{t('settingsRestartSummary')}</span>
      </div>
      <div className="workspace-grid settings-grid">
        <Panel actions={<SettingImpactBadge type="mixed" />} title={t('restSettings')}>
          <SettingImpactNote live={t('restLiveNote')} restart={t('restRestartNote')} />
          <div className="form-grid two">
            <label className="form-field">
              <span>{t('hostname')}</span>
              <input value={draft.Rest.Hostname} onChange={(event) => updateSetting('Rest', 'Hostname', event.target.value)} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field">
              <span>{t('port')}</span>
              <input min="1" type="number" value={draft.Rest.Port} onChange={(event) => updateSetting('Rest', 'Port', Number(event.target.value))} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="toggle-field">
              <input checked={draft.Rest.Ssl} onChange={(event) => updateSetting('Rest', 'Ssl', event.target.checked)} type="checkbox" />
              <span>{t('ssl')}</span>
              <small className="setting-impact restart inline">{t('restartRequired')}</small>
            </label>
            <label className="toggle-field">
              <input checked={draft.Rest.RequireAuthentication} onChange={(event) => updateSetting('Rest', 'RequireAuthentication', event.target.checked)} type="checkbox" />
              <span>{t('requireAuthentication')}</span>
              <small className="setting-impact live inline">{t('appliesImmediately')}</small>
            </label>
          </div>
        </Panel>
        <Panel actions={<SettingImpactBadge type="live" />} title={t('corsSettings')}>
          <SettingImpactNote live={t('corsLiveNote')} />
          <div className="form-stack">
            <label className="toggle-field">
              <input checked={draft.Cors.Enabled} onChange={(event) => updateSetting('Cors', 'Enabled', event.target.checked)} type="checkbox" />
              <span>{t('enabled')}</span>
              <small className="setting-impact live inline">{t('appliesImmediately')}</small>
            </label>
            <label className="form-field">
              <span>{t('allowedOrigins')}</span>
              <textarea rows={2} value={listToText(draft.Cors.AllowedOrigins)} onChange={(event) => updateListSetting('Cors', 'AllowedOrigins', event.target.value)} />
              <small className="setting-impact live">{t('appliesImmediately')}</small>
            </label>
            <div className="form-grid two">
              <label className="form-field">
                <span>{t('allowedHeaders')}</span>
                <textarea rows={2} value={listToText(draft.Cors.AllowedHeaders)} onChange={(event) => updateListSetting('Cors', 'AllowedHeaders', event.target.value)} />
                <small className="setting-impact live">{t('appliesImmediately')}</small>
              </label>
              <label className="form-field">
                <span>{t('allowedMethods')}</span>
                <textarea rows={2} value={listToText(draft.Cors.AllowedMethods)} onChange={(event) => updateListSetting('Cors', 'AllowedMethods', event.target.value)} />
                <small className="setting-impact live">{t('appliesImmediately')}</small>
              </label>
            </div>
          </div>
        </Panel>
        <Panel actions={<SettingImpactBadge type="mixed" />} title={t('webSocketSettings')}>
          <SettingImpactNote live={t('webSocketLiveNote')} restart={t('webSocketRestartNote')} />
          <div className="form-grid two">
            <label className="toggle-field">
              <input checked={draft.WebSocket.Enabled} onChange={(event) => updateSetting('WebSocket', 'Enabled', event.target.checked)} type="checkbox" />
              <span>{t('enabled')}</span>
              <small className="setting-impact restart inline">{t('restartRequired')}</small>
            </label>
            <label className="form-field">
              <span>{t('path')}</span>
              <input value={draft.WebSocket.Path} onChange={(event) => updateSetting('WebSocket', 'Path', event.target.value)} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field">
              <span>{t('heartbeatIntervalMs')}</span>
              <input min="1000" type="number" value={draft.WebSocket.HeartbeatIntervalMs} onChange={(event) => updateSetting('WebSocket', 'HeartbeatIntervalMs', Number(event.target.value))} />
              <small className="setting-impact live">{t('appliesToNewActivity')}</small>
            </label>
            <label className="form-field">
              <span>{t('requestTimeoutMs')}</span>
              <input min="1000" type="number" value={draft.WebSocket.RequestTimeoutMs} onChange={(event) => updateSetting('WebSocket', 'RequestTimeoutMs', Number(event.target.value))} />
              <small className="setting-impact live">{t('appliesToNewActivity')}</small>
            </label>
          </div>
        </Panel>
        <Panel actions={<SettingImpactBadge type="restart" />} title={t('databaseSettings')}>
          <SettingImpactNote restart={t('databaseRestartNote')} />
          <div className="form-grid two">
            <label className="form-field">
              <span>{t('provider')}</span>
              <select value={draft.Database.Type} onChange={(event) => updateSetting('Database', 'Type', event.target.value)}>
                {['Sqlite', 'Postgresql', 'SqlServer', 'Mysql'].map((item) => <option key={item} value={item}>{item}</option>)}
              </select>
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field">
              <span>{t('filename')}</span>
              <input value={draft.Database.Filename} onChange={(event) => updateSetting('Database', 'Filename', event.target.value)} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field">
              <span>{t('server')}</span>
              <input value={draft.Database.Server || ''} onChange={(event) => updateSetting('Database', 'Server', event.target.value)} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field">
              <span>{t('port')}</span>
              <input min="0" type="number" value={draft.Database.Port} onChange={(event) => updateSetting('Database', 'Port', Number(event.target.value))} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field">
              <span>{t('databaseName')}</span>
              <input value={draft.Database.DatabaseName || ''} onChange={(event) => updateSetting('Database', 'DatabaseName', event.target.value)} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field">
              <span>{t('username')}</span>
              <input value={draft.Database.Username || ''} onChange={(event) => updateSetting('Database', 'Username', event.target.value)} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field">
              <span>{t('password')}</span>
              <input type="password" value={draft.Database.Password || ''} onChange={(event) => updateSetting('Database', 'Password', event.target.value)} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field">
              <span>{t('connectionString')}</span>
              <input value={draft.Database.ConnectionString || ''} onChange={(event) => updateSetting('Database', 'ConnectionString', event.target.value)} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
          </div>
        </Panel>
        <Panel actions={<SettingImpactBadge type="restart" />} title={t('loggingSettings')}>
          <SettingImpactNote restart={t('loggingRestartNote')} />
          <div className="form-grid two">
            <label className="toggle-field">
              <input checked={draft.Logging.ConsoleLogging} onChange={(event) => updateSetting('Logging', 'ConsoleLogging', event.target.checked)} type="checkbox" />
              <span>{t('consoleLogging')}</span>
              <small className="setting-impact restart inline">{t('restartRequired')}</small>
            </label>
            <label className="toggle-field">
              <input checked={draft.Logging.FileLogging} onChange={(event) => updateSetting('Logging', 'FileLogging', event.target.checked)} type="checkbox" />
              <span>{t('fileLogging')}</span>
              <small className="setting-impact restart inline">{t('restartRequired')}</small>
            </label>
            <label className="form-field">
              <span>{t('logDirectory')}</span>
              <input value={draft.Logging.LogDirectory} onChange={(event) => updateSetting('Logging', 'LogDirectory', event.target.value)} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field">
              <span>{t('logFilename')}</span>
              <input value={draft.Logging.LogFilename} onChange={(event) => updateSetting('Logging', 'LogFilename', event.target.value)} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
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
        <SettingImpactNote live={t('rawJsonLiveNote')} restart={t('rawJsonRestartNote')} />
        <textarea className="backup-editor config-editor" value={configText} onChange={(event) => setConfigText(event.target.value)} aria-label={t('rawConfigJson')} />
      </Panel>
    </div>
  );
}

function SettingImpactBadge({ type }) {
  const { t } = useTranslation();
  const tone = type === 'live' ? 'success' : 'warning';
  const label = type === 'live' ? t('appliesImmediately') : type === 'mixed' ? t('mixedRestartImpact') : t('restartRequired');
  return <StatusChip tone={tone} value={label} />;
}

function SettingImpactNote({ live, restart }) {
  const { t } = useTranslation();
  if (!live && !restart) return null;
  return (
    <div className="settings-impact-note">
      {restart ? (
        <div className="setting-impact-line">
          <StatusChip tone="warning" value={t('restart')} />
          <span>{restart}</span>
        </div>
      ) : null}
      {live ? (
        <div className="setting-impact-line">
          <StatusChip tone="success" value={t('live')} />
          <span>{live}</span>
        </div>
      ) : null}
    </div>
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

function useResource(path, fallback) {
  const [data, setData] = useState(fallback);
  const [updatedAt, setUpdatedAt] = useState(0);
  const [version, setVersion] = useState(0);

  useEffect(() => {
    let alive = true;
    api.get(path)
      .then((result) => {
        if (!alive) return;
        setData(result);
        setUpdatedAt(Date.now());
      })
      .catch(() => {
        if (!alive) return;
        setData(fallback);
        setUpdatedAt(Date.now());
      });
    return () => { alive = false; };
  }, [path, version]);

  return { data, refresh: () => setVersion((current) => current + 1), updatedAt };
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

function settingSaveRequiresRestart(input) {
  return input?.restartRequired === true || input?.RestartRequired === true;
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

function Panel({ actions, children, className = '', eyebrow, title }) {
  return (
    <section className={`workspace-card ${className}`.trim()}>
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

function IconButton({ disabled = false, icon, iconOnly = false, label, onClick, variant = 'secondary' }) {
  return (
    <button className={`action-button ${variant} ${iconOnly ? 'icon-only' : ''}`} disabled={disabled} onClick={onClick} title={label} type="button">
      {icon}
      <span className={iconOnly ? 'sr-only' : ''}>{label}</span>
    </button>
  );
}

function DataTable({ actions, canDelete, columns, deleteMessage, deleteTitle, onDelete, onRefresh, onRowClick, rowActionLabel, rows, subtitle, title }) {
  const { t } = useTranslation();
  const [query, setQuery] = useState('');
  const [columnFilters, setColumnFilters] = useState({});
  const [deleteRow, setDeleteRow] = useState(null);
  const [deleting, setDeleting] = useState(false);
  const [jsonRow, setJsonRow] = useState(null);
  const [sort, setSort] = useState({ key: '', direction: 'asc' });
  const [currentPage, setCurrentPage] = useState(1);
  const [pageSize, setPageSize] = useState(10);
  const safeRows = collectionRows(rows);
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

  async function confirmDelete() {
    if (!deleteRow || !onDelete || deleting) return;
    setDeleting(true);
    try {
      await onDelete(deleteRow);
      setDeleteRow(null);
      onRefresh?.();
    } finally {
      setDeleting(false);
    }
  }

  function rowActions(row) {
    const deleteAllowed = Boolean(onDelete) && (canDelete ? canDelete(row) : true);
    const items = [];
    if (onRowClick) {
      items.push({
        icon: <Pencil size={14} />,
        key: 'details',
        label: rowActionLabel || t('viewDetails'),
        onSelect: () => onRowClick(row)
      });
    }
    items.push({
      icon: <FileJson size={14} />,
      key: 'view-json',
      label: t('viewJson'),
      onSelect: () => setJsonRow(row)
    });
    items.push({ divider: true, key: 'divider-delete' });
    items.push({
      destructive: true,
      disabled: !deleteAllowed,
      icon: <Trash2 size={14} />,
      key: 'delete',
      label: deleteAllowed ? t('delete') : t('deleteUnavailable'),
      onSelect: () => setDeleteRow(row)
    });
    return items;
  }

  const deletePrompt = deleteRow
    ? (typeof deleteMessage === 'function'
      ? deleteMessage(deleteRow)
      : deleteMessage || t('deleteRecordMessage', { record: describeRow(deleteRow) }))
    : '';

  return (
    <section className="workspace-card data-panel">
      <div className="workspace-card-header">
        <div>
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
              <th className="actions-cell">{t('actions')}</th>
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
                <th />
              </tr>
            ) : null}
          </thead>
          <tbody>
            {pagedRows.length === 0 && (
              <tr>
                <td className="empty-cell" colSpan={normalizedColumns.length + 1}>
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
                <td className="actions-cell">
                  <ActionMenu items={rowActions(row)} />
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {jsonRow ? <JsonRecordModal data={jsonRow} onClose={() => setJsonRow(null)} title={t('recordJson')} /> : null}
      <ConfirmModal
        confirmLabel={deleting ? t('deleting') : t('delete')}
        destructive
        message={deletePrompt}
        onCancel={() => setDeleteRow(null)}
        onConfirm={confirmDelete}
        open={Boolean(deleteRow)}
        title={deleteTitle || t('deleteRecordTitle')}
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

function EditRecordModal({ title, data, onClose }) {
  const { t } = useTranslation();
  const record = data || {};
  const entries = Object.entries(record);
  const scalarEntries = entries.filter(([, fieldValue]) => isScalarValue(fieldValue));
  const complexEntries = entries.filter(([, fieldValue]) => !isScalarValue(fieldValue));

  return (
    <Modal
      actions={<button className="action-button secondary" onClick={onClose} type="button">{t('close')}</button>}
      headerActions={<CopyButton label={t('copyJson')} value={stringifyJson(record)} />}
      onClose={onClose}
      open
      subtitle={t('recordDetailsSubtitle')}
      title={title}
    >
      <div className="record-detail-stack">
        {scalarEntries.length ? (
          <RecordSummaryTable entries={scalarEntries} />
        ) : null}
        {complexEntries.map(([key, fieldValue]) => (
          <RecordComplexSection fieldKey={key} key={key} value={fieldValue} />
        ))}
      </div>
    </Modal>
  );
}

function UserEditModal({ data, mode = 'edit', onClose, onSave }) {
  const { t } = useTranslation();
  const userId = recordId(data);
  const isCreate = mode === 'create';
  const [draft, setDraft] = useState(() => ({
    username: value(data, 'username') || '',
    email: value(data, 'email') || '',
    isAdmin: value(data, 'isAdmin') === true,
    isTenantAdmin: value(data, 'isTenantAdmin') === true,
    active: value(data, 'active') !== false,
    password: ''
  }));
  const [saving, setSaving] = useState(false);
  const [status, setStatus] = useState('');
  const [statusTone, setStatusTone] = useState('success');

  function updateField(key, fieldValue) {
    setDraft((current) => ({ ...current, [key]: fieldValue }));
    setStatus('');
    setStatusTone('success');
  }

  async function save() {
    if ((!isCreate && !userId) || saving) return;
    setSaving(true);
    setStatus('');
    try {
      const payload = {
        username: draft.username,
        email: draft.email,
        isAdmin: draft.isAdmin,
        isTenantAdmin: draft.isTenantAdmin,
        active: draft.active
      };
      if (draft.password.trim()) payload.password = draft.password;
      await onSave(isCreate ? null : userId, payload);
      setDraft((current) => ({ ...current, password: '' }));
      setStatusTone('success');
      setStatus(t('userSaved'));
    } catch (error) {
      setStatusTone('danger');
      setStatus(error instanceof Error ? error.message : t('saveFailed'));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      actions={(
        <>
          <button className="action-button secondary" onClick={onClose} type="button">{t('close')}</button>
          <button className="action-button primary" disabled={saving || !draft.username.trim() || (isCreate && !draft.password.trim())} onClick={save} type="button">
            {saving ? t('saving') : t('save')}
          </button>
        </>
      )}
      headerActions={<CopyButton label={t('copyJson')} value={stringifyJson(data || {})} />}
      onClose={onClose}
      open
      subtitle={isCreate ? t('createUserSubtitle') : t('editUserSubtitle')}
      title={isCreate ? t('createUser') : t('editUser')}
    >
      <div className="form-stack">
        {!isCreate ? <RecordSummaryTable entries={[['id', userId], ['tenantId', value(data, 'tenantId')]]} /> : null}
        <div className="form-grid two">
          <label className="form-field">
            <span>{t('username')}</span>
            <input value={draft.username} onChange={(event) => updateField('username', event.target.value)} />
          </label>
          <label className="form-field">
            <span>{t('email')}</span>
            <input value={draft.email} onChange={(event) => updateField('email', event.target.value)} />
          </label>
        </div>
        <div className="form-grid two">
          <label className="toggle-field">
            <input checked={draft.active} onChange={(event) => updateField('active', event.target.checked)} type="checkbox" />
            <span>{t('active')}</span>
          </label>
          <label className="toggle-field">
            <input checked={draft.isTenantAdmin} onChange={(event) => updateField('isTenantAdmin', event.target.checked)} type="checkbox" />
            <span>{t('isTenantAdmin')}</span>
          </label>
          <label className="toggle-field">
            <input checked={draft.isAdmin} onChange={(event) => updateField('isAdmin', event.target.checked)} type="checkbox" />
            <span>{t('isAdmin')}</span>
          </label>
        </div>
        <label className="form-field">
          <span>{t('newPassword')}</span>
          <input
            autoComplete="new-password"
            placeholder={isCreate ? t('passwordRequired') : t('passwordOptional')}
            type="password"
            value={draft.password}
            onChange={(event) => updateField('password', event.target.value)}
          />
        </label>
        {status ? <p className={`form-status ${statusTone}`}>{status}</p> : null}
      </div>
    </Modal>
  );
}

function TenantEditModal({ data, mode = 'edit', onClose, onSave }) {
  const { t } = useTranslation();
  const tenantId = recordId(data);
  const isCreate = mode === 'create';
  const [draft, setDraft] = useState(() => ({
    name: value(data, 'name') || '',
    active: value(data, 'active') !== false
  }));
  const [saving, setSaving] = useState(false);
  const [status, setStatus] = useState('');
  const [statusTone, setStatusTone] = useState('success');

  function updateField(key, fieldValue) {
    setDraft((current) => ({ ...current, [key]: fieldValue }));
    setStatus('');
    setStatusTone('success');
  }

  async function save() {
    if ((!isCreate && !tenantId) || saving) return;
    setSaving(true);
    setStatus('');
    try {
      await onSave(isCreate ? null : tenantId, { name: draft.name, active: draft.active });
      setStatusTone('success');
      setStatus(t('tenantSaved'));
    } catch (error) {
      setStatusTone('danger');
      setStatus(error instanceof Error ? error.message : t('saveFailed'));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      actions={(
        <>
          <button className="action-button secondary" onClick={onClose} type="button">{t('close')}</button>
          <button className="action-button primary" disabled={saving || !draft.name.trim()} onClick={save} type="button">
            {saving ? t('saving') : t('save')}
          </button>
        </>
      )}
      headerActions={<CopyButton label={t('copyJson')} value={stringifyJson(data || {})} />}
      onClose={onClose}
      open
      subtitle={isCreate ? t('createTenantSubtitle') : t('editTenantSubtitle')}
      title={isCreate ? t('createTenant') : t('editTenant')}
    >
      <div className="form-stack">
        {!isCreate ? <RecordSummaryTable entries={[['id', tenantId]]} /> : null}
        <label className="form-field">
          <span>{t('name')}</span>
          <input value={draft.name} onChange={(event) => updateField('name', event.target.value)} />
        </label>
        <label className="toggle-field">
          <input checked={draft.active} onChange={(event) => updateField('active', event.target.checked)} type="checkbox" />
          <span>{t('active')}</span>
        </label>
        {status ? <p className={`form-status ${statusTone}`}>{status}</p> : null}
      </div>
    </Modal>
  );
}

function CredentialEditModal({ data, mode = 'edit', onClose, onSave }) {
  const { t } = useTranslation();
  const credentialId = recordId(data);
  const isCreate = mode === 'create';
  const [draft, setDraft] = useState(() => ({
    userId: value(data, 'userId') || '',
    accessKey: value(data, 'accessKey') || '',
    secretKey: '',
    active: value(data, 'active') !== false
  }));
  const [saving, setSaving] = useState(false);
  const [status, setStatus] = useState('');
  const [statusTone, setStatusTone] = useState('success');

  function updateField(key, fieldValue) {
    setDraft((current) => ({ ...current, [key]: fieldValue }));
    setStatus('');
    setStatusTone('success');
  }

  async function save() {
    if ((!isCreate && !credentialId) || saving) return;
    setSaving(true);
    setStatus('');
    try {
      const payload = {
        userId: draft.userId,
        accessKey: draft.accessKey,
        active: draft.active
      };
      if (draft.secretKey.trim()) payload.secretKey = draft.secretKey;
      await onSave(isCreate ? null : credentialId, payload);
      setDraft((current) => ({ ...current, secretKey: '' }));
      setStatusTone('success');
      setStatus(t('credentialSaved'));
    } catch (error) {
      setStatusTone('danger');
      setStatus(error instanceof Error ? error.message : t('saveFailed'));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      actions={(
        <>
          <button className="action-button secondary" onClick={onClose} type="button">{t('close')}</button>
          <button className="action-button primary" disabled={saving || (!isCreate && !draft.accessKey.trim())} onClick={save} type="button">
            {saving ? t('saving') : t('save')}
          </button>
        </>
      )}
      headerActions={<CopyButton label={t('copyJson')} value={stringifyJson(data || {})} />}
      onClose={onClose}
      open
      subtitle={isCreate ? t('createCredentialSubtitle') : t('editCredentialSubtitle')}
      title={isCreate ? t('createCredential') : t('editCredential')}
    >
      <div className="form-stack">
        {!isCreate ? <RecordSummaryTable entries={[['id', credentialId], ['tenantId', value(data, 'tenantId')]]} /> : null}
        <div className="form-grid two">
          <label className="form-field">
            <span>{t('userId')}</span>
            <input value={draft.userId} onChange={(event) => updateField('userId', event.target.value)} />
          </label>
          <label className="form-field">
            <span>{t('accessKey')}</span>
            <input value={draft.accessKey} onChange={(event) => updateField('accessKey', event.target.value)} />
          </label>
        </div>
        <label className="form-field">
          <span>{t('secretKey')}</span>
          <input
            autoComplete="new-password"
            placeholder={isCreate ? t('secretKeyGeneratedOptional') : t('secretKeyOptional')}
            type="password"
            value={draft.secretKey}
            onChange={(event) => updateField('secretKey', event.target.value)}
          />
        </label>
        <label className="toggle-field">
          <input checked={draft.active} onChange={(event) => updateField('active', event.target.checked)} type="checkbox" />
          <span>{t('active')}</span>
        </label>
        {status ? <p className={`form-status ${statusTone}`}>{status}</p> : null}
      </div>
    </Modal>
  );
}

function RoleEditModal({ data, mode = 'edit', onClose, onSave, permissions }) {
  const { t } = useTranslation();
  const roleId = recordId(data);
  const isCreate = mode === 'create';
  const permissionRows = Array.isArray(permissions) ? permissions : [];
  const [draft, setDraft] = useState(() => ({
    name: value(data, 'name') || '',
    active: value(data, 'active') !== false,
    permissionIds: new Set(Array.isArray(value(data, 'permissionIds')) ? value(data, 'permissionIds') : [])
  }));
  const [saving, setSaving] = useState(false);
  const [status, setStatus] = useState('');
  const [statusTone, setStatusTone] = useState('success');

  function updateField(key, fieldValue) {
    setDraft((current) => ({ ...current, [key]: fieldValue }));
    setStatus('');
    setStatusTone('success');
  }

  function togglePermission(permissionId) {
    setDraft((current) => {
      const next = new Set(current.permissionIds);
      if (next.has(permissionId)) next.delete(permissionId);
      else next.add(permissionId);
      return { ...current, permissionIds: next };
    });
    setStatus('');
    setStatusTone('success');
  }

  async function save() {
    if ((!isCreate && !roleId) || saving) return;
    setSaving(true);
    setStatus('');
    try {
      await onSave(isCreate ? null : roleId, {
        name: draft.name,
        active: draft.active,
        permissionIds: [...draft.permissionIds]
      });
      setStatusTone('success');
      setStatus(t('roleSaved'));
    } catch (error) {
      setStatusTone('danger');
      setStatus(error instanceof Error ? error.message : t('saveFailed'));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      actions={(
        <>
          <button className="action-button secondary" onClick={onClose} type="button">{t('close')}</button>
          <button className="action-button primary" disabled={saving || !draft.name.trim()} onClick={save} type="button">
            {saving ? t('saving') : t('save')}
          </button>
        </>
      )}
      headerActions={<CopyButton label={t('copyJson')} value={stringifyJson(data || {})} />}
      onClose={onClose}
      open
      subtitle={isCreate ? t('createRoleSubtitle') : t('editRoleSubtitle')}
      title={isCreate ? t('createRole') : t('editRole')}
    >
      <div className="form-stack">
        {!isCreate ? <RecordSummaryTable entries={[['id', roleId], ['tenantId', value(data, 'tenantId')], ['builtIn', value(data, 'builtIn') === true]]} /> : null}
        <label className="form-field">
          <span>{t('name')}</span>
          <input value={draft.name} onChange={(event) => updateField('name', event.target.value)} />
        </label>
        <label className="toggle-field">
          <input checked={draft.active} onChange={(event) => updateField('active', event.target.checked)} type="checkbox" />
          <span>{t('active')}</span>
        </label>
        <div className="form-field">
          <span>{t('rolePermissions')}</span>
          <div className="permission-picker">
            {permissionRows.length === 0 ? <p className="empty-inline">{t('noPermissions')}</p> : permissionRows.map((permission) => {
              const permissionId = recordId(permission);
              return (
                <label className="permission-option" key={permissionId || describePermission(permission)}>
                  <input
                    checked={permissionId ? draft.permissionIds.has(permissionId) : false}
                    disabled={!permissionId}
                    onChange={() => togglePermission(permissionId)}
                    type="checkbox"
                  />
                  <span>
                    <strong>{describePermission(permission)}</strong>
                    <CopyableValue label={t('copyValue')} value={permissionId} />
                  </span>
                </label>
              );
            })}
          </div>
        </div>
        {status ? <p className={`form-status ${statusTone}`}>{status}</p> : null}
      </div>
    </Modal>
  );
}

function PermissionEditModal({ data, mode = 'edit', onClose, onSave }) {
  const { t } = useTranslation();
  const permissionId = recordId(data);
  const isCreate = mode === 'create';
  const [draft, setDraft] = useState(() => ({
    resourceType: value(data, 'resourceType') || '',
    operationType: value(data, 'operationType') || 'Read',
    permit: value(data, 'permit') !== false,
    active: value(data, 'active') !== false
  }));
  const [saving, setSaving] = useState(false);
  const [status, setStatus] = useState('');
  const [statusTone, setStatusTone] = useState('success');
  const operationTypes = ['Read', 'Write', 'Delete', 'Admin'];

  function updateField(key, fieldValue) {
    setDraft((current) => ({ ...current, [key]: fieldValue }));
    setStatus('');
    setStatusTone('success');
  }

  async function save() {
    if ((!isCreate && !permissionId) || saving) return;
    setSaving(true);
    setStatus('');
    try {
      await onSave(isCreate ? null : permissionId, draft);
      setStatusTone('success');
      setStatus(t('permissionSaved'));
    } catch (error) {
      setStatusTone('danger');
      setStatus(error instanceof Error ? error.message : t('saveFailed'));
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      actions={(
        <>
          <button className="action-button secondary" onClick={onClose} type="button">{t('close')}</button>
          <button className="action-button primary" disabled={saving || !draft.resourceType.trim()} onClick={save} type="button">
            {saving ? t('saving') : t('save')}
          </button>
        </>
      )}
      headerActions={<CopyButton label={t('copyJson')} value={stringifyJson(data || {})} />}
      onClose={onClose}
      open
      subtitle={isCreate ? t('createPermissionSubtitle') : t('editPermissionSubtitle')}
      title={isCreate ? t('createPermission') : t('editPermission')}
    >
      <div className="form-stack">
        {!isCreate ? <RecordSummaryTable entries={[['id', permissionId], ['tenantId', value(data, 'tenantId')]]} /> : null}
        <div className="form-grid two">
          <label className="form-field">
            <span>{t('resourceType')}</span>
            <input value={draft.resourceType} onChange={(event) => updateField('resourceType', event.target.value)} />
          </label>
          <label className="form-field">
            <span>{t('operationType')}</span>
            <select value={draft.operationType} onChange={(event) => updateField('operationType', event.target.value)}>
              {operationTypes.map((operationType) => <option key={operationType} value={operationType}>{operationType}</option>)}
            </select>
          </label>
        </div>
        <div className="form-grid two">
          <label className="toggle-field">
            <input checked={draft.permit} onChange={(event) => updateField('permit', event.target.checked)} type="checkbox" />
            <span>{t('permit')}</span>
          </label>
          <label className="toggle-field">
            <input checked={draft.active} onChange={(event) => updateField('active', event.target.checked)} type="checkbox" />
            <span>{t('active')}</span>
          </label>
        </div>
        {status ? <p className={`form-status ${statusTone}`}>{status}</p> : null}
      </div>
    </Modal>
  );
}

function JsonRecordModal({ data, onClose, title }) {
  const { t } = useTranslation();
  return (
    <Modal
      actions={<button className="action-button secondary" onClick={onClose} type="button">{t('close')}</button>}
      headerActions={<CopyButton label={t('copyJson')} value={stringifyJson(data)} />}
      onClose={onClose}
      open
      subtitle={t('recordJsonSubtitle')}
      title={title}
    >
      <JsonBlock data={data} />
    </Modal>
  );
}

function RecordSummaryTable({ entries }) {
  const { t } = useTranslation();
  const safeEntries = (Array.isArray(entries) ? entries : []).filter(([, fieldValue]) => fieldValue !== undefined && fieldValue !== null && fieldValue !== '');
  if (safeEntries.length === 0) return null;

  return (
    <div className="record-summary-table-wrap">
      <table className="record-summary-table">
        <tbody>
          {safeEntries.map(([key, fieldValue]) => (
            <tr key={key}>
              <th>{t(key, key)}</th>
              <td><CellValue columnKey={key} value={fieldValue} /></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function RecordField({ fieldKey, value: fieldValue }) {
  const { t } = useTranslation();
  return (
    <div className="record-field">
      <span className="record-field-label">{t(fieldKey, fieldKey)}</span>
      <div className="record-field-value">
        <CellValue columnKey={fieldKey} value={fieldValue} />
      </div>
    </div>
  );
}

function RecordComplexSection({ fieldKey, value: fieldValue }) {
  const { t } = useTranslation();
  const title = t(fieldKey, fieldKey);

  if (Array.isArray(fieldValue)) {
    return (
      <section className="record-section">
        <div className="record-section-header">
          <div>
            <span className="eyebrow">{t('collection')}</span>
            <h3>{title}</h3>
          </div>
          <span className="row-count">{fieldValue.length} {t('items')}</span>
        </div>
        {fieldValue.length === 0 ? (
          <p className="muted-copy">{t('emptyCollection')}</p>
        ) : fieldValue.every(isPlainRecord) ? (
          <NestedRecordTable rows={fieldValue} />
        ) : (
          <div className="primitive-list">
            {fieldValue.map((item, index) => (
              <span className="primitive-list-item" key={`${fieldKey}-${index}`}>
                <CellValue columnKey={fieldKey} value={item} />
              </span>
            ))}
          </div>
        )}
      </section>
    );
  }

  if (isPlainRecord(fieldValue)) {
    const entries = Object.entries(fieldValue);
    const scalarEntries = entries.filter(([, itemValue]) => isScalarValue(itemValue));
    const complexEntries = entries.filter(([, itemValue]) => !isScalarValue(itemValue));
    return (
      <section className="record-section">
        <div className="record-section-header">
          <div>
            <span className="eyebrow">{t('object')}</span>
            <h3>{title}</h3>
          </div>
        </div>
        {scalarEntries.length ? (
          <RecordSummaryTable entries={scalarEntries} />
        ) : null}
        {complexEntries.map(([key, itemValue]) => (
          <RecordComplexSection fieldKey={key} key={key} value={itemValue} />
        ))}
      </section>
    );
  }

  return (
    <section className="record-section">
      <div className="record-section-header">
        <div>
          <span className="eyebrow">{t('value')}</span>
          <h3>{title}</h3>
        </div>
      </div>
      <CellValue columnKey={fieldKey} value={fieldValue} />
    </section>
  );
}

function NestedRecordTable({ rows }) {
  const { t } = useTranslation();
  const columns = nestedRecordColumns(rows);

  if (columns.length === 0) {
    return <JsonBlock data={rows} />;
  }

  return (
    <div className="nested-table-scroll">
      <table className="nested-record-table">
        <thead>
          <tr>
            {columns.map((column) => (
              <th key={column}>{t(column, column)}</th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((row, rowIndex) => (
            <tr key={value(row, 'id') || value(row, 'guid') || rowIndex}>
              {columns.map((column) => (
                <td key={column}>
                  <CellValue columnKey={column} value={value(row, column)} />
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function ExplorerResponsePanel({ isSending, onClear, response }) {
  const { t } = useTranslation();
  const [activeTab, setActiveTab] = useState('preview');
  const normalized = normalizeExplorerResponse(response);
  const tabs = [
    { id: 'preview', label: t('responsePreview') },
    { id: 'body', label: t('responseBody') },
    { id: 'headers', label: t('responseHeaders') },
    { id: 'status', label: t('responseStatus') }
  ];
  const bodyText = explorerResponseBodyText(normalized);
  const headersText = formatExplorerValue(normalized.headers);
  const previewText = normalized.preview || extractExplorerPreview(normalized.body) || bodyText;
  const hasHeaders = Object.keys(normalized.headers || {}).length > 0;
  const hasStatus = normalized.status !== null || normalized.error;

  return (
    <section className="api-explorer-card">
      <div className="api-explorer-card-header api-explorer-response-header">
        <h2>{t('jsonResponse')}</h2>
        <div className="api-explorer-tabs" role="tablist" aria-label={t('response')}>
          {tabs.map((tab) => (
            <button
              aria-selected={activeTab === tab.id}
              className={`api-explorer-tab ${activeTab === tab.id ? 'active' : ''}`}
              key={tab.id}
              onClick={() => setActiveTab(tab.id)}
              role="tab"
              type="button"
            >
              {tab.label}
            </button>
          ))}
        </div>
        <div className="api-explorer-response-actions">
          {renderExplorerStatusBadge(normalized, t)}
          {response ? <IconButton iconOnly onClick={onClear} label={t('clear')} icon={<Trash2 size={16} />} /> : null}
        </div>
      </div>
      <div className="api-explorer-response-body">
        {activeTab === 'preview' ? (
          <div className="api-explorer-tab-content">
            {isSending ? (
              <div className="api-explorer-streaming">
                <span className="streaming-dot" />
                <span>{normalized.streamed ? t('streamingResponse') : t('waitingForResponse')}</span>
              </div>
            ) : null}
            {previewText ? (
              <>
                <pre className="api-explorer-code plain">{previewText}</pre>
                <div className="api-explorer-copy-overlay">
                  <CopyButton label={t('copy')} value={previewText} />
                </div>
              </>
            ) : (
              <EmptyState title={t('noData')} message={isSending ? t('waitingForResponse') : t('noResponseBody')} />
            )}
          </div>
        ) : null}

        {activeTab === 'body' ? (
          <div className="api-explorer-tab-content">
            {bodyText ? (
              <>
                <pre className="api-explorer-code">{bodyText}</pre>
                <div className="api-explorer-copy-overlay">
                  <CopyButton label={t('copyJson')} value={bodyText} />
                </div>
              </>
            ) : (
              <EmptyState title={t('noData')} message={t('noResponseBody')} />
            )}
          </div>
        ) : null}

        {activeTab === 'headers' ? (
          <div className="api-explorer-tab-content">
            {hasHeaders ? (
              <>
                <pre className="api-explorer-code">{headersText}</pre>
                <div className="api-explorer-copy-overlay">
                  <CopyButton label={t('copyJson')} value={headersText} />
                </div>
              </>
            ) : (
              <EmptyState title={t('noData')} message={t('noResponseHeaders')} />
            )}
          </div>
        ) : null}

        {activeTab === 'status' ? (
          <div className="api-explorer-tab-content">
            {hasStatus ? (
              <div className="api-explorer-metrics">
                <MetricTile label={t('status')} value={normalized.error ? normalized.error : `${normalized.status} ${normalized.statusText || ''}`.trim()} />
                <MetricTile label={t('transferMode')} value={normalized.streamed ? streamTypeLabel(normalized.streamType, t) : t('standardResponse')} />
                <MetricTile label={t('timeToFirstByte')} value={formatDuration(normalized.timing.timeToFirstByteMs)} />
                <MetricTile label={t('totalTime')} value={formatDuration(normalized.timing.totalTimeMs ?? normalized.timing.requestTimeMs)} />
              </div>
            ) : (
              <EmptyState title={t('noData')} message={t('noResponseStatus')} />
            )}
          </div>
        ) : null}
      </div>
    </section>
  );
}

function MetricTile({ label, value }) {
  return (
    <div className="api-explorer-metric">
      <span>{label}</span>
      <strong>{value || '-'}</strong>
    </div>
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

function recordId(row) {
  return value(row, 'id') || value(row, 'guid');
}

function describeRow(row) {
  return String(
    value(row, 'name')
    || value(row, 'username')
    || value(row, 'email')
    || value(row, 'accessKey')
    || value(row, 'path')
    || recordId(row)
    || 'record'
  );
}

function describePermission(permission) {
  const resource = value(permission, 'resourceType') || 'Resource';
  const operation = value(permission, 'operationType') || 'Operation';
  const permit = value(permission, 'permit') === false ? 'Deny' : 'Allow';
  return `${resource} / ${operation} / ${permit}`;
}

async function deleteRecord(path, row) {
  const id = recordId(row);
  if (!id) throw new Error('Record id is required.');
  await api.delete(`${path}/${encodeURIComponent(id)}`);
}

async function deleteClientModel(row) {
  await api.delete('/api/v1/clientmodels', {
    clientId: value(row, 'clientId'),
    name: value(row, 'name'),
    source: value(row, 'source')
  });
}

async function deleteRequestHistoryGroup(filter) {
  await api.post(`${requestHistoryEndpoint}/bulk`, filter);
}

function normalizeExplorerResponse(response) {
  return {
    body: response?.body ?? null,
    error: response?.error || '',
    headers: response?.headers || {},
    ok: response?.ok === true,
    preview: response?.preview || '',
    rawBody: response?.rawBody || '',
    status: response?.status ?? null,
    statusText: response?.statusText || '',
    streamed: response?.streamed === true,
    streamType: response?.streamType || 'standard',
    timing: response?.timing || {}
  };
}

function explorerResponseBodyText(response) {
  if (response.rawBody) return formatExplorerValue(response.rawBody);
  if (response.body !== null && response.body !== undefined) return formatExplorerValue(response.body);
  return '';
}

function formatExplorerValue(value) {
  if (value === undefined || value === null) return '';
  if (typeof value === 'string') {
    if (!value.trim()) return '';
    try {
      return JSON.stringify(JSON.parse(value), null, 2);
    } catch {
      return value;
    }
  }

  return JSON.stringify(value, null, 2);
}

function extractExplorerPreview(value) {
  if (value === undefined || value === null) return '';
  if (typeof value === 'string') return value;
  if (Array.isArray(value)) return value.map(extractExplorerPreview).filter(Boolean).join('');
  if (typeof value !== 'object') return String(value);

  const choice = Array.isArray(value.choices) ? value.choices[0] : null;
  if (typeof choice?.message?.content === 'string') return choice.message.content;
  if (typeof choice?.delta?.content === 'string') return choice.delta.content;
  if (typeof choice?.text === 'string') return choice.text;
  if (typeof value.response === 'string') return value.response;
  if (typeof value.output_text === 'string') return value.output_text;
  if (typeof value.content === 'string') return value.content;
  return '';
}

function renderExplorerStatusBadge(response, t) {
  if (response.error) return <StatusChip tone="danger" value={response.statusText || t('failed')} />;
  if (response.status === null) return null;
  if (response.status >= 200 && response.status < 300) return <StatusChip tone="success" value={`${response.status} ${response.statusText || ''}`.trim()} />;
  if (response.status >= 400) return <StatusChip tone="danger" value={`${response.status} ${response.statusText || ''}`.trim()} />;
  return <StatusChip tone="warning" value={`${response.status} ${response.statusText || ''}`.trim()} />;
}

function streamTypeLabel(streamType, t) {
  if (streamType === 'sse') return t('serverSentEvents');
  if (streamType === 'ndjson') return t('ndjsonStream');
  if (streamType === 'chunked') return t('chunkedResponse');
  return t('standardResponse');
}

function formatDuration(value) {
  const number = Number(value);
  if (!Number.isFinite(number)) return '-';
  if (number < 1000) return `${Math.max(0, number).toFixed(0)} ms`;
  return `${(number / 1000).toFixed(2)} s`;
}

function buildApiOperations(openApi) {
  const paths = openApi?.paths && typeof openApi.paths === 'object' ? openApi.paths : {};
  const methods = ['get', 'post', 'put', 'delete', 'patch'];
  return Object.entries(paths)
    .flatMap(([path, pathItem]) => methods
      .filter((method) => pathItem?.[method])
      .map((method) => {
        const operation = pathItem[method] || {};
        const tag = Array.isArray(operation.tags) && operation.tags.length > 0 ? operation.tags[0] : 'Other';
        return {
          description: operation.description || '',
          key: `${method.toUpperCase()} ${path}`,
          method: method.toUpperCase(),
          operationId: operation.operationId || `${method}_${path}`,
          path,
          summary: operation.summary || path,
          tag
        };
      }))
    .sort((left, right) => {
      const tag = String(left.tag).localeCompare(String(right.tag));
      if (tag !== 0) return tag;
      const path = String(left.path).localeCompare(String(right.path));
      if (path !== 0) return path;
      return methodWeight(left.method) - methodWeight(right.method);
    });
}

function methodWeight(method) {
  return { GET: 1, POST: 2, PUT: 3, PATCH: 4, DELETE: 5 }[method] || 99;
}

function methodTone(method) {
  if (method === 'GET') return 'neutral';
  if (method === 'POST') return 'success';
  if (method === 'PUT' || method === 'PATCH') return 'warning';
  if (method === 'DELETE') return 'danger';
  return 'neutral';
}

function extractPathParameters(path) {
  return [...String(path || '').matchAll(/\{([^}]+)\}/g)].map((match) => match[1]);
}

function buildExplorerPath(path, pathValues, query) {
  const resolvedPath = String(path || '').replace(/\{([^}]+)\}/g, (_, name) => {
    const value = pathValues?.[name];
    return value ? encodeURIComponent(value) : `{${name}}`;
  });
  const search = new URLSearchParams();
  Object.entries(query && typeof query === 'object' && !Array.isArray(query) ? query : {}).forEach(([key, itemValue]) => {
    if (itemValue === undefined || itemValue === null || itemValue === '') return;
    if (Array.isArray(itemValue)) {
      itemValue.forEach((arrayValue) => search.append(key, String(arrayValue)));
    } else {
      search.append(key, String(itemValue));
    }
  });
  const queryString = search.toString();
  return queryString ? `${resolvedPath}?${queryString}` : resolvedPath;
}

function extractModelOptions(input) {
  const discovered = collectionRows(input)
    .map((model) => value(model, 'id') || value(model, 'name'))
    .filter(Boolean)
    .map(String);
  return [...new Set([...discovered, 'gpt-oss:120b', 'all-minilm'])];
}

function operationKind(operation) {
  if (!operation) return '';

  const operationId = String(operation.operationId || '').toLowerCase();
  const method = String(operation.method || '').toUpperCase();
  const path = normalizeOperationPath(operation.path);
  if (operationId === 'createchatcompletion' || path.endsWith('/v1/chat/completions')) return 'createChatCompletion';
  if (operationId === 'createcompletion' || path.endsWith('/v1/completions')) return 'createCompletion';
  if (operationId === 'createembedding' || path.endsWith('/v1/embeddings')) return 'createEmbedding';
  if (operationId) {
    const known = {
      login: 'login',
      createtoken: 'createToken',
      refreshtoken: 'refreshToken',
      logout: 'logout',
      createtenant: 'createTenant',
      updatetenant: 'updateTenant',
      createuser: 'createUser',
      updateuser: 'updateUser',
      createcredential: 'createCredential',
      updatecredential: 'updateCredential',
      createrole: 'createRole',
      updaterole: 'updateRole',
      createpermission: 'createPermission',
      updatepermission: 'updatePermission',
      deleteclientmodel: 'deleteClientModel',
      bulkdeleterequesthistory: 'bulkDeleteRequestHistory',
      updatesettings: 'updateSettings',
      restore: 'restore',
      deletetenant: 'deleteTenant',
      deleteuser: 'deleteUser',
      deletecredential: 'deleteCredential',
      deleterole: 'deleteRole',
      deletepermission: 'deletePermission',
      deleteauditrecord: 'deleteAuditRecord',
      deleteclient: 'deleteClient',
      deleterequesthistory: 'deleteRequestHistory'
    };
    if (known[operationId]) return known[operationId];
  }

  if (method === 'POST' && path.endsWith('/auth/login')) return 'login';
  if (method === 'POST' && path.endsWith('/auth/token')) return 'createToken';
  if (method === 'POST' && path.endsWith('/auth/token/refresh')) return 'refreshToken';
  if (method === 'POST' && path.endsWith('/auth/logout')) return 'logout';
  if (method === 'POST' && path.endsWith('/tenants')) return 'createTenant';
  if (method === 'PUT' && path.includes('/tenants/{id}')) return 'updateTenant';
  if (method === 'POST' && path.endsWith('/users')) return 'createUser';
  if (method === 'PUT' && path.includes('/users/{id}')) return 'updateUser';
  if (method === 'POST' && path.endsWith('/credentials')) return 'createCredential';
  if (method === 'PUT' && path.includes('/credentials/{id}')) return 'updateCredential';
  if (method === 'POST' && path.endsWith('/roles')) return 'createRole';
  if (method === 'PUT' && path.includes('/roles/{id}')) return 'updateRole';
  if (method === 'POST' && path.endsWith('/permissions')) return 'createPermission';
  if (method === 'PUT' && path.includes('/permissions/{id}')) return 'updatePermission';
  if (method === 'DELETE' && (path.endsWith('/client-models') || path.endsWith('/clientmodels'))) return 'deleteClientModel';
  if (method === 'POST' && path.endsWith('/request-history/bulk')) return 'bulkDeleteRequestHistory';
  if (method === 'POST' && path.endsWith('/requesthistory/bulk')) return 'bulkDeleteRequestHistory';
  if (method === 'POST' && path.endsWith('/settings')) return 'updateSettings';
  if (method === 'POST' && path.endsWith('/restore')) return 'restore';
  return '';
}

function normalizeOperationPath(path) {
  const normalized = String(path || '').toLowerCase().replace(/\/+$/, '');
  if (normalized.startsWith('/api/v1/openai/v1/')) return normalized.replace('/api/v1/openai/v1/', '/v1/');
  if (normalized.startsWith('/api/v1/requesthistory')) return normalized.replace('/api/v1/requesthistory', '/v1.0/api/request-history');
  if (normalized === '/api/v1/clientmodels') return '/v1.0/api/client-models';
  return normalized;
}

function operationUsesModel(operation) {
  return ['createChatCompletion', 'createCompletion', 'createEmbedding'].includes(operationKind(operation));
}

function modelForOperation(operation, model) {
  const current = String(model || '').trim();
  const kind = operationKind(operation);
  if (kind === 'createEmbedding') {
    return !current || current === 'gpt-oss:120b' ? 'all-minilm' : current;
  }

  if (kind === 'createChatCompletion' || kind === 'createCompletion') {
    return !current || current === 'all-minilm' ? 'gpt-oss:120b' : current;
  }

  return current;
}

function defaultQueryForOperation(operation) {
  if (!operation) return {};
  if (operation.operationId === 'summarizeRequestHistory') return { bucketMinutes: 60 };
  return {};
}

function exampleBodyForOperation(operation, model) {
  if (!operation || operation.method === 'GET') return {};
  const selectedModel = modelForOperation(operation, model);

  switch (operationKind(operation)) {
    case 'login':
      return {
        tenantId: 'ten_default',
        username: 'admin@xeno',
        password: 'password'
      };
    case 'createToken':
      return {
        accessKey: 'default',
        secretKey: 'default'
      };
    case 'refreshToken':
    case 'logout':
      return {};
    case 'createTenant':
      return {
        id: '',
        name: 'Example Tenant',
        active: true
      };
    case 'updateTenant':
      return {
        name: 'Updated Tenant',
        active: true
      };
    case 'createUser':
      return {
        tenantId: 'ten_default',
        username: 'user@example.com',
        email: 'user@example.com',
        password: 'password',
        isAdmin: false,
        isTenantAdmin: false,
        active: true
      };
    case 'updateUser':
      return {
        username: 'user@example.com',
        email: 'user@example.com',
        isAdmin: false,
        isTenantAdmin: false,
        active: true
      };
    case 'createCredential':
      return {
        tenantId: 'ten_default',
        userId: 'usr_replace_me',
        accessKey: '',
        secretKey: '',
        active: true
      };
    case 'updateCredential':
      return {
        userId: 'usr_replace_me',
        accessKey: 'default',
        secretKey: '',
        active: true
      };
    case 'createRole':
      return {
        name: 'Example Role',
        permissionIds: [],
        active: true
      };
    case 'updateRole':
      return {
        name: 'Updated Role',
        permissionIds: [],
        active: true
      };
    case 'createPermission':
      return {
        resourceType: 'Clients',
        operationType: 'Read',
        permit: true,
        active: true
      };
    case 'updatePermission':
      return {
        resourceType: 'Clients',
        operationType: 'Read',
        permit: true,
        active: true
      };
    case 'deleteClientModel':
      return {
        clientId: 'xcl_replace_me',
        name: selectedModel || 'gpt-oss:120b',
        source: ''
      };
    case 'bulkDeleteRequestHistory':
      return {
        ids: [],
        model: selectedModel || 'gpt-oss:120b',
        path: '/v1/chat/completions',
        selectedClientId: ''
      };
    case 'updateSettings':
      return defaultServerSettings();
    case 'restore':
      return samplePlatformState();
    case 'createChatCompletion':
      return {
        model: selectedModel || 'gpt-oss:120b',
        messages: openAiChatMessages(),
        temperature: 0.7,
        max_tokens: 150
      };
    case 'createCompletion':
      return {
        model: selectedModel || 'gpt-oss:120b',
        prompt: 'Write a concise explanation of how an inverted index works in search engines.',
        temperature: 0.7,
        max_tokens: 150
      };
    case 'createEmbedding':
      return {
        model: selectedModel || 'all-minilm',
        input: embeddingInputsFromText()
      };
    case 'deleteTenant':
    case 'deleteUser':
    case 'deleteCredential':
    case 'deleteRole':
    case 'deletePermission':
    case 'deleteAuditRecord':
    case 'deleteClient':
    case 'deleteRequestHistory':
      return {};
    default:
      return {};
  }
}

function openAiChatMessages(prompt) {
  return [
    {
      role: 'system',
      content: defaultOpenAiSystemPrompt
    },
    {
      role: 'user',
      content: prompt || defaultOpenAiPrompt
    }
  ];
}

function embeddingInputsFromText(input) {
  const values = String(input || defaultEmbeddingInputText)
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean);
  return values.length > 0 ? values : defaultEmbeddingInputText.split(/\r?\n/);
}

function samplePlatformState() {
  return {
    tenants: [
      {
        id: 'ten_default',
        name: 'Default',
        active: true
      }
    ],
    users: [],
    credentials: [],
    roles: [],
    permissions: [],
    roleAssignments: [],
    clients: [],
    modelDefinitions: [],
    modelConfigurations: [],
    requestHistory: [],
    auditRecords: [],
    authSessions: []
  };
}

function formatJson(input) {
  return JSON.stringify(input ?? {}, null, 2);
}

function parseJsonOrFallback(text, fallback) {
  if (typeof text !== 'string' || text.trim() === '') return fallback;
  try {
    return JSON.parse(text);
  } catch {
    return fallback;
  }
}

function parseJsonForExplorer(text, fallback) {
  if (typeof text !== 'string' || text.trim() === '') return { ok: true, value: fallback };
  try {
    return { ok: true, value: JSON.parse(text) };
  } catch (error) {
    return { error: error instanceof Error ? error.message : String(error), ok: false, value: fallback };
  }
}

function value(row, key) {
  if (!row) return undefined;
  const pascal = key.charAt(0).toUpperCase() + key.slice(1);
  return row[key] ?? row[pascal];
}

function collectionRows(input) {
  if (Array.isArray(input)) return input;
  if (!input || typeof input !== 'object') return [];

  const keys = ['data', 'Data', 'items', 'Items', 'rows', 'Rows', 'entries', 'Entries', 'records', 'Records', 'requestHistory', 'RequestHistory'];
  for (const key of keys) {
    if (Array.isArray(input[key])) return input[key];
  }

  return [];
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
    const total = Number(bucket.count ?? bucket.Count ?? 0);
    const failed = Number(bucket.failed ?? bucket.Failed ?? bucket.errors ?? bucket.Errors ?? 0);
    const explicitSuccess = bucket.success ?? bucket.Success;
    const success = explicitSuccess === undefined || explicitSuccess === null
      ? Math.max(0, total - failed)
      : Number(explicitSuccess);
    const normalizedFailed = Number.isFinite(failed) ? failed : 0;
    const normalizedSuccess = Number.isFinite(success) ? success : 0;
    return {
      failed: summary.failed + normalizedFailed,
      success: summary.success + normalizedSuccess,
      total: summary.total + normalizedSuccess + normalizedFailed
    };
  }, { failed: 0, success: 0, total: 0 });
}

function groupByCount(rows, key) {
  const groups = new Map();
  collectionRows(rows).forEach((row) => {
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

function flattenClientModels(clients) {
  return (Array.isArray(clients) ? clients : []).flatMap((client) => {
    const clientId = value(client, 'id');
    const clientName = value(client, 'name');
    const models = value(client, 'models');
    return (Array.isArray(models) ? models : []).map((model, index) => ({
      id: `${clientId || clientName || 'client'}:${value(model, 'name') || index}`,
      clientId,
      clientName,
      name: value(model, 'name'),
      provider: value(model, 'provider'),
      source: value(model, 'source'),
      supportsCompletions: value(model, 'supportsCompletions'),
      supportsEmbeddings: value(model, 'supportsEmbeddings'),
      lastAdvertisedUtc: value(model, 'lastAdvertisedUtc')
    }));
  });
}

function isScalarValue(input) {
  return input === undefined
    || input === null
    || typeof input === 'string'
    || typeof input === 'number'
    || typeof input === 'boolean';
}

function isPlainRecord(input) {
  return input !== null && typeof input === 'object' && !Array.isArray(input) && !isReactNode(input);
}

function nestedRecordColumns(rows) {
  const preferred = ['id', 'name', 'provider', 'source', 'supportsCompletions', 'supportsEmbeddings', 'lastAdvertisedUtc', 'active'];
  const keys = [];
  (Array.isArray(rows) ? rows : []).forEach((row) => {
    Object.entries(row || {}).forEach(([key, itemValue]) => {
      if (!isScalarValue(itemValue) || keys.includes(key)) return;
      keys.push(key);
    });
  });
  return keys.sort((left, right) => {
    const leftIndex = preferred.indexOf(left);
    const rightIndex = preferred.indexOf(right);
    if (leftIndex === -1 && rightIndex === -1) return 0;
    if (leftIndex === -1) return 1;
    if (rightIndex === -1) return -1;
    return leftIndex - rightIndex;
  });
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

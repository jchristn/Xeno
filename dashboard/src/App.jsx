import { lazy, Suspense, useEffect, useMemo, useRef, useState } from 'react';
import { BrowserRouter, Navigate, NavLink, Route, Routes, useLocation, useNavigate, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import {
  Activity,
  ArrowDown,
  ArrowUp,
  ArrowUpDown,
  BarChart3,
  BrainCircuit,
  ChevronDown,
  ChevronRight,
  Cpu,
  Download,
  FileJson,
  Gauge,
  GitBranch,
  Globe2,
  History,
  KeyRound,
  Layers3,
  LineChart,
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
import ApiClient, { authFailureEventName } from './utils/api.js';
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
const dashboardSessionKey = 'xeno.dashboard.session';
const portalSessionHashKey = 'xeno-portal-session';
const requestHistoryEndpoint = '/v1.0/api/request-history';
const requestHistorySummaryEndpoint = `${requestHistoryEndpoint}/summary`;
const analyticsOverviewEndpoint = '/api/v1/analytics/overview';
const analyticsRanges = [
  { key: 'lastHour', durationMs: 60 * 60 * 1000, bucketMinutes: 1 },
  { key: 'lastDay', durationMs: 24 * 60 * 60 * 1000, bucketMinutes: 15 },
  { key: 'lastWeek', durationMs: 7 * 24 * 60 * 60 * 1000, bucketMinutes: 120 },
  { key: 'lastMonth', durationMs: 30 * 24 * 60 * 60 * 1000, bucketMinutes: 480 }
];
const telemetryChartOpenDefaults = {
  cpu: true,
  memory: false,
  gpu: false,
  providerAvailable: true,
  providerLoaded: false,
  providerActive: false
};
const hostTelemetryChartConfigs = [
  { color: 'var(--color-info)', key: 'cpu', labelFallback: 'CPU', labelKey: 'cpu', metric: 'CpuUtilizationPercent', titleFallback: 'CPU Telemetry', titleKey: 'cpuTelemetry' },
  { color: 'var(--color-success)', key: 'memory', labelFallback: 'Memory', labelKey: 'memory', metric: 'MemoryUtilizationPercent', titleFallback: 'Memory Telemetry', titleKey: 'memoryTelemetry' },
  { color: 'var(--color-warning)', key: 'gpu', labelFallback: 'GPU', labelKey: 'gpu', metric: 'GpuUtilizationPercent', titleFallback: 'GPU Telemetry', titleKey: 'gpuTelemetry' }
];
const providerTelemetryChartConfigs = [
  { color: 'var(--color-success)', key: 'providerAvailable', labelFallback: 'Available Models', labelKey: 'availableModels', metric: 'AvailableModelCount', rollupKey: 'available', titleFallback: 'Available Models Telemetry', titleKey: 'availableModelsTelemetry' },
  { color: 'var(--color-warning)', key: 'providerLoaded', labelFallback: 'Loaded Models', labelKey: 'loadedModels', metric: 'LoadedModelCount', rollupKey: 'loaded', titleFallback: 'Loaded Models Telemetry', titleKey: 'loadedModelsTelemetry' },
  { color: 'var(--color-info)', key: 'providerActive', labelFallback: 'Active Requests', labelKey: 'activeRequests', metric: 'ActiveRequests', rollupKey: 'active', titleFallback: 'Active Requests Telemetry', titleKey: 'activeRequestsTelemetry' }
];
const defaultOpenAiPrompt = 'Hello! Can you give me a brief explanation of how an inverted index works?';
const defaultOpenAiSystemPrompt = 'You are a helpful and concise assistant.';
const defaultEmbeddingInputText = [
  'The quick brown fox jumps over the lazy dog.',
  'Artificial intelligence is transforming software development.',
  'How do I cook a perfect medium-rare steak?'
].join('\n');
const MarkdownPreview = lazy(() => import('./components/MarkdownPreview.jsx'));

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

const tooltipBackfillSelector = [
  'button',
  'a',
  'input',
  'select',
  'textarea',
  '[role="button"]'
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
      { to: '/dashboard/client-telemetry', key: 'clientTelemetry', icon: LineChart },
      { to: '/dashboard/clients/advertised-models', key: 'advertisedModels', icon: BrainCircuit },
      { to: '/dashboard/routing-policies', key: 'routingPolicies', icon: GitBranch }
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
  '/dashboard/client-telemetry': { title: 'clientTelemetry', description: 'clientTelemetryDescription' },
  '/dashboard/routing-policies': { title: 'routingPolicies', description: 'routingPoliciesDescription' },
  '/dashboard/analytics': { title: 'analytics', description: 'analyticsDescription' },
  '/dashboard/settings': { title: 'settings', description: 'settingsDescription' }
};

function useDashboardTooltipBackfill() {
  const { t, i18n } = useTranslation();

  useEffect(() => {
    function applyTooltips() {
      document.querySelectorAll(tooltipBackfillSelector).forEach((element) => {
        const existingTitle = element.getAttribute('title');
        const generated = element.getAttribute('data-tooltip-generated') === 'true';
        if (existingTitle && !generated) return;
        if (element.closest('[aria-hidden="true"]')) return;
        const label = interactiveElementLabel(element, t('control'));
        element.setAttribute('title', describeInteractiveElement(t, element, label));
        element.setAttribute('data-tooltip-generated', 'true');
      });
    }

    applyTooltips();
    const observer = new MutationObserver(applyTooltips);
    observer.observe(document.body, {
      attributeFilter: ['aria-label', 'placeholder', 'value'],
      attributes: true,
      childList: true,
      characterData: true,
      subtree: true
    });
    return () => observer.disconnect();
  }, [i18n.language, t]);
}

function interactiveElementLabel(element, fallback) {
  const explicit = element.getAttribute('aria-label') || element.getAttribute('placeholder') || element.getAttribute('data-tooltip-label');
  if (explicit && explicit.trim()) return explicit.trim();

  const label = element.closest('label');
  if (label) {
    const labelText = label.innerText?.replace(/\s+/g, ' ').trim();
    if (labelText) return labelText;
  }

  const text = element.innerText?.replace(/\s+/g, ' ').trim();
  if (text) return text;

  return fallback;
}

function describeInteractiveElement(t, element, label) {
  const tagName = element.tagName.toLowerCase();
  if (tagName === 'a') return t('tooltipGenericLink', { label });
  if (tagName === 'select') return t('tooltipGenericSelect', { label });
  if (tagName === 'textarea') return t('tooltipGenericTextarea', { label });
  if (tagName === 'input') return t('tooltipGenericInput', { label });
  if (tagName === 'button') return t('tooltipGenericButton', { label });
  return t('tooltipGenericControl', { label });
}

function readDashboardSession() {
  const portalSession = readPortalSessionFromHash();
  if (portalSession) {
    writeDashboardSession(portalSession);
    return portalSession;
  }

  try {
    const json = window.localStorage.getItem(dashboardSessionKey);
    if (!json) return null;
    const session = JSON.parse(json);
    if (!session?.token || !session?.tenantId || !session?.serverUrl) return null;
    return session;
  } catch {
    return null;
  }
}

function readPortalSessionFromHash() {
  const hash = window.location.hash || '';
  if (!hash.includes(portalSessionHashKey)) return null;

  try {
    const params = new URLSearchParams(hash.replace(/^#/, ''));
    const encoded = params.get(portalSessionHashKey);
    if (!encoded) return null;

    const session = JSON.parse(decodeBase64Json(encoded));
    if (!session?.token || !session?.tenantId || !session?.serverUrl) return null;
    window.history.replaceState(null, '', `${window.location.pathname}${window.location.search}`);
    return session;
  } catch {
    window.history.replaceState(null, '', `${window.location.pathname}${window.location.search}`);
    return null;
  }
}

function decodeBase64Json(encoded) {
  const binary = window.atob(encoded);
  const bytes = Uint8Array.from(binary, (character) => character.charCodeAt(0));
  return new TextDecoder().decode(bytes);
}

function safeLogoutUrl(value) {
  if (!value) return '';
  try {
    const url = new URL(value, window.location.origin);
    if (url.protocol !== 'http:' && url.protocol !== 'https:') return '';
    return url.href;
  } catch {
    return '';
  }
}

function writeDashboardSession(session) {
  if (!session) {
    window.localStorage.removeItem(dashboardSessionKey);
    return;
  }
  window.localStorage.setItem(dashboardSessionKey, JSON.stringify(session));
}

function navTooltipKey(key) {
  const mapped = {
    advertisedModels: 'tooltipNavAdvertisedModels',
    analytics: 'tooltipNavAnalytics',
    clients: 'tooltipNavClients',
    clientTelemetry: 'tooltipNavClientTelemetry',
    credentials: 'tooltipNavCredentials',
    explorer: 'tooltipNavExplorer',
    home: 'tooltipNavHome',
    requests: 'tooltipNavRequests',
    routingPolicies: 'tooltipNavRoutingPolicies',
    security: 'tooltipNavSecurity',
    settings: 'tooltipNavSettings',
    tenants: 'tooltipNavTenants',
    users: 'tooltipNavUsers'
  };
  return mapped[key] || 'tooltipGenericLink';
}

function Shell({ onLogout, onToggleTheme, session, theme }) {
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
        <nav className="sidebar-nav" aria-label={t('dashboard')}>
          {navGroups.map((group) => (
            <div className="sidebar-group" key={group.key}>
              <div className="sidebar-group-label">{t(group.key)}</div>
              {group.links.map((link) => {
                const Icon = link.icon;
                return (
                  <NavLink className={`sidebar-link ${link.nested ? 'nested' : ''}`.trim()} end key={link.to} title={t(navTooltipKey(link.key), { label: t(link.key) })} to={link.to}>
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
            <span className="topbar-server-url" title={t('tooltipServerUrl')}>
              <Server size={15} />
              <span>{api.baseUrl}</span>
              <CopyButton label={t('copyServerUrl')} tooltip={t('tooltipCopyServerUrl')} value={api.baseUrl} />
            </span>
            <LanguageSelector />
            <a
              aria-label={t('github')}
              className="icon-button"
              href="https://github.com/jchristn/Xeno"
              rel="noreferrer"
              target="_blank"
              title={t('tooltipGitHub')}
            >
              <GitHubMark size={17} />
            </a>
            <button aria-label={t('toggleTheme')} className="icon-button" onClick={onToggleTheme} title={t('tooltipToggleTheme')} type="button">
              {theme === 'dark' ? <Sun size={17} /> : <Moon size={17} />}
            </button>
            <button aria-label={t('logout')} className="icon-button danger" onClick={onLogout} title={t('tooltipLogout')} type="button">
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
            <Route path="users" element={<UsersView session={session} />} />
            <Route path="credentials" element={<CredentialsView />} />
            <Route path="security" element={<SecurityView />} />
            <Route path="clients" element={<ClientsView />} />
            <Route path="clients/advertised-models" element={<AdvertisedModelsView />} />
            <Route path="client-telemetry" element={<ClientTelemetryView />} />
            <Route path="routing-policies" element={<RoutingPoliciesView />} />
            <Route path="analytics" element={<AnalyticsView />} />
            <Route path="settings" element={<SettingsView />} />
            <Route path="*" element={<HomeView />} />
          </Routes>
        </main>
      </div>
    </div>
  );
}

function ClientTelemetryView() {
  const { t } = useTranslation();
  const [searchParams, setSearchParams] = useSearchParams();
  const clients = useResource('/api/v1/clients', []);
  const [status, setStatus] = useState(null);
  const [latest, setLatest] = useState({ hostTelemetry: [], providerTelemetry: [] });
  const [attempts, setAttempts] = useState([]);
  const [hostRollups, setHostRollups] = useState({ cpu: [], memory: [], gpu: [] });
  const [providerRollups, setProviderRollups] = useState({ active: [], available: [], loaded: [] });
  const [telemetryRange, setTelemetryRange] = useState('lastHour');
  const [rangeAnchor, setRangeAnchor] = useState(Date.now());
  const [summaryLoading, setSummaryLoading] = useState(true);
  const [attemptsLoading, setAttemptsLoading] = useState(true);
  const [rollupLoading, setRollupLoading] = useState({});
  const [openTelemetryCharts, setOpenTelemetryCharts] = useState(telemetryChartOpenDefaults);
  const [collecting, setCollecting] = useState(false);
  const telemetryWindow = useMemo(() => buildAnalyticsWindow(telemetryRange, rangeAnchor), [rangeAnchor, telemetryRange]);
  const selectedClientId = searchParams.get('clientId') || 'all';
  const selectedClient = clients.data.find((client) => String(value(client, 'id') || '') === selectedClientId);
  const selectedClientName = value(selectedClient, 'name') || selectedClientId;
  const selectedClientExists = selectedClientId === 'all' || Boolean(selectedClient);
  const selectedClientQuery = selectedClientId === 'all' ? '' : `?clientId=${encodeURIComponent(selectedClientId)}`;
  const latestHostTelemetry = value(latest, 'hostTelemetry');
  const latestProviderTelemetry = value(latest, 'providerTelemetry');
  const hostRows = Array.isArray(latestHostTelemetry) ? latestHostTelemetry : [];
  const providerRows = Array.isArray(latestProviderTelemetry) ? latestProviderTelemetry : [];
  const connectedClientCount = selectedClientId === 'all'
    ? value(status, 'connectedClientCount') || 0
    : (value(selectedClient, 'connected') === true ? 1 : 0);
  const freshHostCount = hostRows.filter((item) => telemetryAgeMs(item) <= 30000).length;
  const averageCpu = average(hostRows.map((item) => value(item, 'cpuUtilizationPercent')));
  const averageMemory = average(hostRows.map((item) => value(item, 'memoryUtilizationPercent')));
  const averageGpu = average(hostRows.map((item) => value(item, 'gpuAverageUtilizationPercent')));
  const unhealthyProviders = providerRows.filter((item) => String(value(item, 'healthStatus') || '').toLowerCase() === 'unhealthy').length;
  const openTelemetryChartKeys = Object.keys(openTelemetryCharts).filter((key) => openTelemetryCharts[key]).join('|');
  const chartLoading = Object.values(rollupLoading).some(Boolean);

  function refreshWithCurrentTime() {
    setRangeAnchor(Date.now());
  }

  async function collectNow() {
    setCollecting(true);
    try {
      await api.post('/v1.0/api/client-telemetry/collect', { clientId: selectedClientId === 'all' ? null : selectedClientId });
      refreshWithCurrentTime();
    } finally {
      setCollecting(false);
    }
  }

  function updateTelemetryRange(nextRange) {
    setTelemetryRange(nextRange);
    setRangeAnchor(Date.now());
  }

  function updateSelectedClient(clientId) {
    const nextClientId = String(clientId || 'all');
    if (nextClientId === 'all') {
      setSearchParams({});
      return;
    }

    setSearchParams({ clientId: nextClientId });
  }

  function updateTelemetryChartOpen(key, open) {
    setOpenTelemetryCharts((current) => ({ ...current, [key]: open }));
    if (!open) setRollupLoading((current) => ({ ...current, [key]: false }));
  }

  useEffect(() => {
    let alive = true;
    setSummaryLoading(true);
    Promise.allSettled([
      api.get('/v1.0/api/client-telemetry/status'),
      api.get(`/v1.0/api/client-telemetry/latest${selectedClientQuery}`)
    ])
      .then(([statusResult, latestResult]) => {
        if (!alive) return;
        setStatus(statusResult.status === 'fulfilled' ? statusResult.value || null : null);
        setLatest(latestResult.status === 'fulfilled' ? latestResult.value || { hostTelemetry: [], providerTelemetry: [] } : { hostTelemetry: [], providerTelemetry: [] });
      })
      .finally(() => {
        if (alive) setSummaryLoading(false);
      });
    return () => { alive = false; };
  }, [rangeAnchor, selectedClientQuery]);

  useEffect(() => {
    let alive = true;
    setAttemptsLoading(true);
    api.get(`/v1.0/api/client-telemetry/collection-attempts${selectedClientQuery}`)
      .then((nextAttempts) => {
        if (alive) setAttempts(Array.isArray(nextAttempts) ? nextAttempts : []);
      })
      .catch(() => {
        if (alive) setAttempts([]);
      })
      .finally(() => {
        if (alive) setAttemptsLoading(false);
      });
    return () => { alive = false; };
  }, [rangeAnchor, selectedClientQuery]);

  useEffect(() => {
    let alive = true;
    const clientId = selectedClientId === 'all' ? null : selectedClientId;
    const rollupBase = {
      bucketMinutes: telemetryWindow.bucketMinutes,
      clientId,
      fromUtc: telemetryWindow.fromUtc,
      toUtc: telemetryWindow.toUtc
    };

    async function loadHost(config) {
      setRollupLoading((current) => ({ ...current, [config.key]: true }));
      try {
        const result = await api.post('/v1.0/api/client-telemetry/history/rollups', { ...rollupBase, metric: config.metric });
        if (alive) setHostRollups((current) => ({ ...current, [config.key]: Array.isArray(result) ? result : [] }));
      } catch {
        if (alive) setHostRollups((current) => ({ ...current, [config.key]: [] }));
      } finally {
        if (alive) setRollupLoading((current) => ({ ...current, [config.key]: false }));
      }
    }

    async function loadProvider(config) {
      setRollupLoading((current) => ({ ...current, [config.key]: true }));
      try {
        const result = await api.post('/v1.0/api/client-telemetry/providers/history/rollups', { ...rollupBase, metric: config.metric });
        if (alive) setProviderRollups((current) => ({ ...current, [config.rollupKey]: Array.isArray(result) ? result : [] }));
      } catch {
        if (alive) setProviderRollups((current) => ({ ...current, [config.rollupKey]: [] }));
      } finally {
        if (alive) setRollupLoading((current) => ({ ...current, [config.key]: false }));
      }
    }

    hostTelemetryChartConfigs
      .filter((config) => openTelemetryCharts[config.key])
      .forEach((config) => { loadHost(config); });
    providerTelemetryChartConfigs
      .filter((config) => openTelemetryCharts[config.key])
      .forEach((config) => { loadProvider(config); });

    return () => { alive = false; };
  }, [openTelemetryChartKeys, rangeAnchor, selectedClientId, telemetryRange]);

  return (
    <div className="view-stack client-telemetry-view">
      <div className="analytics-range-toolbar">
        <div>
          <span className="eyebrow">{t('timeRange')}</span>
          <h2>{t('clientTelemetryTimeRange', 'Telemetry Time Range')}</h2>
        </div>
        <div className="chart-action-row home-range-actions">
          <TimeRangeSelector onChange={updateTelemetryRange} range={telemetryRange} />
          <IconButton iconOnly disabled={summaryLoading || attemptsLoading || chartLoading} onClick={refreshWithCurrentTime} label={t('refresh')} tooltip={t('tooltipManualRefresh')} icon={<RefreshCw size={16} />} />
          <IconButton iconOnly disabled={collecting} onClick={collectNow} label={t('collectNow', 'Collect Now')} tooltip={t('collectNow', 'Collect Now')} icon={<Play size={16} />} />
        </div>
      </div>

      <div className="client-telemetry-toolbar">
        <label className="form-field inline compact-field table-inline-filter">
          <span>{t('clientFilter')}</span>
          <select value={selectedClientId} onChange={(event) => updateSelectedClient(event.target.value)} title={t('filterClientTelemetry', 'Filter client telemetry by client')}>
            <option value="all">{t('allClients')}</option>
            {!selectedClientExists ? <option value={selectedClientId}>{selectedClientName}</option> : null}
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

      <div className="metric-grid telemetry-kpi-grid">
        <MetricCard icon={<Network size={18} />} label={t('connectedClients')} value={formatNumber(connectedClientCount)} subtext={t('clients')} tone="info" />
        <MetricCard icon={<Gauge size={18} />} label={t('freshTelemetry', 'Fresh Telemetry')} value={formatNumber(freshHostCount)} subtext={t('clients')} tone="success" />
        <MetricCard icon={<Cpu size={18} />} label={t('averageCpu', 'Average CPU')} value={formatPercent(averageCpu / 100)} />
        <MetricCard icon={<Activity size={18} />} label={t('averageMemory', 'Average Memory')} value={formatPercent(averageMemory / 100)} />
        <MetricCard icon={<Zap size={18} />} label={t('averageGpu', 'Average GPU')} value={formatPercent(averageGpu / 100)} />
        <MetricCard icon={<Shield size={18} />} label={t('unhealthyProviders', 'Unhealthy Providers')} value={formatNumber(unhealthyProviders)} tone={unhealthyProviders > 0 ? 'danger' : 'success'} />
        <MetricCard icon={<ShieldCheck size={18} />} label={t('enabled', 'Enabled')} value={String(Boolean(value(status, 'enabled')))} tone={value(status, 'enabled') ? 'success' : 'warning'} />
        <MetricCard icon={<Activity size={18} />} label={t('running', 'Running')} value={String(Boolean(value(status, 'running')))} tone={value(status, 'running') ? 'success' : 'warning'} />
        <MetricCard icon={<History size={18} />} label={t('collectionInterval', 'Collection Interval')} value={`${formatNumber(value(status, 'collectionIntervalSeconds') || 0)}s`} />
        <MetricCard icon={<Gauge size={18} />} label={t('requestTimeout', 'Request Timeout')} value={`${formatNumber(value(status, 'requestTimeoutMs') || 0)}ms`} />
        <MetricCard icon={<History size={18} />} label={t('lastCycle', 'Last Cycle')} value={formatDateTime(value(status, 'lastCycleUtc'))} valueClassName="metric-value-timestamp" />
        <MetricCard icon={<History size={18} />} label={t('nextCycle', 'Next Cycle')} value={formatDateTime(value(status, 'nextCycleUtc'))} valueClassName="metric-value-timestamp" />
      </div>

      <div className="client-telemetry-chart-grid view-stack">
        {hostTelemetryChartConfigs.map((config) => (
          <TelemetryChartDisclosure
            key={config.key}
            loading={Boolean(rollupLoading[config.key])}
            onToggle={(open) => updateTelemetryChartOpen(config.key, open)}
            open={Boolean(openTelemetryCharts[config.key])}
            subtitle={t('hostTelemetryMetricSubtitle', 'Server-collected host telemetry for the selected time range.')}
            title={t(config.titleKey, config.titleFallback)}
          >
            <TelemetrySparkChart
              emptyTitle={t('noData')}
              framed={false}
              loading={Boolean(rollupLoading[config.key])}
              loadingLabel={t('loading')}
              series={[{
                color: config.color,
                key: config.key,
                label: t(config.labelKey, config.labelFallback),
                values: telemetryRollupValues(hostRollups[config.key], telemetryWindow)
              }]}
              subtitle={t('hostTelemetryMetricSubtitle', 'Server-collected host telemetry for the selected time range.')}
              title={t(config.titleKey, config.titleFallback)}
              totalLabel={t('total')}
              valueFormatter={(nextValue) => formatPercent(nextValue / 100)}
            />
          </TelemetryChartDisclosure>
        ))}
        {providerTelemetryChartConfigs.map((config) => (
          <TelemetryChartDisclosure
            key={config.key}
            loading={Boolean(rollupLoading[config.key])}
            onToggle={(open) => updateTelemetryChartOpen(config.key, open)}
            open={Boolean(openTelemetryCharts[config.key])}
            subtitle={t('providerTelemetryMetricSubtitle', 'Server-collected provider telemetry for the selected time range.')}
            title={t(config.titleKey, config.titleFallback)}
          >
            <TelemetrySparkChart
              emptyTitle={t('noData')}
              framed={false}
              loading={Boolean(rollupLoading[config.key])}
              loadingLabel={t('loading')}
              series={[{
                color: config.color,
                key: config.key,
                label: t(config.labelKey, config.labelFallback),
                values: telemetryRollupValues(providerRollups[config.rollupKey], telemetryWindow)
              }]}
              subtitle={t('providerTelemetryMetricSubtitle', 'Server-collected provider telemetry for the selected time range.')}
              title={t(config.titleKey, config.titleFallback)}
              totalLabel={t('total')}
              valueFormatter={(nextValue) => formatOptionalNumber(nextValue)}
            />
          </TelemetryChartDisclosure>
        ))}
      </div>

      <DataTable
        columns={[
          { key: 'clientName', label: t('client') },
          { key: 'machineName', label: t('machine', 'Machine') },
          { key: 'collectedUtc', label: t('collectedUtc', 'Collected UTC'), render: (item) => formatDateTime(value(item, 'collectedUtc')) },
          { key: 'cpuUtilizationPercent', label: t('cpu', 'CPU'), render: (item) => formatPercent(Number(value(item, 'cpuUtilizationPercent') || 0) / 100) },
          { key: 'memoryUtilizationPercent', label: t('memory', 'Memory'), render: (item) => formatPercent(Number(value(item, 'memoryUtilizationPercent') || 0) / 100) },
          { key: 'gpuAverageUtilizationPercent', label: t('gpu', 'GPU'), render: (item) => formatPercent(Number(value(item, 'gpuAverageUtilizationPercent') || 0) / 100) }
        ]}
        loading={summaryLoading}
        rows={hostRows}
        subtitle={t('latestHostTelemetrySubtitle', 'Latest host telemetry samples collected by the server.')}
        title={t('latestHostTelemetry', 'Latest Host Telemetry')}
      />

      <DataTable
        columns={[
          { key: 'clientName', label: t('client') },
          { key: 'providerName', label: t('provider', 'Provider') },
          { key: 'collectedUtc', label: t('collectedUtc', 'Collected UTC'), render: (item) => formatDateTime(value(item, 'collectedUtc')) },
          { key: 'healthStatus', label: t('health', 'Health') },
          { key: 'queuedRequests', label: t('queuedRequests', 'Queued'), render: (item) => formatOptionalNumber(value(item, 'queuedRequests')) },
          { key: 'latencyP95Ms', label: t('latencyP95', 'Latency p95'), render: (item) => formatDuration(value(item, 'latencyP95Ms')) }
        ]}
        loading={summaryLoading}
        rows={providerRows}
        subtitle={t('latestProviderTelemetrySubtitle', 'Latest provider telemetry samples collected by the server.')}
        title={t('latestProviderTelemetry', 'Latest Provider Telemetry')}
      />

      <DataTable
        columns={[
          { key: 'startedUtc', label: t('startedUtc', 'Started UTC'), render: (item) => formatDateTime(value(item, 'startedUtc')) },
          { key: 'clientName', label: t('client') },
          { key: 'status', label: t('status') },
          { key: 'elapsedMs', label: t('elapsed', 'Elapsed'), render: (item) => formatDuration(value(item, 'elapsedMs')) },
          { key: 'providerSampleCount', label: t('providerSamples', 'Provider Samples'), render: (item) => formatOptionalNumber(value(item, 'providerSampleCount')) }
        ]}
        loading={attemptsLoading}
        rows={attempts}
        subtitle={t('collectionAttemptsSubtitle', 'Recent server telemetry collection attempts.')}
        title={t('collectionAttempts', 'Collection Attempts')}
      />

    </div>
  );
}

function TelemetryChartDisclosure({ children, loading, onToggle, open, subtitle, title }) {
  return (
    <details className="workspace-card telemetry-collapsible-chart" onToggle={(event) => onToggle?.(event.currentTarget.open)} open={open}>
      <summary className="workspace-card-header telemetry-collapsible-summary">
        <div>
          <h2>{title}</h2>
          {subtitle ? <p>{subtitle}</p> : null}
        </div>
        <div className="workspace-card-actions">
          {loading ? <div aria-hidden="true" className="table-loading-spinner compact-spinner" /> : null}
          {open ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
        </div>
      </summary>
      {open ? <div className="workspace-card-body">{children}</div> : null}
    </details>
  );
}

function TelemetrySparkChart({ emptyTitle, framed = true, loading = false, loadingLabel = 'Loading...', series, subtitle, title, totalLabel = 'Total', valueFormatter }) {
  const canvasRef = useRef(null);
  const [hovered, setHovered] = useState(null);
  const normalizedSeries = series.map((item) => ({
    ...item,
    values: Array.isArray(item.values) ? item.values : []
  }));
  const finiteValues = normalizedSeries.flatMap((item) => item.values.map((point) => Number(point.value)).filter((nextValue) => Number.isFinite(nextValue)));
  const chart = { width: 900, height: 360, left: 54, right: 28, top: 24, bottom: 44 };
  const plotWidth = chart.width - chart.left - chart.right;
  const plotHeight = chart.height - chart.top - chart.bottom;
  const hasData = finiteValues.length > 0;
  const bucketCount = Math.max(0, ...normalizedSeries.map((item) => item.values.length));
  const stackedTotals = Array.from({ length: bucketCount }, (_, index) => normalizedSeries.reduce((total, item) => {
    const pointValue = Number(item.values[index]?.value);
    return total + (Number.isFinite(pointValue) ? Math.max(0, pointValue) : 0);
  }, 0));
  const max = Math.max(1, ...stackedTotals);
  const axisMax = telemetryAxisMaximum(max);
  const yTicks = buildTelemetryYAxisTicks(axisMax);
  const bucketSlot = plotWidth / Math.max(1, bucketCount);
  const barWidth = Math.max(2, Math.min(18, bucketSlot - Math.min(12, Math.max(2, bucketSlot * 0.22))));
  const xTicks = buildTelemetryXAxisTicks(normalizedSeries, bucketCount, chart.left, plotWidth);

  function updateHover(event) {
    if (bucketCount === 0 || !canvasRef.current) {
      setHovered(null);
      return;
    }

    const bounds = canvasRef.current.getBoundingClientRect();
    const svgX = (event.clientX - bounds.left) / Math.max(1, bounds.width) * chart.width;
    const boundedX = Math.max(chart.left, Math.min(chart.left + plotWidth, svgX));
    const index = Math.max(0, Math.min(bucketCount - 1, Math.floor(((boundedX - chart.left) / Math.max(1, plotWidth)) * bucketCount)));
    const x = chart.left + index * bucketSlot + bucketSlot / 2;
    const rows = normalizedSeries.map((item) => {
      const point = item.values[index] || {};
      const pointValue = Number(point.value);
      return {
        color: item.color,
        key: item.key,
        label: item.label,
        value: Number.isFinite(pointValue) ? pointValue : null
      };
    });
    const label = normalizedSeries.map((item) => item.values[index]?.label).find(Boolean) || '';
    const total = rows.reduce((sum, row) => sum + (Number.isFinite(Number(row.value)) ? Number(row.value) : 0), 0);
    setHovered({
      label,
      rows,
      svgX: x,
      total,
      ...telemetryTooltipFromPointer(event, canvasRef.current)
    });
  }

  const chartBody = (
    <>
      {!hasData && !loading ? <EmptyState title={emptyTitle} description={subtitle} /> : (
        <div className="activity-chart-panel compact-chart-panel telemetry-spark-chart-panel">
          <div className="chart-canvas" ref={canvasRef}>
            <svg className="activity-chart" onMouseLeave={() => setHovered(null)} onMouseMove={updateHover} viewBox={`0 0 ${chart.width} ${chart.height}`} role="img" aria-label={title}>
              <line x1={chart.left} y1={chart.top} x2={chart.left} y2={chart.top + plotHeight} stroke="var(--color-border)" />
              <line x1={chart.left} y1={chart.top + plotHeight} x2={chart.left + plotWidth} y2={chart.top + plotHeight} stroke="var(--color-border)" />
              {yTicks.map((tick) => {
                const y = chart.top + (1 - tick.ratio) * plotHeight;
                return (
                  <g key={tick.key}>
                    <line x1={chart.left} y1={y} x2={chart.left + plotWidth} y2={y} stroke="var(--color-border)" strokeDasharray="4 6" opacity="0.72" />
                    <text className="axis-label" x={chart.left - 10} y={y + 4} textAnchor="end">{valueFormatter(tick.value)}</text>
                  </g>
                );
              })}
              {Array.from({ length: bucketCount }, (_, index) => {
                const x = chart.left + index * bucketSlot + (bucketSlot - barWidth) / 2;
                let stackedHeight = 0;
                return (
                  <g key={`bucket-${index}`}>
                    <rect x={chart.left + index * bucketSlot} y={chart.top} width={bucketSlot} height={plotHeight} fill="transparent" />
                    {normalizedSeries.map((item) => {
                      const point = item.values[index] || {};
                      const pointValue = Number(point.value);
                      if (!Number.isFinite(pointValue)) return null;
                      const height = Math.max(0, (Math.max(0, pointValue) / axisMax) * plotHeight);
                      const y = chart.top + plotHeight - stackedHeight - height;
                      stackedHeight += height;
                      return (
                        <rect
                          fill={item.color}
                          height={height}
                          key={`${item.key}-${index}`}
                          rx="2"
                          width={barWidth}
                          x={x}
                          y={y}
                        >
                          <title>{`${item.label} ${point.label}: ${valueFormatter(pointValue)}`}</title>
                        </rect>
                      );
                    })}
                  </g>
                );
              })}
              {hovered ? (
                <rect className="chart-hover-band" x={hovered.svgX - bucketSlot / 2} y={chart.top} width={bucketSlot} height={plotHeight} />
              ) : null}
              {xTicks.map((tick) => (
                <text className="axis-label" key={tick.key} x={tick.x} y={chart.top + plotHeight + 18} textAnchor="middle">{tick.label}</text>
              ))}
              <rect
                fill="transparent"
                height={plotHeight}
                onFocus={(event) => updateHover({ ...event, clientX: event.currentTarget.getBoundingClientRect().left, clientY: event.currentTarget.getBoundingClientRect().top })}
                width={plotWidth}
                x={chart.left}
                y={chart.top}
              />
            </svg>
            {hovered ? (
              <div className="chart-tooltip telemetry-chart-tooltip" style={{ left: `${hovered.x}px`, top: `${hovered.y}px`, transform: hovered.transform }}>
                <strong>{hovered.label}</strong>
                {hovered.rows.map((row) => (
                  <span className="chart-tooltip-row" key={row.key}>
                    <i style={{ background: row.color }} />
                    <span>{row.label}</span>
                    <b>{row.value === null ? '-' : valueFormatter(row.value)}</b>
                  </span>
                ))}
                <span className="chart-tooltip-row">
                  <i />
                  <span>{totalLabel}</span>
                  <b>{valueFormatter(hovered.total)}</b>
                </span>
              </div>
            ) : null}
            {loading ? <ChartLoadingOverlay label={loadingLabel} /> : null}
          </div>
          <div className="chart-legend">
            {normalizedSeries.map((item) => <span key={item.key}><i style={{ background: item.color }} />{item.label}</span>)}
          </div>
        </div>
      )}
    </>
  );

  if (!framed) return chartBody;
  return <Panel title={title}>{chartBody}</Panel>;
}

function ChartLoadingOverlay({ label }) {
  return (
    <div aria-live="polite" className="chart-loading-overlay" role="status">
      <div aria-hidden="true" className="table-loading-spinner" />
      <strong>{label}</strong>
    </div>
  );
}

function telemetryTooltipFromPointer(event, element) {
  if (!element) return { x: 0, y: 0, transform: 'translate(14px, -50%)' };
  const bounds = element.getBoundingClientRect();
  return telemetryTooltipPosition(event.clientX - bounds.left, event.clientY - bounds.top, bounds.width, bounds.height);
}

function telemetryAxisMaximum(maximum) {
  if (!Number.isFinite(maximum) || maximum <= 1) return 1;
  const magnitude = 10 ** Math.floor(Math.log10(maximum));
  const normalized = maximum / magnitude;
  let niceNormalized = 10;
  if (normalized <= 1) niceNormalized = 1;
  else if (normalized <= 2) niceNormalized = 2;
  else if (normalized <= 4) niceNormalized = 4;
  else if (normalized <= 8) niceNormalized = 8;
  return niceNormalized * magnitude;
}

function buildTelemetryYAxisTicks(axisMax) {
  if (!Number.isFinite(axisMax) || axisMax <= 1) {
    return [
      { key: 'y-1', ratio: 1, value: 1 },
      { key: 'y-0', ratio: 0, value: 0 }
    ];
  }

  return [1, 0.75, 0.5, 0.25, 0].map((ratio) => ({
    key: `y-${ratio}`,
    ratio,
    value: axisMax * ratio
  }));
}

function buildTelemetryXAxisTicks(series, bucketCount, left, plotWidth) {
  if (bucketCount <= 0) return [];
  const tickCount = Math.min(8, bucketCount);
  if (tickCount === 1) {
    return [{ key: 'x-0', label: telemetryLabelAt(series, 0), x: left + plotWidth / 2 }];
  }

  return Array.from({ length: tickCount }, (_, tickIndex) => {
    const ratio = tickIndex / (tickCount - 1);
    const bucketIndex = Math.min(bucketCount - 1, Math.max(0, Math.round(ratio * (bucketCount - 1))));
    return {
      key: `x-${tickIndex}-${bucketIndex}`,
      label: telemetryLabelAt(series, bucketIndex),
      x: left + ratio * plotWidth
    };
  });
}

function telemetryLabelAt(series, index) {
  return series.map((item) => item.values[index]?.label).find(Boolean) || '';
}

function telemetryTooltipPosition(x, y, width, height) {
  const tooltipWidth = 220;
  const clampedX = Math.max(10, Math.min(width - 10, x));
  const clampedY = Math.max(24, Math.min(height - 24, y));
  const transform = clampedX > width - tooltipWidth ? 'translate(calc(-100% - 14px), -50%)' : 'translate(14px, -50%)';
  return { x: clampedX, y: clampedY, transform };
}

function LoginView({ onLogin, onToggleTheme, theme }) {
  const { t } = useTranslation();
  const [serverUrl, setServerUrl] = useState(api.baseUrl || 'http://localhost:9000');
  const [email, setEmail] = useState('');
  const [tenants, setTenants] = useState([]);
  const [selectedTenantId, setSelectedTenantId] = useState('');
  const [password, setPassword] = useState('');
  const [step, setStep] = useState('identity');
  const [status, setStatus] = useState('');
  const [loading, setLoading] = useState(false);

  async function submitLogin(event) {
    event.preventDefault();
    if (step === 'identity') {
      await discoverTenants();
      return;
    }

    await authenticate();
  }

  async function handleLoginKeyDown(event) {
    if (event.key !== 'Enter' || step !== 'password') return;
    event.preventDefault();
    if (loading || !canSubmit) return;
    await authenticate();
  }

  async function discoverTenants() {
    setStatus('');
    setLoading(true);
    try {
      api.setBaseUrl(serverUrl);
      const result = await api.post('/api/v1/auth/tenants', { email: email.trim() }, { skipAuth: true });
      if (result?.error) throw new Error(result.message || result.error);

      const tenantList = Array.isArray(result.tenants) ? result.tenants : [];
      if (tenantList.length === 0) {
        setTenants([]);
        setSelectedTenantId('');
        setStatus(t('loginNoTenants'));
        return;
      }

      setTenants(tenantList);
      setSelectedTenantId(tenantList.length === 1 ? tenantList[0].id : '');
      setStep('password');
    } catch (error) {
      setStatus(error?.message || t('loginTenantLookupFailed'));
    } finally {
      setLoading(false);
    }
  }

  async function authenticate() {
    setStatus('');
    if (!selectedTenantId) {
      setStatus(t('loginSelectTenant'));
      return;
    }

    setLoading(true);
    try {
      api.setBaseUrl(serverUrl);
      const result = await api.post('/api/v1/auth/dashboard/login', {
        tenantId: selectedTenantId,
        email: email.trim(),
        password
      }, { skipAuth: true });
      if (result?.error) throw new Error(result.message || result.error);
      if (!result?.token || !result?.tenantId) throw new Error(t('loginInvalidResponse'));

      onLogin({
        email: result.email || email.trim(),
        expiresUtc: result.expiresUtc,
        serverUrl: api.baseUrl,
        sessionId: result.sessionId,
        tenantId: result.tenantId,
        tenantName: result.tenantName || tenantName(result.tenantId),
        token: result.token,
        tokenType: result.tokenType || 'Bearer',
        userId: result.userId
      });
    } catch (error) {
      setStatus(error?.message || t('loginFailed'));
    } finally {
      setLoading(false);
    }
  }

  function tenantName(tenantId) {
    return tenants.find((tenant) => tenant.id === tenantId)?.name || tenantId;
  }

  function resetIdentity() {
    setStep('identity');
    setTenants([]);
    setSelectedTenantId('');
    setPassword('');
    setStatus('');
  }

  const passwordStep = step === 'password';
  const canSubmit = passwordStep
    ? serverUrl.trim() && email.trim() && password && selectedTenantId
    : serverUrl.trim() && email.trim();

  return (
    <div className="login-view">
      <div className="login-shell">
        <section className="login-brand-panel">
          <img className="login-brand-logo" src="/logo.png" alt={t('appTitle')} />
          <span className="eyebrow">{t('alphaRelease')}</span>
          <h1>{t('appTitle')}</h1>
          <p>{t('loginSubtitle')}</p>
        </section>

        <section className="login-card">
          <div className="login-card-actions">
            <a aria-label={t('github')} className="icon-button" href="https://github.com/jchristn/Xeno" rel="noreferrer" target="_blank" title={t('tooltipGitHub')}>
              <GitHubMark size={17} />
            </a>
            <button aria-label={t('toggleTheme')} className="icon-button" onClick={onToggleTheme} title={t('tooltipToggleTheme')} type="button">
              {theme === 'dark' ? <Sun size={17} /> : <Moon size={17} />}
            </button>
          </div>
          <div className="login-card-header">
            <div>
              <span className="eyebrow">{t('dashboard')}</span>
              <h2>{t('signInToDashboard')}</h2>
            </div>
          </div>
          <form className="login-form" onKeyDown={handleLoginKeyDown} onSubmit={submitLogin}>
            <label className="form-field" title={t('tooltipLoginServerUrl')}>
              <span>{t('serverUrl')}</span>
              <input
                autoComplete="url"
                disabled={loading || passwordStep}
                onChange={(event) => setServerUrl(event.target.value)}
                title={t('tooltipLoginServerUrl')}
                type="url"
                value={serverUrl}
              />
            </label>
            <label className="form-field" title={t('tooltipLoginEmail')}>
              <span>{t('email')}</span>
              <input
                autoComplete="email"
                disabled={loading || passwordStep}
                onChange={(event) => setEmail(event.target.value)}
                title={t('tooltipLoginEmail')}
                type="email"
                value={email}
              />
            </label>
            {passwordStep && tenants.length > 1 ? (
              <label className="form-field" title={t('tooltipLoginTenant')}>
                <span>{t('tenant')}</span>
                <select
                  disabled={loading}
                  onChange={(event) => setSelectedTenantId(event.target.value)}
                  title={t('tooltipLoginTenant')}
                  value={selectedTenantId}
                >
                  <option value="">{t('selectTenant')}</option>
                  {tenants.map((tenant) => (
                    <option key={tenant.id} value={tenant.id}>{tenant.name} ({tenant.id})</option>
                  ))}
                </select>
              </label>
            ) : null}
            {passwordStep && tenants.length === 1 ? (
              <div className="login-tenant-confirmation" title={t('tooltipLoginTenant')}>
                <span>{t('tenant')}</span>
                <strong>{tenants[0].name}</strong>
              </div>
            ) : null}
            {passwordStep ? (
              <label className="form-field" title={t('tooltipLoginPassword')}>
                <span>{t('password')}</span>
                <input
                  autoComplete="current-password"
                  disabled={loading}
                  onChange={(event) => setPassword(event.target.value)}
                  title={t('tooltipLoginPassword')}
                  type="password"
                  value={password}
                />
              </label>
            ) : null}
            {status ? <div className="form-status danger">{status}</div> : null}
            <div className="button-row">
              {passwordStep ? (
                <button className="action-button secondary" disabled={loading} onClick={resetIdentity} title={t('tooltipLoginChangeIdentity')} type="button">
                  {t('change')}
                </button>
              ) : null}
              <button className="primary-button" disabled={loading || !canSubmit} title={t('tooltipLogin')} type="submit">
                <Shield size={16} />
                <span>{loading ? t('signingIn') : passwordStep ? t('login') : t('continue')}</span>
              </button>
            </div>
          </form>
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
  const [activityRange, setActivityRange] = useState('lastDay');
  const [rangeAnchor, setRangeAnchor] = useState(Date.now());
  const activityWindow = useMemo(() => buildAnalyticsWindow(activityRange, rangeAnchor), [activityRange, rangeAnchor]);
  const analytics = useResource(analyticsOverviewPath(activityWindow), { total: 0, success: 0, failures: 0, failureRate: 0, connectedClientCount: 0, totalClientCount: 0, advertisedModelCount: 0, completionModelCount: 0 });
  const summary = useResource(requestHistorySummaryPath(activityWindow), { buckets: [] });
  const connected = Number(value(analytics.data, 'connectedClientCount') || 0);
  const totalClientCount = Number(value(analytics.data, 'totalClientCount') || 0);
  const advertisedModelCount = Number(value(analytics.data, 'advertisedModelCount') || 0);
  const completionModels = Number(value(analytics.data, 'completionModelCount') || 0);

  function refreshAll() {
    setRangeAnchor(Date.now());
    analytics.refresh();
    summary.refresh();
  }

  function updateActivityRange(nextRange) {
    setActivityRange(nextRange);
    setRangeAnchor(Date.now());
  }

  return (
    <div className="view-stack">
      <div className="analytics-range-toolbar">
        <div>
          <span className="eyebrow">{t('timeRange')}</span>
          <h2>{t('homeTimeRange', 'Dashboard Time Range')}</h2>
        </div>
        <div className="chart-action-row home-range-actions">
          <TimeRangeSelector onChange={updateActivityRange} range={activityRange} />
          <IconButton iconOnly onClick={refreshAll} label={t('refresh')} tooltip={t('tooltipManualRefresh')} icon={<RefreshCw size={16} />} />
        </div>
      </div>

      <div className="metric-grid">
        <MetricCard icon={<History size={18} />} label={t('requestVolume')} value={formatNumber(analytics.data.total)} subtext={t('total')} />
        <MetricCard icon={<Zap size={18} />} label={t('success')} value={formatNumber(analytics.data.success)} subtext={formatPercent(1 - Number(analytics.data.failureRate || 0))} tone="success" />
        <MetricCard icon={<Shield size={18} />} label={t('failed')} value={formatNumber(analytics.data.failures)} subtext={formatPercent(Number(analytics.data.failureRate || 0))} tone="danger" />
        <MetricCard icon={<Network size={18} />} label={t('connectedClients')} value={formatNumber(connected)} subtext={t('clients')} tone="info" />
        <MetricCard icon={<BrainCircuit size={18} />} label={t('advertisedModels')} value={formatNumber(advertisedModelCount)} subtext={t('models')} tone="success" />
        <MetricCard icon={<Server size={18} />} label={t('completionModels')} value={formatNumber(completionModels)} subtext={t('models')} tone="neutral" />
      </div>

      <Panel
        className="chart-panel-card"
        title={t('requestActivity')}
      >
        <ActivityChart loading={summary.loading} range={activityRange} showControls={false} summary={summary.data} />
      </Panel>

      <Panel
        className="chart-panel-card"
        title={t('connectedClients')}
      >
        <ConnectedClientsChart
          connected={connected}
          loading={analytics.loading}
          range={activityRange}
          sampleVersion={analytics.updatedAt}
          showControls={false}
          total={totalClientCount}
        />
      </Panel>
    </div>
  );
}

function RequestHistoryView() {
  const { t } = useTranslation();
  const [activityRange, setActivityRange] = useState('lastDay');
  const [rangeAnchor, setRangeAnchor] = useState(Date.now());
  const activityWindow = useMemo(() => buildAnalyticsWindow(activityRange, rangeAnchor), [activityRange, rangeAnchor]);
  const history = useResource(requestHistoryListPath(), { items: [], total: 0, page: 1, pageSize: 100 });
  const summary = useResource(requestHistorySummaryPath(activityWindow), { buckets: [] });
  const [selected, setSelected] = useState(null);
  const [confirmClear, setConfirmClear] = useState(false);
  const bucketTotals = summarizeBuckets(summary.data.buckets || []);

  async function clearHistory() {
    await api.post(`${requestHistoryEndpoint}/bulk`, {});
    setRangeAnchor(Date.now());
    history.refresh();
    summary.refresh();
    setConfirmClear(false);
  }

  function refreshAll() {
    setRangeAnchor(Date.now());
    history.refresh();
    summary.refresh();
  }

  function updateActivityRange(nextRange) {
    setActivityRange(nextRange);
    setRangeAnchor(Date.now());
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
            <IconButton iconOnly onClick={refreshAll} label={t('refresh')} tooltip={t('tooltipManualRefresh')} icon={<RefreshCw size={16} />} />
          </div>
        )}
        className="chart-panel-card"
        title={t('requestActivity')}
      >
        <ActivityChart loading={summary.loading} onRangeChange={updateActivityRange} onRefresh={refreshAll} range={activityRange} summary={summary.data} />
      </Panel>
      <DataTable
        actions={<IconButton iconOnly onClick={() => setConfirmClear(true)} label={t('clear')} tooltip={t('clearHistoryMessage')} icon={<Trash2 size={16} />} variant="danger" />}
        columns={['statusCode', 'method', 'path', 'model', 'requestorPrincipalType', 'requestorPrincipalId', 'requestorTenantId', 'authScheme', 'selectedClientId', 'durationMs', 'createdUtc']}
        onDelete={(row) => {
          if (recordId(selected) === recordId(row)) setSelected(null);
          return deleteRecord(requestHistoryEndpoint, row);
        }}
        onRowClick={setSelected}
        onRefresh={refreshAll}
        loading={history.loading}
        rows={collectionRows(history.data)}
        subtitle={t('requestHistorySubtitle')}
        title={t('requests')}
      />
      {selected && <RequestDetailsModal data={selected} onClose={() => setSelected(null)} />}
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
            <IconButton disabled={!selected || isSending} onClick={execute} label={isSending ? t('sending') : t('execute')} tooltip={t('tooltipExecuteRequest')} icon={<Play size={16} />} variant="primary" />
            {isSending ? <IconButton onClick={stopRequest} label={t('stop')} tooltip={t('tooltipStopRequest')} icon={<Square size={16} />} variant="danger" /> : null}
          </div>
        )}
        title={t('requestBuilder')}
      >
        {operations.length === 0 ? (
          <EmptyState message={t('openApiUnavailable')} title={t('noData')} />
        ) : (
          <div className="form-stack">
            <div className="form-grid two">
              <label className="form-field" title={t('tooltipEndpointFilter')}>
                <span>{t('endpoints')}</span>
                <select value={tag} onChange={(event) => updateTag(event.target.value)} title={t('tooltipEndpointFilter')}>
                  {tags.map((item) => <option key={item} value={item}>{item === 'all' ? t('allEndpoints') : item}</option>)}
                </select>
              </label>
              <label className="form-field" title={t('tooltipOperationSelect')}>
                <span>{t('operation')}</span>
                <select className="explorer-operation-select" value={selected?.key || ''} onChange={(event) => updateOperation(event.target.value)} title={t('tooltipOperationSelect')}>
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
                      <label className="form-field" key={name} title={t('tooltipPathParameter')}>
                        <span>{t('pathParameter')}: {name}</span>
                        <input
                          value={pathValues[name] || ''}
                          onChange={(event) => setPathValues((current) => ({ ...current, [name]: event.target.value }))}
                          title={t('tooltipPathParameter')}
                        />
                      </label>
                    ))}
                  </div>
                ) : null}

                <div className="form-grid two">
                  <label className="form-field" title={t('tooltipQueryJson')}>
                    <span className="form-field-heading">
                      <span>{t('queryParameters')}</span>
                      <CopyButton label={t('copyJson')} value={queryText} />
                    </span>
                    <textarea className="explorer-json-editor" rows={10} value={queryText} onChange={(event) => setQueryText(event.target.value)} title={t('tooltipQueryJson')} />
                  </label>
                  <label className="form-field" title={t('tooltipRequestBodyJson')}>
                    <span className="form-field-heading">
                      <span>{t('requestBody')}</span>
                      <CopyButton label={t('copyJson')} value={selected.method === 'GET' ? '' : bodyText} />
                    </span>
                    <textarea
                      className="explorer-json-editor"
                      disabled={selected.method === 'GET'}
                      rows={10}
                      value={selected.method === 'GET' ? t('noRequestBody') : bodyText}
                      onChange={(event) => setBodyText(event.target.value)}
                      title={t('tooltipRequestBodyJson')}
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
  const grantablePermissions = useResource('/api/v1/permissions/grantable', []);
  const roleAssignments = useResource('/api/v1/roleassignments', []);
  const users = useResource('/api/v1/users', []);
  const credentials = useResource('/api/v1/credentials', []);
  const effective = useResource('/api/v1/effective-permissions', []);
  const audit = useResource('/api/v1/audit', []);
  const [selectedRole, setSelectedRole] = useState(null);
  const [selectedPermission, setSelectedPermission] = useState(null);
  const [creatingRole, setCreatingRole] = useState(false);
  const [creatingPermission, setCreatingPermission] = useState(false);
  const [creatingRoleAssignment, setCreatingRoleAssignment] = useState(false);
  const roleAssignmentRows = useMemo(() => roleAssignments.data.map((assignment) => {
    const role = roles.data.find((item) => recordId(item) === value(assignment, 'roleId'));
    const rolePermissionIds = Array.isArray(value(role, 'permissionIds')) ? value(role, 'permissionIds') : [];
    const effectivePermissionItems = rolePermissionIds
      .map((permissionId) => permissions.data.find((permission) => recordId(permission) === permissionId))
      .filter(Boolean)
      .map(describePermission);
    return {
      ...assignment,
      roleName: value(role, 'name') || value(assignment, 'roleId'),
      effectivePermissions: effectivePermissionItems
    };
  }), [permissions.data, roleAssignments.data, roles.data]);

  async function saveRole(roleId, payload) {
    const result = roleId
      ? await api.put(`/api/v1/roles/${encodeURIComponent(roleId)}`, payload)
      : await api.post('/api/v1/roles', payload);
    if (result?.error) throw new Error(result.message || result.error);
    setSelectedRole(result);
    setCreatingRole(false);
    roles.refresh();
    grantablePermissions.refresh();
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
    grantablePermissions.refresh();
    effective.refresh();
  }

  async function saveRoleAssignment(payload) {
    const result = await api.post('/api/v1/roleassignments', payload);
    if (result?.error) throw new Error(result.message || result.error);
    setCreatingRoleAssignment(false);
    roleAssignments.refresh();
    effective.refresh();
  }

  return (
    <div className="view-stack">
      <div className="metric-grid compact">
        <MetricCard icon={<LockKeyhole size={18} />} label={t('roles')} value={formatNumber(roles.data.length)} />
        <MetricCard icon={<ShieldCheck size={18} />} label={t('permissions')} value={formatNumber(permissions.data.length)} />
        <MetricCard icon={<KeyRound size={18} />} label={t('roleAssignments')} value={formatNumber(roleAssignments.data.length)} tone="info" />
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
        loading={roles.loading}
        rowActionLabel={t('edit')}
        actions={<IconButton onClick={() => setCreatingRole(true)} label={t('createRole')} tooltip={t('tooltipCreateRole')} icon={<Plus size={16} />} variant="primary" />}
      />
      <DataTable
        title={t('permissions')}
        rows={permissions.data}
        columns={['id', 'tenantId', 'resourceType', 'operationType', 'permit', 'builtIn', 'active']}
        onDelete={(row) => {
          if (recordId(selectedPermission) === recordId(row)) setSelectedPermission(null);
          return deleteRecord('/api/v1/permissions', row);
        }}
        onRefresh={permissions.refresh}
        onRowClick={setSelectedPermission}
        loading={permissions.loading}
        rowActionLabel={t('edit')}
        actions={<IconButton onClick={() => setCreatingPermission(true)} label={t('createPermission')} tooltip={t('tooltipCreatePermission')} icon={<Plus size={16} />} variant="primary" />}
      />
      <DataTable
        title={t('roleAssignments')}
        rows={roleAssignmentRows}
        columns={[
          'id',
          'tenantId',
          'roleId',
          'roleName',
          'principalType',
          'principalId',
          { key: 'effectivePermissions', label: 'effectivePermissions' },
          'active'
        ]}
        onDelete={(row) => deleteRecord('/api/v1/roleassignments', row)}
        onRefresh={roleAssignments.refresh}
        loading={roleAssignments.loading || roles.loading}
        actions={<IconButton onClick={() => setCreatingRoleAssignment(true)} label={t('createRoleAssignment')} tooltip={t('tooltipCreateRoleAssignment')} icon={<Plus size={16} />} variant="primary" />}
      />
      <DataTable title={t('effectivePermissions')} rows={effective.data} columns={['resourceType', 'operationType', 'permit', 'sourceRole']} loading={effective.loading} onRefresh={effective.refresh} />
      <DataTable
        columns={['createdUtc', 'resourceType', 'operationType', 'reasonCode', 'statusCode', 'principalType', 'principalId', 'targetTenantId']}
        onDelete={(row) => deleteRecord('/api/v1/audit', row)}
        onRefresh={audit.refresh}
        loading={audit.loading}
        rows={audit.data}
        subtitle={t('auditSubtitle')}
        title={t('audit')}
      />
      {creatingRole && <RoleEditModal data={null} mode="create" onClose={() => setCreatingRole(false)} onSave={saveRole} permissions={grantablePermissions.data} />}
      {selectedRole && <RoleEditModal data={selectedRole} onClose={() => setSelectedRole(null)} onSave={saveRole} permissions={grantablePermissions.data} />}
      {creatingPermission && <PermissionEditModal data={null} mode="create" onClose={() => setCreatingPermission(false)} onSave={savePermission} />}
      {selectedPermission && <PermissionEditModal data={selectedPermission} onClose={() => setSelectedPermission(null)} onSave={savePermission} />}
      {creatingRoleAssignment && (
        <RoleAssignmentEditModal
          credentials={credentials.data}
          onClose={() => setCreatingRoleAssignment(false)}
          onSave={saveRoleAssignment}
          roles={roles.data}
          users={users.data}
        />
      )}
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
        loading={tenants.loading}
        rows={tenants.data}
        rowActionLabel={t('edit')}
        subtitle={t('tenantsTableSubtitle')}
        title={t('tenants')}
        actions={<IconButton onClick={() => setCreating(true)} label={t('createTenant')} tooltip={t('tooltipCreateTenant')} icon={<Plus size={16} />} variant="primary" />}
      />
      {creating && <TenantEditModal data={null} mode="create" onClose={() => setCreating(false)} onSave={saveTenant} />}
      {selected && <TenantEditModal data={selected} onClose={() => setSelected(null)} onSave={saveTenant} />}
    </div>
  );
}

function UsersView({ session }) {
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
        loading={users.loading}
        rows={users.data}
        rowActionLabel={t('edit')}
        subtitle={t('usersTableSubtitle')}
        title={t('users')}
        actions={<IconButton onClick={() => setCreating(true)} label={t('createUser')} tooltip={t('tooltipCreateUser')} icon={<Plus size={16} />} variant="primary" />}
      />
      {creating && <UserEditModal actorUser={users.data.find((item) => recordId(item) === session?.userId)} data={null} mode="create" onClose={() => setCreating(false)} onSave={saveUser} session={session} />}
      {selected && <UserEditModal actorUser={users.data.find((item) => recordId(item) === session?.userId)} data={selected} onClose={() => setSelected(null)} onSave={saveUser} session={session} />}
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
        <IconButton onClick={() => setCreating(true)} label={t('createCredential')} tooltip={t('tooltipCreateCredential')} icon={<KeyRound size={16} />} variant="primary" />
      </div>
      {created ? <JsonPanelInline title={t('newCredential')} data={created} /> : null}
      <DataTable
        columns={['id', 'tenantId', 'userId', 'accessKey', 'active']}
        onDelete={(row) => {
          if (recordId(selected) === recordId(row)) setSelected(null);
          return deleteRecord('/api/v1/credentials', row);
        }}
        onRowClick={setSelected}
        onRefresh={credentials.refresh}
        loading={credentials.loading}
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
  const navigate = useNavigate();
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
        columns={['id', 'name', 'ownerPrincipalType', 'connected', 'serviceState', 'inFlightRequests', 'maxParallelRequests', 'lastHeartbeatUtc']}
        onDelete={(row) => {
          if (recordId(selectedClient) === recordId(row)) setSelectedClient(null);
          return deleteRecord('/api/v1/clients', row);
        }}
        onRefresh={clients.refresh}
        onRowClick={setSelectedClient}
        getRowActions={(row) => {
          const clientId = recordId(row);
          return [{
            disabled: !clientId,
            icon: <LineChart size={14} />,
            key: 'view-telemetry',
            label: t('viewTelemetry', 'View Telemetry'),
            tooltip: t('tooltipViewClientTelemetry', 'Open client telemetry filtered to this client.'),
            onSelect: () => navigate(`/dashboard/client-telemetry?clientId=${encodeURIComponent(clientId)}`)
          }];
        }}
        loading={clients.loading}
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
      <DataTable
        actions={(
          <label className="form-field inline compact-field table-inline-filter">
            <span>{t('clientFilter')}</span>
            <select value={selectedClientId} onChange={(event) => setSelectedClientId(event.target.value)} title={t('tooltipNavAdvertisedModels')}>
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
        )}
        columns={['clientName', 'clientId', 'name', 'apiType', 'source', 'supportsCompletions', 'supportsEmbeddings', 'lastAdvertisedUtc']}
        onDelete={(row) => {
          if (recordId(selectedModel) === recordId(row)) setSelectedModel(null);
          return deleteClientModel(row);
        }}
        onRefresh={clients.refresh}
        onRowClick={setSelectedModel}
        loading={clients.loading}
        rows={filteredModels}
        subtitle={t('advertisedModelsTableSubtitle')}
        title={t('advertisedModels')}
      />
      {selectedModel && <EditRecordModal title={t('modelDetails')} data={selectedModel} onClose={() => setSelectedModel(null)} />}
    </div>
  );
}

function RoutingPoliciesView() {
  const { t } = useTranslation();
  const policies = useResource('/api/v1/routingpolicies', []);
  const [selected, setSelected] = useState(null);
  const [creating, setCreating] = useState(false);
  const policyCount = policies.data.length;
  const defaultCount = policies.data.filter((policy) => value(policy, 'isDefault') === true).length;
  const activeCount = policies.data.filter((policy) => value(policy, 'active') !== false).length;

  async function saveRoutingPolicy(policyId, payload) {
    const result = policyId
      ? await api.put(`/api/v1/routingpolicies/${encodeURIComponent(policyId)}`, payload)
      : await api.post('/api/v1/routingpolicies', payload);
    if (result?.error) throw new Error(result.message || result.error);
    setSelected(result);
    setCreating(false);
    policies.refresh();
  }

  return (
    <div className="view-stack">
      <div className="metric-grid compact">
        <MetricCard icon={<GitBranch size={18} />} label={t('routingPolicies')} value={formatNumber(policyCount)} />
        <MetricCard icon={<ShieldCheck size={18} />} label={t('defaultPolicies')} value={formatNumber(defaultCount)} tone="info" />
        <MetricCard icon={<Activity size={18} />} label={t('activePolicies')} value={formatNumber(activeCount)} tone="success" />
      </div>
      <DataTable
        actions={<IconButton onClick={() => setCreating(true)} label={t('createRoutingPolicy')} tooltip={t('tooltipCreateRoutingPolicy')} icon={<Plus size={16} />} variant="primary" />}
        canDelete={(row) => value(row, 'isDefault') !== true}
        columns={[
          'id',
          { key: 'tenantId', label: 'routingTenant' },
          'name',
          { key: 'modelName', label: 'routingModel' },
          'clientName',
          { className: 'col-compact col-mode', key: 'loadBalancingMode', label: 'routingMode' },
          { className: 'col-compact col-timeout', key: 'requestTimeoutMs', label: 'routingTimeoutMs' },
          { className: 'col-compact col-bool', key: 'isDefault', label: 'routingDefault' },
          { className: 'col-compact col-bool', key: 'active' }
        ]}
        deleteMessage={(row) => t('deleteRoutingPolicyMessage', { record: describeRow(row) })}
        deleteTitle={t('deleteRoutingPolicyTitle')}
        onDelete={(row) => {
          if (recordId(selected) === recordId(row)) setSelected(null);
          return deleteRecord('/api/v1/routingpolicies', row);
        }}
        onRefresh={policies.refresh}
        onRowClick={setSelected}
        loading={policies.loading}
        rows={policies.data}
        rowActionLabel={t('edit')}
        subtitle={t('routingPoliciesTableSubtitle')}
        title={t('routingPolicies')}
      />
      {creating && <RoutingPolicyEditModal data={null} mode="create" onClose={() => setCreating(false)} onSave={saveRoutingPolicy} />}
      {selected && <RoutingPolicyEditModal data={selected} onClose={() => setSelected(null)} onSave={saveRoutingPolicy} />}
    </div>
  );
}

function AnalyticsView() {
  const { t } = useTranslation();
  const [analyticsRange, setAnalyticsRange] = useState('lastDay');
  const [rangeAnchor, setRangeAnchor] = useState(() => Date.now());
  const analyticsWindow = useMemo(() => buildAnalyticsWindow(analyticsRange, rangeAnchor), [analyticsRange, rangeAnchor]);
  const summary = useResource(requestHistorySummaryPath(analyticsWindow), { buckets: [] });
  const overview = useResource(analyticsOverviewPath(analyticsWindow), { total: 0, success: 0, failures: 0, failureRate: 0, byModel: [], byPath: [] });
  const metrics = useTextResource('/metrics', '');
  const analyticsTotals = {
    failed: Number(value(overview.data, 'failures') || value(overview.data, 'failed') || 0),
    failureRate: Number(value(overview.data, 'failureRate') || 0),
    success: Number(value(overview.data, 'success') || 0),
    total: Number(value(overview.data, 'total') || 0)
  };
  const byModel = collectionRows(value(overview.data, 'byModel'));
  const byPath = collectionRows(value(overview.data, 'byPath'));

  function refreshAnalytics() {
    setRangeAnchor(Date.now());
    summary.refresh();
    overview.refresh();
    metrics.refresh();
  }

  function updateAnalyticsRange(nextRange) {
    setAnalyticsRange(nextRange);
    setRangeAnchor(Date.now());
  }

  return (
    <div className="view-stack">
      <div className="analytics-range-toolbar">
        <div>
          <span className="eyebrow">{t('timeRange')}</span>
          <h2>{t('analyticsTimeRange')}</h2>
        </div>
        <TimeRangeSelector onChange={updateAnalyticsRange} range={analyticsRange} />
      </div>
      <div className="metric-grid compact">
        <MetricCard icon={<Activity size={18} />} label={t('total')} value={formatNumber(analyticsTotals.total)} />
        <MetricCard icon={<Zap size={18} />} label={t('success')} value={formatNumber(analyticsTotals.success)} tone="success" />
        <MetricCard icon={<Shield size={18} />} label={t('failed')} value={formatNumber(analyticsTotals.failed)} tone="danger" />
        <MetricCard icon={<Gauge size={18} />} label={t('failureRate')} value={formatPercent(analyticsTotals.failureRate)} tone={analyticsTotals.failureRate > 0 ? 'warning' : 'success'} />
      </div>
      <Panel
        actions={<IconButton iconOnly onClick={refreshAnalytics} label={t('refresh')} tooltip={t('tooltipManualRefresh')} icon={<RefreshCw size={16} />} />}
        title={t('requestActivity')}
      >
        <ActivityChart loading={summary.loading} onRangeChange={updateAnalyticsRange} onRefresh={refreshAnalytics} range={analyticsRange} showControls={false} summary={summary.data} />
      </Panel>
      <div className="workspace-grid">
        <DataTable title={t('requestsByModel')} rows={byModel} columns={['name', 'count', 'success', 'failed']} loading={overview.loading} onDelete={(row) => deleteRequestHistoryGroup({ model: value(row, 'name') })} onRefresh={refreshAnalytics} />
        <DataTable title={t('requestsByPath')} rows={byPath} columns={['name', 'count', 'success', 'failed']} loading={overview.loading} onDelete={(row) => deleteRequestHistoryGroup({ path: value(row, 'name') })} onRefresh={refreshAnalytics} />
      </div>
      <Panel
        actions={<IconButton iconOnly onClick={metrics.refresh} label={t('refresh')} tooltip={t('tooltipManualRefresh')} icon={<RefreshCw size={16} />} />}
        title={t('prometheusMetrics')}
      >
        <pre className="metrics-output">{metrics.data || t('noData')}</pre>
      </Panel>
    </div>
  );
}

function TimeRangeSelector({ onChange, range }) {
  const { t } = useTranslation();
  return (
    <div className="chart-controls analytics-range-controls" aria-label={t('chartRange')}>
      {analyticsRanges.map((item) => (
        <button
          className={item.key === range ? 'active' : ''}
          key={item.key}
          onClick={() => onChange(item.key)}
          title={t('tooltipChartRangeOption', { range: t(item.key) })}
          type="button"
        >
          {t(item.key)}
        </button>
      ))}
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

  function updateNestedSetting(section, child, key, inputValue) {
    setDraft((current) => ({
      ...current,
      [section]: {
        ...current[section],
        [child]: {
          ...(current[section]?.[child] || {}),
          [key]: inputValue
        }
      }
    }));
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
        <IconButton iconOnly onClick={settings.refresh} label={t('refresh')} tooltip={t('tooltipManualRefresh')} icon={<RefreshCw size={16} />} />
        <IconButton onClick={saveSettings} label={t('saveSettings')} tooltip={t('tooltipSaveSettings')} icon={<Upload size={16} />} variant="primary" />
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
            <label className="form-field" title={t('tooltipSettingsHostname')}>
              <span>{t('hostname')}</span>
              <input value={draft.Rest.Hostname} onChange={(event) => updateSetting('Rest', 'Hostname', event.target.value)} title={t('tooltipSettingsHostname')} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsPort')}>
              <span>{t('port')}</span>
              <input min="1" type="number" value={draft.Rest.Port} onChange={(event) => updateSetting('Rest', 'Port', Number(event.target.value))} title={t('tooltipSettingsPort')} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="toggle-field" title={t('tooltipSettingsSsl')}>
              <input checked={draft.Rest.Ssl} onChange={(event) => updateSetting('Rest', 'Ssl', event.target.checked)} title={t('tooltipSettingsSsl')} type="checkbox" />
              <span>{t('ssl')}</span>
              <small className="setting-impact restart inline">{t('restartRequired')}</small>
            </label>
            <label className="toggle-field" title={t('tooltipSettingsRequireAuthentication')}>
              <input checked={draft.Rest.RequireAuthentication} onChange={(event) => updateSetting('Rest', 'RequireAuthentication', event.target.checked)} title={t('tooltipSettingsRequireAuthentication')} type="checkbox" />
              <span>{t('requireAuthentication')}</span>
              <small className="setting-impact live inline">{t('appliesImmediately')}</small>
            </label>
          </div>
        </Panel>
        <Panel actions={<SettingImpactBadge type="live" />} title={t('corsSettings')}>
          <SettingImpactNote live={t('corsLiveNote')} />
          <div className="form-stack">
            <label className="toggle-field" title={t('tooltipSettingsCorsEnabled')}>
              <input checked={draft.Cors.Enabled} onChange={(event) => updateSetting('Cors', 'Enabled', event.target.checked)} title={t('tooltipSettingsCorsEnabled')} type="checkbox" />
              <span>{t('enabled')}</span>
              <small className="setting-impact live inline">{t('appliesImmediately')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsAllowedOrigins')}>
              <span>{t('allowedOrigins')}</span>
              <textarea rows={2} value={listToText(draft.Cors.AllowedOrigins)} onChange={(event) => updateListSetting('Cors', 'AllowedOrigins', event.target.value)} title={t('tooltipSettingsAllowedOrigins')} />
              <small className="setting-impact live">{t('appliesImmediately')}</small>
            </label>
            <div className="form-grid two">
              <label className="form-field" title={t('tooltipSettingsAllowedHeaders')}>
                <span>{t('allowedHeaders')}</span>
                <textarea rows={2} value={listToText(draft.Cors.AllowedHeaders)} onChange={(event) => updateListSetting('Cors', 'AllowedHeaders', event.target.value)} title={t('tooltipSettingsAllowedHeaders')} />
                <small className="setting-impact live">{t('appliesImmediately')}</small>
              </label>
              <label className="form-field" title={t('tooltipSettingsAllowedMethods')}>
                <span>{t('allowedMethods')}</span>
                <textarea rows={2} value={listToText(draft.Cors.AllowedMethods)} onChange={(event) => updateListSetting('Cors', 'AllowedMethods', event.target.value)} title={t('tooltipSettingsAllowedMethods')} />
                <small className="setting-impact live">{t('appliesImmediately')}</small>
              </label>
            </div>
          </div>
        </Panel>
        <Panel actions={<SettingImpactBadge type="mixed" />} title={t('webSocketSettings')}>
          <SettingImpactNote live={t('webSocketLiveNote')} restart={t('webSocketRestartNote')} />
          <div className="form-grid two">
            <label className="toggle-field" title={t('tooltipSettingsWebSocketEnabled')}>
              <input checked={draft.WebSocket.Enabled} onChange={(event) => updateSetting('WebSocket', 'Enabled', event.target.checked)} title={t('tooltipSettingsWebSocketEnabled')} type="checkbox" />
              <span>{t('enabled')}</span>
              <small className="setting-impact restart inline">{t('restartRequired')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsWebSocketPath')}>
              <span>{t('path')}</span>
              <input value={draft.WebSocket.Path} onChange={(event) => updateSetting('WebSocket', 'Path', event.target.value)} title={t('tooltipSettingsWebSocketPath')} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsHeartbeat')}>
              <span>{t('heartbeatIntervalMs')}</span>
              <input min="1000" type="number" value={draft.WebSocket.HeartbeatIntervalMs} onChange={(event) => updateSetting('WebSocket', 'HeartbeatIntervalMs', Number(event.target.value))} title={t('tooltipSettingsHeartbeat')} />
              <small className="setting-impact live">{t('appliesToNewActivity')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsRequestTimeout')}>
              <span>{t('requestTimeoutMs')}</span>
              <input min="1000" type="number" value={draft.WebSocket.RequestTimeoutMs} onChange={(event) => updateSetting('WebSocket', 'RequestTimeoutMs', Number(event.target.value))} title={t('tooltipSettingsRequestTimeout')} />
              <small className="setting-impact live">{t('appliesToNewActivity')}</small>
            </label>
          </div>
        </Panel>
        <Panel actions={<SettingImpactBadge type="live" />} title={t('routingSettings')}>
          <SettingImpactNote live={t('routingLiveNote')} />
          <div className="form-grid two">
            <label className="form-field" title={t('tooltipSettingsLoadBalancingMode')}>
              <span>{t('loadBalancingMode')}</span>
              <select value={draft.Routing.LoadBalancingMode} onChange={(event) => updateSetting('Routing', 'LoadBalancingMode', event.target.value)} title={t('tooltipSettingsLoadBalancingMode')}>
                {['RoundRobin', 'Random', 'FirstAvailable', 'LeastRecentlyUsed', 'Adaptive'].map((item) => <option key={item} value={item}>{item}</option>)}
              </select>
              <small className="setting-impact live">{t('appliesToNewActivity')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsRoutingTimeout')}>
              <span>{t('defaultTimeoutMs')}</span>
              <input min="1000" type="number" value={draft.Routing.DefaultTimeoutMs} onChange={(event) => updateSetting('Routing', 'DefaultTimeoutMs', Number(event.target.value))} title={t('tooltipSettingsRoutingTimeout')} />
              <small className="setting-impact live">{t('appliesToNewActivity')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsAdaptiveSampleCount')}>
              <span>{t('adaptiveSampleCount')}</span>
              <input min="1" max="8" type="number" value={draft.Routing.Adaptive.SampleCount} onChange={(event) => updateNestedSetting('Routing', 'Adaptive', 'SampleCount', Number(event.target.value))} title={t('tooltipSettingsAdaptiveSampleCount')} />
              <small className="setting-impact live">{t('appliesToNewActivity')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsAdaptiveCapacityWeight')}>
              <span>{t('capacityWeight')}</span>
              <input min="0" type="number" value={draft.Routing.Adaptive.CapacityWeight} onChange={(event) => updateNestedSetting('Routing', 'Adaptive', 'CapacityWeight', Number(event.target.value))} title={t('tooltipSettingsAdaptiveCapacityWeight')} />
              <small className="setting-impact live">{t('appliesToNewActivity')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsAdaptiveInFlightWeight')}>
              <span>{t('inFlightWeight')}</span>
              <input min="0" type="number" value={draft.Routing.Adaptive.InFlightWeight} onChange={(event) => updateNestedSetting('Routing', 'Adaptive', 'InFlightWeight', Number(event.target.value))} title={t('tooltipSettingsAdaptiveInFlightWeight')} />
              <small className="setting-impact live">{t('appliesToNewActivity')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsAdaptiveClientWeight')}>
              <span>{t('clientWeight')}</span>
              <input min="0" type="number" value={draft.Routing.Adaptive.ClientWeight} onChange={(event) => updateNestedSetting('Routing', 'Adaptive', 'ClientWeight', Number(event.target.value))} title={t('tooltipSettingsAdaptiveClientWeight')} />
              <small className="setting-impact live">{t('appliesToNewActivity')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsAdaptiveHeartbeatWeight')}>
              <span>{t('heartbeatWeight')}</span>
              <input min="0" type="number" value={draft.Routing.Adaptive.HeartbeatWeight} onChange={(event) => updateNestedSetting('Routing', 'Adaptive', 'HeartbeatWeight', Number(event.target.value))} title={t('tooltipSettingsAdaptiveHeartbeatWeight')} />
              <small className="setting-impact live">{t('appliesToNewActivity')}</small>
            </label>
          </div>
        </Panel>
        <Panel actions={<SettingImpactBadge type="restart" />} title={t('databaseSettings')}>
          <SettingImpactNote restart={t('databaseRestartNote')} />
          <div className="form-grid two">
            <label className="form-field" title={t('tooltipSettingsDatabaseType')}>
              <span>{t('provider')}</span>
              <select value={draft.Database.Type} onChange={(event) => updateSetting('Database', 'Type', event.target.value)} title={t('tooltipSettingsDatabaseType')}>
                {['Sqlite', 'Postgresql', 'SqlServer', 'Mysql'].map((item) => <option key={item} value={item}>{item}</option>)}
              </select>
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsDatabaseFilename')}>
              <span>{t('filename')}</span>
              <input value={draft.Database.Filename} onChange={(event) => updateSetting('Database', 'Filename', event.target.value)} title={t('tooltipSettingsDatabaseFilename')} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsDatabaseServer')}>
              <span>{t('server')}</span>
              <input value={draft.Database.Server || ''} onChange={(event) => updateSetting('Database', 'Server', event.target.value)} title={t('tooltipSettingsDatabaseServer')} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsDatabasePort')}>
              <span>{t('port')}</span>
              <input min="0" type="number" value={draft.Database.Port} onChange={(event) => updateSetting('Database', 'Port', Number(event.target.value))} title={t('tooltipSettingsDatabasePort')} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsDatabaseName')}>
              <span>{t('databaseName')}</span>
              <input value={draft.Database.DatabaseName || ''} onChange={(event) => updateSetting('Database', 'DatabaseName', event.target.value)} title={t('tooltipSettingsDatabaseName')} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsDatabaseUsername')}>
              <span>{t('username')}</span>
              <input value={draft.Database.Username || ''} onChange={(event) => updateSetting('Database', 'Username', event.target.value)} title={t('tooltipSettingsDatabaseUsername')} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsDatabasePassword')}>
              <span>{t('password')}</span>
              <input type="password" value={draft.Database.Password || ''} onChange={(event) => updateSetting('Database', 'Password', event.target.value)} title={t('tooltipSettingsDatabasePassword')} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsConnectionString')}>
              <span>{t('connectionString')}</span>
              <input value={draft.Database.ConnectionString || ''} onChange={(event) => updateSetting('Database', 'ConnectionString', event.target.value)} title={t('tooltipSettingsConnectionString')} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
          </div>
        </Panel>
        <Panel actions={<SettingImpactBadge type="restart" />} title={t('loggingSettings')}>
          <SettingImpactNote restart={t('loggingRestartNote')} />
          <div className="form-grid two">
            <label className="toggle-field" title={t('tooltipSettingsConsoleLogging')}>
              <input checked={draft.Logging.ConsoleLogging} onChange={(event) => updateSetting('Logging', 'ConsoleLogging', event.target.checked)} title={t('tooltipSettingsConsoleLogging')} type="checkbox" />
              <span>{t('consoleLogging')}</span>
              <small className="setting-impact restart inline">{t('restartRequired')}</small>
            </label>
            <label className="toggle-field" title={t('tooltipSettingsFileLogging')}>
              <input checked={draft.Logging.FileLogging} onChange={(event) => updateSetting('Logging', 'FileLogging', event.target.checked)} title={t('tooltipSettingsFileLogging')} type="checkbox" />
              <span>{t('fileLogging')}</span>
              <small className="setting-impact restart inline">{t('restartRequired')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsLogDirectory')}>
              <span>{t('logDirectory')}</span>
              <input value={draft.Logging.LogDirectory} onChange={(event) => updateSetting('Logging', 'LogDirectory', event.target.value)} title={t('tooltipSettingsLogDirectory')} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
            <label className="form-field" title={t('tooltipSettingsLogFilename')}>
              <span>{t('logFilename')}</span>
              <input value={draft.Logging.LogFilename} onChange={(event) => updateSetting('Logging', 'LogFilename', event.target.value)} title={t('tooltipSettingsLogFilename')} />
              <small className="setting-impact restart">{t('restartRequired')}</small>
            </label>
          </div>
        </Panel>
        <Panel title={t('backupRestore')}>
          <div className="button-row">
            <IconButton onClick={downloadBackup} label={t('backup')} tooltip={t('tooltipBackupDownload')} icon={<Download size={16} />} />
            <IconButton onClick={restoreBackup} label={t('restore')} tooltip={t('tooltipRestoreBackup')} icon={<Upload size={16} />} variant="primary" />
            <CopyButton label={t('copyJson')} value={backup} />
          </div>
          <textarea className="backup-editor" value={backup} onChange={(event) => setBackup(event.target.value)} aria-label={t('backup')} title={t('tooltipBackupEditor')} />
        </Panel>
      </div>
      <Panel
        actions={(
          <div className="button-row">
            <CopyButton label={t('copyJson')} value={configText} />
            <IconButton onClick={saveJsonSettings} label={t('saveJson')} tooltip={t('tooltipSaveJson')} icon={<Upload size={16} />} variant="primary" />
          </div>
        )}
        title={t('rawConfigJson')}
      >
        <SettingImpactNote live={t('rawJsonLiveNote')} restart={t('rawJsonRestartNote')} />
        <textarea className="backup-editor config-editor" value={configText} onChange={(event) => setConfigText(event.target.value)} aria-label={t('rawConfigJson')} title={t('tooltipRawConfigJson')} />
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
  const [loading, setLoading] = useState(true);
  const [updatedAt, setUpdatedAt] = useState(0);
  const [version, setVersion] = useState(0);

  useEffect(() => {
    let alive = true;
    setLoading(true);
    api.get(path)
      .then((result) => {
        if (!alive) return;
        if (result?.error) {
          setData(fallback);
          setUpdatedAt(Date.now());
          setLoading(false);
          return;
        }

        setData(result);
        setUpdatedAt(Date.now());
        setLoading(false);
      })
      .catch(() => {
        if (!alive) return;
        setData(fallback);
        setUpdatedAt(Date.now());
        setLoading(false);
      });
    return () => { alive = false; };
  }, [path, version]);

  return { data, loading, refresh: () => setVersion((current) => current + 1), updatedAt };
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
    Routing: {
      DefaultTimeoutMs: 300000,
      LoadBalancingMode: 'RoundRobin',
      Adaptive: { SampleCount: 2, CapacityWeight: 45, InFlightWeight: 30, ClientWeight: 15, HeartbeatWeight: 10 }
    },
    TelemetryCollection: {
      Enabled: true,
      CollectionIntervalSeconds: 60,
      RequestTimeoutMs: 5000,
      RetentionDays: 30,
      PruneIntervalMinutes: 60,
      CollectHostTelemetry: true,
      CollectProviderTelemetry: true,
      PersistRawSnapshots: true,
      MaxRawSnapshotBytes: 262144,
      MaxSamplesPerClientResponse: 1,
      MaxProviderMetricRowsPerResponse: 1000,
      MaxConcurrentClientRequests: 8
    },
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
    Routing: {
      ...defaults.Routing,
      ...(source.Routing || source.routing || {}),
      Adaptive: {
        ...defaults.Routing.Adaptive,
        ...((source.Routing || source.routing || {}).Adaptive || (source.Routing || source.routing || {}).adaptive || {})
      }
    },
    TelemetryCollection: {
      ...defaults.TelemetryCollection,
      ...(source.TelemetryCollection || source.telemetryCollection || {})
    },
    Database: { ...defaults.Database, ...(source.Database || source.database || {}) },
    Logging: { ...defaults.Logging, ...(source.Logging || source.logging || {}) }
  };
}

function listToText(input) {
  return Array.isArray(input) ? input.join('\n') : '';
}

function MetricCard({ icon, label, subtext, tone = 'neutral', value, valueClassName = '' }) {
  return (
    <div className={`metric-card ${tone}`}>
      <div className="metric-icon">{icon}</div>
      <div className="metric-copy">
        <span>{label}</span>
        <strong className={valueClassName}>{value ?? '-'}</strong>
        {subtext ? <small>{subtext}</small> : null}
      </div>
    </div>
  );
}

function KeyValue({ label, value }) {
  return (
    <div className="detail-item">
      <span>{label}</span>
      <strong>{value ?? '-'}</strong>
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

function IconButton({ disabled = false, icon, iconOnly = false, label, onClick, tooltip, variant = 'secondary' }) {
  return (
    <button className={`action-button ${variant} ${iconOnly ? 'icon-only' : ''}`} disabled={disabled} onClick={onClick} title={tooltip || label} type="button">
      {icon}
      <span className={iconOnly ? 'sr-only' : ''}>{label}</span>
    </button>
  );
}

function DataTable({ actions, canDelete, columns, deleteMessage, deleteTitle, getRowActions, loading = false, onDelete, onRefresh, onRowClick, rowActionLabel, rows, subtitle, title }) {
  const { t } = useTranslation();
  const [query, setQuery] = useState('');
  const [columnFilters, setColumnFilters] = useState({});
  const [columnFilterDrafts, setColumnFilterDrafts] = useState({});
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

  function columnFilterValue(columnKey) {
    return Object.prototype.hasOwnProperty.call(columnFilterDrafts, columnKey)
      ? columnFilterDrafts[columnKey]
      : columnFilters[columnKey] || '';
  }

  function updateColumnFilterDraft(columnKey, filterValue) {
    setColumnFilterDrafts((current) => ({ ...current, [columnKey]: filterValue }));
  }

  function applyColumnFilter(columnKey, filterValue) {
    setColumnFilters((current) => ({ ...current, [columnKey]: filterValue }));
    setColumnFilterDrafts((current) => {
      const next = { ...current };
      delete next[columnKey];
      return next;
    });
  }

  function handleColumnFilterKeyDown(event, columnKey) {
    if (event.key === 'Enter') {
      event.preventDefault();
      event.stopPropagation();
      applyColumnFilter(columnKey, event.currentTarget.value);
    }

    if (event.key === 'Escape') {
      event.preventDefault();
      event.stopPropagation();
      setColumnFilterDrafts((current) => {
        const next = { ...current };
        delete next[columnKey];
        return next;
      });
    }
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
        tooltip: t('tooltipEditRow'),
        onSelect: () => onRowClick(row)
      });
    }
    if (getRowActions) {
      const additionalItems = getRowActions(row);
      if (Array.isArray(additionalItems) && additionalItems.length > 0) {
        items.push(...additionalItems);
      }
    }
    items.push({
      icon: <FileJson size={14} />,
      key: 'view-json',
      label: t('viewJson'),
      tooltip: t('tooltipViewJson'),
      onSelect: () => setJsonRow(row)
    });
    items.push({ divider: true, key: 'divider-delete' });
    items.push({
      destructive: true,
      disabled: !deleteAllowed,
      icon: <Trash2 size={14} />,
      key: 'delete',
      label: deleteAllowed ? t('delete') : t('deleteUnavailable'),
      tooltip: t('tooltipDeleteRow'),
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
          <label className="search-box" title={t('tooltipSearchTable')}>
            <Search size={15} />
            <input aria-label={t('search')} onChange={(event) => setQuery(event.target.value)} placeholder={t('search')} title={t('tooltipSearchTable')} value={query} />
          </label>
          <span className="row-count" title={t('showingRange', { start: sortedRows.length === 0 ? 0 : 1, end: filteredRows.length, total: filteredRows.length })}>{filteredRows.length} {t('rows')}</span>
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
                <th className={[column.sortable === false ? '' : 'sortable', column.className || ''].filter(Boolean).join(' ')} key={column.key} onClick={() => handleSort(column)} title={column.sortable === false ? undefined : t('tooltipSortColumn')}>
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
                  <th className={column.className || undefined} key={`${column.key}-filter`}>
                    {column.filterable === false ? <span /> : (
                      <input
                        aria-label={t('filterColumn', { column: t(column.label || column.key, column.label || column.key) })}
                        onBlur={(event) => applyColumnFilter(column.key, event.target.value)}
                        onChange={(event) => updateColumnFilterDraft(column.key, event.target.value)}
                        onClick={(event) => event.stopPropagation()}
                        onKeyDown={(event) => handleColumnFilterKeyDown(event, column.key)}
                        placeholder={t('filter')}
                        title={t('tooltipColumnFilter')}
                        value={columnFilterValue(column.key)}
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
                  {loading ? <TableLoadingState /> : <EmptyState message={t('emptyTableMessage')} title={t('noData')} />}
                </td>
              </tr>
            )}
            {pagedRows.map((row, index) => (
              <tr className={onRowClick ? 'clickable-row' : ''} key={value(row, 'id') || value(row, 'guid') || index} onClick={(event) => handleRowClick(event, row)}>
                {normalizedColumns.map((column) => (
                  <td className={column.className || undefined} key={column.key}>
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

function TableLoadingState() {
  const { t } = useTranslation();
  return (
    <div aria-live="polite" className="table-loading-state" role="status">
      <div aria-hidden="true" className="table-loading-spinner" />
      <strong>{t('loading')}</strong>
    </div>
  );
}

function CellValue({ columnKey, value: cell }) {
  const { t } = useTranslation();
  if (cell === undefined || cell === null || cell === '') return <span className="muted-cell">-</span>;
  if (isReactNode(cell)) return cell;
  if (isIdentifierColumn(columnKey)) return <CopyableValue label={t('copyValue')} value={cell} />;
  if (typeof cell === 'boolean') return <StatusChip tone={cell ? 'success' : 'danger'} value={cell ? t('yes') : t('no')} />;
  if (isStatusColumn(columnKey)) return <StatusChip tone={toneForValue(cell)} value={String(cell)} />;
  if (Array.isArray(cell)) {
    const arrayText = cell.join(', ');
    return <span className="array-cell" title={arrayText}>{arrayText}</span>;
  }
  if (typeof cell === 'object') {
    const objectText = JSON.stringify(cell);
    return <code className="code-cell" title={objectText}>{objectText}</code>;
  }
  return <span title={String(cell)}>{String(cell)}</span>;
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

function RequestDetailsModal({ data, onClose }) {
  const { t } = useTranslation();
  const record = data || {};
  const telemetry = value(record, 'telemetry') || {};
  const routing = value(record, 'routingDecision') || {};
  const timeline = Array.isArray(value(record, 'timeline')) ? value(record, 'timeline') : [];
  const requestHeaders = value(record, 'requestHeaders') || {};
  const responseHeaders = value(record, 'responseHeaders') || {};
  const requestBody = value(record, 'requestBody');
  const responseBody = value(record, 'responseBody');
  const statusCode = value(record, 'statusCode');
  const id = recordId(record);
  const candidateClientIds = value(routing, 'candidateClientIds');
  const routingRows = [
    { label: t('tenant', 'Tenant'), value: value(record, 'tenantId'), copy: true },
    { label: t('requestorTenantId', 'Requestor Tenant ID'), value: value(record, 'requestorTenantId') || value(record, 'tenantId'), copy: true },
    { label: t('requestorPrincipalType', 'Requestor Principal Type'), value: value(record, 'requestorPrincipalType') || value(record, 'principalType') },
    { label: t('requestorPrincipalId', 'Requestor Principal ID'), value: value(record, 'requestorPrincipalId') || value(record, 'credentialId') || value(record, 'userId'), copy: true },
    { label: t('authScheme', 'Auth Scheme'), value: value(record, 'authScheme') },
    { label: t('client', 'Client'), value: value(record, 'selectedClientId') || value(routing, 'selectedClientId'), copy: true },
    { label: t('selectedClientName', 'Selected client name'), value: value(routing, 'selectedClientName') },
    { label: t('model', 'Model'), value: value(record, 'model') || value(routing, 'effectiveModel') },
    { label: t('sourceIp', 'Source IP'), value: value(record, 'sourceIp') },
    { label: t('outcome', 'Outcome'), value: value(routing, 'outcomeCode') },
    { label: t('routingPolicy', 'Routing Policy'), value: value(routing, 'routingPolicyId'), copy: true },
    { label: t('routingPolicyName', 'Routing Policy Name'), value: value(routing, 'routingPolicyName') },
    { label: t('routingPolicyMatch', 'Routing Policy Match'), value: `${value(routing, 'routingPolicyModelName') || '*'} / ${value(routing, 'routingPolicyClientName') || '*'}` },
    { label: t('routingPolicyReason', 'Routing Policy Reason'), value: value(routing, 'routingPolicyReason') },
    { label: t('routingPolicyTimeout', 'Routing Policy Timeout'), value: formatDuration(value(routing, 'routingPolicyRequestTimeoutMs')) },
    { label: t('loadBalancingMode', 'Load-balancing mode'), value: value(routing, 'loadBalancingMode') },
    { label: t('candidateCount', 'Candidate count'), value: formatOptionalNumber(value(routing, 'candidateCount')) },
    { label: t('candidateClients', 'Candidate clients'), value: Array.isArray(candidateClientIds) ? candidateClientIds.join(', ') : candidateClientIds, copy: true },
    { label: t('selectedApiType', 'Selected API Type'), value: value(routing, 'selectedProvider') },
    { label: t('selectedSource', 'Selected source'), value: value(routing, 'selectedSource') },
    { label: t('selectedInFlight', 'Selected in-flight'), value: formatOptionalNumber(value(routing, 'selectedClientInFlightRequests')) },
    { label: t('selectedMaxParallel', 'Selected max parallel'), value: formatOptionalNumber(value(routing, 'selectedClientMaxParallelRequests')) },
    { label: t('selectedWeight', 'Selected weight'), value: formatOptionalNumber(value(routing, 'selectedClientWeight')) },
    { label: t('lastHeartbeatUtc', 'Last heartbeat UTC'), value: formatDateTime(value(routing, 'selectedClientLastHeartbeatUtc')) },
    { label: t('adaptiveSampleCount', 'Adaptive sample count'), value: formatOptionalNumber(value(routing, 'adaptiveSampleCount')) },
    { label: t('adaptiveScore', 'Adaptive score'), value: value(routing, 'adaptiveScore') },
    { label: t('selectionReason', 'Selection reason'), value: value(routing, 'selectionReason') },
    { label: t('createdUtc', 'Created UTC'), value: formatDateTime(value(record, 'createdUtc')) },
    { label: t('completedUtc', 'Completed UTC'), value: formatDateTime(value(record, 'completedUtc')) }
  ];
  const telemetryRows = [
    { label: t('status', 'Status'), value: statusCode ? String(statusCode) : '-' },
    { label: t('totalTime', 'Total time'), value: formatDuration(value(record, 'durationMs') ?? value(telemetry, 'totalTimeMs')) },
    { label: t('timeToFirstToken', 'Time to first token'), value: formatDuration(value(telemetry, 'timeToFirstTokenMs')) },
    { label: t('streamingTime', 'Streaming time'), value: formatDuration(value(telemetry, 'totalStreamingTimeMs')) },
    { label: t('clientRoundTrip', 'Client round trip'), value: formatDuration(value(telemetry, 'clientRoundTripMs') ?? value(routing, 'clientRoundTripMs')) },
    { label: t('upstreamTotal', 'Upstream total'), value: formatDuration(value(telemetry, 'upstreamTotalMs')) },
    { label: t('requestToHeaders', 'Request to headers'), value: formatDuration(value(telemetry, 'requestToHeadersMs')) },
    { label: t('transferMode', 'Transfer mode'), value: value(telemetry, 'transferMode') },
    { label: t('promptTokens', 'Prompt tokens'), value: formatOptionalNumber(value(telemetry, 'promptTokens')) },
    { label: t('completionTokens', 'Completion tokens'), value: formatOptionalNumber(value(telemetry, 'completionTokens')) },
    { label: t('totalTokens', 'Total tokens'), value: formatOptionalNumber(value(telemetry, 'totalTokens')) },
    { label: t('chunks', 'Chunks'), value: formatOptionalNumber(value(telemetry, 'streamChunkCount')) },
    { label: t('streamEvents', 'Stream events'), value: formatOptionalNumber(value(telemetry, 'streamEventCount')) },
    { label: t('providerRequest', 'Provider request'), value: value(telemetry, 'providerRequestId'), copy: true },
    { label: t('requestBytes', 'Request bytes'), value: formatBytes(value(telemetry, 'requestBytes') ?? value(record, 'requestBodyLength')) },
    { label: t('responseBytes', 'Response bytes'), value: formatBytes(value(telemetry, 'responseBytes') ?? value(record, 'responseBodyLength')) }
  ];

  return (
    <Modal
      actions={<button className="action-button secondary" onClick={onClose} title={t('tooltipFormClose')} type="button">{t('close')}</button>}
      headerActions={<CopyButton label={t('copyJson')} value={stringifyJson(record)} />}
      onClose={onClose}
      open
      size="request-details"
      subtitle={t('requestDetailsSubtitle', 'Captured routing, telemetry, headers, and payloads for this request.')}
      title={t('requestDetails')}
    >
      <div className="request-detail-layout">
        <section className="request-detail-summary">
          <div className="request-detail-titleline">
            <span className={`method-pill ${methodTone(value(record, 'method'))}`}>{value(record, 'method') || '-'}</span>
            <code>{value(record, 'path') || '-'}</code>
          </div>
          <div className="request-detail-id">
            <span>{t('id', 'ID')}</span>
            <CopyableValue label={t('copyValue')} value={id || '-'} />
          </div>
        </section>

        <section className="request-detail-section">
          <h3>{t('routing', 'Routing')}</h3>
          <DetailTable rows={routingRows} />
        </section>

        <section className="request-detail-section">
          <h3>{t('telemetry', 'Telemetry')}</h3>
          <DetailTable rows={telemetryRows} />
        </section>

        <section className="request-detail-section">
          <h3>{t('timeline', 'Timeline')}</h3>
          <div className="request-timeline">
            {timeline.length > 0 ? timeline.map((item, index) => (
              <div className="request-timeline-item" key={`${value(item, 'stage') || index}-${index}`}>
                <div className="request-timeline-marker" />
                <div className="request-timeline-content">
                  <div className="request-timeline-heading">
                    <strong>{value(item, 'stage') || '-'}</strong>
                    <span>{formatDateTime(value(item, 'timestampUtc'))} · {formatDuration(value(item, 'elapsedMs'))}</span>
                  </div>
                  <p>{value(item, 'message') || '-'}</p>
                </div>
              </div>
            )) : <p className="muted-copy">{t('noTimeline', 'No timeline events were captured.')}</p>}
          </div>
        </section>

        <div className="request-payload-grid">
          <PayloadPanel title={t('requestHeadersTitle', 'Request Headers')} value={requestHeaders} />
          <PayloadPanel title={t('requestBodyTitle', 'Request Body')} meta={bodyMeta(record, 'request')} value={requestBody} />
          <PayloadPanel title={t('responseHeadersTitle', 'Response Headers')} value={responseHeaders} />
          <PayloadPanel title={t('responseBodyTitle', 'Response Body')} meta={bodyMeta(record, 'response')} value={responseBody} />
        </div>
      </div>
    </Modal>
  );
}

function DetailTable({ rows }) {
  return (
    <div className="request-detail-table-wrap">
      <table className="request-detail-table">
        <tbody>
          {rows.map((row) => (
            <tr key={row.label}>
              <th scope="row">{row.label}</th>
              <td><DetailTableValue row={row} /></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function DetailTableValue({ row }) {
  const display = row.value === undefined || row.value === null || row.value === '' ? '-' : row.value;
  if (row.copy && display !== '-') return <CopyableValue value={display} />;
  return <span title={String(display)}>{String(display)}</span>;
}

function PayloadPanel({ meta, title, value: fieldValue }) {
  const { t } = useTranslation();
  const [expanded, setExpanded] = useState(true);
  const text = formatPayload(fieldValue);
  return (
    <section className={`request-payload-panel ${expanded ? 'expanded' : 'collapsed'}`}>
      <div className="request-payload-header">
        <div>
          <button
            aria-expanded={expanded}
            className="request-payload-toggle"
            onClick={() => setExpanded((current) => !current)}
            title={expanded ? t('collapse') : t('expand')}
            type="button"
          >
            {expanded ? <ChevronDown size={16} /> : <ChevronRight size={16} />}
            <h3>{title}</h3>
          </button>
          {meta ? <span>{meta}</span> : null}
        </div>
        <CopyButton label={t('copy')} value={text} />
      </div>
      {expanded ? <pre className="request-detail-code">{text || '(empty)'}</pre> : null}
    </section>
  );
}

function formatPayload(input) {
  if (input === undefined || input === null || input === '') return '';
  if (typeof input === 'string') {
    const trimmed = input.trim();
    if ((trimmed.startsWith('{') && trimmed.endsWith('}')) || (trimmed.startsWith('[') && trimmed.endsWith(']'))) {
      try {
        return JSON.stringify(JSON.parse(trimmed), null, 2);
      } catch {
        return input;
      }
    }
    return input;
  }

  return JSON.stringify(input, null, 2);
}

function bodyMeta(record, prefix) {
  const length = value(record, `${prefix}BodyLength`);
  const truncated = value(record, `${prefix}BodyTruncated`) === true;
  const parts = [];
  if (length !== undefined && length !== null) parts.push(formatBytes(length));
  if (truncated) parts.push('truncated');
  return parts.join(' · ');
}

function EditRecordModal({ title, data, onClose }) {
  const { t } = useTranslation();
  const record = data || {};
  const entries = Object.entries(record);
  const scalarEntries = entries.filter(([, fieldValue]) => isScalarValue(fieldValue));
  const complexEntries = entries.filter(([, fieldValue]) => !isScalarValue(fieldValue));

  return (
    <Modal
      actions={<button className="action-button secondary" onClick={onClose} title={t('tooltipFormClose')} type="button">{t('close')}</button>}
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

function UserEditModal({ actorUser, data, mode = 'edit', onClose, onSave, session }) {
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
  const targetTenantId = value(data, 'tenantId') || session?.tenantId;
  const actorIsSystemAdmin = value(actorUser, 'isAdmin') === true;
  const actorIsTenantAdmin = value(actorUser, 'isTenantAdmin') === true && value(actorUser, 'tenantId') === targetTenantId;
  const canEditSystemAdmin = actorIsSystemAdmin;
  const canEditTenantAdmin = actorIsSystemAdmin || actorIsTenantAdmin;

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
        active: draft.active
      };
      if (canEditSystemAdmin) payload.isAdmin = draft.isAdmin;
      if (canEditTenantAdmin) payload.isTenantAdmin = draft.isTenantAdmin;
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
          <button className="action-button secondary" onClick={onClose} title={t('tooltipFormClose')} type="button">{t('close')}</button>
          <button className="action-button primary" disabled={saving || !draft.username.trim() || (isCreate && !draft.password.trim())} onClick={save} title={t('tooltipFormSave')} type="button">
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
            <input checked={draft.isTenantAdmin} disabled={!canEditTenantAdmin} onChange={(event) => updateField('isTenantAdmin', event.target.checked)} type="checkbox" />
            <span>{t('isTenantAdmin')}</span>
          </label>
          {canEditSystemAdmin ? (
            <label className="toggle-field">
              <input checked={draft.isAdmin} onChange={(event) => updateField('isAdmin', event.target.checked)} type="checkbox" />
              <span>{t('isAdmin')}</span>
            </label>
          ) : null}
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
          <button className="action-button secondary" onClick={onClose} title={t('tooltipFormClose')} type="button">{t('close')}</button>
          <button className="action-button primary" disabled={saving || !draft.name.trim()} onClick={save} title={t('tooltipFormSave')} type="button">
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
      await onSave(isCreate ? null : credentialId, payload);
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
          <button className="action-button secondary" onClick={onClose} title={t('tooltipFormClose')} type="button">{t('close')}</button>
          <button className="action-button primary" disabled={saving || (!isCreate && !draft.accessKey.trim())} onClick={save} title={t('tooltipFormSave')} type="button">
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
          <button className="action-button secondary" onClick={onClose} title={t('tooltipFormClose')} type="button">{t('close')}</button>
          <button className="action-button primary" disabled={saving || !draft.name.trim()} onClick={save} title={t('tooltipFormSave')} type="button">
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

function RoleAssignmentEditModal({ credentials, onClose, onSave, roles, users }) {
  const { t } = useTranslation();
  const roleRows = Array.isArray(roles) ? roles : [];
  const userRows = Array.isArray(users) ? users : [];
  const credentialRows = Array.isArray(credentials) ? credentials : [];
  const [draft, setDraft] = useState(() => ({
    roleId: recordId(roleRows[0]) || '',
    principalType: 'User',
    principalId: recordId(userRows[0]) || '',
    active: true
  }));
  const [saving, setSaving] = useState(false);
  const [status, setStatus] = useState('');
  const [statusTone, setStatusTone] = useState('success');
  const principalRows = draft.principalType === 'Credential' ? credentialRows : userRows;

  function updateField(key, fieldValue) {
    setDraft((current) => ({ ...current, [key]: fieldValue }));
    setStatus('');
    setStatusTone('success');
  }

  function updatePrincipalType(principalType) {
    const nextRows = principalType === 'Credential' ? credentialRows : userRows;
    setDraft((current) => ({
      ...current,
      principalType,
      principalId: recordId(nextRows[0]) || ''
    }));
    setStatus('');
    setStatusTone('success');
  }

  async function save() {
    if (saving || !draft.roleId || !draft.principalId) return;
    setSaving(true);
    setStatus('');
    try {
      await onSave(draft);
      setStatusTone('success');
      setStatus(t('roleAssignmentSaved'));
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
          <button className="action-button secondary" onClick={onClose} title={t('tooltipFormClose')} type="button">{t('close')}</button>
          <button className="action-button primary" disabled={saving || !draft.roleId || !draft.principalId} onClick={save} title={t('tooltipFormSave')} type="button">
            {saving ? t('saving') : t('save')}
          </button>
        </>
      )}
      headerActions={<CopyButton label={t('copyJson')} value={stringifyJson(draft)} />}
      onClose={onClose}
      open
      subtitle={t('createRoleAssignmentSubtitle')}
      title={t('createRoleAssignment')}
    >
      <div className="form-stack">
        <label className="form-field">
          <span>{t('role')}</span>
          <select value={draft.roleId} onChange={(event) => updateField('roleId', event.target.value)}>
            {roleRows.map((role) => <option key={recordId(role)} value={recordId(role)}>{value(role, 'name') || recordId(role)}</option>)}
          </select>
        </label>
        <label className="form-field">
          <span>{t('principalType')}</span>
          <select value={draft.principalType} onChange={(event) => updatePrincipalType(event.target.value)}>
            <option value="User">{t('user')}</option>
            <option value="Credential">{t('credential')}</option>
          </select>
        </label>
        <label className="form-field">
          <span>{t('principalId')}</span>
          <select value={draft.principalId} onChange={(event) => updateField('principalId', event.target.value)}>
            {principalRows.map((principal) => (
              <option key={recordId(principal)} value={recordId(principal)}>
                {value(principal, 'email') || value(principal, 'username') || value(principal, 'accessKey') || recordId(principal)}
              </option>
            ))}
          </select>
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
  const operationTypes = ['Read', 'Write', 'Delete', 'Execute', 'Admin'];

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
          <button className="action-button secondary" onClick={onClose} title={t('tooltipFormClose')} type="button">{t('close')}</button>
          <button className="action-button primary" disabled={saving || !draft.resourceType.trim()} onClick={save} title={t('tooltipFormSave')} type="button">
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

function RoutingPolicyEditModal({ data, mode = 'edit', onClose, onSave }) {
  const { t } = useTranslation();
  const policyId = recordId(data);
  const isCreate = mode === 'create';
  const isDefault = value(data, 'isDefault') === true;
  const sourceAdaptive = value(data, 'adaptive') || {};
  const [draft, setDraft] = useState(() => ({
    name: value(data, 'name') || '',
    modelName: value(data, 'modelName') || '*',
    clientName: value(data, 'clientName') || '*',
    requestTimeoutMs: value(data, 'requestTimeoutMs') || 300000,
    loadBalancingMode: value(data, 'loadBalancingMode') || 'LeastRecentlyUsed',
    active: value(data, 'active') !== false,
    adaptive: {
      sampleCount: value(sourceAdaptive, 'sampleCount') || value(sourceAdaptive, 'SampleCount') || 3,
      capacityWeight: value(sourceAdaptive, 'capacityWeight') || value(sourceAdaptive, 'CapacityWeight') || 40,
      inFlightWeight: value(sourceAdaptive, 'inFlightWeight') || value(sourceAdaptive, 'InFlightWeight') || 30,
      clientWeight: value(sourceAdaptive, 'clientWeight') || value(sourceAdaptive, 'ClientWeight') || 20,
      heartbeatWeight: value(sourceAdaptive, 'heartbeatWeight') || value(sourceAdaptive, 'HeartbeatWeight') || 10
    }
  }));
  const [saving, setSaving] = useState(false);
  const [status, setStatus] = useState('');
  const [statusTone, setStatusTone] = useState('success');
  const loadBalancingModes = ['LeastRecentlyUsed', 'RoundRobin', 'Random', 'FirstAvailable', 'Adaptive'];

  function updateField(key, fieldValue) {
    setDraft((current) => ({ ...current, [key]: fieldValue }));
    setStatus('');
    setStatusTone('success');
  }

  function updateAdaptiveField(key, fieldValue) {
    setDraft((current) => ({
      ...current,
      adaptive: {
        ...current.adaptive,
        [key]: Number(fieldValue)
      }
    }));
    setStatus('');
    setStatusTone('success');
  }

  async function save() {
    if ((!isCreate && !policyId) || saving) return;
    setSaving(true);
    setStatus('');
    try {
      const payload = {
        name: draft.name,
        modelName: isDefault ? '*' : draft.modelName,
        clientName: isDefault ? '*' : draft.clientName,
        requestTimeoutMs: Number(draft.requestTimeoutMs),
        loadBalancingMode: draft.loadBalancingMode,
        adaptive: {
          sampleCount: Number(draft.adaptive.sampleCount),
          capacityWeight: Number(draft.adaptive.capacityWeight),
          inFlightWeight: Number(draft.adaptive.inFlightWeight),
          clientWeight: Number(draft.adaptive.clientWeight),
          heartbeatWeight: Number(draft.adaptive.heartbeatWeight)
        },
        active: isDefault ? true : draft.active
      };
      await onSave(isCreate ? null : policyId, payload);
      setStatusTone('success');
      setStatus(t('routingPolicySaved'));
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
          <button className="action-button secondary" onClick={onClose} title={t('tooltipFormClose')} type="button">{t('close')}</button>
          <button className="action-button primary" disabled={saving || !draft.name.trim()} onClick={save} title={t('tooltipFormSave')} type="button">
            {saving ? t('saving') : t('save')}
          </button>
        </>
      )}
      headerActions={<CopyButton label={t('copyJson')} value={stringifyJson(data || {})} />}
      onClose={onClose}
      open
      subtitle={isCreate ? t('createRoutingPolicySubtitle') : t('editRoutingPolicySubtitle')}
      title={isCreate ? t('createRoutingPolicy') : t('editRoutingPolicy')}
    >
      <div className="form-stack">
        {!isCreate ? <RecordSummaryTable entries={[['id', policyId], ['tenantId', value(data, 'tenantId')], ['isDefault', isDefault]]} /> : null}
        {isDefault ? <p className="form-status warning">{t('defaultRoutingPolicyNotice')}</p> : null}
        <div className="form-grid two">
          <label className="form-field">
            <span>{t('name')}</span>
            <input onChange={(event) => updateField('name', event.target.value)} title={t('tooltipRoutingPolicyName')} value={draft.name} />
          </label>
          <label className="form-field">
            <span>{t('loadBalancingMode')}</span>
            <select onChange={(event) => updateField('loadBalancingMode', event.target.value)} title={t('tooltipRoutingPolicyLoadBalancingMode')} value={draft.loadBalancingMode}>
              {loadBalancingModes.map((modeName) => <option key={modeName} value={modeName}>{modeName}</option>)}
            </select>
          </label>
        </div>
        <div className="form-grid three">
          <label className="form-field">
            <span>{t('modelName')}</span>
            <input disabled={isDefault} onChange={(event) => updateField('modelName', event.target.value)} title={t('tooltipRoutingPolicyModelName')} value={isDefault ? '*' : draft.modelName} />
          </label>
          <label className="form-field">
            <span>{t('clientName')}</span>
            <input disabled={isDefault} onChange={(event) => updateField('clientName', event.target.value)} title={t('tooltipRoutingPolicyClientName')} value={isDefault ? '*' : draft.clientName} />
          </label>
          <label className="form-field">
            <span>{t('requestTimeoutMs')}</span>
            <input min="1000" onChange={(event) => updateField('requestTimeoutMs', event.target.value)} title={t('tooltipRoutingPolicyTimeout')} type="number" value={draft.requestTimeoutMs} />
          </label>
        </div>
        <div className="form-grid five">
          <label className="form-field">
            <span>{t('sampleCount')}</span>
            <input min="1" onChange={(event) => updateAdaptiveField('sampleCount', event.target.value)} title={t('tooltipRoutingPolicySampleCount')} type="number" value={draft.adaptive.sampleCount} />
          </label>
          <label className="form-field">
            <span>{t('capacityWeight')}</span>
            <input min="0" onChange={(event) => updateAdaptiveField('capacityWeight', event.target.value)} title={t('tooltipRoutingPolicyCapacityWeight')} type="number" value={draft.adaptive.capacityWeight} />
          </label>
          <label className="form-field">
            <span>{t('inFlightWeight')}</span>
            <input min="0" onChange={(event) => updateAdaptiveField('inFlightWeight', event.target.value)} title={t('tooltipRoutingPolicyInFlightWeight')} type="number" value={draft.adaptive.inFlightWeight} />
          </label>
          <label className="form-field">
            <span>{t('clientWeight')}</span>
            <input min="0" onChange={(event) => updateAdaptiveField('clientWeight', event.target.value)} title={t('tooltipRoutingPolicyClientWeight')} type="number" value={draft.adaptive.clientWeight} />
          </label>
          <label className="form-field">
            <span>{t('heartbeatWeight')}</span>
            <input min="0" onChange={(event) => updateAdaptiveField('heartbeatWeight', event.target.value)} title={t('tooltipRoutingPolicyHeartbeatWeight')} type="number" value={draft.adaptive.heartbeatWeight} />
          </label>
        </div>
        <label className="toggle-field">
          <input checked={isDefault ? true : draft.active} disabled={isDefault} onChange={(event) => updateField('active', event.target.checked)} type="checkbox" />
          <span>{t('active')}</span>
        </label>
        {status ? <p className={`form-status ${statusTone}`}>{status}</p> : null}
      </div>
    </Modal>
  );
}

function JsonRecordModal({ data, onClose, title }) {
  const { t } = useTranslation();
  return (
    <Modal
      actions={<button className="action-button secondary" onClick={onClose} title={t('tooltipFormClose')} type="button">{t('close')}</button>}
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
  const hadResponseRef = useRef(false);
  const normalized = normalizeExplorerResponse(response);
  const tabs = [
    { id: 'preview', label: t('responsePreview') },
    { id: 'body', label: t('responseBody') },
    { id: 'headers', label: t('responseHeaders') }
  ];
  const bodyText = explorerResponseBodyText(normalized);
  const headersText = formatExplorerValue(normalized.headers);
  const previewText = normalized.preview || extractExplorerPreview(normalized.body) || (normalized.streamed ? '' : bodyText);
  const hasHeaders = Object.keys(normalized.headers || {}).length > 0;
  const hasJsonBody = isExplorerJsonResponse(normalized);
  const copyText = explorerResponseCopyText(activeTab, normalized, bodyText, headersText, previewText);

  useEffect(() => {
    if (!response) {
      hadResponseRef.current = false;
      setActiveTab('preview');
      return;
    }

    if (!hadResponseRef.current) {
      hadResponseRef.current = true;
      setActiveTab(normalized.streamed ? 'preview' : 'body');
    }
  }, [normalized.streamed, response]);

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
              title={t('tooltipResponseTab')}
              type="button"
            >
              {tab.label}
            </button>
          ))}
        </div>
        <div className="api-explorer-response-actions">
          {renderExplorerStatusBadge(normalized, t)}
          {response ? (
            <div className="api-explorer-response-indicators" aria-label={t('responseStatus')}>
              <ResponseHeaderIndicator label={t('transferMode')} value={normalized.streamed ? streamTypeLabel(normalized.streamType, t) : t('standardResponse')} />
              <ResponseHeaderIndicator label={t('timeToFirstByte')} value={formatDuration(normalized.timing.timeToFirstByteMs)} />
              <ResponseHeaderIndicator label={t('totalTime')} value={formatDuration(normalized.timing.totalTimeMs ?? normalized.timing.requestTimeMs)} />
            </div>
          ) : null}
          {response ? <CopyButton label={t('copy')} value={copyText} /> : null}
          {response && hasJsonBody ? (
            <IconButton
              iconOnly
              onClick={() => setActiveTab('body')}
              label={t('formatJson')}
              tooltip={t('tooltipFormatJson')}
              icon={<FileJson size={16} />}
            />
          ) : null}
          {response ? <IconButton iconOnly onClick={onClear} label={t('clear')} tooltip={t('tooltipClearResponse')} icon={<Trash2 size={16} />} /> : null}
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
              normalized.streamed ? (
                <Suspense fallback={<pre className="api-explorer-code">{previewText}</pre>}>
                  <MarkdownPreview text={previewText} />
                </Suspense>
              ) : (
                <pre className="api-explorer-code">{previewText}</pre>
              )
            ) : (
              <EmptyState title={t('noData')} message={isSending ? t('waitingForResponse') : t('noResponseBody')} />
            )}
          </div>
        ) : null}

        {activeTab === 'body' ? (
          <div className="api-explorer-tab-content">
            {bodyText ? (
              <pre className="api-explorer-code">{bodyText}</pre>
            ) : (
              <EmptyState title={t('noData')} message={t('noResponseBody')} />
            )}
          </div>
        ) : null}

        {activeTab === 'headers' ? (
          <div className="api-explorer-tab-content">
            {hasHeaders ? (
              <pre className="api-explorer-code">{headersText}</pre>
            ) : (
              <EmptyState title={t('noData')} message={t('noResponseHeaders')} />
            )}
          </div>
        ) : null}
      </div>
    </section>
  );
}

function ResponseHeaderIndicator({ label, value }) {
  return (
    <span className="api-explorer-response-indicator" title={`${label}: ${value || '-'}`}>
      <span>{label}</span>
      <strong>{value || '-'}</strong>
    </span>
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
      <JsonTreeNode data={data} />
    </div>
  );
}

function JsonTreeNode({ data, fieldKey = '' }) {
  if (data === null || data === undefined) return <span className="json-null">null</span>;
  if (Array.isArray(data)) {
    return (
      <div className="json-array">
        {data.length === 0 ? <span className="json-null">[]</span> : data.map((item, index) => (
          <div className="json-row" key={index}>
            <span className="json-key">{index}</span>
            <JsonTreeNode data={item} fieldKey={fieldKey} />
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
            <JsonTreeNode data={value} fieldKey={key} />
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

function explorerResponseCopyText(activeTab, response, bodyText, headersText, previewText) {
  if (activeTab === 'headers') return headersText;
  if (activeTab === 'preview') return previewText;
  return bodyText;
}

function isExplorerJsonResponse(response) {
  if (!response) return false;
  const contentType = String(response.headers?.['content-type'] || response.headers?.['Content-Type'] || '').toLowerCase();
  if (contentType.includes('json')) return true;
  if (response.body !== null && response.body !== undefined && typeof response.body === 'object') return true;
  if (!response.rawBody || typeof response.rawBody !== 'string') return false;
  try {
    JSON.parse(response.rawBody);
    return true;
  } catch {
    return false;
  }
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

function formatOptionalNumber(input) {
  const number = Number(input);
  return Number.isFinite(number) ? number.toLocaleString() : '-';
}

function formatBytes(input) {
  const number = Number(input);
  if (!Number.isFinite(number)) return '-';
  if (number < 1024) return `${number.toLocaleString()} B`;
  if (number < 1024 * 1024) return `${(number / 1024).toFixed(1)} KB`;
  return `${(number / (1024 * 1024)).toFixed(2)} MB`;
}

function formatDateTime(input) {
  if (!input) return '-';
  const date = new Date(input);
  if (Number.isNaN(date.getTime())) return String(input);
  return date.toLocaleString();
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
  if (operationId === 'createresponse' || path.endsWith('/v1/responses')) return 'createResponse';
  if (operationId === 'creategeneration' || operationId === 'generate' || operationId.includes('generation') || path.endsWith('/v1/generations') || path.endsWith('/v1/generate')) return 'createGeneration';
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
  return ['createChatCompletion', 'createCompletion', 'createGeneration', 'createResponse', 'createEmbedding'].includes(operationKind(operation));
}

function modelForOperation(operation, model) {
  const current = String(model || '').trim();
  const kind = operationKind(operation);
  if (kind === 'createEmbedding') {
    return !current || current === 'gpt-oss:120b' ? 'all-minilm' : current;
  }

  if (kind === 'createChatCompletion' || kind === 'createCompletion' || kind === 'createGeneration' || kind === 'createResponse') {
    return !current || current === 'all-minilm' ? 'gpt-oss:120b' : current;
  }

  return current;
}

function defaultQueryForOperation(operation) {
  if (!operation) return {};
  if (operation.operationId === 'summarizeRequestHistory') return { bucketMinutes: 15 };
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
        accessKey: 'default'
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
        active: true
      };
    case 'updateCredential':
      return {
        userId: 'usr_replace_me',
        accessKey: 'default',
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
        max_output_tokens: 2000,
        stream: true,
        stream_options: {
          include_usage: true
        }
      };
    case 'createCompletion':
      return {
        model: selectedModel || 'gpt-oss:120b',
        prompt: 'Write a concise explanation of how an inverted index works in search engines.',
        temperature: 0.7,
        max_output_tokens: 2000,
        stream: true,
        stream_options: {
          include_usage: true
        }
      };
    case 'createGeneration':
      return {
        model: selectedModel || 'gpt-oss:120b',
        prompt: 'Write a concise explanation of how an inverted index works in search engines.',
        temperature: 0.7,
        max_output_tokens: 2000,
        stream: true
      };
    case 'createResponse':
      return {
        model: selectedModel || 'gpt-oss:120b',
        instructions: defaultOpenAiSystemPrompt,
        input: defaultOpenAiPrompt,
        temperature: 0.7,
        max_output_tokens: 2000,
        stream: true
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

function average(values) {
  const numbers = values.map(Number).filter((item) => Number.isFinite(item));
  if (numbers.length === 0) return 0;
  return numbers.reduce((sum, item) => sum + item, 0) / numbers.length;
}

function telemetryAgeMs(item) {
  const collected = value(item, 'collectedUtc');
  if (!collected) return Number.POSITIVE_INFINITY;
  const timestamp = new Date(collected).getTime();
  if (!Number.isFinite(timestamp)) return Number.POSITIVE_INFINITY;
  return Date.now() - timestamp;
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

function analyticsRangeConfig(rangeKey) {
  return analyticsRanges.find((item) => item.key === rangeKey) || analyticsRanges[1];
}

function buildAnalyticsWindow(rangeKey, anchorMs) {
  const config = analyticsRangeConfig(rangeKey);
  const toMs = Number.isFinite(Number(anchorMs)) ? Number(anchorMs) : Date.now();
  const fromMs = toMs - config.durationMs;
  return {
    bucketMinutes: config.bucketMinutes,
    fromMs,
    fromUtc: new Date(fromMs).toISOString(),
    key: config.key,
    toMs,
    toUtc: new Date(toMs).toISOString()
  };
}

function telemetryRollupValues(rollups, window) {
  const bucketMs = Math.max(1, Number(window.bucketMinutes || 1)) * 60 * 1000;
  const bucketCount = Math.max(1, Math.ceil((window.toMs - window.fromMs) / bucketMs));
  const values = Array.from({ length: bucketCount }, (_, index) => {
    const startMs = window.fromMs + index * bucketMs;
    return {
      label: telemetryBucketLabel(startMs, window.key),
      value: null
    };
  });

  (Array.isArray(rollups) ? rollups : []).forEach((bucket) => {
    const startUtc = value(bucket, 'startUtc');
    const startMs = Date.parse(String(startUtc || ''));
    if (!Number.isFinite(startMs)) return;
    const index = Math.floor((startMs - window.fromMs) / bucketMs);
    if (index < 0 || index >= values.length) return;
    const averageValue = Number(value(bucket, 'average'));
    values[index] = {
      label: value(bucket, 'label') || telemetryBucketLabel(startMs, window.key),
      value: Number.isFinite(averageValue) ? averageValue : null
    };
  });

  return values;
}

function telemetryBucketLabel(timestamp, rangeKey) {
  const date = new Date(timestamp);
  if (rangeKey === 'lastHour' || rangeKey === 'lastDay') {
    return date.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
  }

  return date.toLocaleDateString([], { month: 'short', day: 'numeric' });
}

function requestHistorySummaryPath(window) {
  const parameters = new URLSearchParams({
    bucketMinutes: String(window.bucketMinutes),
    fromUtc: window.fromUtc,
    toUtc: window.toUtc
  });
  return `${requestHistorySummaryEndpoint}?${parameters.toString()}`;
}

function requestHistoryListPath() {
  const parameters = new URLSearchParams({
    page: '1',
    pageSize: '250'
  });
  return `${requestHistoryEndpoint}?${parameters.toString()}`;
}

function analyticsOverviewPath(window) {
  const parameters = new URLSearchParams({
    fromUtc: window.fromUtc,
    toUtc: window.toUtc
  });
  return `${analyticsOverviewEndpoint}?${parameters.toString()}`;
}

function filterRowsByDateRange(rows, window) {
  return collectionRows(rows).filter((row) => {
    const timestamp = requestHistoryTimestampMs(row);
    return timestamp >= window.fromMs && timestamp <= window.toMs;
  });
}

function requestHistoryTimestampMs(row) {
  const timestamp = value(row, 'completedUtc') || value(row, 'createdUtc');
  const parsed = Date.parse(String(timestamp || ''));
  return Number.isFinite(parsed) ? parsed : 0;
}

function summarizeHistoryRows(rows) {
  return collectionRows(rows).reduce((summary, row) => {
    const statusCode = Number(value(row, 'statusCode') || 0);
    const success = statusCode >= 200 && statusCode < 400;
    const nextSuccess = summary.success + (success ? 1 : 0);
    const nextFailed = summary.failed + (success ? 0 : 1);
    const total = nextSuccess + nextFailed;
    return {
      failed: nextFailed,
      failureRate: total === 0 ? 0 : nextFailed / total,
      success: nextSuccess,
      total
    };
  }, { failed: 0, failureRate: 0, success: 0, total: 0 });
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
      apiType: value(model, 'apiType') || value(model, 'provider'),
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
  const preferred = ['id', 'name', 'apiType', 'provider', 'source', 'supportsCompletions', 'supportsEmbeddings', 'lastAdvertisedUtc', 'active'];
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

function LogoutOverlay() {
  const { t } = useTranslation();

  return (
    <div aria-labelledby="logout-overlay-title" aria-modal="true" className="logout-overlay" role="alertdialog">
      <div className="logout-dialog">
        <div aria-hidden="true" className="logout-spinner" />
        <div>
          <h2 id="logout-overlay-title">{t('signingOut')}</h2>
          <p>{t('logoutInProgress')}</p>
        </div>
      </div>
    </div>
  );
}

function AppRoutes() {
  const [theme, setTheme] = useState(() => window.localStorage.getItem('xeno.theme') || 'light');
  const [loggingOut, setLoggingOut] = useState(false);
  const [session, setSession] = useState(() => {
    const storedSession = readDashboardSession();
    const configuredServerUrl = storedSession?.serverUrl || window.__XENO_CONFIG__?.serverUrl || 'http://localhost:9000';
    api.setBaseUrl(configuredServerUrl);
    if (storedSession) api.setAuth(storedSession);
    else api.clearAuth();
    return storedSession;
  });
  useDashboardTooltipBackfill();

  useEffect(() => {
    document.documentElement.dataset.theme = theme;
    window.localStorage.setItem('xeno.theme', theme);
  }, [theme]);

  useEffect(() => {
    const configuredServerUrl = session?.serverUrl || window.__XENO_CONFIG__?.serverUrl || 'http://localhost:9000';
    api.setBaseUrl(configuredServerUrl);
    if (session) api.setAuth(session);
    else api.clearAuth();
  }, [session]);

  useEffect(() => {
    function handleAuthFailure() {
      writeDashboardSession(null);
      api.clearAuth();
      setSession(null);
    }

    window.addEventListener(authFailureEventName, handleAuthFailure);
    return () => window.removeEventListener(authFailureEventName, handleAuthFailure);
  }, []);

  function toggleTheme() {
    setTheme((current) => current === 'dark' ? 'light' : 'dark');
  }

  function login(nextSession) {
    writeDashboardSession(nextSession);
    setSession(nextSession);
  }

  async function logout() {
    if (loggingOut) return;

    setLoggingOut(true);
    const logoutUrl = safeLogoutUrl(session?.logoutUrl);
    try {
      await api.post('/api/v1/auth/logout');
    } catch {
      // Local session cleanup should still happen if the server is unavailable.
    }
    writeDashboardSession(null);
    api.clearAuth();
    setSession(null);
    if (logoutUrl) {
      window.location.assign(logoutUrl);
      return;
    }

    setLoggingOut(false);
  }

  return (
    <>
      <Routes>
        <Route path="/" element={session ? <Navigate replace to="/dashboard/home" /> : <LoginView onLogin={login} onToggleTheme={toggleTheme} theme={theme} />} />
        <Route path="/dashboard/*" element={session ? <Shell onLogout={logout} onToggleTheme={toggleTheme} session={session} theme={theme} /> : <Navigate replace to="/" />} />
      </Routes>
      {loggingOut ? <LogoutOverlay /> : null}
    </>
  );
}

export default function App() {
  return (
    <BrowserRouter>
      <AppRoutes />
    </BrowserRouter>
  );
}

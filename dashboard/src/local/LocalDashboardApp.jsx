import { useEffect, useMemo, useRef, useState } from 'react';
import {
  Activity,
  BarChart3,
  BrainCircuit,
  ChevronDown,
  ChevronRight,
  CircleAlert,
  Cpu,
  Download,
  FileJson,
  Gauge,
  HardDrive,
  History,
  MemoryStick,
  MessagesSquare,
  MessageSquareText,
  Moon,
  Pencil,
  Play,
  Plus,
  RefreshCw,
  Route as RouteIcon,
  Search,
  Server,
  Settings as SettingsIcon,
  Square,
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
import ColumnPicker, { useColumnVisibility } from '../components/ColumnPicker.jsx';
import { jsonBody, localApi } from './localApi.js';
import { ImportModelsDialog, ManagedForm, ModelPullsIndicator, ModelPullsPanel, ProviderModelsDialog, SaveActions, useModelPullsFinished } from './ProviderModels.jsx';
import ChatPage from '../chat/ChatPage.jsx';
import { ChatApiContext } from '../chat/ChatApiContext.js';
import * as localChatApi from './chat/chatApi.js';
import ChatHistoryPage from './chat/ChatHistoryPage.jsx';
import ChatFeedbackPage from './chat/ChatFeedbackPage.jsx';
import UsagePage from '../usage/UsagePage.jsx';
import ProviderHealthPanel from '../usage/ProviderHealthPanel.jsx';
import DrillBanner, { matchesDrill } from '../usage/DrillBanner.jsx';
import { UsageApiContext } from '../usage/UsageApiContext.js';
import { localUsageApi } from '../usage/localUsageApi.js';
import TimingBars, { servedRequestStages } from '../charts/TimingBars.jsx';
import { AutoRefreshSelect, useAutoRefresh } from '../charts/autoRefresh.jsx';
import RollupChart from '../charts/RollupChart.jsx';
import { hostMetrics, hostMetricUnit, providerMetrics, providerMetricUnit } from '../charts/telemetryMetrics.js';

const pages = [
  { id: 'home', descriptionKey: 'localHomeDescription', icon: Activity, labelKey: 'home', tooltipKey: 'tooltipLocalNavHome' },
  { id: 'providers', descriptionKey: 'localProvidersDescription', icon: Server, labelKey: 'localProviders', tooltipKey: 'tooltipLocalNavProviders' },
  { id: 'localEndpoint', descriptionKey: 'localEndpointDescription', icon: RouteIcon, labelKey: 'localEndpoint', tooltipKey: 'tooltipLocalNavLocalEndpoint' },
  { id: 'models', descriptionKey: 'localModelsDescription', icon: BrainCircuit, labelKey: 'advertisedModels', tooltipKey: 'tooltipLocalNavModels' },
  { id: 'usage', descriptionKey: 'usageDescription', icon: BarChart3, labelKey: 'usage', tooltipKey: 'tooltipLocalNavUsage' },
  { id: 'requests', descriptionKey: 'localRequestsDescription', icon: History, labelKey: 'requests', tooltipKey: 'tooltipLocalNavRequests' },
  { id: 'apiExplorer', descriptionKey: 'localApiExplorerDescription', icon: FileJson, labelKey: 'apiExplorer', tooltipKey: 'tooltipLocalNavApiExplorer' },
  { id: 'chat', descriptionKey: 'chatDescription', icon: MessagesSquare, labelKey: 'chat', tooltipKey: 'tooltipLocalNavChat' },
  { id: 'chatHistory', descriptionKey: 'chatHistoryDescription', icon: History, labelKey: 'chatHistory', tooltipKey: 'tooltipLocalNavChatHistory' },
  { id: 'chatFeedback', descriptionKey: 'chatFeedbackDescription', icon: MessageSquareText, labelKey: 'chatFeedback', tooltipKey: 'tooltipLocalNavChatFeedback' },
  { id: 'logs', descriptionKey: 'localLogsDescription', icon: Terminal, labelKey: 'localLogs', tooltipKey: 'tooltipLocalNavLogs' },
  { id: 'telemetry', descriptionKey: 'localTelemetryDescription', icon: Gauge, labelKey: 'host', tooltipKey: 'tooltipLocalNavTelemetry' },
  { id: 'providerTelemetry', descriptionKey: 'localProviderTelemetryDescription', icon: Server, labelKey: 'provider', tooltipKey: 'tooltipLocalNavProviderTelemetry' },
  { id: 'settings', descriptionKey: 'localSettingsDescription', icon: SettingsIcon, labelKey: 'settings', tooltipKey: 'tooltipLocalNavSettings' }
];

const localChatProvider = { ...localChatApi, supportsTools: true, surface: 'Web' };

const localNavGroups = [
  { key: 'observe', pageIds: ['home', 'providers', 'localEndpoint', 'models', 'usage', 'requests', 'apiExplorer'] },
  { key: 'chatGroup', pageIds: ['chat', 'chatHistory', 'chatFeedback'] },
  { key: 'telemetry', pageIds: ['telemetry', 'providerTelemetry'] },
  { key: 'administration', pageIds: ['logs', 'settings'] }
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

const tooltipBackfillSelector = [
  'button:not([title])',
  'a:not([title])',
  'input:not([title])',
  'select:not([title])',
  'textarea:not([title])',
  'label:not([title])',
  'summary:not([title])',
  'th:not([title])',
  '[role="button"]:not([title])'
].join(', ');

const localProviderApiTypeOptions = [
  { label: 'Ollama', value: 'Ollama' },
  { label: 'OpenAI', value: 'OpenAI' },
  { label: 'Gemini', value: 'Gemini' },
  { label: 'vLLM', value: 'vLLM' },
  { label: 'OpenAI Compatible', value: 'OpenAICompatible' }
];

export default function LocalDashboardApp() {
  const { t } = useTranslation();
  const [page, setPage] = useState('home');
  const [drill, setDrill] = useState(null);
  const [usageRefresh, setUsageRefresh] = useState(0);
  const [chatConversationId, setChatConversationId] = useState(null);
  const [theme, setTheme] = useState(document.documentElement.getAttribute('data-theme') || 'light');
  const [state, setState] = useState(null);
  const [statistics, setStatistics] = useState(null);
  const [summary, setSummary] = useState([]);
  const [providers, setProviders] = useState([]);
  const [models, setModels] = useState([]);
  const [requests, setRequests] = useState([]);
  const [logs, setLogs] = useState([]);
  const [database, setDatabase] = useState(null);
  const [telemetryLatest, setTelemetryLatest] = useState(null);
  const [hostTelemetryRollups, setHostTelemetryRollups] = useState([]);
  const [telemetrySamples, setTelemetrySamples] = useState([]);
  const [telemetryMetric, setTelemetryMetric] = useState('CpuUtilizationPercent');
  const [providerTelemetryLatest, setProviderTelemetryLatest] = useState(null);
  const [providerTelemetrySamples, setProviderTelemetrySamples] = useState([]);
  const [providerTelemetryRollups, setProviderTelemetryRollups] = useState([]);
  const [providerTelemetryMetric, setProviderTelemetryMetric] = useState('ActiveRequests');
  const [providerTelemetryProvider, setProviderTelemetryProvider] = useState('');
  const [providerTelemetryModel, setProviderTelemetryModel] = useState('');
  const [range, setRange] = useState('lastDay');
  const [providerFilter, setProviderFilter] = useState('');
  const [logSeverity, setLogSeverity] = useState('all');
  const [logTail, setLogTail] = useState(200);
  const [followLogs, setFollowLogs] = useState(false);
  const [modal, setModal] = useState(null);
  const [localRouting, setLocalRouting] = useState(null);
  const [localEndpointRequests, setLocalEndpointRequests] = useState([]);
  const [error, setError] = useState('');

  useEffect(() => {
    document.documentElement.setAttribute('data-theme', theme);
  }, [theme]);

  useEffect(() => {
    refresh();
  }, [page, range, logSeverity, logTail, telemetryMetric, providerTelemetryMetric, providerTelemetryProvider, providerTelemetryModel]);

  useEffect(() => {
    if (page !== 'logs' || !followLogs) return undefined;
    const interval = window.setInterval(() => {
      refresh();
    }, 1000);
    return () => window.clearInterval(interval);
  }, [page, followLogs, logSeverity, logTail]);

  useEffect(() => {
    const timeout = window.setTimeout(() => {
      document.querySelectorAll(tooltipBackfillSelector).forEach((element) => {
        const generated = element.getAttribute('data-tooltip-generated') === 'true';
        if (element.getAttribute('title') && !generated) return;
        element.setAttribute('title', localTooltipForElement(element, t));
        element.setAttribute('data-tooltip-generated', 'true');
      });
    }, 0);
    return () => window.clearTimeout(timeout);
  }, [page, t, providers, models, requests, telemetrySamples, providerTelemetrySamples, logs, modal]);

  useModelPullsFinished(() => {
    if (page === 'models' || page === 'providers' || page === 'localEndpoint') refresh();
  });

  async function refresh() {
    try {
      setError('');
      if (page === 'home') {
        localApi('/api/v1/local-inference/routing').then((value) => setLocalRouting(value)).catch(() => setLocalRouting(null));
        const [stateResponse, statisticsResponse, summaryResponse] = await Promise.all([
          localApi('/api/v1/state'),
          localApi('/api/v1/statistics'),
          localApi(`/api/v1/requests/summary?range=${encodeURIComponent(localApiRange(range))}&bucketMinutes=${telemetryBucketMinutes(range)}`)
        ]);
        setState(stateResponse);
        setStatistics(statisticsResponse);
        setSummary(summaryResponse.buckets || []);
      } else if (page === 'localEndpoint') {
        const [routingResponse, requestsResponse] = await Promise.all([
          localApi('/api/v1/local-inference/routing'),
          localApi('/api/v1/local-inference/requests?page=1&pageSize=500')
        ]);
        setLocalRouting(routingResponse);
        setLocalEndpointRequests(Array.isArray(requestsResponse.items) ? requestsResponse.items : []);
      } else if (page === 'providers') {
        const response = await localApi('/api/v1/providers');
        setProviders(response.providers || []);
      } else if (page === 'models') {
        const [response, providerResponse] = await Promise.all([
          localApi('/api/v1/models'),
          localApi('/api/v1/providers')
        ]);
        setModels(response.models || []);
        setProviders(providerResponse.providers || []);
      } else if (page === 'requests') {
        const [historyResponse, summaryResponse] = await Promise.all([
          localApi(drill?.page === 'requests' ? '/api/v1/requests?limit=5000' : '/api/v1/requests'),
          localApi(`/api/v1/requests/summary?range=${encodeURIComponent(localApiRange(range))}&bucketMinutes=${telemetryBucketMinutes(range)}`)
        ]);
        setRequests(historyResponse.entries || []);
        setSummary(summaryResponse.buckets || []);
      } else if (page === 'telemetry') {
        const [latestResponse, searchResponse, rollupResponses] = await Promise.all([
          localApi('/api/v1/telemetry/latest'),
          localApi(`/api/v1/telemetry/history/search?range=${encodeURIComponent(localApiRange(range))}`, {
            method: 'POST',
            body: jsonBody({ metric: telemetryMetric, limit: 250 })
          }),
          localApi(`/api/v1/telemetry/history/rollups?range=${encodeURIComponent(localApiRange(range))}`, {
            method: 'POST',
            body: jsonBody({ metric: telemetryMetric, bucketMinutes: telemetryBucketMinutes(range) })
          })
        ]);
        setTelemetryLatest(latestResponse);
        setTelemetrySamples(localRows(searchResponse.samples || searchResponse));
        setHostTelemetryRollups(localRows(rollupResponses));
      } else if (page === 'providerTelemetry') {
        const [latestResponse, searchResponse, rollupResponse] = await Promise.all([
          localApi(`/api/v1/telemetry/providers/latest${providerTelemetryProvider ? `?providerName=${encodeURIComponent(providerTelemetryProvider)}` : ''}`),
          localApi(`/api/v1/telemetry/providers/history/search?range=${encodeURIComponent(localApiRange(range))}`, {
            method: 'POST',
            body: jsonBody({ metric: providerTelemetryMetric, providerName: providerTelemetryProvider || null, modelName: providerTelemetryModel || null, limit: 250 })
          }),
          localApi(`/api/v1/telemetry/providers/history/rollups?range=${encodeURIComponent(localApiRange(range))}`, {
            method: 'POST',
            body: jsonBody({ metric: providerTelemetryMetric, providerName: providerTelemetryProvider || null, modelName: providerTelemetryModel || null, bucketMinutes: telemetryBucketMinutes(range) })
          })
        ]);
        setProviderTelemetryLatest(latestResponse);
        setProviderTelemetrySamples(localRows(searchResponse.samples || searchResponse));
        setProviderTelemetryRollups(localRows(rollupResponse));
      } else if (page === 'logs') {
        const response = await localApi(`/api/v1/logs?tail=${logTail}&severity=${logSeverity}&_=${Date.now()}`);
        setLogs(sortLogLinesDescending(response.lines || []));
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

  // A Usage bucket opens the list for its dataset, showing only that window until the user clears it or leaves.
  function drillDown(dataset, window) {
    const target = dataset === 'LocalEndpoint' ? 'localEndpoint' : dataset === 'Chat' ? 'chatHistory' : 'requests';
    setDrill({ ...window, page: target });
    setPage(target);
  }

  function openPage(id) {
    setDrill(null);
    setPage(id);
  }

  const pageDrill = drill?.page === page ? drill : null;

  function openChatConversation(id) {
    setChatConversationId(id || null);
    setPage('chat');
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
          {localNavGroups.map((group) => (
            <div className="sidebar-group" key={group.key}>
              <div className="sidebar-group-label">{t(group.key)}</div>
              {group.pageIds.map((id) => pages.find((item) => item.id === id)).filter(Boolean).map((item) => (
                <SidebarButton item={item} key={item.id} page={page} setPage={openPage} />
              ))}
            </div>
          ))}
        </nav>
        <div className="sidebar-footer">
          <span>{t('version')}</span>
          <strong>v0.3.0</strong>
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
            <ModelPullsIndicator onOpen={() => setModal({ title: t('modelPulls'), body: <ModelPullsPanel /> })} />
            <LanguageSelector />
            <a aria-label={t('github')} className="icon-button" href="https://github.com/jchristn/Xeno" rel="noreferrer" target="_blank" title={t('tooltipGitHub')}>
              <GitHubMark size={17} />
            </a>
            <a aria-label={t('discord')} className="icon-button" href="https://discord.gg/tRAN8HgvK5" rel="noreferrer" target="_blank" title={t('tooltipDiscord')}>
              <DiscordMark size={17} />
            </a>
            <button className="icon-button" onClick={() => setTheme(theme === 'dark' ? 'light' : 'dark')} title={t('tooltipToggleTheme')} type="button">
              {theme === 'dark' ? <Sun size={17} /> : <Moon size={17} />}
            </button>
            <button className="icon-button" onClick={() => { setUsageRefresh((value) => value + 1); refresh(); }} title={t('tooltipManualRefresh')} type="button">
              <RefreshCw size={17} />
            </button>
          </div>
        </header>
        <main className={`workspace${page === 'chat' ? ' workspace-chat' : ''}`}>
          {error ? <div className="settings-restart-banner error-state"><CircleAlert size={18} /><span>{error}</span></div> : null}
          {page === 'chat' ? <ChatApiContext.Provider value={localChatProvider}><ChatPage conversationId={chatConversationId} setConversationId={setChatConversationId} setModal={setModal} /></ChatApiContext.Provider> : null}
          <DrillBanner drill={pageDrill} onClear={() => setDrill(null)} />
          {page === 'usage' ? <UsageApiContext.Provider value={localUsageApi}><UsagePage onDrill={drillDown} refreshSignal={usageRefresh} /></UsageApiContext.Provider> : null}
          {page === 'chatHistory' ? <ChatHistoryPage drill={pageDrill} openConversation={openChatConversation} setModal={setModal} /> : null}
          {page === 'chatFeedback' ? <ChatFeedbackPage openConversation={openChatConversation} setModal={setModal} /> : null}
          {page === 'home' ? <Home range={range} refresh={refresh} routing={localRouting} setRange={setRange} state={state} statistics={statistics} summary={summary} /> : null}
          {page === 'providers' ? <Providers providers={providers} refresh={refresh} setModal={setModal} /> : null}
          {page === 'localEndpoint' ? <LocalEndpoint modalOpen={!!modal} refresh={refresh} requests={pageDrill ? localEndpointRequests.filter((item) => matchesDrill(pageDrill, item.receivedUtc, item.model)) : localEndpointRequests} routing={localRouting} setModal={setModal} /> : null}
          {page === 'models' ? <Models models={models} providerFilter={providerFilter} providers={providers} refresh={refresh} setModal={setModal} setProviderFilter={setProviderFilter} /> : null}
          {page === 'requests' ? <Requests range={range} refresh={refresh} requests={pageDrill ? requests.filter((item) => matchesDrill(pageDrill, item.receivedUtc, item.modelName)) : requests} setModal={setModal} setRange={setRange} summary={summary} /> : null}
          {page === 'apiExplorer' ? <LocalApiExplorer state={state} /> : null}
          {page === 'telemetry' ? <Telemetry hostRollups={hostTelemetryRollups} latest={telemetryLatest} metric={telemetryMetric} range={range} refresh={refresh} samples={telemetrySamples} setMetric={setTelemetryMetric} setRange={setRange} setModal={setModal} /> : null}
          {page === 'logs' ? <Logs followLogs={followLogs} logSeverity={logSeverity} logTail={logTail} logs={logs} setFollowLogs={setFollowLogs} setLogSeverity={setLogSeverity} setLogTail={setLogTail} /> : null}
          {page === 'providerTelemetry' ? <ProviderTelemetry advertisedModels={models} latest={providerTelemetryLatest} metric={providerTelemetryMetric} modelFilter={providerTelemetryModel} providerEndpoints={providers} providerFilter={providerTelemetryProvider} range={range} refresh={refresh} rollups={providerTelemetryRollups} samples={providerTelemetrySamples} setMetric={setProviderTelemetryMetric} setModal={setModal} setModelFilter={setProviderTelemetryModel} setProviderFilter={setProviderTelemetryProvider} setRange={setRange} /> : null}
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

function DiscordMark({ size = 17 }) {
  return (
    <svg
      aria-hidden="true"
      fill="currentColor"
      height={size}
      viewBox="0 0 24 24"
      width={size}
    >
      <path d="M20.317 4.3698a19.7913 19.7913 0 0 0-4.8851-1.5152.0741.0741 0 0 0-.0785.0371c-.211.3753-.4447.8648-.6083 1.2495-1.8447-.2762-3.68-.2762-5.4868 0-.1636-.3933-.4058-.8742-.6177-1.2495a.077.077 0 0 0-.0785-.037 19.7363 19.7363 0 0 0-4.8852 1.515.0699.0699 0 0 0-.0321.0277C.5334 9.0458-.319 13.5799.0992 18.0578a.0824.0824 0 0 0 .0312.0561c2.0528 1.5076 4.0413 2.4228 5.9929 3.0294a.0777.0777 0 0 0 .0842-.0276c.4616-.6304.8731-1.2952 1.226-1.9942a.076.076 0 0 0-.0416-.1057c-.6528-.2476-1.2743-.5495-1.8722-.8923a.077.077 0 0 1-.0076-.1277c.1258-.0943.2517-.1923.3718-.2914a.0743.0743 0 0 1 .0776-.0105c3.9278 1.7933 8.18 1.7933 12.0614 0a.0739.0739 0 0 1 .0785.0095c.1202.099.246.1981.3728.2924a.077.077 0 0 1-.0066.1276 12.2986 12.2986 0 0 1-1.873.8914.0766.0766 0 0 0-.0407.1067c.3604.698.7719 1.3628 1.225 1.9932a.076.076 0 0 0 .0842.0286c1.961-.6067 3.9495-1.5219 6.0023-3.0294a.077.077 0 0 0 .0313-.0552c.5004-5.177-.8382-9.6739-3.5485-13.6604a.061.061 0 0 0-.0312-.0286zM8.02 15.3312c-1.1825 0-2.1569-1.0857-2.1569-2.419 0-1.3332.9555-2.4189 2.157-2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332-.9555 2.4189-2.1569 2.4189zm7.9748 0c-1.1825 0-2.1569-1.0857-2.1569-2.419 0-1.3332.9554-2.4189 2.1569-2.4189 1.2108 0 2.1757 1.0952 2.1568 2.419 0 1.3332-.946 2.4189-2.1568 2.4189Z" />
    </svg>
  );
}

function Home({ range, refresh, routing, setRange, state, statistics, summary }) {
  const { t } = useTranslation();
  return (
    <>
      {state?.settings?.localDashboard?.host === '0.0.0.0' ? <div className="settings-restart-banner"><CircleAlert size={18} /><span>{t('localBindWarning')}</span></div> : null}
      {state?.connection?.localInferencePortMigratedFrom ? (
        <div className="settings-restart-banner" role="status">
          <CircleAlert size={18} />
          <span>{t('localInferencePortMigrated', { from: state.connection.localInferencePortMigratedFrom, to: state.connection.localInferencePortMigratedTo || state?.settings?.localInferenceServer?.port || 11435 })}</span>
          <button className="action-button" onClick={async () => { await localApi('/api/v1/local-inference/port-migration/acknowledge', { method: 'POST', body: '{}' }); refresh(); }} title={t('tooltipDismissNotice')} type="button">{t('dismiss')}</button>
        </div>
      ) : null}
      <KpiGrid>
        <Metric icon={<Zap size={18} />} label={t('connected')} subtext={state?.connection?.webSocketUrl} tone={state?.connection?.isConnected ? 'success' : 'danger'} value={state?.connection?.isConnected ? t('yes') : t('no')} />
        <Metric icon={<Server size={18} />} label={t('registered')} tone={state?.connection?.isRegistered ? 'success' : 'warning'} value={state?.connection?.isRegistered ? t('yes') : t('no')} />
        <Metric icon={<History size={18} />} label={t('requests')} value={formatCount(statistics?.requests || 0, t)} />
        <Metric icon={<Gauge size={18} />} label={t('failures')} tone={statistics?.failures ? 'danger' : 'neutral'} value={formatCount(statistics?.failures || 0, t)} />
      </KpiGrid>
      {routing ? (
        <KpiGrid>
          <Metric icon={<RouteIcon size={18} />} label={t('localEndpoint')} subtext={routing.url} value={t(`routingMode${routing.routingMode}`, routing.routingMode)} />
          <Metric icon={<Zap size={18} />} label={t('answeredHere')} subtext={t('sinceStart')} tone="success" value={formatCount(routing.localRequests, t)} />
          <Metric icon={<Activity size={18} />} label={t('sentToNetwork')} subtext={t('fallbacksCount', { count: routing.fallbacks || 0 })} value={formatCount(routing.networkRequests, t)} />
          <Metric icon={<Server size={18} />} label={t('providersHealthy')} subtext={routing.runtimeAvailable ? '' : t('healthUnknownNotRunning')} tone={routing.runtimeAvailable && routing.healthyBackends < routing.routableBackends ? 'warning' : 'neutral'} value={routing.runtimeAvailable ? `${routing.healthyBackends} / ${routing.routableBackends}` : '-'} />
        </KpiGrid>
      ) : null}
      <Panel className="chart-panel-card" title={t('requestsOverTime')} actions={<button className="icon-button" onClick={refresh} title={t('tooltipManualRefresh')} type="button"><RefreshCw size={16} /></button>}>
        <ActivityChart onRangeChange={setRange} onRefresh={refresh} range={range} summary={{ buckets: summary }} />
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
        { icon: <HardDrive size={14} />, key: 'models', label: t('manageModels'), onSelect: () => setModal(providerModelsModal(t, row, refresh)), tooltip: t('tooltipManageProviderModels') },
        { divider: true, key: 'divider-delete' },
        { destructive: true, icon: <Trash2 size={14} />, key: 'delete', label: t('delete'), onSelect: () => remove(row), tooltip: t('tooltipDeleteLocalProvider') }
      ]}
      columns={['name', 'apiType', 'endpoint', 'timeoutMs', 'credential', 'allowModelManagement', 'shareWithNetwork', 'routeLocalRequests', 'maxConcurrentRequests']}
      defaultColumns={['name', 'apiType', 'endpoint', 'shareWithNetwork', 'routeLocalRequests', 'maxConcurrentRequests']}
      tableId="local.providers"
      headerActions={<button className="primary-button" onClick={() => setModal(providerModal(t, null, refresh, setModal))} title={t('tooltipCreateLocalProvider')} type="button"><Plus size={16} />{t('createProvider')}</button>}
      onRefresh={refresh}
      onRowClick={(row) => setModal(providerModal(t, row, refresh, setModal))}
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
      columns={['name', 'provider', 'apiType', 'supportsCompletions', 'supportsEmbeddings', 'modelManagementAllowed', 'maxConcurrentRequests', 'effectiveMaxConcurrentRequests']}
      defaultColumns={['name', 'provider', 'apiType', 'supportsCompletions', 'supportsEmbeddings', 'effectiveMaxConcurrentRequests']}
      tableId="local.models"
      headerActions={(
        <>
          <label className="form-field inline compact-field" title={t('tooltipLocalModelProviderFilter')}>
            <span>{t('provider')}</span>
            <select value={providerFilter} onChange={(event) => setProviderFilter(event.target.value)} title={t('tooltipLocalModelProviderFilter')}>
              <option value="">{t('all')}</option>
              {providerNames.map((name) => <option key={name} value={name}>{name}</option>)}
            </select>
          </label>
          <button className="action-button" disabled={providers.length === 0} onClick={() => setModal(importModelsModal(t, providers, refresh))} title={t('tooltipImportModels')} type="button"><Download size={16} />{t('import')}</button>
          <button className="primary-button" onClick={() => setModal(modelModal(t, null, providers, refresh, setModal))} title={t('tooltipCreateLocalModel')} type="button"><Plus size={16} />{t('createModel')}</button>
        </>
      )}
      onRefresh={refresh}
      onRowClick={(row) => setModal(modelModal(t, row, providers, refresh, setModal))}
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
        <ActivityChart onRangeChange={setRange} onRefresh={refresh} range={range} summary={{ buckets: summary }} />
      </Panel>
      <DataTable
      actions={(row) => [
          { destructive: true, icon: <Trash2 size={14} />, key: 'delete', label: t('delete'), onSelect: () => remove(row), tooltip: t('tooltipDeleteLocalRequest') }
        ]}
        columns={['id', 'receivedUtc', 'method', 'path', 'modelName', 'requestorPrincipalId', 'providerName', 'statusCode', 'outcome']}
        defaultColumns={['receivedUtc', 'path', 'modelName', 'providerName', 'statusCode', 'outcome']}
        tableId="local.requests"
        headerActions={<button className="action-button danger icon-only" onClick={clear} title={t('tooltipClearLocalHistory')} type="button"><Trash2 size={16} /><span className="sr-only">{t('clear')}</span></button>}
        onRefresh={refresh}
        onRowClick={details}
        rows={requests}
        title={t('requestHistory')}
      />
    </>
  );
}

function Telemetry({ hostRollups, latest, metric, range, refresh, samples, setMetric, setRange, setModal }) {
  const { t } = useTranslation();
  const [autoRefreshSeconds, setAutoRefreshSeconds] = useState(15);
  useAutoRefresh(autoRefreshSeconds, refresh);
  const sample = latest?.detail?.sample || {};
  const snapshot = latest?.snapshot || {};
  const gpus = latest?.detail?.gpus || snapshot?.gpu?.devices || [];

  async function details(row) {
    const entry = await localApi(`/api/v1/telemetry/history/${encodeURIComponent(row.id)}`);
    setModal({ title: t('telemetrySampleDetails'), json: entry, size: 'request-details', body: <TelemetryDetails detail={entry} /> });
  }

  async function remove(row) {
    setModal(confirmModal(
      t,
      t('deleteTelemetryTitle'),
      t('deleteTelemetryMessage', { id: row.id }),
      async () => {
        await localApi(`/api/v1/telemetry/history/${encodeURIComponent(row.id)}`, { method: 'DELETE' });
        refresh();
        setModal(null);
      }
    ));
  }

  async function collect() {
    const response = await localApi('/api/v1/telemetry/collect', { method: 'POST', body: '{}' });
    setModal(messageModal(t, t('telemetryCollected'), t('telemetryCollectedMessage'), response));
    refresh();
  }

  const metricValues = samples.map((item) => Number(item[telemetryMetricField(metric)] || 0)).filter((value) => Number.isFinite(value));
  const average = metricValues.length ? metricValues.reduce((sum, value) => sum + value, 0) / metricValues.length : null;
  const maximum = metricValues.length ? Math.max(...metricValues) : null;

  return (
    <>
      <div className="chart-controls page-level-controls split telemetry-selector-bar" aria-label={t('chartRange')}>
        <TimeRangeButtons range={range} setRange={setRange} />
        <div className="chart-action-row telemetry-control-actions">
          <TelemetryMetricSelect metric={metric} setMetric={setMetric} />
          <AutoRefreshSelect onChange={setAutoRefreshSeconds} onRefresh={refresh} seconds={autoRefreshSeconds} />
          <button className="icon-button" onClick={collect} title={t('tooltipCollectTelemetry')} type="button"><Zap size={16} /></button>
        </div>
      </div>
      <KpiGrid>
        <Metric icon={<Cpu size={18} />} label={t('cpuUtilization')} value={formatPercent(sample.cpuUtilizationPercent)} />
        <Metric icon={<MemoryStick size={18} />} label={t('memoryUtilization')} value={formatPercent(sample.memoryUtilizationPercent)} />
        <Metric icon={<HardDrive size={18} />} label={t('diskUtilization')} value={formatPercent(sample.diskUtilizationPercent)} />
        <Metric icon={<Gauge size={18} />} label={t('gpuUtilization')} value={formatPercent(sample.gpuAverageUtilizationPercent)} />
      </KpiGrid>
      <KpiGrid>
        <Metric icon={<History size={18} />} label={t('samples')} value={samples.length} />
        <Metric icon={<Gauge size={18} />} label={t('average')} value={formatMetric(average)} />
        <Metric icon={<Gauge size={18} />} label={t('maximum')} value={formatMetric(maximum)} />
        <Metric icon={<Activity size={18} />} label={t('metric')} value={t(metric)} />
      </KpiGrid>
      <RollupChart id="telemetry.host" metric={metric} range={range} rollups={hostRollups} unit={hostMetricUnit(metric)} />
      <DataTable
        actions={(row) => [
          { destructive: true, icon: <Trash2 size={14} />, key: 'delete', label: t('delete'), onSelect: () => remove(row), tooltip: t('tooltipDeleteTelemetrySample') }
        ]}
        columns={['id', 'collectedUtc', 'cpuUtilizationPercent', 'memoryUtilizationPercent', 'diskUtilizationPercent', 'gpuAverageUtilizationPercent', 'gpuAverageTemperatureCelsius']}
        defaultColumns={['collectedUtc', 'cpuUtilizationPercent', 'memoryUtilizationPercent', 'diskUtilizationPercent', 'gpuAverageUtilizationPercent', 'gpuAverageTemperatureCelsius']}
        tableId="local.telemetry.host"
        onRefresh={refresh}
        onRowClick={details}
        rows={samples}
        title={t('telemetrySamples')}
      />
      <Panel title={t('telemetryStatus')} actions={<CopyButton label={t('copyJson')} value={JSON.stringify(latest || {}, null, 2)} />}>
        <RecordTable fields={['enabled', 'running', 'collectionIntervalMs', 'retentionDays', 'samples', 'newestSampleUtc', 'lastCollectionUtc', 'lastPruneUtc', 'lastMessage']} record={latest?.status || {}} />
      </Panel>
      <Panel title={t('systemTelemetry')}>
        <RecordTable fields={['id', 'machineName', 'collectedUtc', 'osDescription', 'architecture', 'processorCount', 'collectionElapsedMs']} record={{ ...sample, id: sample.id }} />
      </Panel>
      <Panel title={t('resourceTelemetry')}>
        <RecordTable fields={['cpuUtilizationPercent', 'memoryTotalBytes', 'memoryUsedBytes', 'memoryAvailableBytes', 'memoryUtilizationPercent', 'diskTotalBytes', 'diskUsedBytes', 'diskAvailableBytes', 'diskUtilizationPercent']} record={sample} />
      </Panel>
      <DataTable
        columns={['uuid', 'name', 'utilizationPercent', 'memoryUtilizationPercent', 'temperatureCelsius', 'powerWatts']}
        onRefresh={refresh}
        onRowClick={(row) => setModal({ title: t('gpuDetails'), json: row, body: <RecordTable fields={Object.keys(row)} record={row} /> })}
        rows={gpus}
        title={t('gpuDevices')}
      />
    </>
  );
}

function TelemetryMetricSelect({ metric, setMetric }) {
  const { t } = useTranslation();
  return (
    <label className="form-field inline compact-field" title={t('tooltipTelemetryMetric')}>
      <span>{t('metric')}</span>
      <select value={metric} onChange={(event) => setMetric(event.target.value)} title={t('tooltipTelemetryMetric')}>
        {hostMetrics.map((item) => <option key={item} value={item}>{t(item)}</option>)}
      </select>
    </label>
  );
}

export function TimeRangeButtons({ range, setRange }) {
  const { t } = useTranslation();
  return (
    <div className="time-range-button-row">
      {['lastHour', 'lastDay', 'lastWeek', 'lastMonth'].map((item) => (
        <button className={item === range ? 'active' : ''} key={item} onClick={() => setRange(item)} title={t('tooltipChartRangeOption', { range: t(item) })} type="button">{t(item)}</button>
      ))}
    </div>
  );
}

function ProviderTelemetry({ advertisedModels, latest, metric, modelFilter, providerEndpoints, providerFilter, range, refresh, rollups, samples, setMetric, setModal, setModelFilter, setProviderFilter, setRange }) {
  const { t } = useTranslation();
  const [autoRefreshSeconds, setAutoRefreshSeconds] = useState(15);
  useAutoRefresh(autoRefreshSeconds, refresh);
  const configuredModels = Array.isArray(advertisedModels) ? advertisedModels : [];
  const configuredProviders = Array.isArray(providerEndpoints) ? providerEndpoints : [];
  const providers = latest?.snapshot?.providers || [];
  const latestSample = latest?.detail?.sample || {};
  const modelRows = latest?.detail?.models || providers.flatMap((provider) => provider.models || []);
  const activeProviders = providers.filter((provider) => provider.status === 'Available' || provider.status === 'Partial').length;
  const loadedModels = providers.reduce((sum, provider) => sum + Number(provider.loadedModelCount || 0), 0);
  const activeRequests = providers.reduce((sum, provider) => sum + Number(provider.activeRequests || 0), 0);
  const queuedRequests = providers.reduce((sum, provider) => sum + Number(provider.queuedRequests || 0), 0);
  const providerOptions = uniqueSortedValues([
    ...configuredProviders.map((provider) => value(provider, 'name') || value(provider, 'providerName')),
    ...providers.map((provider) => value(provider, 'name') || value(provider, 'providerName')),
    ...samples.map((sample) => value(sample, 'providerName'))
  ]);
  const modelOptions = uniqueSortedValues([
    ...configuredModels.map((model) => value(model, 'name') || value(model, 'modelName')),
    ...modelRows.map((model) => value(model, 'modelName') || value(model, 'name')),
    ...samples.map((sample) => value(sample, 'modelName'))
  ]);

  async function details(row) {
    const entry = await localApi(`/api/v1/telemetry/providers/history/${encodeURIComponent(row.id)}`);
    setModal({ title: t('providerTelemetrySampleDetails'), json: entry, size: 'request-details', body: <ProviderTelemetryDetails detail={entry} /> });
  }

  async function remove(row) {
    setModal(confirmModal(
      t,
      t('deleteProviderTelemetryTitle'),
      t('deleteProviderTelemetryMessage', { id: row.id }),
      async () => {
        await localApi(`/api/v1/telemetry/providers/history/${encodeURIComponent(row.id)}`, { method: 'DELETE' });
        refresh();
        setModal(null);
      }
    ));
  }

  async function collect() {
    const response = await localApi('/api/v1/telemetry/providers/collect', { method: 'POST', body: '{}' });
    setModal(messageModal(t, t('providerTelemetryCollected'), t('providerTelemetryCollectedMessage'), response));
    refresh();
  }

  const metricValues = samples.map((item) => Number(item[providerTelemetryMetricField(metric)] || 0)).filter((value) => Number.isFinite(value));
  const average = metricValues.length ? metricValues.reduce((sum, value) => sum + value, 0) / metricValues.length : null;
  const maximum = metricValues.length ? Math.max(...metricValues) : null;

  return (
    <>
      <div className="chart-controls page-level-controls split telemetry-selector-bar" aria-label={t('chartRange')}>
        <TimeRangeButtons range={range} setRange={setRange} />
        <div className="chart-action-row telemetry-control-actions">
          <ProviderTelemetryControls metric={metric} modelFilter={modelFilter} modelOptions={modelOptions} providerFilter={providerFilter} providerOptions={providerOptions} setMetric={setMetric} setModelFilter={setModelFilter} setProviderFilter={setProviderFilter} />
          <AutoRefreshSelect onChange={setAutoRefreshSeconds} onRefresh={refresh} seconds={autoRefreshSeconds} />
          <button className="icon-button" onClick={collect} title={t('tooltipCollectProviderTelemetry')} type="button"><Zap size={16} /></button>
        </div>
      </div>
      <KpiGrid>
        <Metric icon={<Server size={18} />} label={t('healthyProviders')} value={activeProviders} />
        <Metric icon={<BrainCircuit size={18} />} label={t('loadedModels')} value={loadedModels} />
        <Metric icon={<Activity size={18} />} label={t('activeRequests')} value={formatMetric(activeRequests)} />
        <Metric icon={<History size={18} />} label={t('queuedRequests')} value={formatMetric(queuedRequests)} />
      </KpiGrid>
      <KpiGrid>
        <Metric icon={<History size={18} />} label={t('samples')} value={samples.length} />
        <Metric icon={<Gauge size={18} />} label={t('average')} value={formatMetric(average)} />
        <Metric icon={<Gauge size={18} />} label={t('maximum')} value={formatMetric(maximum)} />
        <Metric icon={<Activity size={18} />} label={t('metric')} value={t(metric)} />
      </KpiGrid>
      <UsageApiContext.Provider value={localUsageApi}><ProviderHealthPanel providerName={providerFilter || undefined} range={range} /></UsageApiContext.Provider>
      <Panel
        className="chart-panel-card telemetry-chart-card"
        title={t('providerTelemetryOverTime')}
      >
        <RollupChart id="telemetry.provider" metric={metric} range={range} rollups={rollups} unit={providerMetricUnit(metric)} />
      </Panel>
      <DataTable
        actions={(row) => [
          { destructive: true, icon: <Trash2 size={14} />, key: 'delete', label: t('delete'), onSelect: () => remove(row), tooltip: t('tooltipDeleteProviderTelemetrySample') }
        ]}
        columns={['id', 'collectedUtc', 'providerName', 'providerKind', 'status', 'healthStatus', 'activeRequests', 'queuedRequests', 'loadedModelCount', 'latencyP95Ms', 'timeToFirstTokenP95Ms']}
        onRefresh={refresh}
        onRowClick={details}
        rows={samples}
        title={t('providerTelemetrySamples')}
      />
      <Panel title={t('providerTelemetryStatus')} actions={<CopyButton label={t('copyJson')} value={JSON.stringify(latest || {}, null, 2)} />}>
        <RecordTable fields={['enabled', 'running', 'collectionIntervalMs', 'retentionDays', 'samples', 'newestSampleUtc', 'lastCollectionUtc', 'lastPruneUtc', 'lastMessage']} record={latest?.status || {}} />
      </Panel>
      <Panel title={t('latestProviderSample')}>
        <RecordTable fields={['id', 'providerName', 'apiType', 'providerKind', 'status', 'healthStatus', 'version', 'availableModelCount', 'loadedModelCount', 'activeRequests', 'queuedRequests', 'latencyP95Ms', 'timeToFirstTokenP95Ms']} record={latestSample} />
      </Panel>
      <DataTable
        columns={['providerName', 'apiType', 'providerKind', 'status', 'healthStatus', 'availableModelCount', 'loadedModelCount', 'activeRequests', 'queuedRequests']}
        onRefresh={refresh}
        onRowClick={(row) => setModal({ title: t('providerTelemetryDetails'), json: row, size: 'request-details', body: <RecordTable fields={Object.keys(row)} record={row} /> })}
        rows={providers.map((provider) => ({ ...provider, ...provider.endpoint }))}
        title={t('providerRuntimeStatus')}
      />
      <DataTable
        columns={['modelName', 'providerName', 'family', 'quantization', 'parameterSize', 'loaded', 'sizeBytes', 'memoryBytes']}
        onRefresh={refresh}
        rows={modelRows.map((model) => ({ ...model, modelName: model.modelName || model.name, providerName: model.providerName || latestSample.providerName }))}
        title={t('providerModelTelemetry')}
      />
    </>
  );
}

function ProviderTelemetryControls({ metric, modelFilter, modelOptions, providerFilter, providerOptions, setMetric, setModelFilter, setProviderFilter }) {
  const { t } = useTranslation();
  return (
    <div className="chart-action-row provider-telemetry-controls">
      <ProviderTelemetryMetricSelect metric={metric} setMetric={setMetric} />
      <label className="form-field inline compact-field" title={t('tooltipProviderTelemetryProviderFilter')}>
        <span>{t('provider')}</span>
        <select onChange={(event) => setProviderFilter(event.target.value)} title={t('tooltipProviderTelemetryProviderFilter')} value={providerFilter}>
          <option value="">{t('all')}</option>
          {!providerOptions.includes(providerFilter) && providerFilter ? <option value={providerFilter}>{providerFilter}</option> : null}
          {providerOptions.map((name) => <option key={name} value={name}>{name}</option>)}
        </select>
      </label>
      <label className="form-field inline compact-field" title={t('tooltipProviderTelemetryModelFilter')}>
        <span>{t('model')}</span>
        <select onChange={(event) => setModelFilter(event.target.value)} title={t('tooltipProviderTelemetryModelFilter')} value={modelFilter}>
          <option value="">{t('all')}</option>
          {!modelOptions.includes(modelFilter) && modelFilter ? <option value={modelFilter}>{modelFilter}</option> : null}
          {modelOptions.map((name) => <option key={name} value={name}>{name}</option>)}
        </select>
      </label>
    </div>
  );
}

function ProviderTelemetryMetricSelect({ metric, setMetric }) {
  const { t } = useTranslation();
  return (
    <label className="form-field inline compact-field" title={t('tooltipProviderTelemetryMetric')}>
      <span>{t('metric')}</span>
      <select value={metric} onChange={(event) => setMetric(event.target.value)} title={t('tooltipProviderTelemetryMetric')}>
        {providerMetrics.map((item) => <option key={item} value={item}>{t(item)}</option>)}
      </select>
    </label>
  );
}

function ProviderTelemetryDetails({ detail }) {
  const { t } = useTranslation();
  const sample = detail?.sample || {};
  return (
    <div className="details-stack">
      <RecordTable fields={Object.keys(sample)} record={sample} />
      <DataTable columns={['modelName', 'providerName', 'loaded', 'family', 'quantization', 'parameterSize', 'sizeBytes', 'memoryBytes']} compact rows={detail?.models || []} />
      <DataTable columns={['metricName', 'metricKind', 'metricValue', 'metricUnit', 'source', 'modelName']} compact rows={detail?.metrics || []} />
      <details open>
        <summary>{t('snapshotJson')} <CopyButton label={t('copyValue')} value={JSON.stringify(detail?.snapshot || {}, null, 2)} /></summary>
        <pre className="json-preview"><code>{JSON.stringify(detail?.snapshot || {}, null, 2)}</code></pre>
      </details>
    </div>
  );
}

function Logs({ followLogs, logSeverity, logTail, logs, setFollowLogs, setLogSeverity, setLogTail }) {
  const { t } = useTranslation();
  return (
    <Panel
      className="client-logs-card"
      title={t('clientLogs')}
      actions={(
        <div className="client-logs-actions">
          <label className="form-field inline client-log-filter severity-filter" title={t('tooltipLocalLogSeverity')}>
            <span>{t('severity')}</span>
            <select value={logSeverity} onChange={(event) => setLogSeverity(event.target.value)} title={t('tooltipLocalLogSeverity')}>
              {['all', 'debug', 'info', 'warn', 'error'].map((item) => <option key={item} value={item}>{title(item, t)}</option>)}
            </select>
          </label>
          <label className="form-field inline client-log-filter tail-filter" title={t('tooltipLocalLogTail')}>
            <span>{t('tail')}</span>
            <select value={logTail} onChange={(event) => setLogTail(Number(event.target.value))} title={t('tooltipLocalLogTail')}>
              {[50, 100, 200, 500].map((count) => <option key={count} value={count}>{count}</option>)}
            </select>
          </label>
          <label className="toggle-field client-log-follow" title={t('tooltipFollowLocalLogs')}>
            <input checked={followLogs} onChange={(event) => setFollowLogs(event.target.checked)} title={t('tooltipFollowLocalLogs')} type="checkbox" />
            {t('followLive')}
          </label>
        </div>
      )}
    >
      <pre className="json-preview"><code>{logs.join('\n')}</code></pre>
    </Panel>
  );
}

function LocalEndpoint({ modalOpen, refresh, requests, routing, setModal }) {
  const { t } = useTranslation();
  const [autoRefresh, setAutoRefresh] = useState(true);

  useEffect(() => {
    if (!autoRefresh || modalOpen) return undefined;
    const interval = window.setInterval(() => refresh(), 5000);
    return () => window.clearInterval(interval);
  }, [autoRefresh, modalOpen]);

  if (!routing) return <Panel title={t('localEndpoint')}><EmptyState message={t('loading')} /></Panel>;
  const backends = Array.isArray(routing.backends) ? routing.backends : [];
  const rules = Array.isArray(routing.rules) ? routing.rules : [];
  const records = Array.isArray(requests) ? requests : [];
  const status = !routing.enabled ? t('localEndpointDisabled') : routing.runtimeAvailable ? t('listening') : t('localEndpointNotRunning');
  const statusShort = !routing.enabled ? t('localEndpointDisabledShort') : routing.runtimeAvailable ? t('listening') : t('localEndpointNotRunningShort');
  const statusTone = !routing.enabled ? 'neutral' : routing.runtimeAvailable ? 'success' : 'warning';
  const baseUrl = String(routing.url || '').replace(/\/+$/, '');
  const providerNames = backends.filter((item) => item.routable).map((item) => item.provider);

  async function check() {
    const response = await localApi('/api/v1/local-inference/backends/check', { method: 'POST', body: '{}' });
    const list = Array.isArray(response.backends) ? response.backends : [];
    const healthy = list.filter((item) => item.healthy === true).length;
    setModal(messageModal(t, t('checkProviders'), t('providersChecked', { healthy, total: list.length }), response));
    refresh();
  }

  function removeRule(rule) {
    setModal(confirmModal(t, t('deleteRouteRule'), t('deleteRouteRuleMessage', { model: rule.model }), async () => {
      await localApi(`/api/v1/local-inference/routes/${encodeURIComponent(rule.model)}`, { method: 'DELETE' });
      setModal(null);
      refresh();
    }));
  }

  return (
    <>
      {!routing.runtimeAvailable && routing.enabled ? <div className="settings-restart-banner" role="status"><CircleAlert size={18} /><span>{t('localEndpointNotRunningBanner')}</span></div> : null}
      <section className="workspace-card endpoint-hero" aria-label={t('localEndpoint')}>
        <div className="endpoint-hero-main">
          <div className="endpoint-hero-heading">
            <span className="endpoint-hero-icon"><RouteIcon size={20} /></span>
            <h2>{t('localEndpoint')}</h2>
            <span className={`status-chip ${statusTone}`} title={status}>{statusShort}</span>
          </div>
          <p className="endpoint-hero-lede">{t('localEndpointHeroLede')}</p>
          <dl className="endpoint-urls">
            <div>
              <dt>{t('openAiBaseUrl')}</dt>
              <dd><CopyableValue label={t('copyValue')} value={`${baseUrl}/v1`} /></dd>
            </div>
            <div>
              <dt>{t('ollamaBaseUrl')}</dt>
              <dd><CopyableValue label={t('copyValue')} value={baseUrl} /></dd>
            </div>
          </dl>
        </div>
        <dl className="endpoint-hero-facts">
          <div>
            <dt>{t('localRoutingMode')}</dt>
            <dd>{t(`routingMode${routing.routingMode}`, routing.routingMode)}</dd>
            <small>{t('defaultLimitEach', { limit: routing.defaultMaxConcurrentRequests })}</small>
          </div>
          <div className={routing.runtimeAvailable && routing.healthyBackends < routing.routableBackends ? 'warning' : ''}>
            <dt>{t('providersHealthy')}</dt>
            <dd>{routing.runtimeAvailable ? `${routing.healthyBackends} / ${routing.routableBackends}` : '-'}</dd>
            {routing.runtimeAvailable ? null : <small>{t('healthUnknownNotRunning')}</small>}
          </div>
        </dl>
      </section>
      <KpiGrid>
        <Metric icon={<Zap size={18} />} label={t('answeredHere')} subtext={t('sinceStart')} tone="success" value={formatCount(routing.localRequests, t)} />
        <Metric icon={<Activity size={18} />} label={t('sentToNetwork')} subtext={t('sinceStart')} tone="info" value={formatCount(routing.networkRequests, t)} />
        <Metric icon={<RefreshCw size={18} />} label={t('networkFallbacks')} subtext={t('networkFallbacksHelp')} tone={routing.fallbacks > 0 ? 'warning' : 'neutral'} value={formatCount(routing.fallbacks || 0, t)} />
        <Metric icon={<CircleAlert size={18} />} label={t('errors')} subtext={t('sinceStart')} tone={routing.errors > 0 ? 'danger' : 'neutral'} value={formatCount(routing.errors, t)} />
      </KpiGrid>
      <Panel
        actions={(
          <div className="button-row">
            <label className="toggle-field" title={t('tooltipAutoRefreshLocalEndpoint')}><input checked={autoRefresh} onChange={(event) => setAutoRefresh(event.target.checked)} title={t('tooltipAutoRefreshLocalEndpoint')} type="checkbox" />{t('autoRefresh')}</label>
            <button className="action-button" onClick={refresh} title={t('tooltipRefresh')} type="button"><RefreshCw size={16} />{t('refresh')}</button>
          </div>
        )}
        title={t('localEndpointTraffic')}
      >
        <RouteMixChart records={records} />
      </Panel>
      <DataTable
        actions={(row) => [
          { icon: <HardDrive size={14} />, key: 'models', label: t('manageModels'), onSelect: () => setModal(providerModelsModal(t, { name: row.provider, apiType: row.apiType, endpoint: row.endpoint || '' }, refresh)), tooltip: t('tooltipManageProviderModels') },
          { icon: <Play size={14} />, key: 'resolve', label: t('testRoute'), onSelect: () => setModal(resolveModal(t, '')), tooltip: t('tooltipTestRoute') }
        ]}
        columns={['provider', 'apiType', 'health', 'latencyMs', 'inFlight', 'queued', 'usedFor', 'lastError']}
        defaultColumns={['provider', 'health', 'latencyMs', 'inFlight', 'usedFor', 'lastError']}
        onRowClick={(row) => setModal(providerModelsModal(t, { name: row.provider, apiType: row.apiType, endpoint: row.endpoint || '' }, refresh))}
        rowActionLabel={t('manageModels')}
        tableId="local.endpoint.providers"
        headerActions={<button className="action-button" onClick={check} title={t('tooltipCheckProviders')} type="button"><RefreshCw size={16} />{t('checkProviders')}</button>}
        onRefresh={refresh}
        rows={backends.map((item) => ({
          ...item,
          latencyMs: item.latencyMs == null ? '' : Math.round(item.latencyMs),
          health: t(`health_${item.health}`, item.health),
          inFlight: t('inUseValue', { used: item.inFlight || 0, limit: item.limit ?? '-', local: item.inFlightLocal || 0, network: item.inFlightNetwork || 0 }),
          usedFor: t(item.routable && item.shared ? 'usedForBoth' : item.routable ? 'usedForLocal' : item.shared ? 'usedForNetwork' : 'usedForNone')
        }))}
        title={t('providersOnThisMachine')}
      />
      <DataTable
        actions={(row) => [
          { icon: <Pencil size={14} />, key: 'edit', label: t('edit'), onSelect: () => setModal(ruleModal(t, row, providerNames, refresh, setModal)), tooltip: t('tooltipEditRouteRule') },
          { icon: <Play size={14} />, key: 'resolve', label: t('testRoute'), onSelect: () => setModal(resolveModal(t, row.model)), tooltip: t('tooltipTestRoute') },
          { divider: true, key: 'divider-delete' },
          { destructive: true, icon: <Trash2 size={14} />, key: 'delete', label: t('delete'), onSelect: () => removeRule(row), tooltip: t('tooltipDeleteRouteRule') }
        ]}
        columns={['model', 'sendTo', 'fallbackToNetwork']}
        headerActions={(
          <div className="button-row">
            <button className="action-button" onClick={() => setModal(resolveModal(t, ''))} title={t('tooltipTestRoute')} type="button"><Play size={16} />{t('testRoute')}</button>
            <button className="primary-button" onClick={() => setModal(ruleModal(t, null, providerNames, refresh, setModal))} title={t('tooltipAddRouteRule')} type="button"><Plus size={16} />{t('addRouteRule')}</button>
          </div>
        )}
        onRefresh={refresh}
        onRowClick={(row) => setModal(ruleModal(t, row, providerNames, refresh, setModal))}
        rowActionLabel={t('edit')}
        rows={rules.map((item) => ({ ...item, sendTo: item.target === 'Network' ? t('xenoNetwork') : (item.provider || '-') }))}
        title={t('routeRules')}
      />
      <DataTable
        actions={(row) => [
          { icon: <Play size={14} />, key: 'resolve', label: t('testRoute'), onSelect: () => setModal(resolveModal(t, row.model)), tooltip: t('tooltipTestRoute') },
          { icon: <Plus size={14} />, key: 'rule', label: t('addRouteRule'), onSelect: () => setModal(ruleModal(t, { model: row.model, target: 'Local', provider: providerNames[0] || null, fallbackToNetwork: false, isNew: true }, providerNames, refresh, setModal)), tooltip: t('tooltipAddRouteRule') }
        ]}
        columns={['received', 'model', 'route', 'statusCode', 'latencyMs', 'queuedMs', 'promptTokens', 'completionTokens', 'fallback']}
        defaultColumns={['received', 'model', 'route', 'statusCode', 'latencyMs', 'fallback']}
        tableId="local.endpoint.requests"
        onRefresh={refresh}
        rows={records.map((item) => ({ ...item, received: formatLocalTime(item.receivedUtc), latencyMs: Math.round(item.latencyMs || 0), queuedMs: Math.round(item.queuedMs || 0), model: item.model || `${item.method} ${item.path}` }))}
        title={t('recentLocalEndpointRequests')}
      />
    </>
  );
}

function formatLocalTime(value) {
  const time = Date.parse(value);
  if (Number.isNaN(time)) return value || '';
  return new Date(time).toLocaleString([], { month: '2-digit', day: '2-digit', hour: '2-digit', minute: '2-digit', second: '2-digit' });
}

function RouteMixChart({ records }) {
  const { t } = useTranslation();
  const bucketCount = 12;
  const bucketMs = 5 * 60 * 1000;
  const end = Math.ceil(Date.now() / bucketMs) * bucketMs;
  const start = end - bucketCount * bucketMs;
  const buckets = Array.from({ length: bucketCount }, (_, index) => ({ startMs: start + index * bucketMs, local: 0, network: 0, fallback: 0 }));
  (records || []).forEach((record) => {
    const time = Date.parse(record.receivedUtc);
    if (Number.isNaN(time) || time < start || time >= end) return;
    const bucket = buckets[Math.floor((time - start) / bucketMs)];
    if (!bucket) return;
    if (record.fallback) bucket.fallback += 1;
    else if (String(record.route || '').startsWith('local')) bucket.local += 1;
    else bucket.network += 1;
  });
  const max = Math.max(1, ...buckets.map((item) => item.local + item.network + item.fallback));
  const total = buckets.reduce((sum, item) => sum + item.local + item.network + item.fallback, 0);
  if (total === 0) return <EmptyState message={t('noLocalEndpointTraffic')} />;
  const width = 600;
  const height = 140;
  const barWidth = width / bucketCount;
  return (
    <div className="route-mix-chart">
      <svg aria-label={t('localEndpointTrafficSummary', { total })} role="img" viewBox={`0 0 ${width} ${height + 18}`} width="100%">
        {buckets.map((bucket, index) => {
          let y = height;
          const parts = [['local', 'var(--color-success)'], ['network', 'var(--color-info)'], ['fallback', 'var(--color-warning)']];
          const label = new Date(bucket.startMs).toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });
          return (
            <g key={bucket.startMs}>
              <title>{`${label}: ${t('answeredHere')} ${bucket.local}, ${t('sentToNetwork')} ${bucket.network}, ${t('fallback')} ${bucket.fallback}`}</title>
              {parts.map(([key, color]) => {
                const value = bucket[key];
                if (!value) return null;
                const barHeight = (value / max) * (height - 8);
                y -= barHeight;
                return <rect fill={color} height={barHeight} key={key} rx="2" width={barWidth - 6} x={index * barWidth + 3} y={y} />;
              })}
              {index % 3 === 0 ? <text fill="var(--color-muted)" fontSize="10" x={index * barWidth + 3} y={height + 14}>{label}</text> : null}
            </g>
          );
        })}
      </svg>
      <div className="chart-legend">
        <span><i style={{ background: 'var(--color-success)' }} />{t('answeredHere')}</span>
        <span><i style={{ background: 'var(--color-info)' }} />{t('sentToNetwork')}</span>
        <span><i style={{ background: 'var(--color-warning)' }} />{t('fallback')}</span>
      </div>
    </div>
  );
}

function ruleModal(t, rule, providerNames, refresh, setModal) {
  const editing = !!rule && !rule.isNew;
  const networkValue = '__network__';
  const current = rule ? (rule.target === 'Network' ? networkValue : (rule.provider || providerNames[0] || networkValue)) : (providerNames[0] || networkValue);

  async function save(form) {
    const target = form.get('target');
    const network = target === networkValue;
    const model = form.get('model');
    await localApi('/api/v1/local-inference/routes', {
      method: 'PUT',
      body: jsonBody({
        model,
        target: network ? 'Network' : 'Local',
        provider: network ? null : target,
        fallbackToNetwork: !network && form.get('fallbackToNetwork') === 'on',
        originalModel: editing ? rule.model : null
      })
    });
    refresh();
    return model;
  }

  return {
    title: editing ? t('editRouteRule') : t('addRouteRule'),
    json: rule || {},
    body: (
      <ManagedForm clearOnAnother={['model']} id="route-rule-form" onClose={() => setModal(null)} onSave={save}>
        <div className="empty-state span-full">{t('routeRuleIntro')}</div>
        <Field defaultValue={rule?.model || ''} label={t('model')} name="model" tooltip={t('tooltipRouteRuleModel')} />
        <SelectField defaultValue={current} label={t('sendTo')} name="target" options={[...providerNames.map((name) => ({ value: name, label: name })), { value: networkValue, label: t('xenoNetwork') }]} tooltip={t('tooltipRouteRuleTarget')} />
        <Toggle checked={rule?.fallbackToNetwork} label={t('fallbackToNetworkLabel')} name="fallbackToNetwork" tooltip={t('tooltipRouteRuleFallback')} />
      </ManagedForm>
    ),
    actions: <SaveActions allowAnother={!editing} formId="route-rule-form" tooltip={t('tooltipSaveRouteRule')} />
  };
}

function resolveModal(t, initialModel) {
  return {
    title: t('testRoute'),
    body: <RouteResolver initialModel={initialModel} />
  };
}

function RouteResolver({ compact = false, initialModel = '' }) {
  const { t } = useTranslation();
  const [model, setModel] = useState(initialModel || '');
  const [shape, setShape] = useState('openai');
  const [result, setResult] = useState(null);
  const [failure, setFailure] = useState('');

  async function resolve(event) {
    event?.preventDefault();
    setFailure('');
    setResult(null);
    if (!model.trim()) return;
    try {
      setResult(await localApi(`/api/v1/local-inference/resolve?model=${encodeURIComponent(model.trim())}&shape=${shape}`));
    } catch (err) {
      setFailure(err.message);
    }
  }

  return (
    <form className={compact ? 'route-resolver compact' : 'route-resolver'} onSubmit={resolve}>
      <div className="form-grid">
        <label className="form-field" title={t('tooltipRouteRuleModel')}><span>{t('model')}</span><input onChange={(event) => setModel(event.target.value)} title={t('tooltipRouteRuleModel')} value={model} /></label>
        <label className="form-field" title={t('tooltipResolveShape')}>
          <span>{t('apiShape')}</span>
          <select onChange={(event) => setShape(event.target.value)} title={t('tooltipResolveShape')} value={shape}>
            <option value="openai">OpenAI (/v1)</option>
            <option value="ollama">Ollama (/api)</option>
          </select>
        </label>
        <div className="button-row"><button className="primary-button" title={t('tooltipTestRoute')} type="submit"><Play size={16} />{t('testRoute')}</button></div>
      </div>
      {failure ? <div className="settings-restart-banner error-state" role="alert"><CircleAlert size={18} /><span>{failure}</span></div> : null}
      {result ? (
        <div className="route-resolver-result" role="status">
          <strong>{t('modelGoesTo', { model: result.model, route: result.route })}</strong>
          <p>{t(`routeReason_${result.reason}`, result.reasonText)}</p>
          {result.fallbackToNetwork ? <p>{t('routeFallsBack')}</p> : null}
          {result.target === 'local' && !result.shapeSupported ? <p>{t('routeShapeUnsupported')}</p> : null}
        </div>
      ) : null}
    </form>
  );
}

function LocalApiExplorer({ state }) {
  const { t } = useTranslation();
  const [selectedId, setSelectedId] = useState('openai-models');
  const [body, setBody] = useState('');
  const [response, setResponse] = useState('');
  const [status, setStatus] = useState('');
  const [running, setRunning] = useState(false);
  const [routeHeaders, setRouteHeaders] = useState(null);
  const abortRef = useRef(null);
  const localInference = state?.connection || {};
  const settings = state?.settings?.localInferenceServer || {};
  const baseUrl = localInference.localInferenceServerUrl
    || (settings.host && settings.port ? `http://${settings.host}:${settings.port}` : 'http://127.0.0.1:11435');
  const enabled = !!(localInference.localInferenceServerEnabled || settings.enabled);
  const listening = !!localInference.localInferenceServerListening;
  const samples = localInferenceSamples(t);
  const selected = samples.find((sample) => sample.id === selectedId) || samples[0];

  useEffect(() => {
    setBody(selected.body ? JSON.stringify(selected.body, null, 2) : '');
    setResponse('');
    setStatus('');
  }, [selectedId]);

  async function execute() {
    setRunning(true);
    setResponse('');
    setStatus('');
    setRouteHeaders(null);
    const controller = new AbortController();
    abortRef.current = controller;
    try {
      const request = {
        method: selected.method,
        headers: { Accept: 'application/json' },
        signal: controller.signal
      };
      if (selected.target === 'local') request.headers['X-Xeno-Local-Token'] = window.__XENO_LOCAL_TOKEN__ || '';
      if (selected.method !== 'GET') {
        request.headers['Content-Type'] = 'application/json';
        request.body = body || '{}';
      }

      const result = await fetch(sampleBase(selected, baseUrl) + selected.path, request);
      setStatus(`${result.status} ${result.statusText}`);
      setRouteHeaders({ route: result.headers.get('x-xeno-route') || '', fallback: result.headers.get('x-xeno-fallback') === 'true' });
      const contentType = result.headers.get('content-type') || '';
      if (result.body && (contentType.includes('event-stream') || contentType.includes('ndjson'))) {
        const reader = result.body.getReader();
        const decoder = new TextDecoder();
        let streamed = '';
        while (true) {
          const { done, value } = await reader.read();
          if (done) break;
          streamed += decoder.decode(value, { stream: true });
          setResponse(streamed);
        }
        return;
      }

      const text = await result.text();
      try {
        setResponse(JSON.stringify(JSON.parse(text), null, 2));
      } catch {
        setResponse(text);
      }
    } catch (err) {
      if (err.name === 'AbortError') {
        setStatus((current) => `${current || ''} ${t('stopped')}`.trim());
        return;
      }

      setStatus(t('requestFailed'));
      setResponse(err.message);
    } finally {
      abortRef.current = null;
      setRunning(false);
    }
  }

  function stop() {
    abortRef.current?.abort();
  }

  const curl = buildLocalInferenceCurl(sampleBase(selected, baseUrl), selected, body);
  return (
    <div className="local-api-explorer">
      <Panel title={t('localInferenceStatus')} actions={<CopyButton label={t('copyValue')} value={baseUrl} />}>
        <RecordTable fields={['enabled', 'listening', 'baseUrl', 'websocketReady', 'lastError']} record={{
          enabled,
          listening,
          baseUrl,
          websocketReady: !!(state?.connection?.isConnected && state?.connection?.isRegistered),
          lastError: localInference.localInferenceServerLastError || ''
        }} />
      </Panel>
      <Panel title={t('whereDoesAModelGo')}>
        <RouteResolver compact />
      </Panel>
      <Panel title={t('apiExplorer')}>
        <div className="explorer-layout">
          <div className="explorer-list" role="list">
            {samples.map((sample) => (
              <button className={`explorer-item ${sample.id === selected.id ? 'active' : ''}`} key={sample.id} onClick={() => setSelectedId(sample.id)} type="button">
                <span>{sample.group}</span>
                <strong>{sample.label}</strong>
                <code>{sample.method} {sample.path}</code>
              </button>
            ))}
          </div>
          <div className="explorer-detail">
            <div className="explorer-endpoint">
              <span className="method-pill">{selected.method}</span>
              <CopyableValue label={t('copyValue')} value={sampleBase(selected, baseUrl) + selected.path} />
            </div>
            {selected.method !== 'GET' ? (
              <label className="field span-full">
                <span>{t('requestBody')}</span>
                <textarea onChange={(event) => setBody(event.target.value)} rows={12} value={body} />
              </label>
            ) : null}
            <div className="button-row">
              <button className="primary-button" disabled={!enabled || !listening || running} onClick={execute} type="button"><Play size={16} />{running ? t('running') : t('sendRequest')}</button>
              {running ? <button className="action-button danger" onClick={stop} title={t('tooltipStopRequest')} type="button"><Square size={16} />{t('stop')}</button> : null}
              <CopyButton label={t('copyJson')} value={curl} />
            </div>
            <details open>
              <summary>{t('curl')}</summary>
              <pre className="json-preview"><code>{curl}</code></pre>
            </details>
            <details open>
              <summary>{t('response')} {status ? <span className="status-inline">{status}</span> : null} {routeHeaders?.route ? <span className="status-inline" title={t('tooltipRouteHeader')}>X-Xeno-Route: {routeHeaders.route}{routeHeaders.fallback ? ` (${t('fallback')})` : ''}</span> : null}</summary>
              <pre className="json-preview"><code>{response || t('noData')}</code></pre>
            </details>
          </div>
        </div>
      </Panel>
    </div>
  );
}

function localInferenceSamples(t) {
  return [
    { id: 'openai-models', group: 'OpenAI', label: t('listModels'), method: 'GET', path: '/v1/models' },
    { id: 'openai-model', group: 'OpenAI', label: t('retrieveModel'), method: 'GET', path: '/v1/models/llama3.2:latest' },
    { id: 'openai-chat', group: 'OpenAI', label: t('chatCompletions'), method: 'POST', path: '/v1/chat/completions', body: { model: 'llama3.2:latest', messages: [{ role: 'user', content: 'Say hello from Xeno.' }], stream: false } },
    { id: 'openai-chat-stream', group: 'OpenAI', label: t('streamingChat'), method: 'POST', path: '/v1/chat/completions', body: { model: 'llama3.2:latest', messages: [{ role: 'user', content: 'Stream a short greeting from Xeno.' }], stream: true, stream_options: { include_usage: true } } },
    { id: 'openai-embeddings', group: 'OpenAI', label: t('embeddings'), method: 'POST', path: '/v1/embeddings', body: { model: 'nomic-embed-text:latest', input: ['local inference', 'xeno client'] } },
    { id: 'ollama-tags', group: 'Ollama', label: t('listModels'), method: 'GET', path: '/api/tags' },
    { id: 'ollama-ps', group: 'Ollama', label: t('runningModels'), method: 'GET', path: '/api/ps' },
    { id: 'ollama-show', group: 'Ollama', label: t('showModel'), method: 'POST', path: '/api/show', body: { model: 'llama3.2:latest' } },
    { id: 'ollama-generate', group: 'Ollama', label: t('generate'), method: 'POST', path: '/api/generate', body: { model: 'llama3.2:latest', prompt: 'Say hello from Xeno.', stream: false } },
    { id: 'ollama-generate-stream', group: 'Ollama', label: t('streamingGenerate'), method: 'POST', path: '/api/generate', body: { model: 'llama3.2:latest', prompt: 'Stream a short greeting from Xeno.', stream: true } },
    { id: 'ollama-chat', group: 'Ollama', label: t('chat'), method: 'POST', path: '/api/chat', body: { model: 'llama3.2:latest', messages: [{ role: 'user', content: 'Say hello from Xeno.' }], stream: false } },
    { id: 'ollama-chat-stream', group: 'Ollama', label: t('streamingChat'), method: 'POST', path: '/api/chat', body: { model: 'llama3.2:latest', messages: [{ role: 'user', content: 'Stream a short greeting from Xeno.' }], stream: true } },
    { id: 'ollama-embeddings', group: 'Ollama', label: t('legacyEmbeddings'), method: 'POST', path: '/api/embeddings', body: { model: 'nomic-embed-text:latest', prompt: 'local inference' } },
    { id: 'ollama-embed', group: 'Ollama', label: t('embeddings'), method: 'POST', path: '/api/embed', body: { model: 'nomic-embed-text:latest', input: ['local inference', 'xeno client'] } },
    { id: 'chat-models', group: t('chatApiGroup'), label: t('chatApiModels'), method: 'GET', path: '/api/v1/chat/models', target: 'local' },
    { id: 'chat-tools', group: t('chatApiGroup'), label: t('chatApiTools'), method: 'GET', path: '/api/v1/chat/tools', target: 'local' },
    { id: 'chat-conversations', group: t('chatApiGroup'), label: t('chatApiConversations'), method: 'GET', path: '/api/v1/chat/conversations?limit=20', target: 'local' },
    { id: 'chat-create', group: t('chatApiGroup'), label: t('chatApiCreate'), method: 'POST', path: '/api/v1/chat/conversations', body: { model: 'llama3.2:latest', title: 'API Explorer', surface: 'Web' }, target: 'local' },
    { id: 'chat-history', group: t('chatApiGroup'), label: t('chatApiHistory'), method: 'GET', path: '/api/v1/chat/history?pageSize=20', target: 'local' },
    { id: 'chat-feedback', group: t('chatApiGroup'), label: t('chatApiFeedback'), method: 'GET', path: '/api/v1/chat/feedback?pageSize=20', target: 'local' },
    { id: 'chat-analytics', group: t('chatApiGroup'), label: t('chatApiAnalytics'), method: 'GET', path: '/api/v1/chat/analytics?bucketMinutes=60', target: 'local' },
    { id: 'provider-models', group: t('modelApiGroup'), label: t('modelApiInventory'), method: 'GET', path: '/api/v1/providers/local-ollama/models', target: 'local' },
    { id: 'provider-pull', group: t('modelApiGroup'), label: t('modelApiPull'), method: 'POST', path: '/api/v1/providers/local-ollama/pulls', body: { model: 'all-minilm:latest', advertise: false }, target: 'local' },
    { id: 'model-pulls', group: t('modelApiGroup'), label: t('modelApiPulls'), method: 'GET', path: '/api/v1/model-pulls', target: 'local' },
    { id: 'model-pull', group: t('modelApiGroup'), label: t('modelApiPullStatus'), method: 'GET', path: '/api/v1/model-pulls/{id}', target: 'local' },
    { id: 'model-pull-cancel', group: t('modelApiGroup'), label: t('modelApiPullCancel'), method: 'DELETE', path: '/api/v1/model-pulls/{id}', target: 'local' },
    { id: 'model-pulls-clear', group: t('modelApiGroup'), label: t('modelApiPullsClear'), method: 'POST', path: '/api/v1/model-pulls/clear', body: {}, target: 'local' },
    { id: 'provider-model-delete', group: t('modelApiGroup'), label: t('modelApiDelete'), method: 'DELETE', path: '/api/v1/providers/local-ollama/models/{model}', target: 'local' },
    { id: 'models-import', group: t('modelApiGroup'), label: t('modelApiImport'), method: 'POST', path: '/api/v1/models/import', body: { models: [{ provider: 'local-ollama', name: 'llama3.2:latest', supportsCompletions: true, supportsEmbeddings: false }] }, target: 'local' },
    { id: 'usage-served', group: t('usage'), label: t('usageDatasetServedRequests'), method: 'GET', path: '/api/v1/requests/analytics/usage?bucketMinutes=15', target: 'local' },
    { id: 'usage-endpoint', group: t('usage'), label: t('usageDatasetLocalEndpoint'), method: 'GET', path: '/api/v1/local-inference/analytics/usage?bucketMinutes=15', target: 'local' },
    { id: 'usage-chat', group: t('usage'), label: t('usageDatasetChat'), method: 'GET', path: '/api/v1/chat/analytics/usage?bucketMinutes=15', target: 'local' },
    { id: 'usage-health', group: t('usage'), label: t('usageChartHealth'), method: 'GET', path: '/api/v1/telemetry/providers/health?bucketMinutes=15', target: 'local' }
  ];
}

function sampleBase(sample, baseUrl) {
  return sample.target === 'local' ? window.location.origin : baseUrl;
}

function buildLocalInferenceCurl(baseUrl, sample, body) {
  const lines = [`curl -i -X ${sample.method} "${baseUrl}${sample.path}"`];
  if (sample.target === 'local') lines.push('  -H "X-Xeno-Local-Token: <token from ~/.xeno/local-api.json>"');
  if (sample.method !== 'GET') {
    lines.push('  -H "Content-Type: application/json"');
    lines.push(`  -d '${body || '{}'}'`);
  }

  return lines.join(' \\\n');
}

function Settings({ database, refresh, state }) {
  const { t } = useTranslation();
  const [status, setStatus] = useState('');
  const chatSettings = state?.settings?.chat || {};
  const mcpSettings = state?.settings?.mcp || {};

  async function save(event) {
    event.preventDefault();
    setStatus('');
    const form = new FormData(event.currentTarget);
    const settings = state?.settings?.localDashboard || {};
    const localInference = state?.settings?.localInferenceServer || {};
    const telemetry = state?.settings?.telemetry || {};
    const providerTelemetry = telemetry.providerTelemetry || {};
    const chat = state?.settings?.chat || {};
    const mcp = state?.settings?.mcp || {};
    let mcpServers = chat.mcpServers || [];
    try {
      if (form.has('chatMcpServers')) mcpServers = parseMcpServers(form.get('chatMcpServers'), t);
    } catch (err) {
      setStatus(err.message);
      return;
    }
    await localApi('/api/v1/settings', {
      method: 'PUT',
      body: jsonBody({
        enabled: form.has('enabled') ? form.get('enabled') === 'on' : !!settings.enabled,
        host: form.get('host') || settings.host || '127.0.0.1',
        port: Number(form.get('port') || settings.port || 9343),
        openBrowserOnStart: form.has('openBrowserOnStart') ? form.get('openBrowserOnStart') === 'on' : !!settings.openBrowserOnStart,
        requireLocalToken: form.has('requireLocalToken') ? form.get('requireLocalToken') === 'on' : !!settings.requireLocalToken,
        database: { filename: form.get('filename') || settings.database?.filename },
        historyRetention: {
          maxEntries: Number(form.get('maxEntries') || settings.historyRetention?.maxEntries || 1000),
          maxBodyBytes: Number(form.get('maxBodyBytes') || settings.historyRetention?.maxBodyBytes || 65536),
          persistBodies: form.has('persistBodies') ? form.get('persistBodies') === 'on' : !!settings.historyRetention?.persistBodies
        },
        localInferenceServer: {
          ...camelCaseOnly(localInference),
          enabled: form.has('localInferenceEnabled') ? form.get('localInferenceEnabled') === 'on' : !!localInference.enabled,
          host: form.get('localInferenceHost') || localInference.host || '127.0.0.1',
          port: Number(form.get('localInferencePort') || localInference.port || 11435),
          enableCors: form.has('localInferenceEnableCors') ? form.get('localInferenceEnableCors') === 'on' : localInference.enableCors !== false,
          requestTimeoutMs: Number(form.get('localInferenceRequestTimeoutMs') || localInference.requestTimeoutMs || 300000),
          maxRequestBodyBytes: Number(form.get('localInferenceMaxRequestBodyBytes') || localInference.maxRequestBodyBytes || 10485760),
          corsAllowedOrigins: localInference.corsAllowedOrigins || ['http://localhost', 'http://127.0.0.1', 'http://[::1]'],
          routingMode: form.get('localInferenceRoutingMode') || localInference.routingMode || 'PreferLocal',
          routes: localInference.routes || [],
          discoverLocalModels: form.has('localInferenceRoutingMode') ? form.get('localInferenceDiscoverLocalModels') === 'on' : localInference.discoverLocalModels !== false,
          healthCheckIntervalMs: Number(form.get('localInferenceHealthCheckIntervalMs') || localInference.healthCheckIntervalMs || 15000),
          localQueueTimeoutMs: form.has('localInferenceLocalQueueTimeoutMs') && form.get('localInferenceLocalQueueTimeoutMs') !== '' ? Number(form.get('localInferenceLocalQueueTimeoutMs')) : (localInference.localQueueTimeoutMs ?? 30000),
          requestRetentionDays: Number(form.get('localInferenceRequestRetentionDays') || localInference.requestRetentionDays || 30)
        },
        defaultMaxConcurrentRequests: Number(form.get('defaultMaxConcurrentRequests') || state?.settings?.defaultMaxConcurrentRequests || 4),
        analytics: {
          maxRecordsPerQuery: Number(form.get('analyticsMaxRecordsPerQuery') || state?.settings?.analytics?.maxRecordsPerQuery || 250000),
          defaultTopN: Number(form.get('analyticsDefaultTopN') || state?.settings?.analytics?.defaultTopN || 8)
        },
        mcp: {
          enabled: form.has('mcpHost') ? form.get('mcpEnabled') === 'on' : !!mcp.enabled,
          host: form.get('mcpHost') || mcp.host || '127.0.0.1',
          port: Number(form.get('mcpPort') || mcp.port || 11436),
          requireToken: form.has('mcpHost') ? form.get('mcpRequireToken') === 'on' : mcp.requireToken !== false
        },
        chat: {
          // Keep every chat setting this form does not show (it replaces the whole Chat section).
          ...chat,
          historyRetentionDays: Number(form.get('chatHistoryRetentionDays') || chat.historyRetentionDays || 30),
          maxToolIterations: Number(form.get('chatMaxToolIterations') || chat.maxToolIterations || 8),
          maxToolCallsPerTurn: Number(form.get('chatMaxToolCallsPerTurn') || chat.maxToolCallsPerTurn || 16),
          toolApprovalTimeoutSeconds: Number(form.get('chatToolApprovalTimeoutSeconds') || chat.toolApprovalTimeoutSeconds || 120),
          defaultSystemPrompt: form.has('chatDefaultSystemPrompt') ? (form.get('chatDefaultSystemPrompt') || null) : (chat.defaultSystemPrompt || null),
          defaultWorkingDirectory: form.has('chatDefaultWorkingDirectory') ? (String(form.get('chatDefaultWorkingDirectory') || '').trim() || null) : (chat.defaultWorkingDirectory || null),
          processTimeoutSeconds: Number(form.get('chatProcessTimeoutSeconds') || chat.processTimeoutSeconds || 120),
          maxToolOutputCharacters: Number(form.get('chatMaxToolOutputCharacters') || chat.maxToolOutputCharacters || 50000),
          taskPlanningEnabled: form.has('chatProcessTimeoutSeconds') ? form.get('chatTaskPlanningEnabled') === 'on' : chat.taskPlanningEnabled !== false,
          memoryEnabled: form.has('chatProcessTimeoutSeconds') ? form.get('chatMemoryEnabled') === 'on' : chat.memoryEnabled !== false,
          webRetrieveAllowPrivateNetworks: form.has('chatProcessTimeoutSeconds') ? form.get('chatWebRetrieveAllowPrivateNetworks') === 'on' : !!chat.webRetrieveAllowPrivateNetworks,
          webSearch: form.has('chatWebSearchProvider') ? {
            provider: form.get('chatWebSearchProvider') || 'None',
            tavilyApiKey: String(form.get('chatTavilyApiKey') || '').trim() || null,
            youApiKey: String(form.get('chatYouApiKey') || '').trim() || null,
            maxResults: Number(form.get('chatWebSearchMaxResults') || chat.webSearch?.maxResults || 5)
          } : chat.webSearch,
          mcpServers
        },
        telemetry: {
          enabled: form.has('telemetryEnabled') ? form.get('telemetryEnabled') === 'on' : !!telemetry.enabled,
          collectionIntervalMs: Number(form.get('telemetryCollectionIntervalMs') || telemetry.collectionIntervalMs || 15000),
          retentionDays: Number(form.get('telemetryRetentionDays') || telemetry.retentionDays || 30),
          pruneIntervalMinutes: Number(form.get('telemetryPruneIntervalMinutes') || telemetry.pruneIntervalMinutes || 60),
          persistRawSnapshot: form.has('telemetryPersistRawSnapshot') ? form.get('telemetryPersistRawSnapshot') === 'on' : !!telemetry.persistRawSnapshot,
          gpu: {
            enabled: form.has('telemetryGpuEnabled') ? form.get('telemetryGpuEnabled') === 'on' : !!telemetry.gpu?.enabled,
            nvidiaDcgmExporterUrl: form.get('telemetryDcgmUrl') || telemetry.gpu?.nvidiaDcgmExporterUrl || null,
            commandTimeoutMs: Number(form.get('telemetryGpuCommandTimeoutMs') || telemetry.gpu?.commandTimeoutMs || 5000)
          },
          providerTelemetry: {
            enabled: form.has('providerTelemetryEnabled') ? form.get('providerTelemetryEnabled') === 'on' : !!providerTelemetry.enabled,
            collectionIntervalMs: Number(form.get('providerTelemetryCollectionIntervalMs') || providerTelemetry.collectionIntervalMs || 15000),
            requestTimeoutMs: Number(form.get('providerTelemetryRequestTimeoutMs') || providerTelemetry.requestTimeoutMs || 5000),
            concurrentProviderLimit: Number(form.get('providerTelemetryConcurrentProviderLimit') || providerTelemetry.concurrentProviderLimit || 4),
            discoverMetricsEndpoints: form.has('providerTelemetryDiscoverMetricsEndpoints') ? form.get('providerTelemetryDiscoverMetricsEndpoints') === 'on' : !!providerTelemetry.discoverMetricsEndpoints,
            persistRawMetrics: form.has('providerTelemetryPersistRawMetrics') ? form.get('providerTelemetryPersistRawMetrics') === 'on' : !!providerTelemetry.persistRawMetrics,
            rawMetricsMaxBytes: Number(form.get('providerTelemetryRawMetricsMaxBytes') || providerTelemetry.rawMetricsMaxBytes || 262144),
            collectModelDetails: form.has('providerTelemetryCollectModelDetails') ? form.get('providerTelemetryCollectModelDetails') === 'on' : !!providerTelemetry.collectModelDetails,
            collectProcessCorrelation: form.has('providerTelemetryCollectProcessCorrelation') ? form.get('providerTelemetryCollectProcessCorrelation') === 'on' : !!providerTelemetry.collectProcessCorrelation,
            collectCloudHeaderTelemetry: form.has('providerTelemetryCollectCloudHeaderTelemetry') ? form.get('providerTelemetryCollectCloudHeaderTelemetry') === 'on' : !!providerTelemetry.collectCloudHeaderTelemetry,
            endpointOverrides: providerTelemetry.endpointOverrides || []
          }
        }
      })
    });
    refresh();
  }

  async function reloadConfiguration() {
    setStatus('');
    const result = await localApi('/api/v1/settings/reload', { method: 'POST', body: '{}' });
    setStatus(result.inventoryUpdateRequested ? t('configurationReloadedInventoryRequested') : t('configurationReloaded'));
    refresh();
  }

  const settings = state?.settings?.localDashboard || {};
  const localInference = state?.settings?.localInferenceServer || {};
  const telemetry = state?.settings?.telemetry || {};
  const providerTelemetry = telemetry.providerTelemetry || {};
  return (
    <>
      <Panel
        actions={<button className="primary-button" onClick={reloadConfiguration} title={t('tooltipReloadClientConfiguration')} type="button"><RefreshCw size={16} />{t('reloadConfiguration')}</button>}
        title={t('configurationReload')}
      >
        <div className="settings-reload-grid">
          <div className="empty-state">{t('configurationReloadNotice')}</div>
          <RecordTable fields={['settingsFile']} record={{ settingsFile: state?.settingsFile || '' }} />
        </div>
        {status ? <div className="settings-restart-banner success-state"><RefreshCw size={18} /><span>{status}</span></div> : null}
      </Panel>
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
      <Panel title={t('localInferenceSettings')}>
        <div className="empty-state">{t('localSettingsRestartNotice')}</div>
        <form className="form-grid" onSubmit={save}>
          <Toggle checked={localInference.enabled} label={t('enabled')} name="localInferenceEnabled" tooltip={t('localInferenceSettings')} />
          <Field defaultValue={localInference.host || '127.0.0.1'} label={t('host')} name="localInferenceHost" tooltip={t('localInferenceSettings')} />
          <Field defaultValue={localInference.port || 11435} label={t('port')} name="localInferencePort" tooltip={t('tooltipLocalInferencePort')} type="number" />
          <Toggle checked={localInference.enableCors !== false} label={t('cors')} name="localInferenceEnableCors" tooltip={t('localInferenceSettings')} />
          <Field defaultValue={localInference.requestTimeoutMs || 300000} label={t('timeoutMs')} name="localInferenceRequestTimeoutMs" tooltip={t('localInferenceSettings')} type="number" />
          <Field defaultValue={localInference.maxRequestBodyBytes || 10485760} label={t('maxBodyBytes')} name="localInferenceMaxRequestBodyBytes" tooltip={t('localInferenceSettings')} type="number" />
          <SelectField defaultValue={localInference.routingMode || 'PreferLocal'} label={t('localRoutingMode')} name="localInferenceRoutingMode" options={[{ value: 'PreferLocal', label: t('routingModePreferLocal') }, { value: 'NetworkOnly', label: t('routingModeNetworkOnly') }, { value: 'LocalOnly', label: t('routingModeLocalOnly') }]} tooltip={t('tooltipRoutingMode')} />
          <Toggle checked={localInference.discoverLocalModels !== false} label={t('discoverLocalModels')} name="localInferenceDiscoverLocalModels" tooltip={t('tooltipDiscoverLocalModels')} />
          <Field defaultValue={localInference.healthCheckIntervalMs || 15000} label={t('healthCheckIntervalMs')} name="localInferenceHealthCheckIntervalMs" tooltip={t('tooltipHealthCheckIntervalMs')} type="number" />
          <Field defaultValue={localInference.localQueueTimeoutMs ?? 30000} label={t('localQueueTimeoutMs')} name="localInferenceLocalQueueTimeoutMs" tooltip={t('tooltipLocalQueueTimeoutMs')} type="number" />
          <Field defaultValue={localInference.requestRetentionDays || 30} label={t('usageSettingRetention')} name="localInferenceRequestRetentionDays" tooltip={t('tooltipUsageSettingRetention')} type="number" />
          <Field defaultValue={state?.settings?.analytics?.maxRecordsPerQuery || 250000} label={t('usageSettingMaxRecords')} name="analyticsMaxRecordsPerQuery" tooltip={t('tooltipUsageSettingMaxRecords')} type="number" />
          <Field defaultValue={state?.settings?.analytics?.defaultTopN || 8} label={t('usageSettingTopN')} name="analyticsDefaultTopN" tooltip={t('tooltipUsageSettingTopN')} type="number" />
          <Field defaultValue={state?.settings?.defaultMaxConcurrentRequests || 4} label={t('defaultMaxConcurrentRequests')} name="defaultMaxConcurrentRequests" tooltip={t('tooltipDefaultMaxConcurrentRequests')} type="number" />
          <div className="button-row"><button className="primary-button" title={t('tooltipSaveSettings')} type="submit">{t('saveSettings')}</button></div>
        </form>
      </Panel>
      <Panel title={t('telemetrySettings')}>
        <div className="empty-state">{t('telemetrySettingsRestartNotice')}</div>
        <form className="form-grid" onSubmit={save}>
          <Toggle checked={telemetry.enabled} label={t('enabled')} name="telemetryEnabled" tooltip={t('tooltipTelemetryEnabled')} />
          <Field defaultValue={telemetry.collectionIntervalMs || 15000} label={t('collectionIntervalMs')} name="telemetryCollectionIntervalMs" tooltip={t('tooltipTelemetryInterval')} type="number" />
          <Field defaultValue={telemetry.retentionDays || 30} label={t('retentionDays')} name="telemetryRetentionDays" tooltip={t('tooltipTelemetryRetention')} type="number" />
          <Field defaultValue={telemetry.pruneIntervalMinutes || 60} label={t('pruneIntervalMinutes')} name="telemetryPruneIntervalMinutes" tooltip={t('tooltipTelemetryPruneInterval')} type="number" />
          <Toggle checked={telemetry.persistRawSnapshot} label={t('persistRawSnapshot')} name="telemetryPersistRawSnapshot" tooltip={t('tooltipTelemetryPersistRawSnapshot')} />
          <Toggle checked={telemetry.gpu?.enabled} label={t('gpuTelemetry')} name="telemetryGpuEnabled" tooltip={t('tooltipTelemetryGpuEnabled')} />
          <Field defaultValue={telemetry.gpu?.nvidiaDcgmExporterUrl || ''} label={t('dcgmExporterUrl')} name="telemetryDcgmUrl" tooltip={t('tooltipTelemetryDcgmUrl')} />
          <Field defaultValue={telemetry.gpu?.commandTimeoutMs || 5000} label={t('gpuCommandTimeoutMs')} name="telemetryGpuCommandTimeoutMs" tooltip={t('tooltipTelemetryGpuCommandTimeout')} type="number" />
          <div className="button-row"><button className="primary-button" title={t('tooltipSaveSettings')} type="submit">{t('saveSettings')}</button></div>
        </form>
      </Panel>
      <Panel title={t('providerTelemetrySettings')}>
        <div className="empty-state">{t('providerTelemetrySettingsRestartNotice')}</div>
        <form className="form-grid" onSubmit={save}>
          <Toggle checked={providerTelemetry.enabled} label={t('enabled')} name="providerTelemetryEnabled" tooltip={t('tooltipProviderTelemetryEnabled')} />
          <Field defaultValue={providerTelemetry.collectionIntervalMs || 15000} label={t('collectionIntervalMs')} name="providerTelemetryCollectionIntervalMs" tooltip={t('tooltipProviderTelemetryInterval')} type="number" />
          <Field defaultValue={providerTelemetry.requestTimeoutMs || 5000} label={t('requestTimeoutMs')} name="providerTelemetryRequestTimeoutMs" tooltip={t('tooltipProviderTelemetryTimeout')} type="number" />
          <Field defaultValue={providerTelemetry.concurrentProviderLimit || 4} label={t('concurrentProviderLimit')} name="providerTelemetryConcurrentProviderLimit" tooltip={t('tooltipProviderTelemetryConcurrentLimit')} type="number" />
          <Toggle checked={providerTelemetry.discoverMetricsEndpoints} label={t('discoverMetricsEndpoints')} name="providerTelemetryDiscoverMetricsEndpoints" tooltip={t('tooltipProviderTelemetryDiscovery')} />
          <Toggle checked={providerTelemetry.persistRawMetrics} label={t('persistRawMetrics')} name="providerTelemetryPersistRawMetrics" tooltip={t('tooltipProviderTelemetryPersistRaw')} />
          <Field defaultValue={providerTelemetry.rawMetricsMaxBytes || 262144} label={t('rawMetricsMaxBytes')} name="providerTelemetryRawMetricsMaxBytes" tooltip={t('tooltipProviderTelemetryRawBytes')} type="number" />
          <Toggle checked={providerTelemetry.collectModelDetails} label={t('collectModelDetails')} name="providerTelemetryCollectModelDetails" tooltip={t('tooltipProviderTelemetryModelDetails')} />
          <Toggle checked={providerTelemetry.collectProcessCorrelation} label={t('collectProcessCorrelation')} name="providerTelemetryCollectProcessCorrelation" tooltip={t('tooltipProviderTelemetryProcessCorrelation')} />
          <Toggle checked={providerTelemetry.collectCloudHeaderTelemetry} label={t('collectCloudHeaderTelemetry')} name="providerTelemetryCollectCloudHeaderTelemetry" tooltip={t('tooltipProviderTelemetryCloudHeaders')} />
          <div className="button-row"><button className="primary-button" title={t('tooltipSaveSettings')} type="submit">{t('saveSettings')}</button></div>
        </form>
      </Panel>
      <Panel title={t('mcpSettings')}>
        <div className="empty-state">{t('mcpSettingsNotice')}</div>
        <form className="form-grid" onSubmit={save}>
          <Toggle checked={mcpSettings.enabled} label={t('mcpSettingEnabled')} name="mcpEnabled" tooltip={t('tooltipMcpSettingEnabled')} />
          <Toggle checked={mcpSettings.requireToken !== false} label={t('mcpSettingRequireToken')} name="mcpRequireToken" tooltip={t('tooltipMcpSettingRequireToken')} />
          <SelectField defaultValue={mcpSettings.host || '127.0.0.1'} label={t('host')} name="mcpHost" options={['127.0.0.1', 'localhost', '::1', '[::1]'].map((value) => ({ value, label: value }))} tooltip={t('tooltipMcpSettingHost')} />
          <Field defaultValue={mcpSettings.port || 11436} label={t('port')} name="mcpPort" tooltip={t('tooltipMcpSettingPort')} type="number" />
          <div className="button-row"><button className="primary-button" title={t('tooltipSaveSettings')} type="submit">{t('saveSettings')}</button></div>
        </form>
      </Panel>
      <Panel title={t('chatSettings')}>
        <div className="empty-state">{t('chatSettingsNotice')}</div>
        <form className="form-grid" onSubmit={save}>
          <Field defaultValue={chatSettings.historyRetentionDays || 30} label={t('chatSettingRetention')} name="chatHistoryRetentionDays" tooltip={t('tooltipChatSettingRetention')} type="number" />
          <Field defaultValue={chatSettings.toolApprovalTimeoutSeconds || 120} label={t('chatSettingApprovalTimeout')} name="chatToolApprovalTimeoutSeconds" tooltip={t('tooltipChatSettingApprovalTimeout')} type="number" />
          <Field defaultValue={chatSettings.maxToolIterations || 8} label={t('chatSettingMaxIterations')} name="chatMaxToolIterations" tooltip={t('tooltipChatSettingMaxIterations')} type="number" />
          <Field defaultValue={chatSettings.maxToolCallsPerTurn || 16} label={t('chatSettingMaxToolCalls')} name="chatMaxToolCallsPerTurn" tooltip={t('tooltipChatSettingMaxToolCalls')} type="number" />
          <label className="form-field span-full" title={t('tooltipChatSettingSystemPrompt')}><span>{t('chatSettingSystemPrompt')}</span><textarea defaultValue={chatSettings.defaultSystemPrompt || ''} name="chatDefaultSystemPrompt" rows={4} title={t('tooltipChatSettingSystemPrompt')} /></label>
          <div className="button-row"><button className="primary-button" title={t('tooltipSaveSettings')} type="submit">{t('saveSettings')}</button></div>
        </form>
      </Panel>
      <Panel title={t('chatToolSettings')}>
        <div className="empty-state">{t('chatToolSettingsNotice')}</div>
        <form className="form-grid" onSubmit={save}>
          <Field defaultValue={chatSettings.defaultWorkingDirectory || ''} label={t('chatSettingWorkingDirectory')} name="chatDefaultWorkingDirectory" tooltip={t('tooltipChatSettingWorkingDirectory')} />
          <Field defaultValue={chatSettings.processTimeoutSeconds || 120} label={t('chatSettingProcessTimeout')} name="chatProcessTimeoutSeconds" tooltip={t('tooltipChatSettingProcessTimeout')} type="number" />
          <Field defaultValue={chatSettings.maxToolOutputCharacters || 50000} label={t('chatSettingMaxToolOutput')} name="chatMaxToolOutputCharacters" tooltip={t('tooltipChatSettingMaxToolOutput')} type="number" />
          <Toggle checked={chatSettings.taskPlanningEnabled !== false} label={t('chatSettingTaskPlanning')} name="chatTaskPlanningEnabled" tooltip={t('tooltipChatSettingTaskPlanning')} />
          <Toggle checked={chatSettings.memoryEnabled !== false} label={t('chatSettingMemory')} name="chatMemoryEnabled" tooltip={t('tooltipChatSettingMemory')} />
          <Toggle checked={!!chatSettings.webRetrieveAllowPrivateNetworks} label={t('chatSettingWebPrivate')} name="chatWebRetrieveAllowPrivateNetworks" tooltip={t('tooltipChatSettingWebPrivate')} />
          <label className="form-field" title={t('tooltipChatSettingWebSearch')}>
            <span>{t('chatSettingWebSearch')}</span>
            <select defaultValue={chatSettings.webSearch?.provider || 'None'} name="chatWebSearchProvider" title={t('tooltipChatSettingWebSearch')}>
              <option value="None">{t('none')}</option>
              <option value="Tavily">Tavily</option>
              <option value="You">You.com</option>
            </select>
          </label>
          <Field defaultValue={chatSettings.webSearch?.maxResults || 5} label={t('chatSettingWebSearchResults')} name="chatWebSearchMaxResults" tooltip={t('tooltipChatSettingWebSearchResults')} type="number" />
          <Field defaultValue={chatSettings.webSearch?.tavilyApiKey || ''} label={t('chatSettingTavilyKey')} name="chatTavilyApiKey" tooltip={t('tooltipChatSettingTavilyKey')} type="password" />
          <Field defaultValue={chatSettings.webSearch?.youApiKey || ''} label={t('chatSettingYouKey')} name="chatYouApiKey" tooltip={t('tooltipChatSettingYouKey')} type="password" />
          <label className="form-field span-full" title={t('tooltipChatSettingMcpServers')}>
            <span>{t('chatSettingMcpServers')}</span>
            <textarea className="code-textarea" defaultValue={JSON.stringify(chatSettings.mcpServers || [], null, 2)} name="chatMcpServers" rows={10} spellCheck={false} title={t('tooltipChatSettingMcpServers')} />
          </label>
          <McpServerStatusList />
          <div className="button-row"><button className="primary-button" title={t('tooltipSaveSettings')} type="submit">{t('saveSettings')}</button></div>
        </form>
      </Panel>
    </>
  );
}

function providerModal(t, provider, refresh, setModal) {
  async function save(form) {
    const name = form.get('name');
    await localApi(provider ? `/api/v1/providers/${encodeURIComponent(provider.name)}` : '/api/v1/providers', {
      method: provider ? 'PUT' : 'POST',
      body: jsonBody({
        name,
        apiType: form.get('apiType'),
        endpoint: form.get('endpoint'),
        timeoutMs: Number(form.get('timeoutMs')),
        credential: form.get('credential') || null,
        clearCredential: form.get('clearCredential') === 'on',
        allowModelManagement: form.get('allowModelManagement') === 'on',
        shareWithNetwork: form.get('shareWithNetwork') === 'on',
        routeLocalRequests: form.get('routeLocalRequests') === 'on',
        maxConcurrentRequests: form.get('maxConcurrentRequests') ? Number(form.get('maxConcurrentRequests')) : null
      })
    });
    refresh();
    return name;
  }

  return {
    title: provider ? t('editProvider') : t('createProvider'),
    json: provider || {},
    body: (
      <ManagedForm clearOnAnother={['name', 'endpoint', 'credential']} id="provider-form" onClose={() => setModal(null)} onSave={save}>
        <Field defaultValue={provider?.name || ''} label={t('name')} name="name" tooltip={t('tooltipLocalProviderName')} />
        <SelectField defaultValue={provider?.apiType || 'Ollama'} label={t('apiType')} name="apiType" options={localProviderApiTypeOptions} tooltip={t('tooltipLocalProviderApiType')} />
        <Field defaultValue={provider?.endpoint || ''} label={t('endpoint')} name="endpoint" tooltip={t('tooltipLocalProviderEndpoint')} />
        <Field defaultValue={provider?.timeoutMs || 300000} label={t('timeoutMs')} name="timeoutMs" tooltip={t('tooltipLocalProviderTimeout')} type="number" />
        <Field defaultValue="" label={t('credential')} name="credential" tooltip={t('tooltipLocalProviderCredential')} />
        <Toggle checked={false} label={t('clearCredential')} name="clearCredential" tooltip={t('tooltipLocalProviderClearCredential')} />
        <Toggle checked={provider?.allowModelManagement} label={t('serverCanManageModels')} name="allowModelManagement" tooltip={t('tooltipLocalProviderModelManagement')} />
        <Toggle checked={provider ? provider.shareWithNetwork !== false : true} label={t('shareWithNetwork')} name="shareWithNetwork" tooltip={t('tooltipShareWithNetwork')} />
        <Toggle checked={provider ? provider.routeLocalRequests !== false : true} label={t('routeLocalRequests')} name="routeLocalRequests" tooltip={t('tooltipRouteLocalRequests')} />
        <Field defaultValue={provider?.maxConcurrentRequests ?? ''} label={t('maxConcurrentRequests')} name="maxConcurrentRequests" tooltip={t('tooltipProviderMaxConcurrentRequests')} type="number" />
      </ManagedForm>
    ),
    actions: <SaveActions allowAnother={!provider} formId="provider-form" tooltip={t('tooltipSaveLocalProvider')} />
  };
}

function modelModal(t, model, providers, refresh, setModal) {
  async function save(form) {
    const name = form.get('name');
    await localApi(model ? `/api/v1/models/${encodeURIComponent(model.provider)}/${encodeURIComponent(model.name)}` : '/api/v1/models', {
      method: model ? 'PUT' : 'POST',
      body: jsonBody({
        provider: model ? model.provider : form.get('provider'),
        name,
        supportsCompletions: form.get('supportsCompletions') === 'on',
        supportsEmbeddings: form.get('supportsEmbeddings') === 'on',
        maxConcurrentRequests: form.get('maxConcurrentRequests') ? Number(form.get('maxConcurrentRequests')) : null
      })
    });
    refresh();
    return name;
  }

  return {
    title: model ? t('editModel') : t('createModel'),
    json: model || {},
    body: (
      <ManagedForm clearOnAnother={['name', 'maxConcurrentRequests']} id="model-form" onClose={() => setModal(null)} onSave={save}>
        <label className="form-field" title={t('tooltipLocalModelProvider')}>
          <span>{t('provider')}</span>
          <select defaultValue={model?.provider || providers[0]?.name || ''} disabled={!!model} name="provider" title={t('tooltipLocalModelProvider')}>
            {providers.map((provider) => <option key={provider.name} value={provider.name}>{provider.name}</option>)}
          </select>
        </label>
        <Field defaultValue={model?.name || ''} label={t('model')} name="name" tooltip={t('tooltipLocalModelName')} />
        <Toggle checked={model ? model.supportsCompletions : true} label={t('supportsCompletions')} name="supportsCompletions" tooltip={t('tooltipLocalModelCompletions')} />
        <Toggle checked={model ? model.supportsEmbeddings : false} label={t('supportsEmbeddings')} name="supportsEmbeddings" tooltip={t('tooltipLocalModelEmbeddings')} />
        <Field defaultValue={model?.maxConcurrentRequests ?? ''} label={t('maxConcurrentRequests')} name="maxConcurrentRequests" tooltip={t('tooltipModelMaxConcurrentRequests')} type="number" />
      </ManagedForm>
    ),
    actions: <SaveActions allowAnother={!model} formId="model-form" tooltip={t('tooltipSaveLocalModel')} />
  };
}

function importModelsModal(t, providers, refresh) {
  return {
    title: t('importModels'),
    size: 'full',
    body: <ImportModelsDialog formId="import-models-form" onImported={() => refresh()} providers={providers} />,
    actions: <button className="primary-button" form="import-models-form" title={t('tooltipImportSelectedModels')} type="submit"><Download size={16} />{t('importSelected')}</button>
  };
}

function providerModelsModal(t, provider, refresh) {
  return {
    title: t('manageModelsTitle', { name: provider.name }),
    body: <ProviderModelsDialog onChanged={refresh} provider={provider} />
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

export function DataTable({ actions, columns, compact = false, defaultColumns, headerActions, onRefresh, onRowClick, rowActionLabel, rows, tableId, title: tableTitle }) {
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
  const columnVisibility = useColumnVisibility(columns, compact ? columns : defaultColumns, tableId);
  const shownColumns = compact ? columns : columns.filter((column) => columnVisibility.visibleKeys.includes(column));

  function applyFilter(column) {
    setFilters({ ...filters, [column]: draftFilters[column] || '' });
    setPage(1);
  }

  function updateDraftFilter(column, filterValue) {
    setDraftFilters({ ...draftFilters, [column]: filterValue });
    setFilters({ ...filters, [column]: filterValue });
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
      {!compact ? <Pagination columnControl={<ColumnPicker columns={columns.map((column) => ({ key: column, label: title(column, t) }))} onChange={columnVisibility.setVisibleKeys} onReset={columnVisibility.resetVisibleKeys} visibleKeys={columnVisibility.visibleKeys} />} currentPage={safePage} onPageChange={setPage} onPageSizeChange={setPageSize} onRefresh={onRefresh} pageSize={pageSize} totalItems={filteredRows.length} /> : null}
      <div className="table-scroll">
        <table className="data-table">
          <thead>
            <tr>{shownColumns.map((column) => <th className={columnClass(column)} key={column}>{title(column, t)}</th>)}{!compact ? <th className="actions-cell">{t('actions')}</th> : null}</tr>
            {!compact ? (
              <tr className="table-filter-row">
                {shownColumns.map((column) => (
                  <th className={columnClass(column)} key={column}>
                    <input
                      onBlur={() => applyFilter(column)}
                      onChange={(event) => updateDraftFilter(column, event.target.value)}
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
                <td className="empty-cell" colSpan={shownColumns.length + (compact ? 0 : 1)}>
                  <EmptyState message={t('emptyTableMessage')} title={t('noData')} />
                </td>
              </tr>
            ) : pageRows.map((row) => (
              <tr className={onRowClick ? 'clickable-row' : ''} key={row.id || `${row.provider || ''}:${row.name || row.modelName || row.endpoint}`} onClick={(event) => handleRowClick(event, row)}>
                {shownColumns.map((column) => <td className={columnClass(column)} key={column}>{copyColumn(column, row[column]) ? <CopyableValue label={t('copyValue')} value={String(row[column] || '')} /> : display(row[column], t)}</td>)}
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
      <RecordTable fields={['id', 'tenantId', 'requestorTenantId', 'requestorPrincipalType', 'requestorPrincipalId', 'clientId', 'requestorIp', 'providerName', 'apiType', 'modelName', 'outcome', 'statusCode']} record={entry} />
      <h3>{t('usageChartRequestTiming')}</h3>
      <TimingBars stages={servedRequestStages(entry.telemetry)} />
      {['requestHeaders', 'requestBody', 'responseHeaders', 'responseBody', 'telemetry', 'timeline'].map((field) => (
        <details key={field} open>
          <summary>{title(field, t)} <CopyButton label={t('copyValue')} value={typeof entry[field] === 'string' ? entry[field] : JSON.stringify(entry[field] || {}, null, 2)} /></summary>
          <pre className="json-preview"><code>{typeof entry[field] === 'string' ? entry[field] : JSON.stringify(entry[field] || {}, null, 2)}</code></pre>
        </details>
      ))}
    </div>
  );
}

function TelemetryDetails({ detail }) {
  const { t } = useTranslation();
  const sample = detail?.sample || {};
  return (
    <div className="details-stack">
      <RecordTable fields={Object.keys(sample)} record={sample} />
      <DataTable columns={['uuid', 'name', 'utilizationPercent', 'memoryUtilizationPercent', 'temperatureCelsius', 'powerWatts']} compact rows={detail?.gpus || []} />
      <details open>
        <summary>{t('snapshotJson')} <CopyButton label={t('copyValue')} value={detail?.snapshotJson || ''} /></summary>
        <pre className="json-preview"><code>{detail?.snapshotJson || ''}</code></pre>
      </details>
    </div>
  );
}

function localRows(input) {
  if (Array.isArray(input)) return input;
  if (!input || typeof input !== 'object') return [];

  const keys = ['data', 'value', 'items', 'rows', 'entries', 'records', 'samples', 'buckets', 'hostTelemetry', 'providerTelemetry'];
  for (const key of keys) {
    if (Array.isArray(input[key])) return input[key];
  }

  return [];
}

export function Panel({ actions, children, className = '', title: panelTitle }) {
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

export function Metric({ icon, label, subtext, tone = 'neutral', value }) {
  // Words (a routing mode, Yes/No) get a smaller type size than numbers so they fit on one or two lines.
  const textValue = typeof value === 'string' && !/^[-+]?[\d.,\s/%:]*$/.test(value);
  const valueTitle = typeof value === 'string' || typeof value === 'number' ? String(value) : undefined;
  return (
    <div className={`metric-card ${tone}`}>
      <div className="metric-icon">{icon}</div>
      <div className="metric-copy">
        <span title={label}>{label}</span>
        <strong className={textValue ? 'metric-text-value' : undefined} title={valueTitle}>{value}</strong>
        {subtext ? <small title={typeof subtext === 'string' ? subtext : undefined}>{subtext}</small> : null}
      </div>
    </div>
  );
}

// Four KPI cards per row, two by two, or one column, chosen from the width the grid actually has, so a row of four
// never leaves a single card on its own line.
export function KpiGrid({ children }) {
  return (
    <div className="kpi-strip">
      <div className="metric-grid kpi-grid">{children}</div>
    </div>
  );
}

function formatCount(value, t) {
  if (typeof value === 'number' && Number.isFinite(value)) return value.toLocaleString();
  return display(value, t);
}

function camelCaseOnly(value) {
  return Object.fromEntries(Object.entries(value || {}).filter(([key]) => key && key[0] === key[0].toLowerCase()));
}

// MCP servers from the settings JSON editor: an array of { name, transport, command, arguments, url, ... }.
function parseMcpServers(text, t) {
  let value;
  try {
    value = JSON.parse(String(text || '').trim() || '[]');
  } catch (err) {
    throw new Error(t('chatMcpServersInvalid', { message: err.message }));
  }
  if (!Array.isArray(value) || value.some((item) => !item || typeof item !== 'object' || !String(item.name || '').trim())) throw new Error(t('chatMcpServersInvalid', { message: t('chatMcpServersShape') }));
  return value;
}

// Connection state of each external MCP server, with a button to connect again after a settings change.
function McpServerStatusList() {
  const { t } = useTranslation();
  const [servers, setServers] = useState(null);
  const [busy, setBusy] = useState(false);

  async function load(refresh) {
    setBusy(true);
    try {
      const value = await localApi('/api/v1/chat/mcp-servers', refresh ? { method: 'POST' } : undefined);
      setServers(Array.isArray(value) ? value : []);
    } catch {
      setServers([]);
    } finally {
      setBusy(false);
    }
  }

  useEffect(() => {
    load(false);
  }, []);

  return (
    <div className="span-full mcp-server-status">
      <div className="button-row">
        <button className="action-button" disabled={busy} onClick={() => load(true)} title={t('tooltipChatMcpReconnect')} type="button">{t('chatMcpReconnect')}</button>
      </div>
      {servers && servers.length ? (
        <table className="data-table compact-record">
          <tbody>
            {servers.map((server) => (
              <tr key={server.name}>
                <th>{server.name} <span className="chat-muted">({server.transport})</span></th>
                <td>{!server.enabled ? t('chatMcpDisabled') : server.connected ? t('chatMcpConnected', { count: server.toolCount }) : (server.error || t('chatMcpNotConnected'))}</td>
              </tr>
            ))}
          </tbody>
        </table>
      ) : <div className="chat-muted">{t('chatMcpNoServers')}</div>}
    </div>
  );
}

function Field({ defaultValue, label, name, tooltip, type = 'text' }) {
  return <label className="form-field" title={tooltip}><span>{label}</span><input defaultValue={defaultValue || ''} name={name} title={tooltip} type={type} /></label>;
}

function SelectField({ defaultValue, label, name, options, tooltip }) {
  const rows = Array.isArray(options) ? options : [];
  const current = defaultValue || rows[0]?.value || '';
  const hasCurrent = rows.some((option) => option.value === current);
  return (
    <label className="form-field" title={tooltip}>
      <span>{label}</span>
      <select defaultValue={current} name={name} title={tooltip}>
        {!hasCurrent && current ? <option value={current}>{current}</option> : null}
        {rows.map((option) => <option key={option.value} value={option.value}>{option.label}</option>)}
      </select>
    </label>
  );
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

function sortLogLinesDescending(lines) {
  return [...(Array.isArray(lines) ? lines : [])].sort((left, right) => logLineTimestampMs(right) - logLineTimestampMs(left));
}

function uniqueSortedValues(values) {
  return [...new Set(values.map((item) => String(item || '').trim()).filter(Boolean))]
    .sort((left, right) => left.localeCompare(right));
}

function logLineTimestampMs(line) {
  const match = String(line || '').match(/^(\d{4}-\d{2}-\d{2}[ T]\d{2}:\d{2}:\d{2}(?:\.\d+)?Z?)/);
  if (!match) return Number.NEGATIVE_INFINITY;
  const normalized = match[1].includes('T') ? match[1] : match[1].replace(' ', 'T');
  const timestampText = normalized.endsWith('Z') ? normalized : `${normalized}Z`;
  const timestamp = Date.parse(timestampText);
  return Number.isFinite(timestamp) ? timestamp : Number.NEGATIVE_INFINITY;
}

function telemetryBucketMinutes(range) {
  if (range === 'lastHour') return 1;
  if (range === 'lastDay') return 15;
  if (range === 'lastWeek') return 120;
  return 360;
}

function telemetryMetricField(metric) {
  if (metric === 'MemoryUtilizationPercent') return 'memoryUtilizationPercent';
  if (metric === 'DiskUtilizationPercent') return 'diskUtilizationPercent';
  if (metric === 'GpuUtilizationPercent') return 'gpuAverageUtilizationPercent';
  if (metric === 'GpuMemoryUtilizationPercent') return 'gpuAverageMemoryUtilizationPercent';
  if (metric === 'GpuTemperatureCelsius') return 'gpuAverageTemperatureCelsius';
  if (metric === 'GpuPowerWatts') return 'gpuAveragePowerWatts';
  return 'cpuUtilizationPercent';
}

function providerTelemetryMetricField(metric) {
  if (metric === 'QueuedRequests') return 'queuedRequests';
  if (metric === 'WaitingRequests') return 'waitingRequests';
  if (metric === 'RequestRatePerSecond') return 'requestRatePerSecond';
  if (metric === 'ErrorRatePerSecond') return 'errorRatePerSecond';
  if (metric === 'PromptTokensPerSecond') return 'promptTokensPerSecond';
  if (metric === 'GenerationTokensPerSecond') return 'generationTokensPerSecond';
  if (metric === 'TotalTokensPerSecond') return 'totalTokensPerSecond';
  if (metric === 'LatencyP95Ms') return 'latencyP95Ms';
  if (metric === 'TimeToFirstTokenP95Ms') return 'timeToFirstTokenP95Ms';
  if (metric === 'CacheUsagePercent') return 'cacheUsagePercent';
  if (metric === 'LoadedModelCount') return 'loadedModelCount';
  if (metric === 'AvailableModelCount') return 'availableModelCount';
  return 'activeRequests';
}

function formatPercent(value) {
  if (value === null || value === undefined || value === '') return '-';
  return `${Number(value).toFixed(1)}%`;
}

function formatMetric(value) {
  if (value === null || value === undefined || Number.isNaN(Number(value))) return '-';
  return Number(value).toFixed(2);
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
  const mapped = {
    allowModelManagement: 'modelManagement',
    modelManagementAllowed: 'modelManagement',
    modelName: 'model',
    providerName: 'provider',
    providerKind: 'providerKind',
    statusCode: 'status'
  };
  if (mapped[key]) return t(mapped[key]);
  if (key.toLowerCase().includes('percent') || key.toLowerCase().includes('average') || key.toLowerCase().includes('utilization') || key.toLowerCase().includes('temperature')) {
    return humanizeLocalPropertyName(key)
      .replace(/Utilization/g, 'Util')
      .replace(/Temperature/g, 'Temp')
      .replace(/Average/g, 'Avg')
      .replace(/Percent/g, '%')
      .replace(/\s+/g, ' ')
      .trim();
  }

  if (typeof t === 'function') {
    const translated = t(key);
    if (translated !== key) return translated;
  }

  return humanizeLocalPropertyName(key);
}

function humanizeLocalPropertyName(key) {
  const normalized = String(key || '')
    .replace(/[_-]+/g, ' ')
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/([A-Z]+)([A-Z][a-z])/g, '$1 $2')
    .replace(/\s+/g, ' ')
    .trim();
  if (!normalized) return '';

  return normalized
    .split(' ')
    .map((part) => humanizeLocalPropertyWord(part))
    .join(' ');
}

function humanizeLocalPropertyWord(word) {
  const lower = String(word || '').toLowerCase();
  const acronyms = {
    api: 'API',
    cpu: 'CPU',
    db: 'DB',
    gpu: 'GPU',
    guid: 'GUID',
    html: 'HTML',
    http: 'HTTP',
    id: 'ID',
    ip: 'IP',
    json: 'JSON',
    ms: 'ms',
    p50: 'P50',
    p90: 'P90',
    p95: 'P95',
    p99: 'P99',
    sql: 'SQL',
    ssl: 'SSL',
    tls: 'TLS',
    toc: 'TOC',
    uri: 'URI',
    url: 'URL',
    utc: 'UTC',
    uuid: 'UUID',
    ws: 'WS'
  };
  if (acronyms[lower]) return acronyms[lower];
  if (/^p\d+$/i.test(word)) return word.toUpperCase();
  return lower.charAt(0).toUpperCase() + lower.slice(1);
}

function columnClass(column) {
  return `column-${String(column || '').replace(/[^a-z0-9_-]/gi, '')}`;
}

function localTooltipForElement(element, t) {
  const label = localElementLabel(element, t);
  const tagName = element.tagName.toLowerCase();
  if (tagName === 'a') return t('tooltipGenericLink', { label });
  if (tagName === 'select') return t('tooltipGenericSelect', { label });
  if (tagName === 'textarea') return t('tooltipGenericTextarea', { label });
  if (tagName === 'input') return t('tooltipGenericInput', { label });
  if (tagName === 'button') return t('tooltipGenericButton', { label });
  return t('tooltipGenericControl', { label });
}

function localElementLabel(element, t) {
  const explicit = element.getAttribute('aria-label') || element.getAttribute('placeholder') || element.getAttribute('data-tooltip-label');
  if (explicit) return explicit.trim();

  const text = element.textContent?.replace(/\s+/g, ' ').trim();
  if (text) return text;

  const name = element.getAttribute('name') || element.getAttribute('id') || element.getAttribute('value');
  if (name) return title(name, t);

  return t('control');
}

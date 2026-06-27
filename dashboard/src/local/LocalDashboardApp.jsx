import { useEffect, useMemo, useRef, useState } from 'react';
import {
  Activity,
  BrainCircuit,
  ChevronRight,
  CircleAlert,
  Cpu,
  FileJson,
  Gauge,
  HardDrive,
  History,
  LineChart,
  MemoryStick,
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
  { id: 'telemetry', descriptionKey: 'localTelemetryDescription', icon: Gauge, labelKey: 'telemetry', tooltipKey: 'tooltipLocalNavTelemetry' },
  { id: 'telemetryAnalytics', descriptionKey: 'localTelemetryAnalyticsDescription', icon: LineChart, labelKey: 'telemetryAnalytics', tooltipKey: 'tooltipLocalNavTelemetryAnalytics' },
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

const chartRefreshIntervals = [0, 15, 30, 60, 180, 300];

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
  const [telemetryLatest, setTelemetryLatest] = useState(null);
  const [telemetrySamples, setTelemetrySamples] = useState([]);
  const [telemetryRollups, setTelemetryRollups] = useState([]);
  const [telemetryMetric, setTelemetryMetric] = useState('CpuUtilizationPercent');
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
  }, [page, t, providers, models, requests, telemetrySamples, logs, modal]);

  async function refresh() {
    try {
      setError('');
      if (page === 'home') {
        const [stateResponse, statisticsResponse, summaryResponse] = await Promise.all([
          localApi('/api/v1/state'),
          localApi('/api/v1/statistics'),
          localApi(`/api/v1/requests/summary?range=${encodeURIComponent(localApiRange(range))}&bucketMinutes=${telemetryBucketMinutes(range)}`)
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
          localApi(`/api/v1/requests/summary?range=${encodeURIComponent(localApiRange(range))}&bucketMinutes=${telemetryBucketMinutes(range)}`)
        ]);
        setRequests(historyResponse.entries || []);
        setSummary(summaryResponse.buckets || []);
      } else if (page === 'telemetry') {
        const [latestResponse, rollupResponse] = await Promise.all([
          localApi('/api/v1/telemetry/latest'),
          localApi(`/api/v1/telemetry/history/rollups?range=${encodeURIComponent(localApiRange(range))}`, {
            method: 'POST',
            body: jsonBody({ metric: telemetryMetric, bucketMinutes: telemetryBucketMinutes(range) })
          })
        ]);
        setTelemetryLatest(latestResponse);
        setTelemetryRollups(rollupResponse || []);
      } else if (page === 'telemetryAnalytics') {
        const [searchResponse, rollupResponse] = await Promise.all([
          localApi(`/api/v1/telemetry/history/search?range=${encodeURIComponent(localApiRange(range))}`, {
            method: 'POST',
            body: jsonBody({ metric: telemetryMetric, limit: 250 })
          }),
          localApi(`/api/v1/telemetry/history/rollups?range=${encodeURIComponent(localApiRange(range))}`, {
            method: 'POST',
            body: jsonBody({ metric: telemetryMetric, bucketMinutes: telemetryBucketMinutes(range) })
          })
        ]);
        setTelemetrySamples(searchResponse.samples || []);
        setTelemetryRollups(rollupResponse || []);
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
            <div className="sidebar-group-label">{t('administration')}</div>
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
          {page === 'telemetry' ? <Telemetry latest={telemetryLatest} metric={telemetryMetric} range={range} refresh={refresh} rollups={telemetryRollups} setMetric={setTelemetryMetric} setRange={setRange} setModal={setModal} /> : null}
          {page === 'logs' ? <Logs logSeverity={logSeverity} logTail={logTail} logs={logs} setLogSeverity={setLogSeverity} setLogTail={setLogTail} /> : null}
          {page === 'telemetryAnalytics' ? <TelemetryAnalytics metric={telemetryMetric} range={range} refresh={refresh} rollups={telemetryRollups} samples={telemetrySamples} setMetric={setTelemetryMetric} setModal={setModal} setRange={setRange} /> : null}
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
        <ActivityChart onRangeChange={setRange} onRefresh={refresh} range={range} summary={{ buckets: summary }} />
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

function Telemetry({ latest, metric, range, refresh, rollups, setMetric, setRange, setModal }) {
  const { t } = useTranslation();
  const sample = latest?.detail?.sample || {};
  const snapshot = latest?.snapshot || {};
  const gpus = latest?.detail?.gpus || snapshot?.gpu?.devices || [];

  async function collect() {
    const response = await localApi('/api/v1/telemetry/collect', { method: 'POST', body: '{}' });
    setModal(messageModal(t, t('telemetryCollected'), t('telemetryCollectedMessage'), response));
    refresh();
  }

  return (
    <>
      <div className="metric-grid">
        <Metric icon={<Cpu size={18} />} label={t('cpuUtilization')} value={formatPercent(sample.cpuUtilizationPercent)} />
        <Metric icon={<MemoryStick size={18} />} label={t('memoryUtilization')} value={formatPercent(sample.memoryUtilizationPercent)} />
        <Metric icon={<HardDrive size={18} />} label={t('diskUtilization')} value={formatPercent(sample.diskUtilizationPercent)} />
        <Metric icon={<Gauge size={18} />} label={t('gpuUtilization')} value={formatPercent(sample.gpuAverageUtilizationPercent)} />
      </div>
      <Panel
        className="chart-panel-card"
        title={t('telemetryOverTime')}
        actions={(
          <div className="chart-action-row">
            <TelemetryMetricSelect metric={metric} setMetric={setMetric} />
            <button className="icon-button" onClick={collect} title={t('tooltipCollectTelemetry')} type="button"><Zap size={16} /></button>
          </div>
        )}
      >
        <MetricChart onRangeChange={setRange} onRefresh={refresh} range={range} rollups={rollups} />
      </Panel>
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

function TelemetryAnalytics({ metric, range, refresh, rollups, samples, setMetric, setModal, setRange }) {
  const { t } = useTranslation();

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

  const metricValues = samples.map((sample) => Number(sample[telemetryMetricField(metric)] || 0)).filter((value) => Number.isFinite(value));
  const average = metricValues.length ? metricValues.reduce((sum, value) => sum + value, 0) / metricValues.length : null;
  const maximum = metricValues.length ? Math.max(...metricValues) : null;

  return (
    <>
      <div className="chart-controls page-level-controls split" aria-label={t('chartRange')}>
        <div className="time-range-button-row">
          {['lastHour', 'lastDay', 'lastWeek', 'lastMonth'].map((item) => (
            <button className={item === range ? 'active' : ''} key={item} onClick={() => setRange(item)} title={t('tooltipChartRangeOption', { range: t(item) })} type="button">{t(item)}</button>
          ))}
        </div>
        <TelemetryMetricSelect metric={metric} setMetric={setMetric} />
      </div>
      <div className="metric-grid">
        <Metric icon={<History size={18} />} label={t('samples')} value={samples.length} />
        <Metric icon={<Gauge size={18} />} label={t('average')} value={formatMetric(average)} />
        <Metric icon={<Gauge size={18} />} label={t('maximum')} value={formatMetric(maximum)} />
        <Metric icon={<Activity size={18} />} label={t('metric')} value={t(metric)} />
      </div>
      <Panel className="chart-panel-card" title={t('telemetryOverTime')}>
        <MetricChart onRangeChange={setRange} onRefresh={refresh} range={range} rollups={rollups} showControls={false} />
      </Panel>
      <DataTable
        actions={(row) => [
          { destructive: true, icon: <Trash2 size={14} />, key: 'delete', label: t('delete'), onSelect: () => remove(row), tooltip: t('tooltipDeleteTelemetrySample') }
        ]}
        columns={['id', 'collectedUtc', 'cpuUtilizationPercent', 'memoryUtilizationPercent', 'diskUtilizationPercent', 'gpuAverageUtilizationPercent', 'gpuAverageTemperatureCelsius']}
        onRefresh={refresh}
        onRowClick={details}
        rows={samples}
        title={t('telemetrySamples')}
      />
    </>
  );
}

function TelemetryMetricSelect({ metric, setMetric }) {
  const { t } = useTranslation();
  const metrics = ['CpuUtilizationPercent', 'MemoryUtilizationPercent', 'DiskUtilizationPercent', 'GpuUtilizationPercent', 'GpuMemoryUtilizationPercent', 'GpuTemperatureCelsius', 'GpuPowerWatts'];
  return (
    <label className="form-field inline compact-field" title={t('tooltipTelemetryMetric')}>
      <span>{t('metric')}</span>
      <select value={metric} onChange={(event) => setMetric(event.target.value)} title={t('tooltipTelemetryMetric')}>
        {metrics.map((item) => <option key={item} value={item}>{t(item)}</option>)}
      </select>
    </label>
  );
}

function Logs({ logSeverity, logTail, logs, setLogSeverity, setLogTail }) {
  const { t } = useTranslation();
  return (
    <Panel
      className="client-logs-card"
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
    const settings = state?.settings?.localDashboard || {};
    const telemetry = state?.settings?.telemetry || {};
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
          }
        }
      })
    });
    refresh();
  }

  const settings = state?.settings?.localDashboard || {};
  const telemetry = state?.settings?.telemetry || {};
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
            <tr>{columns.map((column) => <th className={columnClass(column)} key={column}>{title(column, t)}</th>)}{!compact ? <th className="actions-cell">{t('actions')}</th> : null}</tr>
            {!compact ? (
              <tr className="table-filter-row">
                {columns.map((column) => (
                  <th className={columnClass(column)} key={column}>
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
                {columns.map((column) => <td className={columnClass(column)} key={column}>{copyColumn(column, row[column]) ? <CopyableValue label={t('copyValue')} value={String(row[column] || '')} /> : display(row[column], t)}</td>)}
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

function MetricChart({ onRangeChange, onRefresh, range, rollups, showControls = true }) {
  const { t } = useTranslation();
  const canvasRef = useRef(null);
  const refreshRef = useRef(onRefresh);
  const [chartWidth, setChartWidth] = useState(760);
  const [autoRefreshSeconds, setAutoRefreshSeconds] = useState(15);
  const [hovered, setHovered] = useState(null);
  const buckets = useMemo(() => buildMetricBuckets(rollups, range), [range, rollups]);
  const max = Math.max(1, ...buckets.map((bucket) => Number(bucket.maximum ?? bucket.average ?? 0)));
  const height = 320;
  const left = 58;
  const right = 28;
  const top = 28;
  const bottom = 52;
  const width = chartWidth;
  const plotWidth = width - left - right;
  const plotHeight = height - top - bottom;
  const baseY = top + plotHeight;
  const slot = plotWidth / Math.max(1, buckets.length);
  const barWidth = Math.max(2, Math.min(14, slot - 2));
  const yTicks = buildMetricYAxisTicks(max);

  useEffect(() => {
    refreshRef.current = onRefresh;
  }, [onRefresh]);

  useEffect(() => {
    if (!autoRefreshSeconds || !refreshRef.current) return undefined;
    const interval = window.setInterval(() => {
      refreshRef.current?.();
    }, autoRefreshSeconds * 1000);
    return () => window.clearInterval(interval);
  }, [autoRefreshSeconds]);

  useEffect(() => {
    const element = canvasRef.current;
    if (!element) return undefined;

    function updateWidth() {
      setChartWidth(Math.max(360, Math.round(element.clientWidth || 760)));
    }

    updateWidth();
    const observer = new ResizeObserver(updateWidth);
    observer.observe(element);
    return () => observer.disconnect();
  }, []);

  return (
    <div className="activity-chart-panel">
      {showControls || onRefresh ? (
        <div className="chart-controls" aria-label={t('chartRange')}>
          {showControls ? ['lastHour', 'lastDay', 'lastWeek', 'lastMonth'].map((item) => (
            <button className={item === range ? 'active' : ''} key={item} onClick={() => onRangeChange?.(item)} title={t('tooltipChartRangeOption', { range: t(item) })} type="button">{t(item)}</button>
          )) : null}
          <MetricChartRefresh
            autoRefreshSeconds={autoRefreshSeconds}
            disabled={!onRefresh}
            onRefresh={onRefresh}
            setAutoRefreshSeconds={setAutoRefreshSeconds}
            t={t}
          />
        </div>
      ) : null}
      <div className="chart-canvas" ref={canvasRef}>
        <svg className="activity-chart" viewBox={`0 0 ${width} ${height}`} role="img" aria-label={t('telemetry')}>
          <line x1={left} y1={top} x2={left} y2={baseY} stroke="var(--color-border)" />
          <line x1={left} y1={baseY} x2={left + plotWidth} y2={baseY} stroke="var(--color-border)" />
          {yTicks.map((tick) => {
            const y = baseY - (tick / max) * plotHeight;
            return <g key={tick}><line x1={left} y1={y} x2={left + plotWidth} y2={y} stroke="var(--color-border)" strokeDasharray="4 6" /><text className="axis-label" x={left - 10} y={y + 4} textAnchor="end">{formatMetric(tick)}</text></g>;
          })}
          <text className="axis-title" transform={`translate(16 ${top + plotHeight / 2}) rotate(-90)`}>{t('metric')}</text>
          <text className="axis-title" x={left + plotWidth / 2} y={height - 12} textAnchor="middle">{t('time')}</text>
          {buckets.map((bucket, index) => {
            const value = Number(bucket.average ?? 0);
            const x = left + index * slot + (slot - barWidth) / 2;
            const barHeight = (value / max) * plotHeight;
            const y = baseY - barHeight;
            const showTick = shouldShowChartTick(index, buckets.length);
            return (
              <g key={`${bucket.label}-${index}`} onMouseLeave={() => setHovered(null)} onMouseMove={(event) => setHovered({ bucket, ...tooltipFromPointer(event, canvasRef.current) })}>
                <rect x={x} y={top} width={barWidth} height={plotHeight} fill="transparent" />
                <rect x={x} y={y} width={barWidth} height={barHeight} rx="3" fill="var(--color-accent)" />
                {showTick ? <text className="axis-label" x={x + barWidth / 2} y={baseY + 18} textAnchor="middle">{bucket.label}</text> : null}
              </g>
            );
          })}
        </svg>
        {hovered ? (
          <div className="chart-tooltip" style={{ left: `${hovered.x}px`, top: `${hovered.y}px`, transform: hovered.transform }}>
            <strong>{hovered.bucket.label}</strong>
            <span>{t('average')}: {formatMetric(hovered.bucket.average)}</span>
            <span>{t('minimum')}: {formatMetric(hovered.bucket.minimum)}</span>
            <span>{t('maximum')}: {formatMetric(hovered.bucket.maximum)}</span>
            <span>{t('samples')}: {hovered.bucket.count}</span>
          </div>
        ) : null}
      </div>
    </div>
  );
}

function MetricChartRefresh({ autoRefreshSeconds, disabled, onRefresh, setAutoRefreshSeconds, t }) {
  if (!onRefresh) return null;
  return (
    <label className="chart-refresh" title={t('tooltipAutoRefresh')}>
      <span>{t('autoRefresh')}</span>
      <select
        disabled={disabled}
        value={autoRefreshSeconds}
        onChange={(event) => setAutoRefreshSeconds(Number(event.target.value))}
        title={t('tooltipAutoRefresh')}
      >
        {chartRefreshIntervals.map((seconds) => (
          <option key={seconds} value={seconds}>{seconds === 0 ? t('none') : `${seconds}s`}</option>
        ))}
      </select>
      <button className="pagination-button" disabled={disabled} onClick={onRefresh} title={t('tooltipManualRefresh')} type="button">
        <RefreshCw size={14} />
      </button>
    </label>
  );
}

function buildMetricBuckets(rollups, range) {
  const desiredCount = desiredChartBuckets(range);
  const raw = Array.isArray(rollups) ? rollups.slice(-desiredCount) : [];
  const normalized = raw.map((bucket, index) => ({
    average: Number(bucket.average ?? bucket.Average ?? 0),
    count: Number(bucket.count ?? bucket.Count ?? 0),
    label: String(bucket.label ?? bucket.Label ?? fallbackMetricLabel(range, Math.max(0, desiredCount - raw.length + index), desiredCount)),
    maximum: Number(bucket.maximum ?? bucket.Maximum ?? bucket.average ?? bucket.Average ?? 0),
    minimum: Number(bucket.minimum ?? bucket.Minimum ?? bucket.average ?? bucket.Average ?? 0)
  }));
  const missing = Math.max(0, desiredCount - normalized.length);
  const padding = Array.from({ length: missing }, (_, index) => ({
    average: 0,
    count: 0,
    label: fallbackMetricLabel(range, index, desiredCount),
    maximum: 0,
    minimum: 0
  }));
  return [...padding, ...normalized].slice(-desiredCount);
}

function desiredChartBuckets(range) {
  if (range === 'lastHour') return 60;
  if (range === 'lastDay') return 96;
  if (range === 'lastWeek') return 84;
  if (range === 'lastMonth') return 90;
  return 96;
}

function fallbackMetricLabel(range, index, count) {
  if (range === 'lastHour') return `${Math.max(0, count - index - 1)}m`;
  if (range === 'lastDay') return `${Math.max(0, Math.floor(index / 4))}h`;
  if (range === 'lastWeek') return `D${Math.max(1, Math.floor(index / 12) + 1)}`;
  if (range === 'lastMonth') return `D${Math.max(1, Math.floor(index / 3) + 1)}`;
  return String(index + 1);
}

function buildMetricYAxisTicks(max) {
  if (!Number.isFinite(max) || max <= 1) return [1, 0];
  const tickCount = Math.min(5, Math.max(2, Math.ceil(max) + 1));
  const step = Math.max(1, Math.ceil(max / (tickCount - 1)));
  const ticks = [];
  for (let tick = 0; tick <= max; tick += step) {
    ticks.push(tick);
  }
  if (!ticks.includes(max)) ticks.push(max);
  return [...new Set(ticks)].sort((left, right) => right - left).slice(0, 5);
}

function shouldShowChartTick(index, count) {
  const step = Math.max(1, Math.ceil(count / 8));
  return index % step === 0 || index === count - 1;
}

function tooltipFromPointer(event, element) {
  if (!element) return { x: 0, y: 0, transform: 'translate(14px, -50%)' };
  const bounds = element.getBoundingClientRect();
  const tooltipWidth = 188;
  const tooltipHeight = 118;
  const x = event.clientX - bounds.left;
  const y = event.clientY - bounds.top;
  const clampedX = Math.min(Math.max(x, 18), Math.max(18, bounds.width - 18));
  const clampedY = Math.min(Math.max(y, 42), Math.max(42, bounds.height - 42));
  const nearRight = clampedX > bounds.width - tooltipWidth;
  const nearBottom = clampedY > bounds.height - tooltipHeight;
  const transform = `${nearRight ? 'translateX(calc(-100% - 14px))' : 'translateX(14px)'} ${nearBottom ? 'translateY(calc(-100% + 24px))' : 'translateY(-50%)'}`;
  return { x: clampedX, y: clampedY, transform };
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

function telemetryBucketMinutes(range) {
  if (range === 'lastHour') return 1;
  if (range === 'lastDay') return 15;
  if (range === 'lastWeek') return 120;
  return 480;
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
    modelName: 'model',
    providerName: 'provider',
    statusCode: 'status'
  };
  if (mapped[key]) return t(mapped[key]);

  if (typeof t === 'function') {
    const translated = t(key);
    if (translated !== key) return translated;
  }

  return key.replace(/([A-Z])/g, ' $1').replace(/^./, (letter) => letter.toUpperCase()).trim();
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

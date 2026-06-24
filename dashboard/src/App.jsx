import { useEffect, useState } from 'react';
import { BrowserRouter, NavLink, Route, Routes } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { Download, KeyRound, Play, RefreshCw, Shield, Trash2, Upload } from 'lucide-react';
import ApiClient from './utils/api.js';
import LanguageSelector from './components/LanguageSelector.jsx';
import ActivityChart from './components/ActivityChart.jsx';

const api = new ApiClient();

function Shell() {
  const { t } = useTranslation();
  const links = [
    ['/dashboard/home', 'home'],
    ['/dashboard/requests', 'requests'],
    ['/dashboard/explorer', 'explorer'],
    ['/dashboard/security', 'security'],
    ['/dashboard/clients', 'clients'],
    ['/dashboard/vmrs', 'vmrs'],
    ['/dashboard/policies', 'policies'],
    ['/dashboard/reservations', 'reservations'],
    ['/dashboard/analytics', 'analytics'],
    ['/dashboard/settings', 'settings']
  ];
  return (
    <div className="app-shell">
      <aside>
        <h1>{t('appTitle')}</h1>
        <nav>{links.map(([to, key]) => <NavLink key={to} to={to}>{t(key)}</NavLink>)}</nav>
      </aside>
      <main>
        <header><LanguageSelector /></header>
        <Routes>
          <Route path="/dashboard/home" element={<HomeView />} />
          <Route path="/dashboard/requests" element={<RequestHistoryView />} />
          <Route path="/dashboard/explorer" element={<ApiExplorerView />} />
          <Route path="/dashboard/security" element={<SecurityView />} />
          <Route path="/dashboard/clients" element={<ClientsView />} />
          <Route path="/dashboard/vmrs" element={<VmrsView />} />
          <Route path="/dashboard/policies" element={<PoliciesView />} />
          <Route path="/dashboard/reservations" element={<ReservationsView />} />
          <Route path="/dashboard/analytics" element={<AnalyticsView />} />
          <Route path="/dashboard/settings" element={<SettingsView />} />
          <Route path="*" element={<HomeView />} />
        </Routes>
      </main>
    </div>
  );
}

function LoginView() {
  const { t } = useTranslation();
  return (
    <div className="login-view">
      <h1>{t('appTitle')}</h1>
      <button onClick={() => { window.location.href = '/dashboard/home'; }}><Shield size={16} />{t('login')}</button>
      <LanguageSelector />
    </div>
  );
}

function HomeView() {
  const { t } = useTranslation();
  const analytics = useResource('/api/v1/analytics/overview', { total: 0, success: 0, failures: 0, failureRate: 0 });
  const clients = useResource('/api/v1/clients', []);
  const endpoints = useResource('/api/v1/modelrunnerendpoints', []);
  const connected = clients.data.filter((client) => value(client, 'connected') === true).length;
  return (
    <section>
      <div className="page-title">
        <h2>{t('home')}</h2>
        <IconButton onClick={() => { analytics.refresh(); clients.refresh(); endpoints.refresh(); }} label={t('refresh')} icon={<RefreshCw size={16} />} />
      </div>
      <div className="tiles">
        <Metric label={t('total')} value={analytics.data.total ?? 0} />
        <Metric label={t('success')} value={analytics.data.success ?? 0} />
        <Metric label={t('failed')} value={analytics.data.failures ?? 0} />
        <Metric label={t('connected')} value={connected} />
      </div>
      <ActivityChart />
      <DataTable title={t('endpoints')} rows={endpoints.data} columns={['name', 'provider', 'serviceState', 'inFlightRequests']} />
    </section>
  );
}

function RequestHistoryView() {
  const { t } = useTranslation();
  const history = useResource('/api/v1/requesthistory', []);
  const summary = useResource('/api/v1/requesthistory/summary', { buckets: [] });
  const [selected, setSelected] = useState(null);
  async function clearHistory() {
    await api.post('/api/v1/requesthistory/bulk', {});
    history.refresh();
    summary.refresh();
  }
  return (
    <section>
      <div className="page-title">
        <h2>{t('requests')}</h2>
        <div className="button-row">
          <IconButton onClick={history.refresh} label={t('refresh')} icon={<RefreshCw size={16} />} />
          <IconButton onClick={clearHistory} label={t('clear')} icon={<Trash2 size={16} />} />
        </div>
      </div>
      <ActivityChart data={summary.data.buckets || []} />
      <DataTable title={t('requests')} rows={history.data} columns={['statusCode', 'path', 'createdUtc']} onRowClick={setSelected} />
      {selected && <JsonPanel title={t('details')} data={selected} onClose={() => setSelected(null)} />}
    </section>
  );
}

function ApiExplorerView() {
  const { t } = useTranslation();
  const spec = useResource('/openapi.json', null);
  const [selectedPath, setSelectedPath] = useState('/v1/chat/completions');
  const [response, setResponse] = useState(null);
  const paths = Object.keys(spec.data?.paths || {});
  async function execute() {
    const data = selectedPath.includes('/embeddings')
      ? await api.post('/v1/embeddings', { model: 'text-embedding-3-small', input: 'hello' })
      : await api.post('/v1/chat/completions', { model: 'llama3.2:latest', messages: [{ role: 'user', content: 'hello' }] });
    setResponse(data);
  }
  return (
    <section>
      <div className="page-title"><h2>{t('explorer')}</h2><IconButton onClick={execute} label={t('execute')} icon={<Play size={16} />} /></div>
      <select value={selectedPath} onChange={(event) => setSelectedPath(event.target.value)}>
        {[selectedPath, ...paths.filter((path) => path !== selectedPath)].map((path) => <option key={path}>{path}</option>)}
      </select>
      <JsonBlock data={response || spec.data || { status: t('noData') }} />
    </section>
  );
}

function SecurityView() {
  const { t } = useTranslation();
  const tenants = useResource('/api/v1/tenants', []);
  const users = useResource('/api/v1/users', []);
  const credentials = useResource('/api/v1/credentials', []);
  const roles = useResource('/api/v1/roles', []);
  const permissions = useResource('/api/v1/permissions', []);
  const effective = useResource('/api/v1/effective-permissions', []);
  const audit = useResource('/api/v1/audit', []);
  async function createCredential() {
    await api.post('/api/v1/credentials', {});
    credentials.refresh();
  }
  return (
    <section>
      <div className="page-title"><h2>{t('security')}</h2><IconButton onClick={createCredential} label={t('createCredential')} icon={<KeyRound size={16} />} /></div>
      <Grid>
        <DataTable title={t('tenants')} rows={tenants.data} columns={['name', 'active']} />
        <DataTable title={t('users')} rows={users.data} columns={['email', 'isTenantAdmin', 'active']} />
        <DataTable title={t('credentials')} rows={credentials.data} columns={['accessKey', 'active']} />
        <DataTable title={t('roles')} rows={roles.data} columns={['name', 'builtIn', 'active']} />
        <DataTable title={t('permissions')} rows={permissions.data} columns={['resourceType', 'operationType', 'permit']} />
        <DataTable title={t('effectivePermissions')} rows={effective.data} columns={['resourceType', 'operationType', 'permit', 'sourceRole']} />
      </Grid>
      <DataTable title={t('audit')} rows={audit.data} columns={['createdUtc', 'resourceType', 'operationType', 'reasonCode', 'statusCode']} />
    </section>
  );
}

function ClientsView() {
  const { t } = useTranslation();
  const clients = useResource('/api/v1/clients', []);
  const endpoints = useResource('/api/v1/modelrunnerendpoints', []);
  const models = useResource('/api/v1/modeldefinitions', []);
  return (
    <section>
      <h2>{t('clients')}</h2>
      <Grid>
        <DataTable title={t('clients')} rows={clients.data} columns={['name', 'connected', 'lastHeartbeatUtc']} />
        <DataTable title={t('endpoints')} rows={endpoints.data} columns={['name', 'provider', 'serviceState', 'maxParallelRequests']} />
        <DataTable title={t('models')} rows={models.data} columns={['name', 'supportsCompletions', 'supportsEmbeddings']} />
      </Grid>
    </section>
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
    <section>
      <div className="page-title"><h2>{t('vmrs')}</h2><IconButton onClick={explain} label={t('explainRouting')} icon={<Play size={16} />} /></div>
      <div className="toolbar">
        <input value={loadModel} onChange={(event) => setLoadModel(event.target.value)} aria-label={t('model')} />
        <IconButton onClick={load} label={t('loadModel')} icon={<Upload size={16} />} />
      </div>
      <DataTable title={t('vmrs')} rows={vmrs.data} columns={['name', 'loadBalancingMode', 'strictMode', 'active']} />
      <Grid>
        <JsonPanelInline title={t('effectiveConfig')} data={effective.data} />
        <JsonPanelInline title={t('routingDecision')} data={routing || loadResult || {}} />
      </Grid>
    </section>
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
    <section>
      <div className="page-title"><h2>{t('policies')}</h2><IconButton onClick={evaluate} label={t('evaluate')} icon={<Play size={16} />} /></div>
      <Grid>
        <DataTable title={t('loadBalancingPolicies')} rows={loadPolicies.data} columns={['name', 'rankingMetric', 'preferLower', 'active']} />
        <DataTable title={t('modelAccessPolicies')} rows={accessPolicies.data} columns={['name', 'mode', 'defaultDecision', 'active']} />
        <JsonPanelInline title={t('metricsCatalog')} data={metrics.data} />
        <JsonPanelInline title={t('evaluation')} data={evaluation || {}} />
      </Grid>
    </section>
  );
}

function ReservationsView() {
  const { t } = useTranslation();
  const reservations = useResource('/api/v1/virtualmodelrunnerreservations', []);
  const effective = useResource('/api/v1/virtualmodelrunnerreservations/effective', {});
  return (
    <section>
      <h2>{t('reservations')}</h2>
      <Grid>
        <DataTable title={t('reservations')} rows={reservations.data} columns={['name', 'startUtc', 'endUtc', 'active']} />
        <JsonPanelInline title={t('effectiveAccess')} data={effective.data} />
      </Grid>
    </section>
  );
}

function AnalyticsView() {
  const { t } = useTranslation();
  const analytics = useResource('/api/v1/analytics/overview', {});
  const metrics = useTextResource('/metrics', '');
  return (
    <section>
      <div className="page-title"><h2>{t('analytics')}</h2><IconButton onClick={() => { analytics.refresh(); metrics.refresh(); }} label={t('refresh')} icon={<RefreshCw size={16} />} /></div>
      <Grid>
        <JsonPanelInline title={t('overview')} data={analytics.data} />
        <pre>{metrics.data}</pre>
      </Grid>
    </section>
  );
}

function SettingsView() {
  const { t } = useTranslation();
  const [backup, setBackup] = useState('');
  async function downloadBackup() {
    setBackup(JSON.stringify(await api.get('/api/v1/backup'), null, 2));
  }
  async function restoreBackup() {
    await api.post('/api/v1/restore', JSON.parse(backup || '{}'));
  }
  return (
    <section>
      <div className="page-title"><h2>{t('settings')}</h2><LanguageSelector /></div>
      <p>{t('serverUrl')}: {api.baseUrl}</p>
      <div className="button-row">
        <IconButton onClick={downloadBackup} label={t('backup')} icon={<Download size={16} />} />
        <IconButton onClick={restoreBackup} label={t('restore')} icon={<Upload size={16} />} />
      </div>
      <textarea value={backup} onChange={(event) => setBackup(event.target.value)} aria-label={t('backup')} />
    </section>
  );
}

function useResource(path, fallback) {
  const [data, setData] = useState(fallback);
  const [version, setVersion] = useState(0);
  useEffect(() => {
    let alive = true;
    api.get(path).then((result) => { if (alive) setData(result); }).catch(() => { if (alive) setData(fallback); });
    return () => { alive = false; };
  }, [path, version]);
  return { data, refresh: () => setVersion((value) => value + 1) };
}

function useTextResource(path, fallback) {
  const [data, setData] = useState(fallback);
  const [version, setVersion] = useState(0);
  useEffect(() => {
    let alive = true;
    api.text(path).then((result) => { if (alive) setData(result); }).catch(() => { if (alive) setData(fallback); });
    return () => { alive = false; };
  }, [path, version]);
  return { data, refresh: () => setVersion((value) => value + 1) };
}

function Metric({ label, value }) {
  return <span>{label}<strong>{value}</strong></span>;
}

function IconButton({ icon, label, onClick }) {
  return <button type="button" onClick={onClick} title={label}>{icon}{label}</button>;
}

function Grid({ children }) {
  return <div className="panel-grid">{children}</div>;
}

function DataTable({ title, rows, columns, onRowClick }) {
  const { t } = useTranslation();
  const safeRows = Array.isArray(rows) ? rows : [];
  return (
    <div className="table-block">
      <h3>{title}</h3>
      <table>
        <thead><tr>{columns.map((column) => <th key={column}>{t(column, column)}</th>)}</tr></thead>
        <tbody>
          {safeRows.length === 0 && <tr><td colSpan={columns.length}>{t('noData')}</td></tr>}
          {safeRows.map((row, index) => (
            <tr key={value(row, 'id') || index} onClick={() => onRowClick?.(row)}>
              {columns.map((column) => <td key={column}>{formatCell(value(row, column))}</td>)}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function JsonPanel({ title, data, onClose }) {
  const { t } = useTranslation();
  return (
    <div className="modal">
      <div className="modal-body">
        <div className="page-title"><h3>{title}</h3><button onClick={onClose}>{t('close')}</button></div>
        <JsonBlock data={data} />
      </div>
    </div>
  );
}

function JsonPanelInline({ title, data }) {
  return <div className="json-panel"><h3>{title}</h3><JsonBlock data={data} /></div>;
}

function JsonBlock({ data }) {
  return <pre>{JSON.stringify(data, null, 2)}</pre>;
}

function value(row, key) {
  if (!row) return undefined;
  const pascal = key.charAt(0).toUpperCase() + key.slice(1);
  return row[key] ?? row[pascal];
}

function formatCell(cell) {
  if (cell === undefined || cell === null) return '';
  if (Array.isArray(cell)) return cell.join(', ');
  if (typeof cell === 'object') return JSON.stringify(cell);
  return String(cell);
}

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<LoginView />} />
        <Route path="/dashboard/*" element={<Shell />} />
      </Routes>
    </BrowserRouter>
  );
}

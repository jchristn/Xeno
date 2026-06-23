import { useEffect, useMemo, useState } from 'react';
import { BrowserRouter, NavLink, Route, Routes } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { RefreshCw } from 'lucide-react';
import ApiClient from './utils/api.js';
import LanguageSelector from './components/LanguageSelector.jsx';
import ActivityChart from './components/ActivityChart.jsx';

function Shell() {
  const { t } = useTranslation();
  return (
    <div className="app-shell">
      <aside>
        <h1>{t('appTitle')}</h1>
        <nav>
          <NavLink to="/dashboard/home">{t('home')}</NavLink>
          <NavLink to="/dashboard/requests">{t('requests')}</NavLink>
          <NavLink to="/dashboard/explorer">{t('explorer')}</NavLink>
          <NavLink to="/dashboard/clients">{t('clients')}</NavLink>
          <NavLink to="/dashboard/vmrs">{t('vmrs')}</NavLink>
          <NavLink to="/dashboard/settings">{t('settings')}</NavLink>
        </nav>
      </aside>
      <main>
        <header><LanguageSelector /></header>
        <Routes>
          <Route path="/dashboard/home" element={<HomeView />} />
          <Route path="/dashboard/requests" element={<RequestHistoryView />} />
          <Route path="/dashboard/explorer" element={<ApiExplorerView />} />
          <Route path="/dashboard/clients" element={<ClientsView />} />
          <Route path="/dashboard/vmrs" element={<VmrsView />} />
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
      <button onClick={() => { window.location.href = '/dashboard/home'; }}>{t('login')}</button>
      <LanguageSelector />
    </div>
  );
}

function HomeView() {
  const { t } = useTranslation();
  const summary = useMemo(() => ({ total: 42, success: 39, failed: 3 }), []);
  return (
    <section>
      <div className="page-title"><h2>{t('home')}</h2><button><RefreshCw size={16} />{t('refresh')}</button></div>
      <div className="tiles">
        <span>{t('total')}<strong>{summary.total}</strong></span>
        <span>{t('success')}<strong>{summary.success}</strong></span>
        <span>{t('failed')}<strong>{summary.failed}</strong></span>
        <span>{t('averageMs')}<strong>24</strong></span>
      </div>
      <ActivityChart />
    </section>
  );
}

function RequestHistoryView() {
  const { t } = useTranslation();
  const [rows, setRows] = useState([]);
  useEffect(() => { new ApiClient().get('/api/v1/requesthistory').then(setRows).catch(() => setRows([])); }, []);
  return (
    <section>
      <h2>{t('requests')}</h2>
      <ActivityChart />
      <table>
        <thead><tr><th>{t('status')}</th><th>{t('path')}</th><th>{t('model')}</th></tr></thead>
        <tbody>{rows.map((row) => <tr key={row.Id || row.id}><td>{row.StatusCode || row.statusCode}</td><td>{row.Path || row.path}</td><td>{row.RoutingDecision?.EffectiveModel || row.routingDecision?.effectiveModel}</td></tr>)}</tbody>
      </table>
    </section>
  );
}

function ApiExplorerView() {
  const { t } = useTranslation();
  const [spec, setSpec] = useState(null);
  const [response, setResponse] = useState(null);
  useEffect(() => { new ApiClient().get('/openapi.json').then(setSpec).catch(() => setSpec(null)); }, []);
  async function execute() {
    const data = await new ApiClient().post('/v1/chat/completions', { model: 'llama3.2:latest', messages: [{ role: 'user', content: 'hello' }] });
    setResponse(data);
  }
  return (
    <section>
      <h2>{t('explorer')}</h2>
      <p>{spec ? t('openapiLoaded') : t('noData')}</p>
      <button onClick={execute}>{t('execute')}</button>
      <pre>{response ? JSON.stringify(response, null, 2) : ''}</pre>
    </section>
  );
}

function ClientsView() {
  const { t } = useTranslation();
  const [clients, setClients] = useState([]);
  useEffect(() => { new ApiClient().get('/api/v1/clients').then(setClients).catch(() => setClients([])); }, []);
  return <TableView title={t('clients')} rows={clients} />;
}

function VmrsView() {
  const { t } = useTranslation();
  const [rows, setRows] = useState([]);
  useEffect(() => { new ApiClient().get('/api/v1/virtualmodelrunners').then(setRows).catch(() => setRows([])); }, []);
  return <TableView title={t('vmrs')} rows={rows} />;
}

function TableView({ title, rows }) {
  const { t } = useTranslation();
  return (
    <section>
      <h2>{title}</h2>
      <table>
        <thead><tr><th>{t('model')}</th><th>{t('status')}</th></tr></thead>
        <tbody>{rows.map((row) => <tr key={row.Id || row.id}><td>{row.Name || row.name}</td><td>{String(row.Active ?? row.active ?? row.Connected ?? row.connected)}</td></tr>)}</tbody>
      </table>
    </section>
  );
}

function SettingsView() {
  const { t } = useTranslation();
  return <section><h2>{t('settings')}</h2><LanguageSelector /><p>{t('serverUrl')}: http://localhost:9000</p></section>;
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

const en = {
  translation: {
    appTitle: 'Xeno',
    login: 'Login',
    dashboard: 'Dashboard',
    home: 'Home',
    requests: 'Request History',
    explorer: 'API Explorer',
    clients: 'Clients',
    vmrs: 'Virtual Model Runners',
    policies: 'Policies',
    reservations: 'Reservations',
    settings: 'Settings',
    total: 'Total',
    success: 'Success',
    failed: 'Failed',
    averageMs: 'Average ms',
    refresh: 'Refresh',
    execute: 'Execute',
    language: 'Language',
    status: 'Status',
    method: 'Method',
    path: 'Path',
    model: 'Model',
    endpoint: 'Endpoint',
    connected: 'Connected',
    openapiLoaded: 'OpenAPI loaded',
    noData: 'No data',
    serverUrl: 'Server URL'
  }
};

const de = { translation: { ...en.translation, home: 'Startseite', requests: 'Anfrageverlauf', settings: 'Einstellungen', language: 'Sprache' } };
const ja = { translation: { ...en.translation, home: 'ホーム', requests: 'リクエスト履歴', settings: '設定', language: '言語' } };
const rtl = { translation: { ...en.translation, home: '‮Home‬', requests: '‮Request History‬', settings: '‮Settings‬' } };
const ploc = { translation: Object.fromEntries(Object.entries(en.translation).map(([key, value]) => [key, `[!! ${value} !!]`])) };

export const resources = {
  en,
  de,
  ja,
  'qps-rtl': rtl,
  'qps-ploc': ploc
};

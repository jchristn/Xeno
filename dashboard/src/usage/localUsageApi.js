import { localApi } from '../local/localApi.js';
import { usageQueryString } from './usageRanges.js';

const paths = {
  ServedRequests: '/api/v1/requests/analytics/usage',
  LocalEndpoint: '/api/v1/local-inference/analytics/usage',
  Chat: '/api/v1/chat/analytics/usage'
};

// Usage API for the local dashboard.
export const localUsageApi = {
  datasets: ['ServedRequests', 'LocalEndpoint', 'Chat'],
  groupings: ['Model', 'Provider', 'Route'],
  showPrincipalFilters: false,
  getUsage: (dataset, query) => localApi(`${paths[dataset]}${usageQueryString(query)}`),
  getProviderHealth: (query) => localApi(`/api/v1/telemetry/providers/health${usageQueryString(query)}`)
};

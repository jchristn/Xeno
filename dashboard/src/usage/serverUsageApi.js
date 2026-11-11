import { camelKeys } from '../chat/chatStream.js';
import { usageQueryString } from './usageRanges.js';

const paths = {
  NetworkRequests: '/v1.0/api/request-history/analytics/usage',
  Chat: '/v1.0/api/chat/analytics/usage'
};

// Usage API for the server dashboard, on its ApiClient (bearer token and tenant headers). Groupings follow the
// caller's role; the server enforces them regardless.
export function createServerUsageApi(api, { isGlobalAdmin, isTenantAdmin }) {
  async function call(path) {
    const response = await api.request(path, { method: 'GET' });
    if (!response.ok) {
      const payload = response.body || {};
      const error = new Error(payload.message || payload.Message || payload.error || response.statusText);
      error.status = response.status;
      throw error;
    }
    return camelKeys(response.body);
  }

  const groupings = ['Model', 'Provider', 'Client'];
  if (isTenantAdmin || isGlobalAdmin) groupings.push('User', 'Credential');
  if (isGlobalAdmin) groupings.push('Tenant');
  return {
    datasets: ['NetworkRequests', 'Chat'],
    groupings,
    showPrincipalFilters: true,
    canReviewTenant: !!(isTenantAdmin || isGlobalAdmin),
    groupingsFor: (dataset) => (dataset === 'Chat' ? ['Model', 'Provider', 'Route', ...(isTenantAdmin || isGlobalAdmin ? ['User'] : [])] : groupings),
    getUsage: (dataset, query) => {
      const tenantWide = dataset === 'Chat' && (isTenantAdmin || isGlobalAdmin) ? { scope: 'tenant' } : {};
      return call(`${paths[dataset]}${usageQueryString({ ...query, ...tenantWide })}`);
    },
    getProviderHealth: (query) => call(`/v1.0/api/client-telemetry/providers/health${usageQueryString(query)}`),
    getConnectedHistory: (query) => call(`/v1.0/api/client-telemetry/connected-history${usageQueryString(query)}`)
  };
}

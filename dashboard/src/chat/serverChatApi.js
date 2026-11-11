import { camelKeys, errorMessage, readChatStream } from './chatStream.js';

// Chat API for the server dashboard, built on its ApiClient (bearer token and tenant headers). Matches the local
// chat API's functions so the shared chat page works with either. The server offers no tools.
export function createServerChatApi(api) {
  const base = '/v1.0/api/chat';

  async function call(path, method = 'GET', body) {
    const response = await api.request(`${base}${path}`, { method, body });
    if (!response.ok) {
      const payload = response.body || {};
      throw new Error(payload.message || payload.Message || payload.error || response.statusText);
    }
    return camelKeys(response.body);
  }

  function query(values) {
    const parts = Object.entries(values || {})
      .filter(([, value]) => value !== undefined && value !== null && value !== '')
      .map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(value)}`);
    return parts.length ? `?${parts.join('&')}` : '';
  }

  return {
    supportsTools: false,
    surface: 'Server',
    listChatModels: () => call('/models'),
    listChatTools: async () => [],
    prepareModel: (model) => call('/models/prepare', 'POST', { model }),
    listConversations: (search = '') => call(`/conversations${query({ search, limit: 200 })}`),
    createConversation: (body) => call('/conversations', 'POST', body),
    getConversation: (id) => call(`/conversations/${encodeURIComponent(id)}`),
    updateConversation: (id, body) => call(`/conversations/${encodeURIComponent(id)}`, 'PUT', body),
    deleteConversation: (id) => call(`/conversations/${encodeURIComponent(id)}`, 'DELETE'),
    cancelTurn: (turnId) => call(`/turns/${encodeURIComponent(turnId)}/cancel`, 'POST', {}),
    approveToolCall: async () => ({ changed: false }),
    setFeedback: (messageId, rating, comment) => call(`/messages/${encodeURIComponent(messageId)}/feedback`, 'PUT', { rating, comment: comment || null }),
    deleteFeedback: (messageId) => call(`/messages/${encodeURIComponent(messageId)}/feedback`, 'DELETE'),
    queryHistory: (filters) => call(`/history${query(filters)}`),
    getTurn: (id) => call(`/history/${encodeURIComponent(id)}`),
    queryFeedback: (filters) => call(`/feedback${query(filters)}`),
    chatAnalytics: (filters) => call(`/analytics${query(filters)}`),
    async sendMessage(conversationId, body, onEvent, signal) {
      const response = await fetch(`${api.baseUrl}${base}/conversations/${encodeURIComponent(conversationId)}/messages`, {
        method: 'POST',
        cache: 'no-store',
        headers: { ...api.headers(), Accept: 'text/event-stream' },
        body: JSON.stringify(body || {}),
        signal
      });
      if (!response.ok) throw new Error(await errorMessage(response));
      await readChatStream(response, onEvent);
    }
  };
}

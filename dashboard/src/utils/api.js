export const authFailureEventName = 'xeno.dashboard.authFailed';

export function defaultServerUrl() {
  return normalizeServerUrl(window.__XENO_CONFIG__?.serverUrl || '');
}

export function normalizeServerUrl(input) {
  const fallback = browserDerivedServerUrl();
  const raw = String(input || '').trim();
  if (!raw) return fallback;

  try {
    const parsed = new URL(raw, window.location.origin);
    if (isLoopbackHost(parsed.hostname) && !isLoopbackHost(window.location.hostname)) {
      parsed.protocol = window.location.protocol === 'https:' ? 'https:' : 'http:';
      parsed.hostname = window.location.hostname;
    }

    parsed.pathname = '';
    parsed.search = '';
    parsed.hash = '';
    return parsed.toString().replace(/\/$/, '');
  } catch {
    return raw.replace(/\/$/, '') || fallback;
  }
}

function browserDerivedServerUrl() {
  const protocol = window.location.protocol === 'https:' ? 'https:' : 'http:';
  const host = window.location.hostname || 'localhost';
  return `${protocol}//${host}:9000`;
}

function isLoopbackHost(hostname) {
  const normalized = String(hostname || '').toLowerCase();
  return normalized === 'localhost' || normalized === '127.0.0.1' || normalized === '::1' || normalized === '[::1]';
}

export class ApiAuthenticationError extends Error {
  constructor(message, status) {
    super(message);
    this.name = 'ApiAuthenticationError';
    this.status = status;
  }
}

export default class ApiClient {
  constructor(baseUrl = defaultServerUrl()) {
    this.baseUrl = normalizeServerUrl(baseUrl);
    this.token = '';
    this.tenantId = '';
  }

  setBaseUrl(baseUrl) {
    this.baseUrl = normalizeServerUrl(baseUrl);
  }

  setAuth(session) {
    this.token = session?.token || '';
    this.tenantId = session?.tenantId || '';
  }

  clearAuth() {
    this.token = '';
    this.tenantId = '';
  }

  async get(path, options = {}) {
    const response = await fetch(`${this.baseUrl}${path}`, { headers: this.headers(options) });
    return readJsonResponse(response, options);
  }

  async post(path, body, options = {}) {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'POST',
      headers: this.headers(options),
      body: JSON.stringify(body ?? {})
    });
    return readJsonResponse(response, options);
  }

  async postJsonLines(path, body, onLine, options = {}) {
    const headers = {
      ...this.headers(options),
      Accept: 'application/x-ndjson'
    };
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'POST',
      headers,
      body: JSON.stringify(body ?? {})
    });
    if (isAuthenticationFailure(response)) {
      const text = await response.text();
      const parsed = parseBody(text);
      throwAuthenticationFailure(response, parsed);
    }

    if (!response.ok) {
      const text = await response.text();
      const parsed = parseResponseText(text, false);
      throw new Error(parsed?.message || parsed?.error || response.statusText || `Request failed with HTTP ${response.status}`);
    }

    if (!response.body) return [];

    const reader = response.body.getReader();
    const decoder = new TextDecoder();
    const items = [];
    let buffer = '';
    while (true) {
      const result = await reader.read();
      if (result.done) break;
      buffer += decoder.decode(result.value, { stream: true });
      const lines = buffer.split(/\r?\n/);
      buffer = lines.pop() || '';
      lines.forEach((line) => {
        const trimmed = line.trim();
        if (!trimmed) return;
        const parsed = parseBody(trimmed);
        items.push(parsed);
        onLine?.(parsed);
      });
    }

    buffer += decoder.decode();
    const trimmed = buffer.trim();
    if (trimmed) {
      const parsed = parseBody(trimmed);
      items.push(parsed);
      onLine?.(parsed);
    }

    return items;
  }

  async put(path, body, options = {}) {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'PUT',
      headers: this.headers(options),
      body: JSON.stringify(body ?? {})
    });
    return readJsonResponse(response, options);
  }

  async delete(path, body, options = {}) {
    const requestOptions = {
      method: 'DELETE',
      headers: this.headers(options)
    };
    if (body !== undefined) requestOptions.body = JSON.stringify(body ?? {});
    const response = await fetch(`${this.baseUrl}${path}`, requestOptions);
    if (response.status === 204) return {};
    return readJsonResponse(response, options);
  }

  async getLedgerStatus() {
    return this.get('/v1.0/api/ledger/status');
  }

  async getMyLedgerBalance() {
    return this.get('/v1.0/api/ledger/balance/me');
  }

  async getLedgerBalances(filters = {}) {
    return this.get(`/v1.0/api/ledger/balances${queryString(filters)}`);
  }

  async getMyLedgerEntries() {
    return this.get('/v1.0/api/ledger/entries/me');
  }

  async searchLedgerEntries(request = {}) {
    return this.post('/v1.0/api/ledger/entries/search', request);
  }

  async drainLedgerQueue() {
    return this.post('/v1.0/api/ledger/drain', {});
  }

  async reconcileLedger() {
    return this.post('/v1.0/api/ledger/reconcile', {});
  }

  async request(path, { body, expectStream = false, method = 'GET', onProgress, signal } = {}) {
    const options = {
      method,
      headers: this.headers(),
      signal
    };
    if (body !== undefined && body !== null && method.toUpperCase() !== 'GET') {
      options.body = JSON.stringify(body);
    }

    const startedAt = performance.now();
    const response = await fetch(`${this.baseUrl}${path}`, options);
    const headersReceivedAt = performance.now();
    const headers = {};
    response.headers.forEach((value, key) => { headers[key] = value; });
    if (isAuthenticationFailure(response)) {
      const text = await response.text();
      const body = parseBody(text);
      throwAuthenticationFailure(response, body);
    }

    const contentType = response.headers.get('content-type') || '';
    const transferEncoding = response.headers.get('transfer-encoding') || '';
    const streamType = response.ok ? detectStreamType(contentType, transferEncoding, expectStream) : 'standard';
    const base = {
      headers,
      ok: response.ok,
      status: response.status,
      statusText: response.statusText,
      streamType,
      streamed: streamType !== 'standard'
    };

    if (response.body && streamType !== 'standard') {
      onProgress?.({
        ...base,
        body: null,
        preview: '',
        rawBody: '',
        timing: { requestTimeMs: null, timeToFirstByteMs: null, totalTimeMs: null }
      });
      const streamed = await readStream(response.body, streamType, startedAt, onProgress, base);
      return {
        ...base,
        ...streamed,
        body: parseBody(streamed.rawBody),
        timing: streamed.timing
      };
    }

    const text = await response.text();
    const endedAt = performance.now();

    return {
      ...base,
      body: parseBody(text),
      preview: extractPreview(parseBody(text)) || text,
      rawBody: text,
      streamed: false,
      streamType: 'standard',
      timing: {
        requestTimeMs: endedAt - startedAt,
        timeToFirstByteMs: headersReceivedAt - startedAt,
        totalTimeMs: endedAt - startedAt
      }
    };
  }

  async text(path) {
    const response = await fetch(`${this.baseUrl}${path}`, { headers: this.headers() });
    if (isAuthenticationFailure(response)) {
      const text = await response.text();
      const body = parseBody(text);
      throwAuthenticationFailure(response, body);
    }

    return response.text();
  }

  headers(options = {}) {
    const headers = {
      'Content-Type': 'application/json',
      'Accept-Language': document.documentElement.lang || 'en'
    };
    if (!options.skipAuth && this.token) {
      headers.Authorization = `Bearer ${this.token}`;
      headers['x-token'] = this.token;
    }
    if (!options.skipAuth && this.tenantId) {
      headers['x-tenant-guid'] = this.tenantId;
      headers['x-tenant-id'] = this.tenantId;
    }
    return headers;
  }
}

function queryString(filters = {}) {
  const params = new URLSearchParams();
  Object.entries(filters).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') return;
    params.set(key, String(value));
  });
  const text = params.toString();
  return text ? `?${text}` : '';
}

async function readJsonResponse(response, options = {}) {
  const text = await response.text();
  const parsed = parseResponseText(text, response.ok);
  if (!options.skipAuth && isAuthenticationFailure(response)) {
    throwAuthenticationFailure(response, parsed);
  }

  if (!text) return {};
  return parsed;
}

function parseResponseText(text, ok) {
  if (!text) return {};
  try {
    return JSON.parse(text);
  } catch {
    return {
      error: ok ? 'InvalidJson' : 'RequestFailed',
      message: text
    };
  }
}

function isAuthenticationFailure(response) {
  return response.status === 401;
}

function throwAuthenticationFailure(response, payload) {
  const message = payload?.message || payload?.error || 'Dashboard session expired. Please sign in again.';
  const error = new ApiAuthenticationError(message, response.status);
  window.dispatchEvent(new CustomEvent(authFailureEventName, {
    detail: {
      message,
      status: response.status
    }
  }));
  throw error;
}

function parseBody(text) {
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return text;
  }
}

function detectStreamType(contentType, transferEncoding, expectStream) {
  const normalizedContentType = String(contentType || '').toLowerCase();
  const normalizedTransferEncoding = String(transferEncoding || '').toLowerCase();
  if (normalizedContentType.includes('text/event-stream')) return 'sse';
  if (normalizedContentType.includes('application/x-ndjson') || normalizedContentType.includes('application/json-seq')) return 'ndjson';
  if (normalizedTransferEncoding.includes('chunked') && expectStream) return 'chunked';
  if (normalizedTransferEncoding.includes('chunked') && !normalizedContentType.includes('application/json')) return 'chunked';
  if (expectStream) return 'sse';
  return 'standard';
}

async function readStream(body, streamType, startedAt, onProgress, base) {
  const reader = body.getReader();
  const decoder = new TextDecoder();
  let rawBody = '';
  let firstByteAt = null;

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      const now = performance.now();
      if (firstByteAt === null) firstByteAt = now;
      const chunk = decoder.decode(value, { stream: true });
      rawBody += chunk;
      const preview = extractStreamingPreview(rawBody, streamType);

      onProgress?.({
        ...base,
        body: null,
        preview,
        rawBody,
        timing: {
          requestTimeMs: null,
          timeToFirstByteMs: firstByteAt === null ? null : firstByteAt - startedAt,
          totalTimeMs: now - startedAt
        }
      });
    }

    const tail = decoder.decode();
    if (tail) {
      rawBody += tail;
    }

    const endedAt = performance.now();
    const preview = extractStreamingPreview(rawBody, streamType);
    return {
      preview: preview || finalStreamPreview(rawBody, streamType),
      rawBody,
      timing: {
        requestTimeMs: null,
        timeToFirstByteMs: firstByteAt === null ? null : firstByteAt - startedAt,
        totalTimeMs: endedAt - startedAt
      }
    };
  } finally {
    reader.releaseLock();
  }
}

function finalStreamPreview(rawBody, streamType) {
  if (streamType === 'sse' || streamType === 'ndjson') return '';
  return extractPreview(parseBody(rawBody)) || String(rawBody || '');
}

function extractStreamingPreview(rawBody, streamType) {
  const text = String(rawBody || '');
  if (!text) return '';
  if (streamType === 'sse') {
    const ssePreview = extractServerSentEventPreview(text);
    if (ssePreview) return ssePreview;
    if (text.includes('data:')) return '';
    return extractJsonLinesPreview(text) || extractPreview(parseBody(text)) || '';
  }
  if (streamType === 'ndjson') return extractJsonLinesPreview(text);
  return extractChunkedPreview(text);
}

function extractChunkedPreview(text) {
  const ssePreview = extractServerSentEventPreview(text);
  if (ssePreview) return ssePreview;
  const jsonLinesPreview = extractJsonLinesPreview(text);
  if (jsonLinesPreview) return jsonLinesPreview;
  return extractPreview(parseBody(text)) || text;
}

function extractServerSentEventPreview(text) {
  const chunks = [];
  for (const eventText of String(text || '').split(/\r?\n\r?\n/)) {
    const dataLines = eventText
      .split(/\r?\n/)
      .map((line) => line.trim())
      .filter((line) => line.startsWith('data:'))
      .map((line) => line.slice(5).trim())
      .filter((line) => line && line !== '[DONE]');

    if (dataLines.length === 0) continue;
    const data = dataLines.join('\n');
    const preview = extractEventDataPreview(data);
    if (preview) chunks.push(preview);
  }

  if (chunks.length > 0) return chunks.join('');

  const inlineDataMatches = [...String(text || '').matchAll(/data:\s*([^\r\n]+)/g)];
  return inlineDataMatches
    .map((match) => match[1].trim())
    .filter((data) => data && data !== '[DONE]')
    .map(extractEventDataPreview)
    .filter(Boolean)
    .join('');
}

function extractJsonLinesPreview(text) {
  return String(text || '')
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .map(extractEventDataPreview)
    .filter(Boolean)
    .join('');
}

function extractEventDataPreview(data) {
  try {
    return extractPreview(JSON.parse(data)) || '';
  } catch {
    return looksLikeIncompleteJson(data) ? '' : `${data}\n`;
  }
}

function looksLikeIncompleteJson(text) {
  const trimmed = String(text || '').trim();
  return trimmed.startsWith('{') || trimmed.startsWith('[');
}

function extractPreview(data) {
  if (data === null || data === undefined) return '';
  if (typeof data === 'string') return data;
  if (Array.isArray(data)) return data.map(extractPreview).filter(Boolean).join('');
  if (typeof data !== 'object') return String(data);

  const choice = Array.isArray(data.choices) ? data.choices[0] : null;
  const delta = choice?.delta;
  const message = choice?.message;
  if (typeof data.delta === 'string') return data.delta;
  if (typeof delta?.content === 'string') return delta.content;
  if (typeof message?.content === 'string') return message.content;
  if (typeof choice?.text === 'string') return choice.text;
  if (typeof data.message?.content === 'string') return data.message.content;
  if (typeof data.response === 'string') return data.response;
  if (typeof data.content === 'string') return data.content;
  if (typeof data.output_text === 'string') return data.output_text;
  if (Array.isArray(data.candidates) && typeof data.candidates[0]?.content?.parts?.[0]?.text === 'string') {
    return data.candidates[0].content.parts[0].text;
  }
  if (Array.isArray(data.data) && data.data.length > 0 && data.data[0]?.embedding) {
    return JSON.stringify(data, null, 2);
  }
  return '';
}

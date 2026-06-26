export default class ApiClient {
  constructor(baseUrl = window.__XENO_CONFIG__?.serverUrl || 'http://localhost:9000') {
    this.baseUrl = baseUrl.replace(/\/$/, '');
    this.token = '';
    this.tenantId = '';
  }

  setBaseUrl(baseUrl) {
    this.baseUrl = String(baseUrl || 'http://localhost:9000').replace(/\/$/, '');
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
    return readJsonResponse(response);
  }

  async post(path, body, options = {}) {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'POST',
      headers: this.headers(options),
      body: JSON.stringify(body ?? {})
    });
    return readJsonResponse(response);
  }

  async put(path, body, options = {}) {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'PUT',
      headers: this.headers(options),
      body: JSON.stringify(body ?? {})
    });
    return readJsonResponse(response);
  }

  async delete(path, body, options = {}) {
    const requestOptions = {
      method: 'DELETE',
      headers: this.headers(options)
    };
    if (body !== undefined) requestOptions.body = JSON.stringify(body ?? {});
    const response = await fetch(`${this.baseUrl}${path}`, requestOptions);
    if (response.status === 204) return {};
    return readJsonResponse(response);
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
    const headers = {};
    response.headers.forEach((value, key) => { headers[key] = value; });
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
        timeToFirstByteMs: null,
        totalTimeMs: endedAt - startedAt
      }
    };
  }

  async text(path) {
    const response = await fetch(`${this.baseUrl}${path}`, { headers: this.headers() });
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

async function readJsonResponse(response) {
  const text = await response.text();
  if (!text) return {};
  try {
    return JSON.parse(text);
  } catch {
    return {
      error: response.ok ? 'InvalidJson' : 'RequestFailed',
      message: text
    };
  }
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

export default class ApiClient {
  constructor(baseUrl = window.__XENO_CONFIG__?.serverUrl || 'http://localhost:9000') {
    this.baseUrl = baseUrl.replace(/\/$/, '');
  }

  async get(path) {
    const response = await fetch(`${this.baseUrl}${path}`, { headers: this.headers() });
    return response.json();
  }

  async post(path, body) {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'POST',
      headers: this.headers(),
      body: JSON.stringify(body ?? {})
    });
    return response.json();
  }

  async put(path, body) {
    const response = await fetch(`${this.baseUrl}${path}`, {
      method: 'PUT',
      headers: this.headers(),
      body: JSON.stringify(body ?? {})
    });
    return response.json();
  }

  async delete(path, body) {
    const options = {
      method: 'DELETE',
      headers: this.headers()
    };
    if (body !== undefined) options.body = JSON.stringify(body ?? {});
    const response = await fetch(`${this.baseUrl}${path}`, options);
    if (response.status === 204) return {};
    return response.json();
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
    const streamType = detectStreamType(contentType, transferEncoding, expectStream);
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

  headers() {
    return {
      'Content-Type': 'application/json',
      'Accept-Language': document.documentElement.lang || 'en'
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
  if (expectStream && !normalizedContentType.includes('application/json')) return 'sse';
  return 'standard';
}

async function readStream(body, streamType, startedAt, onProgress, base) {
  const reader = body.getReader();
  const decoder = new TextDecoder();
  let rawBody = '';
  let preview = '';
  let buffer = '';
  let firstByteAt = null;

  try {
    while (true) {
      const { done, value } = await reader.read();
      if (done) break;

      const now = performance.now();
      if (firstByteAt === null) firstByteAt = now;
      const chunk = decoder.decode(value, { stream: true });
      rawBody += chunk;

      if (streamType === 'sse' || streamType === 'ndjson') {
        buffer += chunk;
        const lines = buffer.split(/\r?\n/);
        buffer = lines.pop() || '';
        for (const line of lines) {
          preview += extractStreamLinePreview(line, streamType);
        }
      } else {
        preview += chunk;
      }

      onProgress?.({
        ...base,
        body: null,
        preview: preview || rawBody,
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
      buffer += tail;
    }

    if (buffer.trim()) {
      preview += extractStreamLinePreview(buffer, streamType);
    }

    const endedAt = performance.now();
    return {
      preview: preview || extractPreview(parseBody(rawBody)) || rawBody,
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

function extractStreamLinePreview(line, streamType) {
  const trimmed = String(line || '').trim();
  if (!trimmed) return '';

  if (streamType === 'sse') {
    if (!trimmed.startsWith('data:')) return '';
    const data = trimmed.slice(5).trim();
    if (!data || data === '[DONE]') return '';
    return extractDataPreview(data);
  }

  return extractDataPreview(trimmed);
}

function extractDataPreview(data) {
  try {
    return extractPreview(JSON.parse(data)) || '';
  } catch {
    return data ? `${data}\n` : '';
  }
}

function extractPreview(data) {
  if (data === null || data === undefined) return '';
  if (typeof data === 'string') return data;
  if (Array.isArray(data)) return data.map(extractPreview).filter(Boolean).join('');
  if (typeof data !== 'object') return String(data);

  const choice = Array.isArray(data.choices) ? data.choices[0] : null;
  const delta = choice?.delta;
  const message = choice?.message;
  if (typeof delta?.content === 'string') return delta.content;
  if (typeof message?.content === 'string') return message.content;
  if (typeof choice?.text === 'string') return choice.text;
  if (typeof data.response === 'string') return data.response;
  if (typeof data.content === 'string') return data.content;
  if (typeof data.output_text === 'string') return data.output_text;
  if (Array.isArray(data.data) && data.data.length > 0 && data.data[0]?.embedding) {
    return JSON.stringify(data, null, 2);
  }
  return '';
}

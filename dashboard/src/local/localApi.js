export async function localApi(path, options = {}) {
  const token = window.__XENO_LOCAL_TOKEN__ || '';
  const headers = {
    'Accept-Language': navigator.language || 'en',
    'Content-Type': 'application/json',
    'X-Xeno-Local-Token': token,
    ...(options.headers || {})
  };
  const response = await fetch(path, { ...options, headers });
  const text = await response.text();
  if (!response.ok) throw new Error(text || response.statusText);
  return text ? normalizeKeys(JSON.parse(text)) : {};
}

export function jsonBody(value) {
  return JSON.stringify(value || {});
}

function normalizeKeys(value) {
  if (Array.isArray(value)) return value.map((item) => normalizeKeys(item));
  if (!value || typeof value !== 'object') return value;

  const normalized = {};
  Object.entries(value).forEach(([key, entry]) => {
    const normalizedValue = normalizeKeys(entry);
    normalized[key] = normalizedValue;
    const alias = key.length > 0 ? key.charAt(0).toLowerCase() + key.slice(1) : key;
    if (!Object.prototype.hasOwnProperty.call(normalized, alias)) {
      normalized[alias] = normalizedValue;
    }
  });
  return normalized;
}

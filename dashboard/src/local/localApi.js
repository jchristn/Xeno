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
  return text ? JSON.parse(text) : {};
}

export function jsonBody(value) {
  return JSON.stringify(value || {});
}

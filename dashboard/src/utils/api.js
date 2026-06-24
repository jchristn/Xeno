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

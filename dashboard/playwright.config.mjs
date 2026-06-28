export default {
  testDir: './tests',
  timeout: 30000,
  fullyParallel: false,
  testMatch: /server-dashboard-security\.spec\.mjs/,
  use: {
    baseURL: 'http://127.0.0.1:4174',
    browserName: 'chromium',
    trace: 'off',
    video: 'off',
    screenshot: 'only-on-failure'
  },
  webServer: {
    command: 'npm run build && npx vite preview --host 127.0.0.1 --port 4174 --strictPort',
    url: 'http://127.0.0.1:4174',
    reuseExistingServer: false,
    timeout: 120000
  }
};

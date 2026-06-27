export default {
  testDir: './tests',
  timeout: 30000,
  fullyParallel: false,
  use: {
    baseURL: 'http://127.0.0.1:4175',
    browserName: 'chromium',
    trace: 'off',
    video: 'off',
    screenshot: 'only-on-failure'
  },
  webServer: {
    command: 'npm run build:local && npx vite preview --config vite.local.config.js --host 127.0.0.1 --port 4175 --strictPort',
    url: 'http://127.0.0.1:4175/index-local.html',
    reuseExistingServer: false,
    timeout: 120000
  }
};

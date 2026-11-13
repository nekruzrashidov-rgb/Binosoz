// Browser checks of the public site, served as static files (frontend/tools/serve-web.mjs) — no API needed:
// without an API the site must show its honest empty states. Run: cd frontend/tools/e2e && npm ci && npx playwright install chromium && npm test
const { defineConfig, devices } = require('@playwright/test');

const PORT = 5510;

module.exports = defineConfig({
  testDir: './tests',
  timeout: 60_000,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [['list'], ['html', { open: 'never' }]] : 'list',
  use: { baseURL: `http://localhost:${PORT}`, trace: 'retain-on-failure' },
  webServer: {
    command: `node ../serve-web.mjs ${PORT}`,
    url: `http://localhost:${PORT}/`,
    reuseExistingServer: !process.env.CI,
  },
  projects: [
    { name: 'desktop', use: { ...devices['Desktop Chrome'], viewport: { width: 1440, height: 900 } } },
    { name: 'mobile', use: { ...devices['Pixel 5'], viewport: { width: 375, height: 812 } } },
  ],
});

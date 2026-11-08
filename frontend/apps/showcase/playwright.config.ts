import { defineConfig, devices } from '@playwright/test';

// Tests the generated static site (.output/public), served like a static host.
// In CI it is built from the API after the Studio e2e has published content.
export default defineConfig({
  testDir: 'e2e',
  timeout: 60_000,
  expect: { timeout: 15_000 },
  retries: process.env.CI ? 1 : 0,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'playwright-report' }]],
  use: { baseURL: 'http://localhost:5175', trace: 'retain-on-failure', screenshot: 'only-on-failure', colorScheme: 'dark' },
  projects: [{
    name: 'chromium',
    use: {
      ...devices['Desktop Chrome'],
      viewport: { width: 1440, height: 900 },
      launchOptions: { args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader'] },
    },
  }],
  webServer: { command: 'node scripts/serve.mjs', url: 'http://localhost:5175/en', reuseExistingServer: !process.env.CI },
});

import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e',
  timeout: 30 * 1000,
  expect: {
    timeout: 5000
  },
  fullyParallel: false,
  workers: 1,
  reporter: 'list',
  use: {
    baseURL: 'http://localhost:8080',
    trace: 'on-first-retry',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
  },
  projects: [
    {
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    }
  ],
  webServer: {
    command: 'dotnet run --project ../ScrumPulse.Api/ScrumPulse.Api.csproj',
    url: 'http://localhost:8080/health',
    reuseExistingServer: true,
    timeout: 120 * 1000,
    env: {
      PORT: '8080',
      ASPNETCORE_ENVIRONMENT: 'Development',
      SeedDemoData: 'true'
    }
  },
});

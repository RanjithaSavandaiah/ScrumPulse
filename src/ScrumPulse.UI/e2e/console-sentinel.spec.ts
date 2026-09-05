import { test, expect } from '@playwright/test';

test.describe('Zero Console Errors & Warnings Sentinel', () => {
  test('should load entire application and all feature tabs with zero console errors or warnings', async ({ page }) => {
    const consoleIssues: string[] = [];

    // Attach listeners for console messages and unhandled page exceptions
    page.on('console', msg => {
      const type = msg.type();
      const text = msg.text();
      // Flag any console error or warning
      if (type === 'error' || type === 'warning') {
        // Ignore benign third-party sandbox ad blockers if offline in CI
        if (text.includes('net::ERR_') && text.includes('googlesyndication')) return;
        consoleIssues.push(`[${type.toUpperCase()}] ${text}`);
      }
    });

    page.on('pageerror', error => {
      consoleIssues.push(`[PAGE ERROR] ${error.name}: ${error.message}\n${error.stack}`);
    });

    // 1. Visit root dashboard
    await page.goto('/', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('app-dashboard')).toBeVisible({ timeout: 15000 });

    // 2. Iterate through primary navigation tabs to exercise all standalone components
    const tabs = [
      'Work Items',
      'Daily Standup',
      'Git PRs',
      'Team Roster',
      'Blockers',
      'Tech Hub',
      'Kudos',
      '1on1 Reviews',
      'Executive Report',
      'AI Coach'
    ];

    for (const tabName of tabs) {
      const tabBtn = page.locator('.tab-btn', { hasText: tabName }).first();
      if (await tabBtn.isVisible()) {
        await tabBtn.click();
        await page.waitForTimeout(300);
      }
    }

    // 3. Visit Legal pages
    await page.goto('/privacy-policy', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('app-privacy-policy')).toBeVisible({ timeout: 10000 });

    await page.goto('/terms', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('app-terms')).toBeVisible({ timeout: 10000 });

    // 4. Assert ZERO console errors or warnings
    expect(consoleIssues, `Detected console errors/warnings during execution:\n${consoleIssues.join('\n')}`).toEqual([]);
  });
});

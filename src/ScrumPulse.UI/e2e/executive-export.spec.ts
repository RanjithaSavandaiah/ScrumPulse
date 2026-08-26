import { test, expect } from '@playwright/test';

test.describe('Executive Suite Multi-Duration Export & AI Synthesis', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.locator('.tab-btn', { hasText: 'Executive Suite' }).click();
    await expect(page.locator('.hero-title')).toBeVisible({ timeout: 10000 });
  });

  test('should display multi-duration time horizon filters', async ({ page }) => {
    // Check filter pills
    await expect(page.locator('.scope-pill', { hasText: 'By Sprint' })).toBeVisible();
    await expect(page.locator('.scope-pill', { hasText: 'By Month' })).toBeVisible();
    await expect(page.locator('.scope-pill', { hasText: 'By Quarter' })).toBeVisible();
    await expect(page.locator('.scope-pill', { hasText: 'Custom Date Frame' })).toBeVisible();
    await expect(page.locator('.scope-pill', { hasText: 'All Time' })).toBeVisible();

    // Verify Export buttons
    await expect(page.locator('.btn-excel')).toBeVisible();
    await expect(page.locator('.btn-pdf')).toBeVisible();
  });

  test('should filter by custom duration and update preview KPIs', async ({ page }) => {
    await page.locator('.scope-pill', { hasText: 'Custom Date Frame' }).click();
    const dateInput = page.locator('.date-select').first();
    await expect(dateInput).toBeVisible();

    // Click 14d preset
    await page.locator('.mini-preset', { hasText: '14d' }).click();

    // Verify metrics cards update
    await expect(page.locator('.metrics-grid')).toBeVisible();
  });

  test('should show 360 AI Intelligence with Strengths and Weakness Findings', async ({ page }) => {
    const aiPanel = page.locator('.ai-executive-panel');
    await expect(aiPanel).toBeVisible({ timeout: 10000 });
    await expect(aiPanel.locator('.ai-findings-list')).toBeVisible();
    await expect(aiPanel.locator('.ai-recs-list')).toBeVisible();
  });
});

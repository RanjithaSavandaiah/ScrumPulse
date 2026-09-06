import { test, expect } from '@playwright/test';

test.describe('Team Growth & Velocity Performance Telemetry', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.locator('.tab-btn', { hasText: 'Team Growth & Performance' }).click();
    await expect(page.locator('.performance-section')).toBeVisible({ timeout: 15000 });
  });

  test('should render performance telemetry, grade badge, and interactive refresh', async ({ page }) => {
    // 1. Ensure loading spinner clears
    await expect(page.locator('.loading-state')).not.toBeVisible({ timeout: 15000 });

    // 2. Assert telemetry content or graceful empty state
    const heroCard = page.locator('.hero-grade-card');
    const noDataCard = page.locator('.no-data-card');

    const hasData = await heroCard.isVisible();

    if (hasData) {
      // Grade badge and score
      const gradeBadge = heroCard.locator('.grade-badge');
      await expect(gradeBadge).toBeVisible();
      const gradeLetter = await gradeBadge.locator('.grade-letter').textContent();
      expect(gradeLetter?.trim().length).toBeGreaterThan(0);

      // Verify metrics grid
      const metricCards = page.locator('.metrics-grid .metric-card');
      const count = await metricCards.count();
      expect(count).toBeGreaterThan(0);

      // Verify at least one metric card has name and value
      const firstMetric = metricCards.first();
      await expect(firstMetric.locator('.metric-name')).toBeVisible();
      await expect(firstMetric.locator('.metric-value')).toBeVisible();

      // Verify team engagement & culture card
      const engagementCard = page.locator('.engagement-card');
      await expect(engagementCard).toBeVisible();
      await expect(engagementCard.locator('.engagement-metrics-row')).toBeVisible();

      // Test refresh button
      const refreshBtn = heroCard.locator('.btn-refresh');
      await expect(refreshBtn).toBeVisible();
      await refreshBtn.click();
      await expect(page.locator('.loading-state')).not.toBeVisible({ timeout: 10000 });
      await expect(heroCard).toBeVisible();
    } else {
      // Graceful empty state when no closed sprints yet
      await expect(noDataCard).toBeVisible();
      await expect(noDataCard.locator('.no-data-steps .step-card')).toHaveCount(3);

      const refreshBtn = noDataCard.locator('.btn-refresh-data');
      await expect(refreshBtn).toBeVisible();
      await refreshBtn.click();
      await expect(page.locator('.loading-state')).not.toBeVisible({ timeout: 10000 });

      // Link to lifecycle
      const gotoLifecycleLink = noDataCard.locator('.btn-goto-lifecycle');
      await expect(gotoLifecycleLink).toBeVisible();
    }
  });
});

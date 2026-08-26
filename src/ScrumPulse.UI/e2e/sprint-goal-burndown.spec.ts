import { test, expect } from '@playwright/test';

test.describe('Sprint Goal, Auto-Capacity & Burndown Radar', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
  });

  test('should display Sprint Goal banner with velocity and leave-adjusted capacity', async ({ page }) => {
    // Navigate to Work Items tab
    await page.locator('.tab-btn', { hasText: 'Work Items' }).click();

    // Verify Sprint Goal banner exists
    const goalBanner = page.locator('.sprint-goal-banner');
    await expect(goalBanner).toBeVisible({ timeout: 10000 });

    // Verify Goal text, Velocity, Net Capacity badges
    await expect(goalBanner.locator('.goal-text')).toBeVisible();
    await expect(goalBanner.locator('.metric-pill').first()).toBeVisible();
    await expect(goalBanner).toContainText('Net Capacity');
  });

  test('should render dynamic Burndown Graph and toggle view', async ({ page }) => {
    await page.locator('.tab-btn', { hasText: 'Work Items' }).click();

    const burndownCard = page.locator('app-sprint-burndown-chart');
    await expect(burndownCard).toBeVisible({ timeout: 10000 });

    // Verify SVG burndown chart exists
    const svgChart = burndownCard.locator('.burndown-svg');
    await expect(svgChart).toBeVisible();

    // Verify Burndown Status pill
    await expect(burndownCard.locator('.pace-pill')).toBeVisible();

    // Toggle collapse/expand
    const toggleBtn = page.locator('.btn-burndown-toggle');
    await toggleBtn.click();
    await expect(burndownCard).not.toBeVisible();

    await toggleBtn.click();
    await expect(burndownCard).toBeVisible();
  });
});

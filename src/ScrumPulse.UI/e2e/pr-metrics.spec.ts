import { test, expect } from '@playwright/test';
import { unlockScrumMaster } from './helpers';

test.describe('Git PRs & Code Review Analytics', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await unlockScrumMaster(page);
    // Navigate to Git PRs & Code Review tab
    await page.getByRole('button', { name: /Git PRs & Code Review/i }).click();
    await expect(page.getByRole('heading', { name: /Developer PR & Code Review Analytics/i })).toBeVisible({ timeout: 15000 });
  });

  test('should log a pull request review, verify KPIs and developer scorecard', async ({ page }) => {
    const timestamp = Date.now();
    const prNumber = `#PR-${timestamp.toString().slice(-4)}`;
    const prTitle = `Feature: Resilient Circuit Breaker & Webhooks ${timestamp}`;

    // Check initial PR count
    const kpiLocator = page.locator('.kpi-card:has-text("Total PRs Created") .kpi-val');
    const initialPrKpi = await kpiLocator.innerText();
    const initialPrCount = parseInt(initialPrKpi, 10) || 0;

    // Open Log PR modal
    await page.getByRole('button', { name: /Log PR & Review Comments/i }).click();
    await expect(page.locator('#prTitleInput')).toBeVisible();

    // Select Author (first contributing engineer)
    const authorSelect = page.locator('#prAuthorSelect');
    const authorOptions = await authorSelect.locator('option').all();
    if (authorOptions.length > 0) {
      const authorVal = await authorOptions[0].getAttribute('value');
      if (authorVal) {
        await authorSelect.selectOption(authorVal);
      }
    }

    // Select Sprint
    const sprintModalSelect = page.locator('#prSprintModalSelect');
    const sprintOptions = await sprintModalSelect.locator('option').all();
    if (sprintOptions.length > 1) {
      await sprintModalSelect.selectOption({ index: 1 });
    }

    // Fill in PR details
    await page.locator('#prNumberInput').fill(prNumber);
    await page.locator('#prTitleInput').fill(prTitle);
    await page.locator('#prTotalCommentsInput').fill('8');
    await page.locator('#prActionableCommentsInput').fill('5');
    await page.locator('#prReviewSummaryTextarea').fill('Refactored polling loop, added timeout backoff, verified idempotency.');
    await page.locator('#prReviewStatusSelect').selectOption('Approved');

    // Save PR
    await page.locator('.btn-save-pr').click();
    await expect(page.locator('#prTitleInput')).not.toBeVisible();

    // Verify row rendered in PR table (waits for async NgRx persistence)
    const prRow = page.locator('.pr-table tr', { hasText: prTitle });
    await expect(prRow).toBeVisible({ timeout: 10000 });
    await expect(prRow).toContainText(prNumber);
    await expect(prRow).toContainText('5 actionable');

    // Verify aggregate KPI incremented with auto-retry
    await expect(kpiLocator).toHaveText(String(initialPrCount + 1), { timeout: 10000 });

    // Advanced Filter: Filter by sprint
    const sprintSelect = page.locator('#prSprintSelect');
    await sprintSelect.selectOption({ index: 1 });
    await expect(page.locator('.pr-metrics-section')).toBeVisible();

    // Reset filter to All Sprints
    await sprintSelect.selectOption('ALL');
    await expect(prRow).toBeVisible();

    // Clean up: delete PR
    await prRow.locator('.btn-delete-pr').click();
    await expect(prRow).not.toBeVisible({ timeout: 5000 });
  });
});

import { test, expect } from '@playwright/test';
import { unlockScrumMaster } from './helpers';

test.describe('Blocker Hours Impact on Capacity, Burndown & Executive Report E2E', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/', { waitUntil: 'domcontentloaded' });
    await unlockScrumMaster(page);
  });

  test('should record blocker hours, deduct from net capacity, reflect on burndown and executive report', async ({ page }) => {
    const timestamp = Date.now();
    const blockerTitle = `E2E Infrastructure Outage ${timestamp}`;
    const blockerDesc = 'Cloud cluster provision failure blocking sprint execution.';

    // 1. Navigate to Blocker SLA Radar
    await page.locator('.tab-btn', { hasText: 'Blocker' }).click();
    await expect(page.locator('.blockers-section')).toBeVisible({ timeout: 15000 });

    // 2. Open Log Blocker Modal
    const logBtn = page.locator('.blockers-section .section-header button', { hasText: 'Log Blocker' });
    await expect(logBtn).toBeVisible();
    await logBtn.click();
    await expect(page.locator('#blockerSummaryInput')).toBeVisible();

    // 3. Fill Blocker details including Blocked Hours = 16
    const categoryCard = page.locator('app-add-blocker-modal .category-card', { hasText: 'Environment & Access' });
    await expect(categoryCard).toBeVisible();
    await categoryCard.click();

    await page.locator('#blockerSummaryInput').fill(blockerTitle);
    await page.locator('#blockerContextTextarea').fill(blockerDesc);
    await page.locator('#blockedHoursInput').fill('16');

    // 4. Submit blocker
    await page.locator('app-add-blocker-modal .btn-save').click();
    await expect(page.locator('#blockerSummaryInput')).not.toBeVisible({ timeout: 5000 });

    // 5. Verify Blocker Card displays blocked hours badge
    const card = page.locator('app-blocker-card', { hasText: blockerTitle });
    await expect(card).toBeVisible({ timeout: 10000 });
    const blockedBadge = card.locator('.blocked-hours-badge');
    await expect(blockedBadge).toBeVisible();
    await expect(blockedBadge).toContainText('16h blocked (-16h capacity)');

    // 6. Navigate to Work Items & Granular Latency Pipeline
    await page.locator('.tab-btn', { hasText: 'Work Items' }).click();
    await expect(page.locator('.work-items-section')).toBeVisible({ timeout: 15000 });

    // 7. Verify Net Capacity pill displays deduction for blockers
    const netCapacityPill = page.locator('.metric-pill', { hasText: 'Net Capacity' });
    await expect(netCapacityPill).toBeVisible();
    await expect(netCapacityPill).toContainText(/-\d+(\.\d+)?h blockers/);

    // 8. Toggle Burndown Graph if collapsed
    const burndownToggle = page.locator('.btn-burndown-toggle');
    await expect(burndownToggle).toBeVisible();
    if (await page.locator('app-sprint-burndown-chart').isHidden()) {
      await burndownToggle.click();
    }

    const burndownChart = page.locator('app-sprint-burndown-chart');
    await expect(burndownChart).toBeVisible({ timeout: 10000 });

    // 9. Verify Blocker Drag KPI block
    const blockerDragKpi = burndownChart.locator('.kpi-block', { hasText: 'Blocker Drag Impact' });
    await expect(blockerDragKpi).toBeVisible();
    await expect(blockerDragKpi).toContainText(/-\d+(\.\d+)?h/);

    // 10. Verify Blocker Slowdown Callout Message in Burndown
    const slowdownCallout = burndownChart.locator('.blocker-slowdown-msg');
    await expect(slowdownCallout).toBeVisible();
    await expect(slowdownCallout).toContainText(/Because of blockers for \d+ hours, our capacity went down by \d+h/);

    // 11. Navigate to Executive Dashboard & Export Hub
    await page.locator('.tab-btn', { hasText: 'Executive' }).click();
    await expect(page.locator('.executive-section')).toBeVisible({ timeout: 15000 });

    // 12. Verify Executive Blocker Capacity Drag Panel & Narrative
    const execDragPanel = page.locator('.blocker-drag-panel');
    await expect(execDragPanel).toBeVisible({ timeout: 10000 });
    await expect(execDragPanel).toContainText(/Blocked/);

    const execStatement = page.locator('.blocker-drag-statement');
    await expect(execStatement).toBeVisible();
    await expect(execStatement).toContainText(/Because of blockers for \d+ hours, our capacity went down by \d+h/);

    // 13. Navigate back to Blocker SLA Radar to test Resolve Modal with Blocked Hours
    await page.locator('.tab-btn', { hasText: 'Blocker' }).click();
    await expect(page.locator('.blockers-section')).toBeVisible({ timeout: 15000 });

    const resolveCard = page.locator('app-blocker-card', { hasText: blockerTitle });
    await expect(resolveCard).toBeVisible();
    await resolveCard.locator('.btn-resolve').click();

    // 14. Verify Resolve modal has final blocked hours initialized
    await expect(page.locator('#resolveBlockedHoursInput')).toBeVisible({ timeout: 5000 });
    await expect(page.locator('#resolveBlockedHoursInput')).toHaveValue('16');

    // Update blocked hours to 18 on resolution with notes
    await page.locator('#resolveBlockedHoursInput').fill('18');
    await page.locator('#resolutionNotesTextarea').fill('Resolved through auto-failover to secondary region.');

    await page.locator('app-resolve-blocker-modal .btn-confirm').click();
    await expect(page.locator('#resolveBlockedHoursInput')).not.toBeVisible({ timeout: 5000 });

    // 15. Verify resolved badge & updated hours on card
    await expect(resolveCard.locator('.status-badge')).toContainText('Resolved');
    await expect(resolveCard.locator('.blocked-hours-badge')).toContainText('18h blocked (-18h capacity)');
  });

  test('should support editing blocked hours and updating sprint burndown drag in real time', async ({ page }) => {
    const timestamp = Date.now();
    const blockerTitle = `E2E Network Glitch ${timestamp}`;
    const blockerDesc = 'Intermittent packet loss on offshore gateway.';

    // 1. Log Blocker with 10h
    await page.locator('.tab-btn', { hasText: 'Blocker' }).click();
    await expect(page.locator('.blockers-section')).toBeVisible({ timeout: 15000 });

    await page.locator('.blockers-section .section-header button', { hasText: 'Log Blocker' }).click();
    await expect(page.locator('#blockerSummaryInput')).toBeVisible();

    const categoryCard = page.locator('app-add-blocker-modal .category-card', { hasText: 'Environment & Access' });
    await expect(categoryCard).toBeVisible();
    await categoryCard.click();

    await page.locator('#blockerSummaryInput').fill(blockerTitle);
    await page.locator('#blockerContextTextarea').fill(blockerDesc);
    await page.locator('#blockedHoursInput').fill('10');
    await page.locator('app-add-blocker-modal .btn-save').click();
    await expect(page.locator('#blockerSummaryInput')).not.toBeVisible({ timeout: 5000 });

    const card = page.locator('app-blocker-card', { hasText: blockerTitle });
    await expect(card).toBeVisible({ timeout: 10000 });
    await expect(card.locator('.blocked-hours-badge')).toContainText('10h blocked (-10h capacity)');

    // 2. Edit blocker: increase to 24h
    await card.locator('.edit-btn').click();
    await expect(page.locator('#blockedHoursInput')).toBeVisible({ timeout: 5000 });
    await expect(page.locator('#blockedHoursInput')).toHaveValue('10');

    await page.locator('#blockedHoursInput').fill('24');
    await page.locator('app-add-blocker-modal .btn-save').click();
    await expect(page.locator('#blockedHoursInput')).not.toBeVisible({ timeout: 5000 });

    await expect(card.locator('.blocked-hours-badge')).toContainText('24h blocked (-24h capacity)');

    // 3. Navigate to Work Items and verify Burndown reflects drag
    await page.locator('.tab-btn', { hasText: 'Work Items' }).click();
    await expect(page.locator('.work-items-section')).toBeVisible({ timeout: 15000 });

    const burndownToggle = page.locator('.btn-burndown-toggle');
    await expect(burndownToggle).toBeVisible();
    if (await page.locator('app-sprint-burndown-chart').isHidden()) {
      await burndownToggle.click();
    }

    const burndownChart = page.locator('app-sprint-burndown-chart');
    await expect(burndownChart).toBeVisible({ timeout: 10000 });

    const slowdownCallout = burndownChart.locator('.blocker-slowdown-msg');
    await expect(slowdownCallout).toBeVisible();
    await expect(slowdownCallout).toContainText(/Because of blockers for \d+ hours, our capacity went down by \d+h/);

    // 4. Cleanup: navigate back and delete blocker
    await page.locator('.tab-btn', { hasText: 'Blocker' }).click();
    await expect(page.locator('.blockers-section')).toBeVisible({ timeout: 15000 });

    const toDeleteCard = page.locator('app-blocker-card', { hasText: blockerTitle });
    await toDeleteCard.locator('.edit-btn').click();
    await expect(page.locator('app-add-blocker-modal .btn-danger')).toBeVisible();
    await page.locator('app-add-blocker-modal .btn-danger').click();

    await expect(toDeleteCard).not.toBeVisible({ timeout: 10000 });
  });
});


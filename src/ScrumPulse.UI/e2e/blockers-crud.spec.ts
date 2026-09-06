import { test, expect } from '@playwright/test';
import { unlockScrumMaster } from './helpers';

test.describe('Blocker SLA Radar End-to-End Lifecycle', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await unlockScrumMaster(page);
    await page.locator('.tab-btn', { hasText: 'Blocker' }).click();
    await expect(page.locator('.blockers-section')).toBeVisible({ timeout: 15000 });
  });

  test('should raise, edit, resolve, and delete a blocker with SLA radar tracking', async ({ page }) => {
    const timestamp = Date.now();
    const blockerTitle = `E2E Database Migration Blocker ${timestamp}`;
    const blockerDesc = 'Waiting on secure connection strings and firewall clearance for test DB.';

    // 1. Click Log Blocker
    const logBtn = page.locator('.blockers-section .section-header button', { hasText: 'Log Blocker' });
    await expect(logBtn).toBeVisible();
    await logBtn.click();

    await expect(page.locator('#blockerSummaryInput')).toBeVisible();

    // 2. Select Category & SLA
    const categoryCard = page.locator('.category-card').first();
    if (await categoryCard.isVisible()) {
      await categoryCard.click();
    }

    await page.locator('#blockerSummaryInput').fill(blockerTitle);
    await page.locator('#blockerContextTextarea').fill(blockerDesc);

    const slaChip = page.locator('.sla-chip', { hasText: '4h' });
    if (await slaChip.isVisible()) {
      await slaChip.click();
    }

    // Save Blocker
    await page.locator('app-add-blocker-modal .btn-save').click();
    await expect(page.locator('#blockerSummaryInput')).not.toBeVisible({ timeout: 5000 });

    // 3. Verify Blocker Card on Radar
    const card = page.locator('app-blocker-card', { hasText: blockerTitle });
    await expect(card).toBeVisible({ timeout: 10000 });
    await expect(card.locator('.blocker-desc')).toContainText(blockerDesc);

    // 4. Edit Blocker
    const editBtn = card.locator('.edit-btn');
    await expect(editBtn).toBeVisible();
    await editBtn.click();

    await expect(page.locator('#blockerSummaryInput')).toBeVisible();
    const updatedTitle = `${blockerTitle} - Escalated`;
    await page.locator('#blockerSummaryInput').fill(updatedTitle);
    await page.locator('app-add-blocker-modal .btn-save').click();
    await expect(page.locator('#blockerSummaryInput')).not.toBeVisible({ timeout: 5000 });

    const updatedCard = page.locator('app-blocker-card', { hasText: updatedTitle });
    await expect(updatedCard).toBeVisible({ timeout: 10000 });

    // 5. Resolve Blocker via Modal
    const resolveBtn = updatedCard.locator('.btn-resolve');
    await expect(resolveBtn).toBeVisible();
    await resolveBtn.click();

    await expect(page.locator('#resolutionNotesTextarea')).toBeVisible();
    await page.locator('#resolutionNotesTextarea').fill('Resolved via automated secret provisioning.');
    await page.locator('app-resolve-blocker-modal .btn-confirm').click();
    await expect(page.locator('#resolutionNotesTextarea')).not.toBeVisible({ timeout: 5000 });

    // Verify card is now marked Resolved
    await expect(updatedCard.locator('.status-badge')).toContainText('Resolved');

    // 6. Delete Blocker via Confirmation Modal
    const deleteBtn = updatedCard.locator('.delete-btn');
    await expect(deleteBtn).toBeVisible();
    await deleteBtn.click();

    const confirmBtn = page.locator('.btn-confirm-action', { hasText: 'Delete Blocker' });
    await expect(confirmBtn).toBeVisible();
    await confirmBtn.click();
    await expect(confirmBtn).not.toBeVisible({ timeout: 5000 });

    // Verify Blocker is completely deleted
    await expect(page.locator('app-blocker-card', { hasText: updatedTitle })).not.toBeVisible({ timeout: 10000 });
  });
});

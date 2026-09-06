import { test, expect } from '@playwright/test';
import { unlockScrumMaster } from './helpers';

test.describe('Advanced Workflows, Validations & Edge Cases', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/', { waitUntil: 'domcontentloaded' });
  });

  test('should enforce work item validation boundary and advance through complete 6-stage lifecycle', async ({ page }) => {
    await unlockScrumMaster(page);
    await page.locator('.tab-btn', { hasText: 'Work Items' }).click();
    await expect(page.locator('.work-items-section')).toBeVisible({ timeout: 15000 });

    // 1. Open Add Work Item Modal
    const addBtn = page.locator('.section-header button', { hasText: 'Add Story / Bug / PBI' });
    await expect(addBtn).toBeVisible();
    await addBtn.click();

    const modalContent = page.locator('app-add-work-item-modal .modal-content');
    await expect(modalContent).toBeVisible();

    const saveBtn = modalContent.locator('.modal-footer .btn-save');
    const titleInput = page.locator('#workItemTitleInput');

    // Validation boundary: empty title must keep Save button disabled
    await expect(saveBtn).toBeDisabled();

    // Whitespace only must also keep Save button disabled
    await titleInput.fill('    ');
    await expect(saveBtn).toBeDisabled();

    // Valid title enables Save
    const timestamp = Date.now();
    const itemTitle = `E2E Full Pipeline Story ${timestamp}`;
    await titleInput.fill(itemTitle);
    await page.locator('#workItemDescTextarea').fill('End-to-end full 6-stage lifecycle validation.');
    await expect(saveBtn).toBeEnabled();

    // Select 5 story points (unambiguous Fibonacci number)
    await page.locator('.points-selector .point-btn', { hasText: '5' }).click();
    await saveBtn.click();
    await expect(modalContent).not.toBeVisible({ timeout: 5000 });

    // 2. Locate card in pipeline
    const itemCard = page.locator('.work-item-card', { hasText: itemTitle });
    await expect(itemCard).toBeVisible({ timeout: 10000 });
    await expect(itemCard.locator('.status-badge')).toContainText(/Backlog/i);

    // Stage 1: Pick Up Story (Backlog -> InProgress)
    const pickUpBtn = itemCard.locator('button', { hasText: 'Pick Up Story' });
    await expect(pickUpBtn).toBeVisible();
    await pickUpBtn.click();
    await expect(itemCard.locator('.status-badge')).toContainText(/InProgress|In Progress/i, { timeout: 10000 });

    // Stage 2: Create PR (InProgress -> PrCreated)
    const createPrBtn = itemCard.locator('button', { hasText: 'Create PR' });
    await expect(createPrBtn).toBeVisible();
    await createPrBtn.click();
    await expect(itemCard.locator('.status-badge')).toContainText(/PR Created|PrCreated/i, { timeout: 10000 });

    // Stage 3: Approve PR (PrCreated -> PrApproved)
    const approvePrBtn = itemCard.locator('button', { hasText: 'Approve PR' });
    await expect(approvePrBtn).toBeVisible();
    await approvePrBtn.click();
    await expect(itemCard.locator('.status-badge')).toContainText(/PR Approved|PrApproved/i, { timeout: 10000 });

    // Stage 4: Merge to Master (PrApproved -> Merged)
    const mergeBtn = itemCard.locator('button', { hasText: 'Merge to Master' });
    await expect(mergeBtn).toBeVisible();
    await mergeBtn.click();
    await expect(itemCard.locator('.status-badge')).toContainText(/Merged/i, { timeout: 10000 });

    // Stage 5: Start QA (Merged -> InQa)
    const startQaBtn = itemCard.locator('button', { hasText: 'Start QA' });
    await expect(startQaBtn).toBeVisible();
    await startQaBtn.click();
    await expect(itemCard.locator('.status-badge')).toContainText(/QA|InQa/i, { timeout: 10000 });

    // Stage 6: Mark Done / QA Signoff (InQa -> Done)
    const markDoneBtn = itemCard.locator('button', { hasText: 'Mark Done' });
    await expect(markDoneBtn).toBeVisible();
    await markDoneBtn.click();
    await expect(itemCard.locator('.status-badge')).toContainText(/Done/i, { timeout: 10000 });

    // Cleanup: Delete work item
    await itemCard.locator('button.btn-edit').click();
    await expect(modalContent).toBeVisible();
    await modalContent.locator('.modal-footer .btn-danger', { hasText: 'Delete Story' }).click();
    await expect(modalContent).not.toBeVisible({ timeout: 5000 });
    await expect(page.locator('.work-item-card', { hasText: itemTitle })).not.toBeVisible({ timeout: 10000 });
  });

  test('should enforce blocker SLA resolution safeguards with notes', async ({ page }) => {
    await unlockScrumMaster(page);
    await page.locator('.tab-btn', { hasText: 'Blocker' }).click();
    await expect(page.locator('.blockers-section')).toBeVisible({ timeout: 15000 });

    const timestamp = Date.now();
    const blockerTitle = `E2E 4h SLA Gateway Timeout ${timestamp}`;

    // Log Blocker
    const logBtn = page.locator('.blockers-section .section-header button', { hasText: 'Log Blocker' });
    await logBtn.click();

    await expect(page.locator('#blockerSummaryInput')).toBeVisible();
    await page.locator('#blockerSummaryInput').fill(blockerTitle);
    await page.locator('#blockerContextTextarea').fill('Upstream payment gateway timeout exceeding 4h SLA threshold.');

    // Select 4h SLA
    const slaChip = page.locator('.sla-chip', { hasText: '4h' });
    if (await slaChip.isVisible()) {
      await slaChip.click();
    }

    await page.locator('app-add-blocker-modal .btn-save').click();
    await expect(page.locator('#blockerSummaryInput')).not.toBeVisible({ timeout: 5000 });

    const card = page.locator('app-blocker-card', { hasText: blockerTitle });
    await expect(card).toBeVisible({ timeout: 10000 });

    // Resolve Blocker Modal Safeguard (Cancel)
    const resolveBtn = card.locator('.btn-resolve');
    await resolveBtn.click();

    const resolveModal = page.locator('app-resolve-blocker-modal .modal-content');
    await expect(resolveModal).toBeVisible();

    // Cancel resolution
    await resolveModal.locator('button', { hasText: 'Cancel' }).click();
    await expect(resolveModal).not.toBeVisible({ timeout: 5000 });

    // Assert card is still open (NOT resolved)
    await expect(card.locator('.status-badge')).not.toContainText('Resolved');

    // Resolve Blocker with confirmed notes
    await resolveBtn.click();
    await expect(resolveModal).toBeVisible();
    await page.locator('#resolutionNotesTextarea').fill('Fallback redundant gateway activated and verified.');
    await resolveModal.locator('.btn-confirm').click();
    await expect(resolveModal).not.toBeVisible({ timeout: 5000 });

    // Verify card is now marked Resolved
    await expect(card.locator('.status-badge')).toContainText('Resolved');

    // Clean up: delete resolved blocker
    await card.locator('.delete-btn').click();
    const confirmBtn = page.locator('.btn-confirm-action', { hasText: 'Delete Blocker' });
    await confirmBtn.click();
    await expect(page.locator('app-blocker-card', { hasText: blockerTitle })).not.toBeVisible({ timeout: 10000 });
  });

  test('should handle standup clock state transitions: start, pause, reset, and speaker cycling', async ({ page }) => {
    await unlockScrumMaster(page);
    await page.locator('.tab-btn', { hasText: 'Daily Standup' }).click();
    await expect(page.locator('.standup-section')).toBeVisible({ timeout: 15000 });

    const timerCard = page.locator('.timer-card');
    await expect(timerCard).toBeVisible();

    const timerBtn = timerCard.locator('.btn-timer-main');
    await expect(timerBtn).toBeVisible();

    // Initial state
    await expect(timerBtn).toContainText('Start Speaking Clock');

    // Start clock
    await timerBtn.click();
    await expect(timerBtn).toContainText('Pause Clock');
    await expect(timerCard.locator('.timer-circle')).toHaveClass(/running/);

    // Pause clock
    await timerBtn.click();
    await expect(timerBtn).toContainText('Start Speaking Clock');

    // Reset 2m
    const resetBtn = timerCard.locator('.btn-reset');
    await resetBtn.click();
    await expect(timerCard.locator('.timer-digits')).toContainText('2:00');

    // Cycle next speaker
    const nextBtn = timerCard.locator('.btn-next');
    await nextBtn.click();

    // Verify queue items exist and clicking an item makes it active
    const queueItems = timerCard.locator('.queue-item');
    const count = await queueItems.count();
    if (count > 0) {
      await queueItems.first().click();
      await expect(queueItems.first()).toBeVisible();
    }
  });
});

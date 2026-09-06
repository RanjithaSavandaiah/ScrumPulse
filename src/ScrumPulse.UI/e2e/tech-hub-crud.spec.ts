import { test, expect } from '@playwright/test';
import { unlockScrumMaster } from './helpers';

test.describe('Tech Hub: Tech Debt Backlog & Tech Talks Lifecycle', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/', { waitUntil: 'domcontentloaded' });
    await unlockScrumMaster(page);
    await page.locator('.tab-btn', { hasText: 'Tech Debt' }).click();
    await expect(page.locator('.tech-hub-section')).toBeVisible({ timeout: 15000 });
  });

  test('should log, edit, resolve, and delete technical debt item with cancel safeguard', async ({ page }) => {
    const timestamp = Date.now();
    const debtTitle = `E2E Tech Debt ${timestamp}`;
    const updatedDebtTitle = `${debtTitle} - V2 Reactive Architecture`;

    // 1. Open Log Tech Debt Modal
    const addDebtBtn = page.locator('.btn-add-debt');
    await expect(addDebtBtn).toBeVisible();
    await addDebtBtn.click();

    await expect(page.locator('#techDebtTitleInput')).toBeVisible();

    // 2. Fill Form
    await page.locator('#techDebtTitleInput').fill(debtTitle);
    await page.locator('#techDebtHoursInput').fill('16');
    await page.locator('#techDebtDescriptionTextarea').fill('Migrate synchronous HTTP calls to reactive RxJS observables.');

    // Save
    await page.locator('app-tech-debt-modal button[type="submit"]').click();
    await expect(page.locator('#techDebtTitleInput')).not.toBeVisible({ timeout: 5000 });

    // 3. Verify item in list
    const debtCard = page.locator('.hub-card', { hasText: debtTitle });
    await expect(debtCard).toBeVisible({ timeout: 10000 });
    await expect(debtCard.locator('.badge-hours')).toContainText('16h');

    // 4. Edit Tech Debt Item
    const editBtn = debtCard.locator('button[title="Edit Tech Debt"]');
    await expect(editBtn).toBeVisible();
    await editBtn.click();

    const editModal = page.locator('app-tech-debt-modal .modal-box');
    await expect(editModal).toBeVisible();
    await expect(editModal.locator('.modal-title')).toContainText('Edit Tech Debt Item');

    // Update title and estimated hours
    await page.locator('#techDebtTitleInput').fill(updatedDebtTitle);
    await page.locator('#techDebtHoursInput').fill('24');
    await editModal.locator('button[type="submit"]', { hasText: 'Save Changes' }).click();
    await expect(editModal).not.toBeVisible({ timeout: 5000 });

    // Verify card updated
    const updatedCard = page.locator('.hub-card', { hasText: updatedDebtTitle });
    await expect(updatedCard).toBeVisible({ timeout: 10000 });
    await expect(updatedCard.locator('.badge-hours')).toContainText('24h');

    // 5. Toggle Resolve Tech Debt
    const resolveBtn = updatedCard.locator('button[title="Mark as Resolved"]');
    await expect(resolveBtn).toBeVisible();
    await resolveBtn.click();

    // Verify card becomes resolved
    await expect(updatedCard).toHaveClass(/resolved-card/, { timeout: 5000 });

    // 6. Delete Tech Debt via confirm modal with cancel safeguard
    const deleteBtn = updatedCard.locator('.btn-delete');
    await expect(deleteBtn).toBeVisible();
    await deleteBtn.click();

    const confirmModal = page.locator('app-confirm-modal .modal-box');
    await expect(confirmModal).toBeVisible();
    await expect(confirmModal).toContainText('Delete Debt Item');

    // Cancel safeguard: item stays
    await confirmModal.locator('.btn-secondary').click();
    await expect(confirmModal).not.toBeVisible({ timeout: 5000 });
    await expect(updatedCard).toBeVisible();

    // Confirm deletion
    await deleteBtn.click();
    await expect(confirmModal).toBeVisible();
    const confirmBtn = page.locator('.btn-confirm-action', { hasText: 'Delete Debt Item' });
    await expect(confirmBtn).toBeVisible();
    await confirmBtn.click();
    await expect(confirmBtn).not.toBeVisible({ timeout: 5000 });

    // Verify removed
    await expect(page.locator('.hub-card', { hasText: updatedDebtTitle })).not.toBeVisible({ timeout: 10000 });
  });

  test('should schedule, edit, and delete a tech sharing session with cancel safeguard', async ({ page }) => {
    const timestamp = Date.now();
    const talkTopic = `E2E Tech Talk ${timestamp}`;
    const updatedTopic = `${talkTopic} - Advanced Masterclass`;

    // 1. Open Log Tech Talk Modal
    const addTalkBtn = page.locator('.btn-add-talk');
    await expect(addTalkBtn).toBeVisible();
    await addTalkBtn.click();

    await expect(page.locator('#techTalkTopicInput')).toBeVisible();

    // 2. Fill Form
    await page.locator('#techTalkTopicInput').fill(talkTopic);

    // Select presenter if options exist
    const presenterSelect = page.locator('#techTalkPresenterSelect');
    const options = await presenterSelect.locator('option').all();
    if (options.length > 0) {
      const val = await options[0].getAttribute('value');
      if (val) await presenterSelect.selectOption(val);
    }

    const today = new Date().toISOString().split('T')[0];
    await page.locator('#techTalkDateInput').fill(today);
    await page.locator('#techTalkTakeawaysTextarea').fill('Deep dive into automated Playwright testing best practices.');

    // Save
    await page.locator('app-tech-talk-modal button[type="submit"]').click();
    await expect(page.locator('#techTalkTopicInput')).not.toBeVisible({ timeout: 5000 });

    // 3. Verify talk in list
    const talkCard = page.locator('.talk-card', { hasText: talkTopic });
    await expect(talkCard).toBeVisible({ timeout: 10000 });

    // 4. Edit Tech Talk
    const editBtn = talkCard.locator('button[title="Edit Tech Talk"]');
    await expect(editBtn).toBeVisible();
    await editBtn.click();

    const editModal = page.locator('app-tech-talk-modal .modal-box');
    await expect(editModal).toBeVisible();
    await expect(editModal.locator('.modal-title')).toContainText('Edit Tech Sharing Session');

    // Update topic and takeaways
    await page.locator('#techTalkTopicInput').fill(updatedTopic);
    await page.locator('#techTalkTakeawaysTextarea').fill('Updated insights on Playwright CI automation and code coverage.');
    await editModal.locator('button[type="submit"]', { hasText: 'Save Changes' }).click();
    await expect(editModal).not.toBeVisible({ timeout: 5000 });

    // Verify updated talk in list
    const updatedTalkCard = page.locator('.talk-card', { hasText: updatedTopic });
    await expect(updatedTalkCard).toBeVisible({ timeout: 10000 });

    // 5. Delete Tech Talk via confirm modal with cancel safeguard
    const deleteBtn = updatedTalkCard.locator('.btn-delete');
    await expect(deleteBtn).toBeVisible();
    await deleteBtn.click();

    const confirmModal = page.locator('app-confirm-modal .modal-box');
    await expect(confirmModal).toBeVisible();
    await expect(confirmModal).toContainText('Delete Session');

    // Cancel safeguard: talk stays
    await confirmModal.locator('.btn-secondary').click();
    await expect(confirmModal).not.toBeVisible({ timeout: 5000 });
    await expect(updatedTalkCard).toBeVisible();

    // Confirm deletion
    await deleteBtn.click();
    await expect(confirmModal).toBeVisible();
    const confirmBtn = page.locator('.btn-confirm-action', { hasText: 'Delete Session' });
    await expect(confirmBtn).toBeVisible();
    await confirmBtn.click();
    await expect(confirmBtn).not.toBeVisible({ timeout: 5000 });

    // Verify removed
    await expect(page.locator('.talk-card', { hasText: updatedTopic })).not.toBeVisible({ timeout: 10000 });
  });
});

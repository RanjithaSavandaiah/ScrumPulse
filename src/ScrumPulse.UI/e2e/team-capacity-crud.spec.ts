import { test, expect } from '@playwright/test';
import { unlockScrumMaster } from './helpers';

test.describe('Team Roster & Leave Capacity Management Lifecycle', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/', { waitUntil: 'domcontentloaded' });
    await unlockScrumMaster(page);
  });

  test('should add new squad member and remove member via confirm modal', async ({ page }) => {
    // 1. Navigate to Team Roster
    await page.locator('.tab-btn', { hasText: 'Team Roster' }).click();
    await expect(page.locator('.team-roster-section')).toBeVisible({ timeout: 15000 });

    const timestamp = Date.now();
    const newMemberName = `E2E Engineer ${timestamp}`;

    // 2. Open Add Member Modal
    const addMemberBtn = page.locator('.team-roster-section button', { hasText: 'Add Team Member' });
    await expect(addMemberBtn).toBeVisible();
    await addMemberBtn.click();

    await expect(page.locator('#newMemberNameInput')).toBeVisible();

    // Select Developer role
    const devRoleCard = page.locator('.role-card', { hasText: 'Developer' });
    if (await devRoleCard.isVisible()) {
      await devRoleCard.click();
    }

    // Fill name
    await page.locator('#newMemberNameInput').fill(newMemberName);

    // Save
    await page.locator('.modal-footer .btn-save').click();
    await expect(page.locator('#newMemberNameInput')).not.toBeVisible({ timeout: 5000 });

    // 3. Verify member card in roster grid
    const memberCard = page.locator('.member-card', { hasText: newMemberName });
    await expect(memberCard).toBeVisible({ timeout: 10000 });

    // 4. Delete member via confirmation modal
    const deleteBtn = memberCard.locator('.delete-btn');
    await expect(deleteBtn).toBeVisible();
    await deleteBtn.click();

    const confirmBtn = page.locator('.btn-confirm-action', { hasText: 'Remove Member' });
    await expect(confirmBtn).toBeVisible();
    await confirmBtn.click();
    await expect(confirmBtn).not.toBeVisible({ timeout: 5000 });

    // Verify member is removed
    await expect(page.locator('.member-card', { hasText: newMemberName })).not.toBeVisible({ timeout: 10000 });
  });

  test('should book planned leave and delete leave record', async ({ page }) => {
    // 1. Navigate to Leave & Capacity
    await page.locator('.tab-btn', { hasText: 'Leave & Capacity' }).click();
    await expect(page.locator('.capacity-section')).toBeVisible({ timeout: 15000 });

    // 2. Click Book Planned Leave
    const bookLeaveBtn = page.locator('.capacity-section .section-header button', { hasText: 'Book Planned Leave' });
    await expect(bookLeaveBtn).toBeVisible();
    await bookLeaveBtn.click();

    await expect(page.locator('#leaveMemberSelect')).toBeVisible();

    // Select first team member
    const memberSelect = page.locator('#leaveMemberSelect');
    const options = await memberSelect.locator('option').all();
    if (options.length > 1) {
      const val = await options[1].getAttribute('value');
      if (val) await memberSelect.selectOption(val);
    }

    // Set Dates
    const today = new Date().toISOString().split('T')[0];
    await page.locator('#leaveStartDateInput').fill(today);
    await page.locator('#leaveEndDateInput').fill(today);

    // Select Sick Leave category explicitly
    const sickLeaveBtn = page.locator('app-book-leave-modal .type-card', { hasText: 'Sick Leave' });
    await expect(sickLeaveBtn).toBeVisible();
    await sickLeaveBtn.click();
    await expect(sickLeaveBtn).toHaveClass(/selected/);

    // Choose preset reason
    const presetChip = page.locator('.preset-chip', { hasText: 'Medical Checkup / Sick Leave' });
    if (await presetChip.isVisible()) {
      await presetChip.click();
    }

    // Save
    const saveBtn = page.locator('app-book-leave-modal .btn-save');
    await expect(saveBtn).toBeEnabled();
    await saveBtn.click();
    await expect(page.locator('#leaveMemberSelect')).not.toBeVisible({ timeout: 5000 });

    // Verify confirmation popup card
    await expect(page.locator('app-confirmation-popup .confirmation-popup-card', { hasText: 'Sick Leave' })).toBeVisible({ timeout: 5000 });

    // 3. Verify Leave Table has the newly booked leave with category "Sick Leave" and NOT "Privilege"
    const leaveRow = page.locator('.leaves-table .leave-row').first();
    await expect(leaveRow).toBeVisible({ timeout: 10000 });

    const leaveTypePill = leaveRow.locator('.leave-type-pill');
    await expect(leaveTypePill).toContainText('Sick Leave');
    await expect(leaveTypePill).not.toContainText('Privilege');

    const deleteLeaveBtn = leaveRow.locator('.delete-btn');
    await expect(deleteLeaveBtn).toBeVisible();
    await deleteLeaveBtn.click();

    const confirmBtn = page.locator('.btn-confirm-action', { hasText: 'Delete Leave Record' });
    await expect(confirmBtn).toBeVisible();
    await confirmBtn.click();
    await expect(confirmBtn).not.toBeVisible({ timeout: 5000 });
  });
});

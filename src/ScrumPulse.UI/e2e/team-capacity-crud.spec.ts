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

    // 4. Delete member via confirmation modal with cancel safeguard
    const deleteBtn = memberCard.locator('.delete-btn');
    await expect(deleteBtn).toBeVisible();
    await deleteBtn.click();

    const confirmModal = page.locator('app-confirm-modal .modal-box');
    await expect(confirmModal).toBeVisible();
    await expect(confirmModal).toContainText('Remove Squad Member');

    // Cancel safeguard: member remains intact
    await confirmModal.locator('.btn-secondary').click();
    await expect(confirmModal).not.toBeVisible({ timeout: 5000 });
    await expect(memberCard).toBeVisible();

    // Re-trigger delete and confirm
    await deleteBtn.click();
    await expect(confirmModal).toBeVisible();
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

  test('should enforce leave date range validation where end date cannot be earlier than start date', async ({ page }) => {
    // 1. Navigate to Leave & Capacity
    await page.locator('.tab-btn', { hasText: 'Leave & Capacity' }).click();
    await expect(page.locator('.capacity-section')).toBeVisible({ timeout: 15000 });

    // 2. Click Book Planned Leave
    const bookLeaveBtn = page.locator('.capacity-section .section-header button', { hasText: 'Book Planned Leave' });
    await expect(bookLeaveBtn).toBeVisible();
    await bookLeaveBtn.click();
    await expect(page.locator('#leaveMemberSelect')).toBeVisible();

    // 3. Set invalid dates: End Date before Start Date
    await page.locator('#leaveStartDateInput').fill('2026-10-20');
    await page.locator('#leaveEndDateInput').fill('2026-10-15');

    // 4. Assert date validation alert is displayed and save is disabled
    const alertBanner = page.locator('app-book-leave-modal .date-validation-alert');
    await expect(alertBanner).toBeVisible({ timeout: 5000 });
    await expect(alertBanner).toContainText('cannot be earlier than Start date');

    const saveBtn = page.locator('app-book-leave-modal .btn-save');
    await expect(saveBtn).toBeDisabled();

    // 5. Correct the End Date to be valid
    await page.locator('#leaveEndDateInput').fill('2026-10-22');
    await expect(alertBanner).not.toBeVisible({ timeout: 5000 });
    await expect(saveBtn).toBeEnabled();

    // 6. Close modal
    await page.locator('app-book-leave-modal .close-btn').click();
    await expect(page.locator('#leaveMemberSelect')).not.toBeVisible({ timeout: 5000 });
  });

  test('should book leave with Comp Off category for specific squad member, verify table pill and confirm modal text', async ({ page }) => {
    // 1. Navigate to Leave & Capacity
    await page.locator('.tab-btn', { hasText: 'Leave & Capacity' }).click();
    await expect(page.locator('.capacity-section')).toBeVisible({ timeout: 15000 });

    // 2. Open Book Planned Leave modal
    const bookLeaveBtn = page.locator('.capacity-section .section-header button', { hasText: 'Book Planned Leave' });
    await expect(bookLeaveBtn).toBeVisible();
    await bookLeaveBtn.click();
    await expect(page.locator('#leaveMemberSelect')).toBeVisible();

    // 3. Select second squad member if available
    const memberSelect = page.locator('#leaveMemberSelect');
    const memberOptions = await memberSelect.locator('option').all();
    let selectedMemberName = '';
    if (memberOptions.length > 2) {
      const val = await memberOptions[2].getAttribute('value');
      const text = await memberOptions[2].textContent();
      selectedMemberName = text ? text.split('(')[0].trim() : '';
      if (val) await memberSelect.selectOption(val);
    } else if (memberOptions.length > 1) {
      const val = await memberOptions[1].getAttribute('value');
      const text = await memberOptions[1].textContent();
      selectedMemberName = text ? text.split('(')[0].trim() : '';
      if (val) await memberSelect.selectOption(val);
    }

    // Set dates
    await page.locator('#leaveStartDateInput').fill('2026-11-02');
    await page.locator('#leaveEndDateInput').fill('2026-11-03');

    // 4. Select Comp Off category card explicitly
    const compOffCard = page.locator('app-book-leave-modal .type-card', { hasText: 'Comp Off' });
    await expect(compOffCard).toBeVisible();
    await compOffCard.click();
    await expect(compOffCard).toHaveClass(/selected/);

    await page.locator('#leaveReasonInput').fill('Sprint weekend support comp off');

    // Save
    const saveBtn = page.locator('app-book-leave-modal .btn-save');
    await expect(saveBtn).toBeEnabled();
    await saveBtn.click();
    await expect(page.locator('#leaveMemberSelect')).not.toBeVisible({ timeout: 5000 });

    // 5. Verify confirmation popup
    await expect(page.locator('app-confirmation-popup .confirmation-popup-card', { hasText: 'Comp Off' })).toBeVisible({ timeout: 5000 });

    // 6. Verify table row has category "Comp Off" and NOT "Privilege"
    const compRow = page.locator('.leaves-table .leave-row', { hasText: 'Comp Off' }).first();
    await expect(compRow).toBeVisible({ timeout: 10000 });

    const pill = compRow.locator('.leave-type-pill');
    await expect(pill).toContainText('Comp Off');
    await expect(pill).not.toContainText('Privilege');

    if (selectedMemberName) {
      await expect(compRow).toContainText(selectedMemberName);
    }

    // 7. Delete Comp Off leave and verify confirmation modal text
    const deleteBtn = compRow.locator('.delete-btn');
    await expect(deleteBtn).toBeVisible();
    await deleteBtn.click();

    const confirmModal = page.locator('app-confirm-modal .modal-box');
    await expect(confirmModal).toBeVisible();
    await expect(confirmModal).toContainText('Comp Off');
    await expect(confirmModal).toContainText('Delete Leave Record');

    const confirmActionBtn = confirmModal.locator('.btn-confirm-action', { hasText: 'Delete Leave Record' });
    await expect(confirmActionBtn).toBeVisible();
    await confirmActionBtn.click();
    await expect(confirmModal).not.toBeVisible({ timeout: 5000 });
  });

  test('should edit an existing leave record, update reason and leave type, and verify updated table pill', async ({ page }) => {
    // 1. Navigate to Leave & Capacity
    await page.locator('.tab-btn', { hasText: 'Leave & Capacity' }).click();
    await expect(page.locator('.capacity-section')).toBeVisible({ timeout: 15000 });

    // 2. Book a leave record to edit
    const timestamp = Date.now();
    const originalReason = `Initial Dental Checkup ${timestamp}`;
    const updatedReason = `Updated Specialist Treatment ${timestamp}`;

    const bookLeaveBtn = page.locator('.capacity-section .section-header button', { hasText: 'Book Planned Leave' });
    await expect(bookLeaveBtn).toBeVisible();
    await bookLeaveBtn.click();
    await expect(page.locator('#leaveMemberSelect')).toBeVisible();

    const memberSelect = page.locator('#leaveMemberSelect');
    const memberOptions = await memberSelect.locator('option').all();
    if (memberOptions.length > 1) {
      const val = await memberOptions[1].getAttribute('value');
      if (val) await memberSelect.selectOption(val);
    }

    const today = new Date().toISOString().split('T')[0];
    await page.locator('#leaveStartDateInput').fill(today);
    await page.locator('#leaveEndDateInput').fill(today);
    await page.locator('#leaveReasonInput').fill(originalReason);

    const saveBtn = page.locator('app-book-leave-modal .btn-save');
    await saveBtn.click();
    await expect(page.locator('#leaveMemberSelect')).not.toBeVisible({ timeout: 5000 });

    // 3. Locate the created row
    const leaveRow = page.locator('.leaves-table .leave-row', { hasText: originalReason });
    await expect(leaveRow).toBeVisible({ timeout: 10000 });

    // 4. Click Edit button on the row
    const editBtn = leaveRow.locator('.edit-btn');
    await expect(editBtn).toBeVisible();
    await editBtn.click();

    // 5. Verify modal opens in Edit Mode
    const editModal = page.locator('app-book-leave-modal .modal-content');
    await expect(editModal).toBeVisible();
    await expect(editModal.locator('.modal-title')).toContainText('Edit Planned Leave / PTO');
    await expect(page.locator('#leaveReasonInput')).toHaveValue(originalReason);

    // 6. Update Reason & Change Category to Sick Leave
    await page.locator('#leaveReasonInput').fill(updatedReason);

    const sickCard = editModal.locator('.type-card', { hasText: 'Sick Leave' });
    await expect(sickCard).toBeVisible();
    await sickCard.click();
    await expect(sickCard).toHaveClass(/selected/);

    // 7. Save Changes
    const saveChangesBtn = editModal.locator('.btn-save', { hasText: 'Save Changes' });
    await expect(saveChangesBtn).toBeVisible();
    await saveChangesBtn.click();
    await expect(editModal).not.toBeVisible({ timeout: 5000 });

    // 8. Verify table row reflects updated reason and Sick Leave pill
    const updatedRow = page.locator('.leaves-table .leave-row', { hasText: updatedReason });
    await expect(updatedRow).toBeVisible({ timeout: 10000 });
    await expect(updatedRow.locator('.leave-type-pill')).toContainText('Sick Leave');

    // Cleanup: delete the updated leave record
    await updatedRow.locator('.delete-btn').click();
    const confirmModal = page.locator('app-confirm-modal .modal-box');
    await expect(confirmModal).toBeVisible();
    await confirmModal.locator('.btn-confirm-action', { hasText: 'Delete Leave Record' }).click();
    await expect(confirmModal).not.toBeVisible({ timeout: 5000 });
    await expect(page.locator('.leaves-table .leave-row', { hasText: updatedReason })).not.toBeVisible({ timeout: 10000 });
  });

  test('should delete leave record directly from within edit modal with cancel safeguard', async ({ page }) => {
    // 1. Navigate to Leave & Capacity
    await page.locator('.tab-btn', { hasText: 'Leave & Capacity' }).click();
    await expect(page.locator('.capacity-section')).toBeVisible({ timeout: 15000 });

    const timestamp = Date.now();
    const testReason = `Leave For Modal Delete Test ${timestamp}`;

    // 2. Book a leave
    await page.locator('.capacity-section .section-header button', { hasText: 'Book Planned Leave' }).click();
    await expect(page.locator('#leaveMemberSelect')).toBeVisible();

    const memberSelect = page.locator('#leaveMemberSelect');
    const memberOptions = await memberSelect.locator('option').all();
    if (memberOptions.length > 1) {
      const val = await memberOptions[1].getAttribute('value');
      if (val) await memberSelect.selectOption(val);
    }

    const today = new Date().toISOString().split('T')[0];
    await page.locator('#leaveStartDateInput').fill(today);
    await page.locator('#leaveEndDateInput').fill(today);
    await page.locator('#leaveReasonInput').fill(testReason);
    await page.locator('app-book-leave-modal .btn-save').click();
    await expect(page.locator('#leaveMemberSelect')).not.toBeVisible({ timeout: 5000 });

    // 3. Open Edit modal from the row
    const leaveRow = page.locator('.leaves-table .leave-row', { hasText: testReason });
    await expect(leaveRow).toBeVisible({ timeout: 10000 });
    await leaveRow.locator('.edit-btn').click();

    const editModal = page.locator('app-book-leave-modal .modal-content');
    await expect(editModal).toBeVisible();

    // 4. Click Delete button inside Edit Modal
    const deleteInsideBtn = editModal.locator('.btn-danger', { hasText: 'Delete Leave' });
    await expect(deleteInsideBtn).toBeVisible();
    await deleteInsideBtn.click();

    // 5. Test cancel safeguard on confirm modal
    const confirmModal = page.locator('app-confirm-modal .modal-box');
    await expect(confirmModal).toBeVisible();
    await expect(confirmModal).toContainText('Delete Leave Record');

    await confirmModal.locator('.btn-secondary').click();
    await expect(confirmModal).not.toBeVisible({ timeout: 5000 });
    // Verify leave is still present in table
    await expect(page.locator('.leaves-table .leave-row', { hasText: testReason })).toBeVisible();

    // 6. Delete again and confirm
    await page.locator('.leaves-table .leave-row', { hasText: testReason }).locator('.delete-btn').click();
    await expect(confirmModal).toBeVisible();
    await confirmModal.locator('.btn-confirm-action', { hasText: 'Delete Leave Record' }).click();
    await expect(confirmModal).not.toBeVisible({ timeout: 5000 });
    await expect(page.locator('.leaves-table .leave-row', { hasText: testReason })).not.toBeVisible({ timeout: 10000 });
  });

  test('should record and display "Logged by: Developer" when Developer role logs a leave', async ({ page }) => {
    // 1. Switch role to Developer
    const roleSelect = page.locator('[data-testid="role-select"]');
    await expect(roleSelect).toBeVisible();
    await roleSelect.selectOption('Developer');
    await page.waitForTimeout(200);

    // 2. Navigate to Leave & Capacity
    await page.locator('.tab-btn', { hasText: 'Leave & Capacity' }).click();
    await expect(page.locator('.capacity-section')).toBeVisible({ timeout: 15000 });

    const timestamp = Date.now();
    const devReason = `Dev Planned Leave ${timestamp}`;

    // 3. Open Book Leave modal as Developer
    const bookBtn = page.locator('.capacity-section .section-header button', { hasText: 'Book Planned Leave' });
    await expect(bookBtn).toBeVisible();
    await bookBtn.click();

    await expect(page.locator('#leaveMemberSelect')).toBeVisible();

    // Select first member
    const memberSelect = page.locator('#leaveMemberSelect');
    const memberOptions = await memberSelect.locator('option').all();
    if (memberOptions.length > 1) {
      const val = await memberOptions[1].getAttribute('value');
      if (val) await memberSelect.selectOption(val);
    }

    const today = new Date().toISOString().split('T')[0];
    await page.locator('#leaveStartDateInput').fill(today);
    await page.locator('#leaveEndDateInput').fill(today);
    await page.locator('#leaveReasonInput').fill(devReason);

    // Save
    await page.locator('app-book-leave-modal .btn-save').click();
    await expect(page.locator('#leaveMemberSelect')).not.toBeVisible({ timeout: 5000 });

    // 4. Verify leave row displays "Logged by: Developer"
    const leaveRow = page.locator('.leaves-table .leave-row', { hasText: devReason });
    await expect(leaveRow).toBeVisible({ timeout: 10000 });
    await expect(leaveRow).toContainText('Logged by: Developer');
    await expect(leaveRow).not.toContainText('Logged by: Scrum Master');
  });

  test('should enforce mandatory reason field validation on leave booking and show meaningful success notification when booked', async ({ page }) => {
    // 1. Navigate to Leave & Capacity
    await page.locator('.tab-btn', { hasText: 'Leave & Capacity' }).click();
    await expect(page.locator('.capacity-section')).toBeVisible({ timeout: 15000 });

    // 2. Open Book Leave modal
    const bookBtn = page.locator('.capacity-section .section-header button', { hasText: 'Book Planned Leave' });
    await expect(bookBtn).toBeVisible();
    await bookBtn.click();

    await expect(page.locator('#leaveMemberSelect')).toBeVisible();

    // 2. Clear reason input and attempt save
    await page.locator('#leaveReasonInput').fill('');
    await page.locator('app-book-leave-modal .btn-save').click();

    // 3. Assert reason validation banner appears
    const alertBanner = page.locator('app-book-leave-modal .validation-alert-banner');
    await expect(alertBanner).toBeVisible();
    await expect(alertBanner).toContainText('Reason is mandatory');

    // 4. Fill valid reason and save
    const timestamp = Date.now();
    const validReason = `Annual Family Vacation ${timestamp}`;
    await page.locator('#leaveReasonInput').fill(validReason);
    await page.locator('app-book-leave-modal .btn-save').click();
    await expect(page.locator('#leaveMemberSelect')).not.toBeVisible({ timeout: 5000 });

    // 5. Verify success toast appears with meaningful text
    const toast = page.locator('.confirmation-popup-card .popup-message');
    await expect(toast).toBeVisible({ timeout: 5000 });
    await expect(toast).toContainText('added successfully');
  });
});

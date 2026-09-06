import { test, expect } from '@playwright/test';
import { unlockScrumMaster } from './helpers';

test.describe('Work Items & Sprints End-to-End Lifecycle', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/', { waitUntil: 'domcontentloaded' });
    await unlockScrumMaster(page);
    await page.locator('.tab-btn', { hasText: 'Work Items' }).click();
    await expect(page.locator('.work-items-section')).toBeVisible({ timeout: 15000 });
  });

  test('should create, advance, edit quality gates, and delete a work item', async ({ page }) => {
    const uniqueTitle = `E2E PBI ${Date.now()}`;

    // 1. Click Add Story / Bug / PBI button
    const addBtn = page.locator('.section-header button', { hasText: 'Add Story / Bug / PBI' });
    await expect(addBtn).toBeVisible();
    await addBtn.click();

    // 2. Add Modal should appear
    await expect(page.locator('#workItemTitleInput')).toBeVisible();

    // Select User Story category
    await page.locator('.type-card', { hasText: 'User Story' }).click();

    // Fill Title, Description, Acceptance Criteria
    await page.locator('#workItemTitleInput').fill(uniqueTitle);
    await page.locator('#workItemDescTextarea').fill('Comprehensive automated end to end verification story.');
    await page.locator('#workItemDoRTextarea').fill('Given automated test execution, When clicked, Then success.');

    // Select Story Points (5 Pts) & Hours (12h)
    await page.locator('.points-selector .point-btn', { hasText: '5' }).click();
    await page.locator('#workItemHoursInput').fill('12');

    // Save
    await page.locator('app-add-work-item-modal .modal-footer .btn-save').click();
    await expect(page.locator('#workItemTitleInput')).not.toBeVisible();

    // 3. Verify newly created card on board
    const createdCard = page.locator('.work-item-card', { hasText: uniqueTitle });
    await expect(createdCard).toBeVisible({ timeout: 10000 });
    await expect(createdCard.locator('.pts-badge')).toContainText('5 Pts');
    await expect(createdCard.locator('.hours-badge')).toContainText('12h');

    // 4. Test stage progression button: Pick Up Story (Backlog -> In Progress)
    const pickUpBtn = createdCard.locator('button', { hasText: 'Pick Up Story' });
    if (await pickUpBtn.isVisible()) {
      await pickUpBtn.click();
      await expect(createdCard.locator('.status-badge')).toContainText(/InProgress|In Progress/, { timeout: 10000 });
    }

    // 5. Open DoR / DoD Quality Gates modal
    await createdCard.locator('button', { hasText: 'DoR / DoD Gates' }).click();
    const gatesContent = page.locator('app-quality-gates-modal .modal-content');
    await expect(gatesContent).toBeVisible();

    // Toggle checkboxes
    await page.locator('#gateDorAcceptanceCriteria').check();
    await page.locator('#gateDodUnitTests').check();
    await gatesContent.locator('button', { hasText: 'Save Gates' }).click();
    await expect(gatesContent).not.toBeVisible();

    // 6. Edit Story Details
    await createdCard.locator('button.btn-edit').click();
    await expect(page.locator('#workItemTitleInput')).toBeVisible();

    const updatedTitle = `${uniqueTitle} - Updated`;
    await page.locator('#workItemTitleInput').fill(updatedTitle);
    await page.locator('app-add-work-item-modal .modal-footer .btn-save').click();
    await expect(page.locator('#workItemTitleInput')).not.toBeVisible();

    // Verify updated title
    const updatedCard = page.locator('.work-item-card', { hasText: updatedTitle });
    await expect(updatedCard).toBeVisible({ timeout: 10000 });

    // 7. Delete Story with Cancel Safeguard
    await updatedCard.locator('button.btn-edit').click();
    await expect(page.locator('#workItemTitleInput')).toBeVisible();
    await page.locator('app-add-work-item-modal .modal-footer .btn-danger', { hasText: 'Delete Story' }).click();

    // Confirm modal opens
    const confirmDeleteModal = page.locator('app-add-work-item-modal app-confirm-modal .modal-box');
    await expect(confirmDeleteModal).toBeVisible();

    // Cancel safeguard: story edit modal remains open and card is not deleted
    await confirmDeleteModal.locator('.btn-secondary').click();
    await expect(confirmDeleteModal).not.toBeVisible({ timeout: 5000 });
    await expect(page.locator('#workItemTitleInput')).toBeVisible();

    // Re-trigger delete and confirm
    await page.locator('app-add-work-item-modal .modal-footer .btn-danger', { hasText: 'Delete Story' }).click();
    await expect(confirmDeleteModal).toBeVisible();
    await confirmDeleteModal.locator('button.btn-confirm-action', { hasText: 'Delete Story' }).click();

    await expect(page.locator('#workItemTitleInput')).not.toBeVisible();
    await expect(page.locator('app-confirmation-popup .confirmation-popup-card', { hasText: 'Work Item Deleted' })).toBeVisible({ timeout: 5000 });

    // Verify deleted
    await expect(page.locator('.work-item-card', { hasText: updatedTitle })).not.toBeVisible({ timeout: 10000 });
  });

  test('should create sprint, add user story into sprint, edit and delete story, edit sprint goal, and delete sprint', async ({ page }) => {
    const timestamp = Date.now();
    const sprintName = `Sprint E2E ${timestamp}`;
    const sprintGoal = `Comprehensive E2E sprint goal deliverable ${timestamp}`;
    const updatedSprintGoal = `Updated sprint goal with extended targets ${timestamp}`;
    const storyTitle = `PBI in Sprint ${timestamp}`;
    const updatedStoryTitle = `${storyTitle} - Refined`;

    // 1. Open Create Sprint modal
    const sprintModalBtn = page.locator('.section-header button', { hasText: 'Create Sprint' });
    await expect(sprintModalBtn).toBeVisible();
    await sprintModalBtn.click();

    // 2. Verify input fields have id and fill new Sprint details
    await expect(page.locator('#sprintNameInput')).toBeVisible();
    await expect(page.locator('#sprintGoalTextarea')).toBeVisible();
    await expect(page.locator('#sprintStartDateInput')).toBeVisible();
    await expect(page.locator('#sprintEndDateInput')).toBeVisible();

    await page.locator('#sprintNameInput').fill(sprintName);
    await page.locator('#sprintGoalTextarea').fill(sprintGoal);

    // Save Sprint
    await page.locator('app-edit-sprint-modal button.btn-primary', { hasText: 'Create Sprint' }).click();
    await expect(page.locator('#sprintNameInput')).not.toBeVisible();

    // 3. Verify sprint chip in quick filter bar
    const sprintChip = page.locator('.sprint-filter-bar .sprint-chip', { hasText: sprintName });
    await expect(sprintChip).toBeVisible({ timeout: 10000 });
    await sprintChip.click();

    // 4. Verify Sprint Goal Banner reflects active selection
    const goalBanner = page.locator('.sprint-goal-banner');
    await expect(goalBanner).toBeVisible({ timeout: 10000 });
    await expect(goalBanner.locator('.sprint-name-chip')).toContainText(sprintName);
    await expect(goalBanner.locator('.goal-text')).toContainText(sprintGoal);

    // 5. Add User Story into this Sprint
    const addStoryBtn = page.locator('.section-header button', { hasText: 'Add Story / Bug / PBI' });
    await expect(addStoryBtn).toBeVisible();
    await addStoryBtn.click();

    await expect(page.locator('#workItemTitleInput')).toBeVisible();
    await page.locator('.type-card', { hasText: 'User Story' }).click();
    await page.locator('#workItemTitleInput').fill(storyTitle);
    await page.locator('#workItemDescTextarea').fill('PBI created specifically inside newly provisioned sprint.');
    await page.locator('.points-selector .point-btn', { hasText: '8' }).click();
    await page.locator('#workItemHoursInput').fill('16');
    await page.locator('app-add-work-item-modal .modal-footer .btn-save').click();
    await expect(page.locator('#workItemTitleInput')).not.toBeVisible();

    // Verify story appears on board under this sprint
    const storyCard = page.locator('.work-item-card', { hasText: storyTitle });
    await expect(storyCard).toBeVisible({ timeout: 10000 });
    await expect(storyCard.locator('.pts-badge')).toContainText('8 Pts');

    // 6. Edit User Story
    await storyCard.locator('button.btn-edit').click();
    await expect(page.locator('#workItemTitleInput')).toBeVisible();
    await page.locator('#workItemTitleInput').fill(updatedStoryTitle);
    await page.locator('app-add-work-item-modal .modal-footer .btn-save').click();
    await expect(page.locator('#workItemTitleInput')).not.toBeVisible();

    // Verify updated title
    const updatedStoryCard = page.locator('.work-item-card', { hasText: updatedStoryTitle });
    await expect(updatedStoryCard).toBeVisible({ timeout: 10000 });

    // 7. Delete User Story
    await updatedStoryCard.locator('button.btn-edit').click();
    await expect(page.locator('#workItemTitleInput')).toBeVisible();
    await page.locator('app-add-work-item-modal .modal-footer .btn-danger', { hasText: 'Delete Story' }).click();

    // Confirm deletion in app-confirm-modal
    const sprintConfirmDeleteModal = page.locator('app-add-work-item-modal app-confirm-modal .modal-box');
    await expect(sprintConfirmDeleteModal).toBeVisible();
    await sprintConfirmDeleteModal.locator('button.btn-confirm-action', { hasText: 'Delete Story' }).click();

    await expect(page.locator('#workItemTitleInput')).not.toBeVisible();
    await expect(page.locator('app-confirmation-popup .confirmation-popup-card', { hasText: 'Work Item Deleted' })).toBeVisible({ timeout: 5000 });
    await expect(page.locator('.work-item-card', { hasText: updatedStoryTitle })).not.toBeVisible({ timeout: 10000 });

    // 8. Edit Sprint Goal
    await goalBanner.locator('.btn-edit-goal').click();
    await expect(page.locator('#sprintGoalTextarea')).toBeVisible();
    await page.locator('#sprintGoalTextarea').fill(updatedSprintGoal);
    await page.locator('app-edit-sprint-modal button.btn-primary', { hasText: 'Save Sprint & Goal' }).click();
    await expect(page.locator('#sprintGoalTextarea')).not.toBeVisible();

    // Verify updated goal banner
    await expect(goalBanner.locator('.goal-text')).toContainText(updatedSprintGoal);

    // 9. Delete Sprint with Cancel Safeguard
    await goalBanner.locator('.btn-edit-goal').click();
    await expect(page.locator('#sprintNameInput')).toBeVisible();
    await page.locator('app-edit-sprint-modal button.btn-danger', { hasText: 'Delete Sprint' }).click();

    // Confirm in deletion modal
    const confirmModal = page.locator('app-confirm-modal .modal-box');
    await expect(confirmModal).toBeVisible();

    // Cancel safeguard: sprint remains active
    await confirmModal.locator('.btn-secondary').click();
    await expect(confirmModal).not.toBeVisible({ timeout: 5000 });
    await expect(sprintChip).toBeVisible();

    // Re-trigger delete and confirm
    await page.locator('app-edit-sprint-modal button.btn-danger', { hasText: 'Delete Sprint' }).click();
    await expect(confirmModal).toBeVisible();
    await confirmModal.locator('button.btn-confirm-action', { hasText: 'Delete Sprint' }).click();
    await expect(confirmModal).not.toBeVisible();

    // 10. Verify sprint chip is gone
    await expect(sprintChip).not.toBeVisible({ timeout: 10000 });
  });

  test('should enforce sprint date range validation where end date must be on or after start date', async ({ page }) => {
    // 1. Open Create Sprint modal
    const sprintModalBtn = page.locator('.section-header button', { hasText: 'Create Sprint' });
    await expect(sprintModalBtn).toBeVisible();
    await sprintModalBtn.click();

    await expect(page.locator('#sprintNameInput')).toBeVisible();

    // 2. Set invalid dates: End Date before Start Date
    await page.locator('#sprintNameInput').fill('Invalid Date Sprint Boundary Test');
    await page.locator('#sprintGoalTextarea').fill('Verify sprint date range validation banner');
    await page.locator('#sprintStartDateInput').fill('2026-12-15');
    await page.locator('#sprintEndDateInput').fill('2026-12-05');

    // 3. Assert date validation banner appears
    const dateBanner = page.locator('app-edit-sprint-modal .date-validation-banner');
    await expect(dateBanner).toBeVisible({ timeout: 5000 });
    await expect(dateBanner).toContainText('Sprint End Date must be on or after Sprint Start Date');

    // 4. Assert Create Sprint button is disabled
    const createBtn = page.locator('app-edit-sprint-modal button.btn-primary');
    await expect(createBtn).toBeDisabled();

    // 5. Correct the End Date
    await page.locator('#sprintEndDateInput').fill('2026-12-25');
    await expect(dateBanner).not.toBeVisible({ timeout: 5000 });
    await expect(createBtn).toBeEnabled();

    // 6. Close modal without saving
    await page.locator('app-edit-sprint-modal .close-btn').click();
    await expect(page.locator('#sprintNameInput')).not.toBeVisible({ timeout: 5000 });
  });

  test('should create Bug work item assigned to a specific team member, verify type pill and assignee, and delete', async ({ page }) => {
    const timestamp = Date.now();
    const bugTitle = `Critical Token Expiry Defect ${timestamp}`;

    // 1. Open Add Work Item modal
    const addBtn = page.locator('.section-header button', { hasText: 'Add Story / Bug / PBI' });
    await expect(addBtn).toBeVisible();
    await addBtn.click();
    await expect(page.locator('#workItemTitleInput')).toBeVisible();

    // 2. Select Bug Fix category card explicitly
    const bugCard = page.locator('app-add-work-item-modal .type-card', { hasText: 'Bug Fix' });
    await expect(bugCard).toBeVisible();
    await bugCard.click();
    await expect(bugCard).toHaveClass(/selected/);

    // 3. Fill Title & Description
    await page.locator('#workItemTitleInput').fill(bugTitle);
    await page.locator('#workItemDescTextarea').fill('Session invalidates unexpectedly during active API request.');

    // 4. Select Assignee (select first available squad member)
    const assigneeSelect = page.locator('#workItemAssigneeSelect');
    const options = await assigneeSelect.locator('option').all();
    let assignedMemberName = '';
    if (options.length > 1) {
      const val = await options[1].getAttribute('value');
      const text = await options[1].textContent();
      assignedMemberName = text ? text.split('(')[0].trim() : '';
      if (val) await assigneeSelect.selectOption(val);
    }

    // Save
    await page.locator('app-add-work-item-modal .modal-footer .btn-save').click();
    await expect(page.locator('#workItemTitleInput')).not.toBeVisible({ timeout: 5000 });

    // 5. Verify created work item card
    const card = page.locator('.work-item-card', { hasText: bugTitle });
    await expect(card).toBeVisible({ timeout: 10000 });

    // Verify type badge renders Bug and NOT User Story
    const typeBadge = card.locator('.header-left .glass-badge').first();
    await expect(typeBadge).toContainText(/Bug/i);
    await expect(typeBadge).not.toContainText('UserStory');

    // Verify assigned member name
    if (assignedMemberName) {
      await expect(card.locator('.assignee-text')).toContainText(assignedMemberName);
    }

    // 6. Delete Bug
    await card.locator('button.btn-edit').click();
    await expect(page.locator('#workItemTitleInput')).toBeVisible();
    await page.locator('app-add-work-item-modal .modal-footer .btn-danger', { hasText: 'Delete Story' }).click();

    const confirmModal = page.locator('app-add-work-item-modal app-confirm-modal .modal-box');
    await expect(confirmModal).toBeVisible();
    await confirmModal.locator('button.btn-confirm-action', { hasText: 'Delete Story' }).click();

    await expect(page.locator('#workItemTitleInput')).not.toBeVisible({ timeout: 5000 });
    await expect(page.locator('.work-item-card', { hasText: bugTitle })).not.toBeVisible({ timeout: 10000 });
  });

  test('should dynamically adapt Sprint Commitment Formula in estimation guide when SM configures daily working hours (8.5h vs 8.0h vs 9.0h)', async ({ page }) => {
    // 1. Open Create Sprint modal
    const sprintModalBtn = page.locator('.section-header button', { hasText: 'Create Sprint' });
    await expect(sprintModalBtn).toBeVisible();
    await sprintModalBtn.click();

    await expect(page.locator('#sprintNameInput')).toBeVisible();

    // 2. Default is 8.5h standard
    const guideBtn = page.locator('app-edit-sprint-modal .btn-guide-link');
    await expect(guideBtn).toBeVisible();
    await guideBtn.click();

    // Verify Fibonacci Matrix Guide modal is open
    const matrixModal = page.locator('app-estimation-matrix-modal .modal-box');
    await expect(matrixModal).toBeVisible();

    // Verify formula card has 8.5 hrs/pt
    const formulaBox = matrixModal.locator('.formula-box code');
    await expect(formulaBox).toContainText('8.5 hrs/pt');

    // Close guide
    await matrixModal.locator('.close-btn').click();
    await expect(matrixModal).not.toBeVisible({ timeout: 5000 });

    // 3. Switch to 8.0h preset pill
    await page.locator('app-edit-sprint-modal .preset-pill', { hasText: '8.0h' }).click();

    // Open guide again
    await guideBtn.click();
    await expect(matrixModal).toBeVisible();
    await expect(formulaBox).toContainText('8.0 hrs/pt');

    // Close guide
    await matrixModal.locator('.close-btn').click();
    await expect(matrixModal).not.toBeVisible({ timeout: 5000 });

    // 4. Switch to 9.0h preset pill
    await page.locator('app-edit-sprint-modal .preset-pill', { hasText: '9.0h' }).click();

    // Open guide again
    await guideBtn.click();
    await expect(matrixModal).toBeVisible();
    await expect(formulaBox).toContainText('9.0 hrs/pt');

    // Close guide and sprint modal
    await matrixModal.locator('.close-btn').click();
    await expect(matrixModal).not.toBeVisible({ timeout: 5000 });
    await page.locator('app-edit-sprint-modal .close-btn').click();
    await expect(page.locator('#sprintNameInput')).not.toBeVisible({ timeout: 5000 });
  });
});

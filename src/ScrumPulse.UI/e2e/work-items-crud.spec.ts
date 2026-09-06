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

    // 7. Delete Story
    await updatedCard.locator('button.btn-edit').click();
    await expect(page.locator('#workItemTitleInput')).toBeVisible();
    await page.locator('app-add-work-item-modal .modal-footer .btn-danger', { hasText: 'Delete Story' }).click();

    // Confirm deletion in app-confirm-modal
    const confirmDeleteModal = page.locator('app-add-work-item-modal app-confirm-modal .modal-box');
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

    // 9. Delete Sprint
    await goalBanner.locator('.btn-edit-goal').click();
    await expect(page.locator('#sprintNameInput')).toBeVisible();
    await page.locator('app-edit-sprint-modal button.btn-danger', { hasText: 'Delete Sprint' }).click();

    // Confirm in deletion modal
    const confirmModal = page.locator('app-confirm-modal .modal-box');
    await expect(confirmModal).toBeVisible();
    await confirmModal.locator('button.btn-confirm-action', { hasText: 'Delete Sprint' }).click();
    await expect(confirmModal).not.toBeVisible();

    // 10. Verify sprint chip is gone
    await expect(sprintChip).not.toBeVisible({ timeout: 10000 });
  });
});

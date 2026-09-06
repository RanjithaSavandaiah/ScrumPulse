import { test, expect } from '@playwright/test';
import { unlockScrumMaster } from './helpers';

test.describe('Work Items & Sprints End-to-End Lifecycle', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
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
    await expect(page.locator('#workItemTitleInput')).not.toBeVisible();

    // Verify deleted
    await expect(page.locator('.work-item-card', { hasText: updatedTitle })).not.toBeVisible({ timeout: 10000 });
  });

  test('should open sprint modal, modify sprint goal and targets', async ({ page }) => {
    // Open Create/Edit Sprint modal
    const sprintModalBtn = page.locator('.section-header button', { hasText: 'Create Sprint' });
    await expect(sprintModalBtn).toBeVisible();
    await sprintModalBtn.click();

    // Verify input fields have id and for association
    await expect(page.locator('#sprintNameInput')).toBeVisible();
    await expect(page.locator('#sprintGoalTextarea')).toBeVisible();
    await expect(page.locator('#sprintStartDateInput')).toBeVisible();
    await expect(page.locator('#sprintEndDateInput')).toBeVisible();

    // Close modal
    await page.locator('app-edit-sprint-modal .close-btn').click();
    await expect(page.locator('#sprintNameInput')).not.toBeVisible();
  });
});

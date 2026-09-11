import { test, expect } from '@playwright/test';
import { unlockScrumMaster } from './helpers';

test.describe('Scrum Master Team DoR & DoD Quality Gates Configuration', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/', { waitUntil: 'domcontentloaded' });
    await unlockScrumMaster(page);
    await page.locator('.tab-btn', { hasText: 'Work Items' }).click();
    await expect(page.locator('.work-items-section')).toBeVisible({ timeout: 15000 });
  });

  test('should allow Scrum Master to configure team DoR/DoD gates, verify they appear dynamically on work items, and enforce role permissions', async ({ page }) => {
    const timestamp = Date.now();
    const customDorTitle = `SecOps Threat Model Review ${timestamp}`;
    const customDodTitle = `Zero Critical Vulnerabilities ${timestamp}`;
    const storyTitle = `E2E Quality Gates Verified Story ${timestamp}`;

    // 1. Verify "Configure DoR / DoD" button is present in the Work Items toolbar for Scrum Master
    const toolbarConfigureBtn = page.locator('.btn-configure-gates', { hasText: 'Configure DoR / DoD' });
    await expect(toolbarConfigureBtn).toBeVisible({ timeout: 10000 });
    await toolbarConfigureBtn.click();

    // 2. Verify Configure Quality Gates modal appears
    const configModal = page.locator('app-configure-gates-modal .modal-content');
    await expect(configModal).toBeVisible({ timeout: 5000 });
    await expect(configModal.locator('.modal-title')).toContainText('Configure Quality Gates');

    // 3. Add custom Definition of Ready (DoR) criterion
    await expect(configModal.locator('.gate-tab-btn', { hasText: 'Definition of Ready (DoR)' })).toHaveClass(/active/);
    await configModal.locator('#newCriterionLabel').fill(customDorTitle);
    await configModal.locator('#newCriterionDesc').fill('Threat model reviewed and approved by AppSec lead');
    await configModal.locator('button', { hasText: 'Add to DOR' }).click();

    // Verify criterion appeared in DoR list
    const addedDorRow = configModal.locator('.criterion-row', { hasText: customDorTitle });
    await expect(addedDorRow).toBeVisible();

    // 4. Switch to Definition of Done (DoD) tab and add custom DoD criterion
    await configModal.locator('.gate-tab-btn', { hasText: 'Definition of Done (DoD)' }).click();
    await expect(configModal.locator('.gate-tab-btn', { hasText: 'Definition of Done (DoD)' })).toHaveClass(/active/);
    await configModal.locator('#newCriterionLabel').fill(customDodTitle);
    await configModal.locator('#newCriterionDesc').fill('Automated SAST/DAST scan shows zero high/critical CVEs');
    await configModal.locator('button', { hasText: 'Add to DOD' }).click();

    // Verify criterion appeared in DoD list
    const addedDodRow = configModal.locator('.criterion-row', { hasText: customDodTitle });
    await expect(addedDodRow).toBeVisible();

    // 5. Save Squad Gates
    await configModal.locator('.modal-footer button.btn-save').click();
    await expect(configModal).not.toBeVisible({ timeout: 10000 });

    // 6. Create a User Story to test quality gates
    const addBtn = page.locator('.section-header button', { hasText: 'Add Story / Bug / PBI' });
    await expect(addBtn).toBeVisible();
    await addBtn.click();
    await expect(page.locator('#workItemTitleInput')).toBeVisible();

    await page.locator('.type-card', { hasText: 'User Story' }).click();
    await page.locator('#workItemTitleInput').fill(storyTitle);
    await page.locator('#workItemDescTextarea').fill('Story created to verify dynamic DoR & DoD quality gates persistence.');
    await page.locator('#workItemDoRTextarea').fill('Given custom gates configured, When opened, Then custom gates show up.');
    await page.getByRole('button', { name: '3', exact: true }).click();
    await page.locator('app-add-work-item-modal .modal-footer .btn-save').click();
    await expect(page.locator('#workItemTitleInput')).not.toBeVisible({ timeout: 5000 });

    const card = page.locator('.work-item-card', { hasText: storyTitle });
    await expect(card).toBeVisible({ timeout: 10000 });

    // 7. Open Quality Gates Modal for the card
    await card.locator('button', { hasText: 'DoR / DoD Gates' }).click();
    const gatesModal = page.locator('app-quality-gates-modal .modal-content');
    await expect(gatesModal).toBeVisible({ timeout: 5000 });

    // 8. Verify the newly configured custom criteria "show up here"
    const dorLabel = gatesModal.locator('.gate-label-wrap', { hasText: customDorTitle });
    await expect(dorLabel).toBeVisible();
    const dodLabel = gatesModal.locator('.gate-label-wrap', { hasText: customDodTitle });
    await expect(dodLabel).toBeVisible();

    // Verify quick "Configure Team Gates" button is available in the header for Scrum Master
    const inModalConfigureBtn = gatesModal.locator('.btn-configure-standards', { hasText: 'Configure Team Gates' });
    await expect(inModalConfigureBtn).toBeVisible();

    // 9. Check off the custom quality gates
    const customDorCheckbox = gatesModal.locator('.gate-checkbox', { hasText: customDorTitle }).locator('input[type="checkbox"]');
    await customDorCheckbox.check();
    expect(await customDorCheckbox.isChecked()).toBe(true);

    const customDodCheckbox = gatesModal.locator('.gate-checkbox', { hasText: customDodTitle }).locator('input[type="checkbox"]');
    await customDodCheckbox.check();
    expect(await customDodCheckbox.isChecked()).toBe(true);

    // Save Quality Gates
    await gatesModal.locator('button', { hasText: 'Save Gates' }).click();
    await expect(gatesModal).not.toBeVisible({ timeout: 5000 });

    // 10. Re-open modal and verify checks are persisted
    await card.locator('button', { hasText: 'DoR / DoD Gates' }).click();
    await expect(gatesModal).toBeVisible({ timeout: 5000 });
    await expect(customDorCheckbox).toBeChecked();
    await expect(customDodCheckbox).toBeChecked();

    // Close gates modal
    await gatesModal.locator('button', { hasText: 'Cancel' }).click();
    await expect(gatesModal).not.toBeVisible({ timeout: 5000 });

    // 11. Verify Role Permissions: Switch to Developer role
    const roleSelect = page.locator('[data-testid="role-select"]');
    await roleSelect.selectOption('Developer');
    await page.waitForTimeout(300);

    // Verify toolbar "Configure DoR / DoD" is hidden for Developer
    await expect(page.locator('.btn-configure-gates', { hasText: 'Configure DoR / DoD' })).not.toBeVisible();

    // Open Quality Gates modal as Developer: "Configure Team Gates" button is hidden, but Developer can view/save gates
    await card.locator('button', { hasText: 'DoR / DoD Gates' }).click();
    await expect(gatesModal).toBeVisible({ timeout: 5000 });
    await expect(gatesModal.locator('.btn-configure-standards')).not.toBeVisible();

    // Developer can still save gates
    await gatesModal.locator('button', { hasText: 'Save Gates' }).click();
    await expect(gatesModal).not.toBeVisible({ timeout: 5000 });

    // 12. Cleanup: switch back to Scrum Master and delete test story
    await roleSelect.selectOption('ScrumMaster');
    await page.waitForTimeout(200);

    await card.locator('button.btn-edit').click();
    await expect(page.locator('#workItemTitleInput')).toBeVisible();
    await page.locator('app-add-work-item-modal .modal-footer .btn-danger', { hasText: 'Delete Story' }).click();

    const confirmDeleteModal = page.locator('app-add-work-item-modal app-confirm-modal .modal-box');
    await expect(confirmDeleteModal).toBeVisible();
    await confirmDeleteModal.locator('button.btn-confirm-action', { hasText: 'Delete Story' }).click();

    await expect(page.locator('#workItemTitleInput')).not.toBeVisible({ timeout: 5000 });
    await expect(card).not.toBeVisible({ timeout: 10000 });
  });
});

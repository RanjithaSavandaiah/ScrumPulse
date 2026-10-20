import { test, expect } from '@playwright/test';
import { unlockScrumMaster } from './helpers';

test.describe('Multi-Squad Management & Tenant Context Switching', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/app', { waitUntil: 'domcontentloaded' });
  });

  test('should create new squad as SM, validate empty fields, and switch squad context', async ({ page }) => {
    await unlockScrumMaster(page);

    // 1. Click create squad button in navbar
    const createSquadBtn = page.locator('.btn-squad-action');
    await expect(createSquadBtn).toBeVisible();
    await createSquadBtn.click();

    const modalCard = page.locator('.modal-overlay .modal-card');
    await expect(modalCard).toBeVisible();

    // 2. Validate empty squad name validation error
    const submitCreateBtn = modalCard.locator('.modal-actions button', { hasText: 'Create Squad' });
    await submitCreateBtn.click();

    const errorBanner = modalCard.locator('.error-banner');
    await expect(errorBanner).toBeVisible();
    await expect(errorBanner).toContainText(/Squad name is (mandatory|required)/i);

    // 3. Fill valid squad details
    const timestamp = Date.now();
    const newSquadName = `FinTech Core Squad ${timestamp}`;
    await page.locator('#newSquadNameInput').fill(newSquadName);
    await page.locator('#newSquadDescInput').fill('High-frequency trading and ledger processing squad.');

    // Submit valid squad creation
    await submitCreateBtn.click();
    await expect(modalCard).not.toBeVisible({ timeout: 10000 });
    await expect(page.locator('app-confirmation-popup .confirmation-popup-card')).toBeVisible({ timeout: 5000 });

    // 4. Verify new squad is selectable in navbar #squadSelect
    const squadSelect = page.locator('#squadSelect');
    await expect(squadSelect).toBeVisible();
    await expect(squadSelect.locator('option', { hasText: newSquadName })).toBeAttached({ timeout: 10000 });

    // Switch to new squad
    await squadSelect.selectOption({ label: newSquadName });

    // Verify current team reflects selection
    const selectedText = await squadSelect.locator('option:checked').textContent();
    expect(selectedText).toContain(newSquadName);

    // 5. Reload page and verify selected squad is retained
    await page.reload({ waitUntil: 'domcontentloaded' });
    await expect(squadSelect.locator('option', { hasText: newSquadName })).toBeAttached({ timeout: 10000 });
    const selectedAfterReload = await squadSelect.locator('option:checked').textContent();
    expect(selectedAfterReload).toContain(newSquadName);
  });

  test('should open join modal as developer, validate empty join code, and handle invalid code', async ({ page }) => {
    // Select developer role
    const roleSelect = page.locator('[data-testid="role-select"]');
    await roleSelect.selectOption('Developer');

    // Select __join__ option in squadSelect
    const squadSelect = page.locator('#squadSelect');
    await squadSelect.selectOption('__join__');

    const modalCard = page.locator('.modal-overlay .modal-card');
    await expect(modalCard).toBeVisible();

    // Verify Join title
    await expect(modalCard.locator('h3')).toContainText('Join Squad with Invite Code');

    // 1. Validate empty join code
    const submitJoinBtn = modalCard.locator('.modal-actions button', { hasText: 'Join Squad' });
    await submitJoinBtn.click();

    const errorBanner = modalCard.locator('.error-banner');
    await expect(errorBanner).toBeVisible();
    await expect(errorBanner).toContainText(/Join code is (mandatory|required)/i);

    // 2. Try invalid code
    await page.locator('#joinCodeInputField').fill('NONEXISTENT_9999');
    await submitJoinBtn.click();
    await expect(errorBanner).toBeVisible({ timeout: 5000 });

    // 3. Cancel modal
    await modalCard.locator('.btn-close').click();
    await expect(modalCard).not.toBeVisible();
  });

  test('should retain squad dropdown selection across page refresh and support All Squads', async ({ page }) => {
    const squadSelect = page.locator('#squadSelect');
    await expect(squadSelect).toBeVisible();

    // 1. Wait for options to load from API
    await expect(squadSelect.locator('option')).not.toHaveCount(1, { timeout: 10000 });

    // 2. Select the second option (a concrete squad)
    const secondOption = squadSelect.locator('option').nth(1);
    const targetSquadName = await secondOption.textContent();
    const targetSquadId = await secondOption.getAttribute('value');
    expect(targetSquadId).toBeTruthy();

    await squadSelect.selectOption({ value: targetSquadId! });
    expect(await squadSelect.locator('option:checked').textContent()).toBe(targetSquadName);

    // 3. Refresh page and verify selected squad is preserved
    await page.reload({ waitUntil: 'domcontentloaded' });
    await expect(squadSelect).toBeVisible();
    await expect(squadSelect.locator('option:checked')).toHaveText(targetSquadName!, { timeout: 10000 });

    // 4. Switch to All Squads (Default)
    await squadSelect.selectOption({ value: '' });
    expect(await squadSelect.locator('option:checked').textContent()).toContain('All Squads (Default)');

    // 5. Refresh page and verify All Squads selection is retained
    await page.reload({ waitUntil: 'domcontentloaded' });
    await expect(squadSelect).toBeVisible();
    await expect(squadSelect.locator('option:checked')).toHaveText(/All Squads \(Default\)/, { timeout: 10000 });
  });
});

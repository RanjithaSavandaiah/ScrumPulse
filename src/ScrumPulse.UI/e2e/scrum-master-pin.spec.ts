import { test, expect } from '@playwright/test';

test.describe('Scrum Master PIN Security & Role Interception', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
  });

  test('should default role to Developer and intercept switching to Scrum Master', async ({ page }) => {
    const roleSelect = page.locator('[data-testid="role-select"]');
    await expect(roleSelect).toBeVisible();

    // Select should initially be Developer
    await expect(roleSelect).toHaveValue('Developer');

    // Attempt to switch to ScrumMaster
    await roleSelect.selectOption('ScrumMaster');

    // PIN modal should appear
    const modalBox = page.locator('.pin-modal-box');
    await expect(modalBox).toBeVisible();
    await expect(page.locator('.pin-title')).toContainText('Scrum Master Security PIN');

    // Role select is reverted to Developer until PIN is verified
    await expect(roleSelect).toHaveValue('Developer');

    // Master PIN 1234 should NOT be exposed in the UI
    await expect(page.locator('body')).not.toContainText('Master PIN: 1234');
  });

  test('should reject invalid PIN and show error alert', async ({ page }) => {
    await page.locator('[data-testid="role-select"]').selectOption('ScrumMaster');

    // Enter wrong PIN (9999) using keypad
    const key9 = page.locator('.keypad-grid .key-btn', { hasText: '9' });
    await key9.click();
    await key9.click();
    await key9.click();
    await key9.click();

    // Verify error message
    const errorAlert = page.locator('.pin-error-alert');
    await expect(errorAlert).toBeVisible();
    await expect(errorAlert).toContainText('Incorrect Security PIN');
  });

  test('should authenticate with PIN 1234 and unlock SM privileges', async ({ page }) => {
    await page.locator('[data-testid="role-select"]').selectOption('ScrumMaster');

    // Enter 1 2 3 4 via keypad
    await page.locator('.keypad-grid .key-btn', { hasText: '1' }).click();
    await page.locator('.keypad-grid .key-btn', { hasText: '2' }).click();
    await page.locator('.keypad-grid .key-btn', { hasText: '3' }).click();
    await page.locator('.keypad-grid .key-btn', { hasText: '4' }).click();

    // Modal should close and role should become ScrumMaster
    await expect(page.locator('app-sm-pin-modal')).not.toBeVisible();
    await expect(page.locator('[data-testid="role-select"]')).toHaveValue('ScrumMaster');

    // Green unlocked status should appear
    const lockBtn = page.locator('.btn-sm-locked');
    await expect(lockBtn).toBeVisible();
    await expect(lockBtn).toContainText('Unlocked');

    // Re-lock session
    await lockBtn.click();
    await expect(page.locator('[data-testid="role-select"]')).toHaveValue('Developer');
  });
});

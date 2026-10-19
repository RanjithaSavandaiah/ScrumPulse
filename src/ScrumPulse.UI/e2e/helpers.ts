import { Page, expect } from '@playwright/test';

export async function unlockScrumMaster(page: Page): Promise<void> {
  await page.request.post('/api/auth/join', {
    data: { joinCode: 'FIKA123', displayName: 'Scrum Master' }
  });

  const roleSelect = page.locator('[data-testid="role-select"]');
  await expect(roleSelect).toBeVisible({ timeout: 15000 });

  // If already unlocked, .btn-sm-locked will be present
  const lockBtn = page.locator('.btn-sm-locked');
  if (await lockBtn.isVisible()) {
    return;
  }

  const currentRole = await roleSelect.inputValue();
  if (currentRole === 'ScrumMaster') {
    await roleSelect.selectOption('Developer');
    await page.waitForTimeout(100);
  }

  await roleSelect.selectOption('ScrumMaster');
  const pinModal = page.locator('.pin-modal-box');
  await expect(pinModal).toBeVisible({ timeout: 5000 });

  for (const digit of ['1', '2', '3', '4']) {
    const key = page.locator(`.keypad-grid .key-btn:has-text("${digit}")`);
    await expect(key).toBeVisible({ timeout: 5000 });
    await key.click();
  }

  // If a transient rate-limit or error occurs in fast CI runners, wait for window reset and retry once
  const errorAlert = page.locator('.pin-error-alert');
  if (await errorAlert.isVisible({ timeout: 1500 }).catch(() => false)) {
    await page.waitForTimeout(2000);
    const clearBtn = page.locator('.keypad-grid .key-btn:has-text("CLR")');
    if (await clearBtn.isVisible()) {
      await clearBtn.click();
    }
    for (const digit of ['1', '2', '3', '4']) {
      await page.locator(`.keypad-grid .key-btn:has-text("${digit}")`).click();
    }
  }

  await expect(page.locator('app-sm-pin-modal')).not.toBeVisible({ timeout: 10000 });
  await expect(page.locator('.btn-sm-locked')).toBeVisible({ timeout: 10000 });
}

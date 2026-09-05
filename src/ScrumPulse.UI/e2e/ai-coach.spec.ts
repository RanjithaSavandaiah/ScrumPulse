import { test, expect } from '@playwright/test';

test.describe('Microsoft AI Intelligence & Coaching Studio', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await page.locator('.tab-btn', { hasText: 'Microsoft AI Coach' }).click();
    await expect(page.locator('.panel-title')).toBeVisible({ timeout: 10000 });
  });

  test('should allow selecting developers and display 360 individual feedback', async ({ page }) => {
    // Individual Coach mode
    const picker = page.locator('.target-picker-bar');
    await expect(picker).toBeVisible({ timeout: 10000 });

    // Select developer
    const devSelect = page.locator('[data-testid="dev-select"]');
    await expect(devSelect).toBeVisible();
    await expect(devSelect.locator('option').nth(1)).toBeAttached({ timeout: 15000 });
    await devSelect.selectOption({ index: 1 });

    // Verify AI insights card with findings and recommendations
    await expect(page.locator('.insight-card')).toBeVisible({ timeout: 15000 });
  });

  test('should support Copilot Agile Chat interaction', async ({ page }) => {
    const chatPanel = page.locator('app-copilot-chat');
    await expect(chatPanel).toBeVisible({ timeout: 10000 });

    // Send a prompt
    const chatInput = chatPanel.locator('.chat-input');
    await chatInput.fill('What is our sprint velocity risk?');
    await chatPanel.locator('.btn-send').click();

    // Verify message added to chat stream
    await expect(chatPanel.locator('.user-msg')).toContainText('What is our sprint velocity risk?');
  });
});

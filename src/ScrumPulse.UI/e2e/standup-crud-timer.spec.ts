import { test, expect } from '@playwright/test';
import { unlockScrumMaster } from './helpers';

test.describe('Daily Standup Feed, CRUD & Co-Located Timer', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/', { waitUntil: 'domcontentloaded' });
    await unlockScrumMaster(page);
    await page.locator('.tab-btn', { hasText: 'Daily Standup' }).click();
    await expect(page.locator('.standup-section')).toBeVisible({ timeout: 15000 });
  });

  test('should log, edit, and delete a daily standup update', async ({ page }) => {
    const timestamp = Date.now();
    const yesterdaySummary = `Shipped core validation tests ${timestamp}`;
    const todayPlan = `Executing E2E test matrix ${timestamp}`;

    // 1. Open Log Standup Modal
    const logBtn = page.locator('app-standup-feed .btn-primary', { hasText: 'Log My Standup' });
    await expect(logBtn).toBeVisible();
    await logBtn.click();

    await expect(page.locator('#standupYesterdaySummary')).toBeVisible();

    // 2. Select Member & Energy Level
    const memberSelect = page.locator('#standupMemberSelect');
    await expect(memberSelect).toBeVisible();
    const options = await memberSelect.locator('option').all();
    if (options.length > 1) {
      const firstVal = await options[1].getAttribute('value');
      if (firstVal) {
        await memberSelect.selectOption(firstVal);
      }
    }

    // Select energy card
    const energyCard = page.locator('.energy-card').first();
    if (await energyCard.isVisible()) {
      await energyCard.click();
    }

    // Fill Yesterday & Today
    await page.locator('#standupYesterdaySummary').fill(yesterdaySummary);
    await page.locator('#standupTodayPlan').fill(todayPlan);
    await page.locator('#standupBlockersText').fill('None');

    // Submit
    await page.locator('app-log-standup-modal .btn-save').click();
    await expect(page.locator('#standupYesterdaySummary')).not.toBeVisible({ timeout: 5000 });

    // 3. Verify logged standup card appears in feed
    const card = page.locator('.standup-item', { hasText: yesterdaySummary });
    await expect(card).toBeVisible({ timeout: 10000 });
    await expect(card).toContainText(todayPlan);

    // 4. Edit Standup
    const editBtn = card.locator('.edit-btn');
    await expect(editBtn).toBeVisible();
    await editBtn.click();

    await expect(page.locator('#standupTodayPlan')).toBeVisible();
    const updatedPlan = `${todayPlan} - VERIFIED`;
    await page.locator('#standupTodayPlan').fill(updatedPlan);
    await page.locator('app-log-standup-modal .btn-save').click();
    await expect(page.locator('#standupTodayPlan')).not.toBeVisible({ timeout: 5000 });

    // Verify updated plan in feed
    await expect(card).toContainText(updatedPlan);

    // 5. Delete Standup via Confirm Modal with Cancel Safeguard
    const deleteBtn = card.locator('.delete-btn');
    await expect(deleteBtn).toBeVisible();
    await deleteBtn.click();

    const confirmModal = page.locator('app-confirm-modal .modal-box');
    await expect(confirmModal).toBeVisible();
    await expect(confirmModal).toContainText('Delete Standup Log');

    // Cancel safeguard: update stays in feed
    await confirmModal.locator('.btn-secondary').click();
    await expect(confirmModal).not.toBeVisible({ timeout: 5000 });
    await expect(card).toBeVisible();

    // Confirm deletion
    await deleteBtn.click();
    await expect(confirmModal).toBeVisible();
    const confirmBtn = page.locator('.btn-confirm-action', { hasText: 'Delete Update' });
    await expect(confirmBtn).toBeVisible();
    await confirmBtn.click();
    await expect(confirmBtn).not.toBeVisible({ timeout: 5000 });

    // Verify card is removed
    await expect(page.locator('.standup-item', { hasText: yesterdaySummary })).not.toBeVisible({ timeout: 10000 });
  });

  test('should control co-located standup timer (start, pause, reset, next, select speaker)', async ({ page }) => {
    const timerCard = page.locator('.timer-card');
    await expect(timerCard).toBeVisible();

    const timerBtn = timerCard.locator('.btn-timer-main');
    await expect(timerBtn).toBeVisible();

    // 1. Start timer
    await timerBtn.click();
    await expect(timerBtn).toContainText('Pause Clock');
    await expect(timerCard.locator('.timer-circle')).toHaveClass(/running/);

    // 2. Pause timer
    await timerBtn.click();
    await expect(timerBtn).toContainText('Start Speaking Clock');

    // 3. Reset 2m
    const resetBtn = timerCard.locator('.btn-reset');
    await expect(resetBtn).toBeVisible();
    await resetBtn.click();
    await expect(timerCard.locator('.timer-digits')).toContainText('2:00');

    // 4. Next speaker
    const nextBtn = timerCard.locator('.btn-next');
    await expect(nextBtn).toBeVisible();
    await nextBtn.click();

    // 5. Select speaker from queue
    const queueItems = timerCard.locator('.queue-item');
    const queueCount = await queueItems.count();
    if (queueCount > 1) {
      await queueItems.nth(1).click();
      await expect(queueItems.nth(1)).toHaveClass(/active-speaker/);
    }
  });

  test('should enforce mandatory field validation on standup modal and display meaningful success notification when logged', async ({ page }) => {
    // 1. Open Log Standup Modal
    const logBtn = page.locator('app-standup-feed .btn-primary', { hasText: 'Log My Standup' });
    await expect(logBtn).toBeVisible();
    await logBtn.click();

    await expect(page.locator('#standupYesterdaySummary')).toBeVisible();

    // 2. Select Member
    const memberSelect = page.locator('#standupMemberSelect');
    await expect(memberSelect).toBeVisible();
    const options = await memberSelect.locator('option').all();
    if (options.length > 1) {
      const firstVal = await options[1].getAttribute('value');
      if (firstVal) {
        await memberSelect.selectOption(firstVal);
      }
    }

    // 3. Clear Yesterday Summary and attempt save
    await page.locator('#standupYesterdaySummary').fill('');
    await page.locator('#standupTodayPlan').fill('Some plan');
    await page.locator('app-log-standup-modal .btn-save').click();

    // 4. Assert Yesterday Summary validation banner
    const alertBanner = page.locator('app-log-standup-modal .validation-alert-banner');
    await expect(alertBanner).toBeVisible();
    await expect(alertBanner).toContainText("Yesterday's accomplishments is mandatory");

    // 5. Fill Yesterday Summary, clear Today Plan
    const timestamp = Date.now();
    const yesterday = `Completed refactor ${timestamp}`;
    await page.locator('#standupYesterdaySummary').fill(yesterday);
    await page.locator('#standupTodayPlan').fill('');
    await page.locator('app-log-standup-modal .btn-save').click();
    await expect(alertBanner).toContainText("Today's commitment is mandatory");

    // 6. Fill Today Plan and submit
    const today = `Verify automated tests ${timestamp}`;
    await page.locator('#standupTodayPlan').fill(today);
    await page.locator('app-log-standup-modal .btn-save').click();
    await expect(page.locator('#standupYesterdaySummary')).not.toBeVisible({ timeout: 5000 });

    // 7. Assert success toast
    const successToast = page.locator('.confirmation-popup-card .popup-message');
    await expect(successToast).toBeVisible({ timeout: 5000 });
    await expect(successToast).toContainText('added successfully');
  });
});

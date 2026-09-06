import { test, expect } from '@playwright/test';
import { unlockScrumMaster } from './helpers';

test.describe('Monthly 1:1 Reviews & 360 Feedback System Lifecycle', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await unlockScrumMaster(page);
    await page.locator('.tab-btn', { hasText: 'Monthly 1:1 Reviews' }).click();
    await expect(page.locator('.reviews-section')).toBeVisible({ timeout: 15000 });
  });

  test('should record 360 feedback, edit, verify cancel safeguard, and delete', async ({ page }) => {
    const timestamp = Date.now();
    const smFeedback = `Exemplary sprint leadership and mentorship ${timestamp}`;
    const cdlFeedback = `Delivered distributed architecture milestone on time ${timestamp}`;
    const clientFeedback = `High stakeholder satisfaction with sprint demo ${timestamp}`;
    const selfReflection = `Looking forward to driving test automation excellence ${timestamp}`;

    // 1. Open Record Modal
    const openBtn = page.locator('#btnOpenRecordFeedback');
    await expect(openBtn).toBeVisible();
    await openBtn.click();

    // Verify modal is open
    const modalContent = page.locator('app-record-feedback-modal .modal-content');
    await expect(modalContent).toBeVisible();

    // 2. Select Team Member
    const memberSelect = page.locator('#reviewMemberSelect');
    await expect(memberSelect).toBeVisible();
    const options = await memberSelect.locator('option:not([disabled])').all();
    expect(options.length).toBeGreaterThan(0);
    const memberVal = await options[0].getAttribute('value');
    if (memberVal) {
      await memberSelect.selectOption(memberVal);
    }

    const saveBtn = modalContent.locator('.btn-save');
    await expect(saveBtn).toBeEnabled();

    // Select Month
    await page.locator('#reviewMonthPickerInput').fill('2026-09');

    // Select Happiness Dial 5 (first button in happiness pills)
    const happinessDials = modalContent.locator('.happiness-pills .dial-btn');
    await happinessDials.first().click();

    // Select SM Rating 5 (last star in star rating row)
    const starBtns = modalContent.locator('.star-rating-row .star-btn');
    await starBtns.last().click();

    // Fill all 4 feedback quadrants
    await page.locator('#feedbackSmTextarea').fill(smFeedback);
    await page.locator('#feedbackCdlTextarea').fill(cdlFeedback);
    await page.locator('#feedbackClientTextarea').fill(clientFeedback);
    await page.locator('#feedbackSelfTextarea').fill(selfReflection);

    // Save review
    await saveBtn.click();
    await expect(modalContent).not.toBeVisible({ timeout: 5000 });

    // 3. Verify Newly Created Review Card
    const reviewCard = page.locator('.feedback-card', { hasText: smFeedback });
    await expect(reviewCard).toBeVisible({ timeout: 10000 });
    await expect(reviewCard).toContainText('2026-09');
    await expect(reviewCard.locator('.sm-badge')).toContainText('SM Rating: 5/10');
    await expect(reviewCard.locator('.happiness-badge')).toContainText('Happiness Index: 5/10');
    await expect(reviewCard).toContainText(cdlFeedback);
    await expect(reviewCard).toContainText(clientFeedback);
    await expect(reviewCard).toContainText(selfReflection);

    // 4. Edit Review
    const editBtn = reviewCard.locator('.edit-btn');
    await expect(editBtn).toBeVisible();
    await editBtn.click();

    await expect(modalContent).toBeVisible();
    const updatedSmFeedback = `${smFeedback} - VERIFIED WITH COACHING`;
    await page.locator('#feedbackSmTextarea').fill(updatedSmFeedback);
    await modalContent.locator('.btn-save').click();
    await expect(modalContent).not.toBeVisible({ timeout: 5000 });

    // Verify updated card content
    const updatedCard = page.locator('.feedback-card', { hasText: updatedSmFeedback });
    await expect(updatedCard).toBeVisible({ timeout: 10000 });

    // 5. Safeguard Edge Case: Cancel on Delete Confirm Modal
    const deleteBtn = updatedCard.locator('.delete-btn');
    await expect(deleteBtn).toBeVisible();
    await deleteBtn.click();

    const confirmModal = page.locator('app-confirm-modal .modal-box');
    await expect(confirmModal).toBeVisible();

    // Click "Keep Review" (Cancel action)
    const keepBtn = confirmModal.locator('button', { hasText: 'Keep Review' });
    await expect(keepBtn).toBeVisible();
    await keepBtn.click();
    await expect(confirmModal).not.toBeVisible({ timeout: 5000 });

    // Verify review was NOT deleted
    await expect(updatedCard).toBeVisible({ timeout: 5000 });

    // 6. Confirm Delete Review
    await deleteBtn.click();
    await expect(confirmModal).toBeVisible();
    const confirmDeleteBtn = confirmModal.locator('button', { hasText: 'Delete Review' });
    await expect(confirmDeleteBtn).toBeVisible();
    await confirmDeleteBtn.click();
    await expect(confirmModal).not.toBeVisible({ timeout: 5000 });

    // Verify review card is completely removed from list
    await expect(page.locator('.feedback-card', { hasText: updatedSmFeedback })).not.toBeVisible({ timeout: 10000 });
  });
});

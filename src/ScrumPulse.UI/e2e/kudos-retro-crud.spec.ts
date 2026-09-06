import { test, expect } from '@playwright/test';
import { unlockScrumMaster } from './helpers';

test.describe('Kudos Wall & Retrospective Board Interactive Lifecycle', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
    await unlockScrumMaster(page);
  });

  test('should publish peer kudos card and trigger reaction counters', async ({ page }) => {
    // 1. Navigate to Appreciation Wall
    await page.locator('.tab-btn', { hasText: 'Appreciation' }).click();
    await expect(page.locator('.kudos-section')).toBeVisible({ timeout: 15000 });

    const timestamp = Date.now();
    const kudosMessage = `Exceptional test automation and code coverage work! ${timestamp}`;

    // 2. Open Give Kudos Modal
    const giveKudosBtn = page.locator('.kudos-section .section-header button', { hasText: 'Give Kudos' });
    await expect(giveKudosBtn).toBeVisible();
    await giveKudosBtn.click();

    await expect(page.locator('#kudosMessageTextarea')).toBeVisible();

    // Select Sender
    const senderSelect = page.locator('#kudosSenderSelect');
    const senderOptions = await senderSelect.locator('option').all();
    if (senderOptions.length > 1) {
      const senderVal = await senderOptions[1].getAttribute('value');
      if (senderVal) await senderSelect.selectOption(senderVal);
    }

    // Select Receiver
    const receiverSelect = page.locator('#kudosReceiverSelect');
    const receiverOptions = await receiverSelect.locator('option').all();
    if (receiverOptions.length > 2) {
      const receiverVal = await receiverOptions[2].getAttribute('value');
      if (receiverVal) await receiverSelect.selectOption(receiverVal);
    } else if (receiverOptions.length > 1) {
      const receiverVal = await receiverOptions[1].getAttribute('value');
      if (receiverVal) await receiverSelect.selectOption(receiverVal);
    }

    // Fill message
    await page.locator('#kudosMessageTextarea').fill(kudosMessage);

    // Submit
    const publishBtn = page.locator('app-give-kudos-modal .btn-save');
    await expect(publishBtn).toBeEnabled();
    await publishBtn.click();
    await expect(page.locator('#kudosMessageTextarea')).not.toBeVisible({ timeout: 5000 });

    // 3. Verify card in wall
    const card = page.locator('.kudos-card', { hasText: kudosMessage });
    await expect(card).toBeVisible({ timeout: 10000 });

    // 4. Click emoji reactions (thumbsUp, zap, heart)
    const reactionBtn = card.locator('.reaction-btn').first();
    await expect(reactionBtn).toBeVisible();
    await reactionBtn.click();
    await expect(reactionBtn).toHaveClass(/active-reaction/);
  });

  test('should add retro card, upvote, manage action items, and delete retro card', async ({ page }) => {
    // 1. Navigate to Retrospective Board
    await page.locator('.tab-btn', { hasText: 'Retrospective' }).click();
    await expect(page.locator('.retro-section')).toBeVisible({ timeout: 15000 });

    const timestamp = Date.now();
    const retroContent = `End-to-end regression validation runs in lightning speed ${timestamp}`;
    const actionTitle = `Automate performance monitoring ${timestamp}`;

    // 2. Open Add Retro Card Modal
    const addCardBtn = page.locator('.retro-section .header-right-actions button', { hasText: 'Add Retro Card' });
    await expect(addCardBtn).toBeVisible();
    await addCardBtn.click();

    await expect(page.locator('#retroContentTextarea')).toBeVisible();

    // Select category tile
    const categoryTile = page.locator('app-add-retro-card-modal .category-card').first();
    if (await categoryTile.isVisible()) {
      await categoryTile.click();
    }

    // Post anonymously
    await page.locator('#retroAnonymousCheckbox').check();
    await page.locator('#retroContentTextarea').fill(retroContent);

    // Save
    await page.locator('app-add-retro-card-modal .btn-save').click();
    await expect(page.locator('#retroContentTextarea')).not.toBeVisible({ timeout: 5000 });

    // 3. Verify card on board and upvote
    const card = page.locator('.retro-card', { hasText: retroContent });
    await expect(card).toBeVisible({ timeout: 10000 });

    const voteBtn = card.locator('.btn-vote');
    await expect(voteBtn).toBeVisible();
    await voteBtn.click();

    // 4. Add Action Item
    const addActionBtn = page.locator('.column-header-row .btn-mini-add');
    await expect(addActionBtn).toBeVisible();
    await addActionBtn.click();

    await expect(page.locator('#retroActionTitleInput')).toBeVisible();
    await page.locator('#retroActionTitleInput').fill(actionTitle);
    await page.locator('.modal-footer .btn-save', { hasText: 'Create Action Item' }).click();
    await expect(page.locator('#retroActionTitleInput')).not.toBeVisible({ timeout: 5000 });

    // Verify action item appears and toggle completed
    const actionCard = page.locator('.action-item-card', { hasText: actionTitle });
    await expect(actionCard).toBeVisible({ timeout: 10000 });

    const checkbox = actionCard.locator('input[type="checkbox"]');
    await expect(checkbox).toBeVisible();
    await checkbox.check();

    // 5. Delete Retro Card via confirm modal
    const deleteBtn = card.locator('.delete-action');
    await expect(deleteBtn).toBeVisible();
    await deleteBtn.click();

    const confirmBtn = page.locator('.btn-confirm-action', { hasText: 'Delete Note' });
    await expect(confirmBtn).toBeVisible();
    await confirmBtn.click();
    await expect(confirmBtn).not.toBeVisible({ timeout: 5000 });

    // Verify deleted
    await expect(page.locator('.retro-card', { hasText: retroContent })).not.toBeVisible({ timeout: 10000 });
  });
});

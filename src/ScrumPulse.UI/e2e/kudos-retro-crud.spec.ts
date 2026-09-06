import { test, expect } from '@playwright/test';
import { unlockScrumMaster } from './helpers';

test.describe('Kudos Wall & Retrospective Board Interactive Lifecycle', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/', { waitUntil: 'domcontentloaded' });
    await unlockScrumMaster(page);
  });

  test('should publish peer kudos card with explicit badge, sender, receiver, and trigger reaction counters', async ({ page }) => {
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
    let senderName = '';
    if (senderOptions.length > 1) {
      const senderVal = await senderOptions[1].getAttribute('value');
      const text = await senderOptions[1].textContent();
      senderName = text ? text.split('(')[0].trim() : '';
      if (senderVal) await senderSelect.selectOption(senderVal);
    }

    // Select Receiver (pick distinct member)
    const receiverSelect = page.locator('#kudosReceiverSelect');
    const receiverOptions = await receiverSelect.locator('option').all();
    let receiverName = '';
    if (receiverOptions.length > 2) {
      const receiverVal = await receiverOptions[2].getAttribute('value');
      const text = await receiverOptions[2].textContent();
      receiverName = text ? text.split('(')[0].trim() : '';
      if (receiverVal) await receiverSelect.selectOption(receiverVal);
    } else if (receiverOptions.length > 1) {
      const receiverVal = await receiverOptions[1].getAttribute('value');
      const text = await receiverOptions[1].textContent();
      receiverName = text ? text.split('(')[0].trim() : '';
      if (receiverVal) await receiverSelect.selectOption(receiverVal);
    }

    // Select specific Badge card: Innovation Star
    const badgeCard = page.locator('app-give-kudos-modal .badge-card', { hasText: 'Innovation Star' });
    if (await badgeCard.isVisible()) {
      await badgeCard.click();
      await expect(badgeCard).toHaveClass(/selected/);
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

    // Verify badge label and sender/receiver text
    await expect(card.locator('.kudos-badge')).toContainText('Innovation Star');
    await expect(card.locator('.kudos-message')).toHaveText(kudosMessage);
    if (senderName) {
      await expect(card.locator('.kudos-author')).toContainText(senderName);
    }
    if (receiverName) {
      await expect(card.locator('.kudos-author')).toContainText(receiverName);
    }

    // 4. Click emoji reactions (thumbsUp, zap, heart)
    const reactionBtn = card.locator('.reaction-btn').first();
    await expect(reactionBtn).toBeVisible();
    await reactionBtn.click();
    await expect(reactionBtn).toHaveClass(/active-reaction/);
  });

  test('should add retro card in specific category column, upvote, manage action items, and delete retro card', async ({ page }) => {
    // 1. Navigate to Retrospective Board
    await page.locator('.tab-btn', { hasText: 'Retrospective' }).click();
    await expect(page.locator('.retro-section')).toBeVisible({ timeout: 15000 });

    const timestamp = Date.now();
    const retroContent = `Pipeline deployment stalls due to Azure VM agent restart ${timestamp}`;
    const actionTitle = `Automate performance monitoring ${timestamp}`;

    // 2. Open Add Retro Card Modal
    const addCardBtn = page.locator('.retro-section .header-right-actions button', { hasText: 'Add Retro Card' });
    await expect(addCardBtn).toBeVisible();
    await addCardBtn.click();

    await expect(page.locator('#retroContentTextarea')).toBeVisible();

    // Select "Didn't Go Well" category tile explicitly
    const didntGoWellCard = page.locator('app-add-retro-card-modal .category-card', { hasText: "Didn't Go Well" });
    if (await didntGoWellCard.isVisible()) {
      await didntGoWellCard.click();
      await expect(didntGoWellCard).toHaveClass(/selected/);
    }

    // Post anonymously
    await page.locator('#retroAnonymousCheckbox').check();
    await page.locator('#retroContentTextarea').fill(retroContent);

    // Save
    await page.locator('app-add-retro-card-modal .btn-save').click();
    await expect(page.locator('#retroContentTextarea')).not.toBeVisible({ timeout: 5000 });

    // 3. Verify card appears inside the "What Didn't Go Well" column panel
    const columnPanel = page.locator('.column-panel', { hasText: "What Didn't Go Well" });
    await expect(columnPanel).toBeVisible({ timeout: 10000 });

    const card = columnPanel.locator('.retro-card', { hasText: retroContent });
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

    // 5. Delete Action Item via confirm modal with cancel safeguard
    const deleteActionBtn = actionCard.locator('.icon-action-btn.delete-action');
    await expect(deleteActionBtn).toBeVisible();
    await deleteActionBtn.click();

    const confirmActionModal = page.locator('app-confirm-modal .modal-box');
    await expect(confirmActionModal).toBeVisible();
    await expect(confirmActionModal).toContainText('Delete Action Item');

    // Cancel safeguard: action item remains intact
    await confirmActionModal.locator('.btn-secondary').click();
    await expect(confirmActionModal).not.toBeVisible({ timeout: 5000 });
    await expect(actionCard).toBeVisible();

    // Confirm deletion
    await deleteActionBtn.click();
    await expect(confirmActionModal).toBeVisible();
    const confirmDeleteActionBtn = confirmActionModal.locator('.btn-confirm-action', { hasText: 'Delete Action Item' });
    await expect(confirmDeleteActionBtn).toBeVisible();
    await confirmDeleteActionBtn.click();
    await expect(confirmActionModal).not.toBeVisible({ timeout: 5000 });
    await expect(page.locator('.action-item-card', { hasText: actionTitle })).not.toBeVisible({ timeout: 10000 });

    // 6. Delete Retro Card via confirm modal with cancel safeguard
    const deleteBtn = card.locator('.delete-action');
    await expect(deleteBtn).toBeVisible();
    await deleteBtn.click();

    const confirmCardModal = page.locator('app-confirm-modal .modal-box');
    await expect(confirmCardModal).toBeVisible();
    await expect(confirmCardModal).toContainText('Delete Note');

    // Cancel safeguard: retro card remains intact
    await confirmCardModal.locator('.btn-secondary').click();
    await expect(confirmCardModal).not.toBeVisible({ timeout: 5000 });
    await expect(card).toBeVisible();

    // Confirm deletion
    await deleteBtn.click();
    await expect(confirmCardModal).toBeVisible();
    const confirmBtn = confirmCardModal.locator('.btn-confirm-action', { hasText: 'Delete Note' });
    await expect(confirmBtn).toBeVisible();
    await confirmBtn.click();
    await expect(confirmCardModal).not.toBeVisible({ timeout: 5000 });

    // Verify deleted
    await expect(page.locator('.retro-card', { hasText: retroContent })).not.toBeVisible({ timeout: 10000 });
  });
});

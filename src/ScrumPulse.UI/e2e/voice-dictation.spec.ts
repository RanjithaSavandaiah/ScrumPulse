import { expect, test, Page } from '@playwright/test';

/**
 * Covers the voice control in a real browser.
 *
 * Unit tests can prove the routing and the text handling, but three things only exist in a
 * browser: whether the control is actually reachable on the page, whether the consent gate
 * stands between a click and a recording, and whether the recogniser the live path depends on
 * is present at all. The last one matters because a silent absence would quietly push every
 * dictation back onto the recorded path and the transcription quota.
 *
 * No real audio is captured. Granting the permission and asserting on the control's own state
 * is as far as this can go without a microphone attached to the runner.
 */
test.describe('Voice dictation', () => {
  const captureBar = '.voice-bar';
  const consentDialog = '.consent-box';

  async function openDashboard(page: Page): Promise<void> {
    await page.goto('/');
    await page.waitForLoadState('networkidle');
  }

  test('the control is reachable from the page', async ({ page }) => {
    await openDashboard(page);

    await expect(page.locator(captureBar)).toBeVisible();
  });

  test('the browser exposes the live recogniser the fast path needs', async ({ page }) => {
    // If this ever fails, dictation still works but silently falls back to recording every
    // phrase, which spends the free transcription budget the standup transcript needs.
    await openDashboard(page);

    const hasRecogniser = await page.evaluate(
      () => 'SpeechRecognition' in window || 'webkitSpeechRecognition' in window
    );

    expect(hasRecogniser).toBe(true);
  });

  test('dictation is offered only once a field is chosen', async ({ page }) => {
    // Dictating with nothing focused would write into nowhere, so the control says so rather
    // than accepting a click and doing nothing visible.
    await openDashboard(page);

    const dictateButton = page.locator(`${captureBar} button`, { hasText: 'Dictate' });

    await expect(dictateButton).toBeDisabled();
  });

  test('focusing a field names it as the destination', async ({ page }) => {
    await openDashboard(page);

    const dictationField = page.locator('[voiceInput]').first();
    const fieldCount = await page.locator('[voiceInput]').count();
    test.skip(fieldCount === 0, 'No dictation-enabled field on the dashboard');

    await dictationField.focus();

    await expect(page.locator(captureBar)).toContainText('Ready');
    await expect(page.locator(`${captureBar} button`, { hasText: 'Dictate' })).toBeEnabled();
  });

  test('capturing a call asks before anything is recorded', async ({ page }) => {
    // Recording colleagues sends their voices to a transcription service. The gate is the
    // thing that keeps that from happening on a single accidental click.
    await openDashboard(page);

    await page.locator(`${captureBar} button`, { hasText: 'Capture call' }).click();

    await expect(page.locator(consentDialog)).toBeVisible();
    await expect(page.locator(consentDialog)).toContainText('Tell your squad');
  });

  test('the consent dialog names the share option that actually carries audio', async ({ page }) => {
    // Picking "Window" is the single most common way this fails, because Chrome and Edge
    // cannot capture audio from a window on any platform.
    await openDashboard(page);
    await page.locator(`${captureBar} button`, { hasText: 'Capture call' }).click();

    await expect(page.locator(consentDialog)).toContainText('Entire Screen');
    await expect(page.locator(consentDialog)).toContainText('system audio');
  });

  test('declining consent records nothing', async ({ page }) => {
    await openDashboard(page);
    await page.locator(`${captureBar} button`, { hasText: 'Capture call' }).click();

    await page.locator(consentDialog).getByRole('button', { name: 'Cancel' }).click();

    await expect(page.locator(consentDialog)).toBeHidden();
    await expect(page.locator(`${captureBar}.is-live`)).toHaveCount(0);
  });

  test('dismissing the dialog by its backdrop also declines', async ({ page }) => {
    await openDashboard(page);
    await page.locator(`${captureBar} button`, { hasText: 'Capture call' }).click();

    await page.locator('.consent-backdrop').click({ position: { x: 5, y: 5 } });

    await expect(page.locator(consentDialog)).toBeHidden();
  });

  test('the dialog is announced as a dialog', async ({ page }) => {
    await openDashboard(page);

    await page.locator(`${captureBar} button`, { hasText: 'Capture call' }).click();

    await expect(page.getByRole('dialog')).toBeVisible();
  });

  test('a live recording is visibly marked', async ({ page, context }) => {
    // Anyone glancing at the screen during a share must be able to tell that the microphone
    // is open. A recording indicator that only the person who started it knows about is the
    // kind of thing that ends up in a complaint.
    await context.grantPermissions(['microphone']);
    await openDashboard(page);

    const dictationField = page.locator('[voiceInput]').first();
    test.skip((await page.locator('[voiceInput]').count()) === 0, 'No dictation-enabled field');

    await dictationField.focus();
    await page.locator(`${captureBar} button`, { hasText: 'Dictate' }).click();

    await expect(page.locator(`${captureBar}.is-live`)).toBeVisible();
    await expect(page.locator('.live-dot')).toBeVisible();
    await expect(page.locator(`${captureBar} button`, { hasText: 'Stop' })).toBeVisible();
  });

  test('stopping returns the control to its resting state', async ({ page, context }) => {
    await context.grantPermissions(['microphone']);
    await openDashboard(page);
    test.skip((await page.locator('[voiceInput]').count()) === 0, 'No dictation-enabled field');

    await page.locator('[voiceInput]').first().focus();
    await page.locator(`${captureBar} button`, { hasText: 'Dictate' }).click();
    await page.locator(`${captureBar} button`, { hasText: 'Stop' }).click();

    await expect(page.locator(`${captureBar}.is-live`)).toHaveCount(0);
    await expect(page.locator(`${captureBar} button`, { hasText: 'Dictate' })).toBeVisible();
  });

  test('a refused microphone is explained rather than failing silently', async ({ page }) => {
    await page.context().clearPermissions();
    await openDashboard(page);
    test.skip((await page.locator('[voiceInput]').count()) === 0, 'No dictation-enabled field');

    await page.locator('[voiceInput]').first().focus();
    await page.locator(`${captureBar} button`, { hasText: 'Dictate' }).click();

    const error = page.locator('.voice-error');

    // The recogniser may take a moment to report the refusal, and on a runner with no audio
    // device at all it may not surface one. Either outcome is acceptable; what is not is the
    // control claiming to be listening when it is not.
    await page.waitForTimeout(1_000);

    if (await error.isVisible()) {
      await expect(error).toContainText('Microphone');
    } else {
      await expect(page.locator(`${captureBar}.is-live`)).toHaveCount(0);
    }
  });

  test('the control follows the user across pages', async ({ page }) => {
    // It lives in the application shell precisely so that it is not re-implemented per screen.
    // A route that loses it means someone has mounted it in a component instead.
    await openDashboard(page);
    await expect(page.locator(captureBar)).toBeVisible();

    await page.goto('/blockers');
    await page.waitForLoadState('networkidle');

    await expect(page.locator(captureBar)).toBeVisible();
  });

  test('the control does not cover the page on a phone', async ({ page }) => {
    // Pinned to the bottom on a narrow screen, it sits exactly where form buttons do.
    await page.setViewportSize({ width: 390, height: 844 });
    await openDashboard(page);

    const bar = page.locator(captureBar);
    await expect(bar).toBeVisible();

    const bounds = await bar.boundingBox();
    expect(bounds).not.toBeNull();
    expect(bounds!.height).toBeLessThan(140);
  });
});

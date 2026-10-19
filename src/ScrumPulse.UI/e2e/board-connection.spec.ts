import { expect, test } from '@playwright/test';

/**
 * Covers the board connection screen and the boundary protecting it.
 *
 * Split deliberately: the form behaviour is asserted through a page, because a user is who it
 * is for, while the authorisation rule is asserted through `request`, because an attacker is
 * not using the form. Checking the rule by looking for a hidden button would only prove the UI
 * hides it, which is a courtesy rather than a control.
 */

const PAGE_URL = '/app/board-connection';

test.describe('Board connection configuration', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto(PAGE_URL);
  });

  test('the page loads and shows the connection form', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Azure DevOps connection' })).toBeVisible();

    await expect(page.locator('[data-testid="organization-url"]')).toBeVisible();
    await expect(page.locator('[data-testid="project-name"]')).toBeVisible();
    await expect(page.locator('[data-testid="personal-access-token"]')).toBeVisible();
    await expect(page.locator('[data-testid="content-policy"]')).toBeVisible();
    await expect(page.locator('[data-testid="save-connection"]')).toBeVisible();
  });

  test('mandatory fields are marked before anything is submitted', async ({ page }) => {
    // The point of the asterisk: a user should know what is required while filling the form
    // in, not after a failed save has thrown them back to the top.
    const organizationUrl = page.locator('[data-testid="organization-url"]');
    await expect(organizationUrl).toHaveAttribute('required', '');

    await expect(page.locator('[data-testid="project-name"]')).toHaveAttribute('required', '');

    await expect(page.getByText('Fields marked with an asterisk are required.')).toBeVisible();

    // The asterisk itself is a ::after on the label, so its presence is asserted through the
    // computed style rather than through the label's text.
    const marker = await page
      .locator('label[for="organizationUrl"]')
      .evaluate((label) => getComputedStyle(label, '::after').content);

    expect(marker).toContain('*');
  });

  test('a non-https organisation URL is refused in the browser', async ({ page }) => {
    await page.locator('[data-testid="organization-url"]').fill('http://dev.azure.com/contoso');
    await page.locator('[data-testid="project-name"]').fill('Delivery Platform');
    await page.locator('[data-testid="save-connection"]').click();

    await expect(page.getByText(/must start with https:\/\//)).toBeVisible();
  });

  test('an empty form reports which fields are missing', async ({ page }) => {
    await page.locator('[data-testid="save-connection"]').click();

    await expect(page.getByText('Organisation URL is required.')).toBeVisible();
    await expect(page.getByText('Project name is required.')).toBeVisible();
  });

  test('the token field is a password input that never carries a value', async ({ page }) => {
    const token = page.locator('[data-testid="personal-access-token"]');

    await expect(token).toHaveAttribute('type', 'password');
    await expect(token).toHaveAttribute('autocomplete', 'off');

    // The API returns no credential in any form, so there is nothing for the field to be
    // populated from, however the squad is configured.
    await expect(token).toHaveValue('');
  });

  test('the content policy is explained where the choice is made', async ({ page }) => {
    await expect(page.locator('[data-testid="content-policy-note"]')).toContainText(
      'Metrics only is the default'
    );
  });

  test('a back-to-dashboard link is present', async ({ page }) => {
    await expect(page.locator('a[href="/app"]')).toBeVisible();
  });

  test('the form is usable on a 390px phone viewport', async ({ page }) => {
    await page.setViewportSize({ width: 390, height: 844 });
    await page.goto(PAGE_URL);

    const organizationUrl = page.locator('[data-testid="organization-url"]');
    await expect(organizationUrl).toBeVisible();

    const box = await organizationUrl.boundingBox();
    expect(box).not.toBeNull();
    expect(box!.width).toBeLessThanOrEqual(390);

    // Nothing may push the layout wider than the viewport: a horizontally scrolling form is
    // how a phone user loses the right-hand edge of every field.
    const overflow = await page.evaluate(
      () => document.documentElement.scrollWidth - document.documentElement.clientWidth
    );
    expect(overflow).toBeLessThanOrEqual(1);

    // The primary action has to be reachable without a sideways scroll as well.
    const save = page.locator('[data-testid="save-connection"]');
    await expect(save).toBeVisible();
    await save.scrollIntoViewIfNeeded();

    const saveBox = await save.boundingBox();
    expect(saveBox!.x).toBeGreaterThanOrEqual(0);
    expect(saveBox!.x + saveBox!.width).toBeLessThanOrEqual(390);
  });
});

test.describe('Board connection API boundary', () => {
  /**
   * The credential these endpoints accept can read a client's whole backlog, and the content
   * policy they set decides how much of that client's text is stored here. Neither is a
   * decision a squad member gets to make on everyone's behalf.
   */
  const ENDPOINTS = [
    { method: 'get' as const, url: '/api/board-connections' },
    { method: 'post' as const, url: '/api/board-connections/test' },
    { method: 'post' as const, url: '/api/board-connections/sync' },
    { method: 'delete' as const, url: '/api/board-connections' }
  ];

  test('a caller without a Scrum Master session is refused', async ({ request }) => {
    for (const endpoint of ENDPOINTS) {
      const response = await request[endpoint.method](endpoint.url);

      expect(
        [401, 403],
        `${endpoint.method.toUpperCase()} ${endpoint.url} must not serve a non-Scrum-Master caller`
      ).toContain(response.status());
    }
  });

  test('a forged X-User-Role header does not grant Scrum Master', async ({ request }) => {
    const response = await request.put('/api/board-connections', {
      headers: { 'X-User-Role': 'ScrumMaster' },
      data: {
        organizationUrl: 'https://dev.azure.com/contoso',
        projectName: 'Delivery Platform',
        personalAccessToken: 'should-never-be-stored'
      }
    });

    expect(response.status()).toBe(401);
  });

  test('no response carries the stored credential back', async ({ request }) => {
    // Even the refusal is checked, because an error body echoing the request is how a token
    // ends up in a log or a browser cache.
    const response = await request.get('/api/board-connections');
    const body = await response.text();

    expect(body.toLowerCase()).not.toContain('personalaccesstoken');
  });
});

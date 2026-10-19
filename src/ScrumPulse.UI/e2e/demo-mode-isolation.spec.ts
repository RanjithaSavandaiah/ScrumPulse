import { test, expect } from '@playwright/test';

test.describe('Live Demo vs Real Dashboard Isolation & Theory Pages Edge Cases', () => {
  test('Edge Case 1: Live Demo -> Return to Home -> Hero Launch Dashboard button exits demo mode', async ({ page }) => {
    // 1. Visit live demo entry
    await page.goto('/demo', { waitUntil: 'domcontentloaded' });
    await page.waitForURL('**/app');

    // Verify demo banner is visible in demo mode
    const demoBanner = page.locator('.demo-banner');
    await expect(demoBanner).toBeVisible({ timeout: 10000 });
    await expect(demoBanner).toContainText('Sample data for a fictional squad');

    // 2. Return to the public landing page
    await page.goto('/', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('app-landing')).toBeVisible();

    // Verify demo banner is NOT displayed on the landing page
    await expect(demoBanner).toHaveCount(0);

    // 3. Click Hero "Launch Dashboard & Tabs"
    const heroLaunchBtn = page.locator('.hero-actions a', { hasText: 'Launch Dashboard & Tabs' }).first();
    await heroLaunchBtn.click();
    await page.waitForURL('**/app');

    // 4. Verify user is in the real application: demo banner is absent
    await expect(demoBanner).toHaveCount(0);
    await expect(page.locator('app-dashboard')).toBeVisible();
  });

  test('Edge Case 2: Live Demo -> Return to Home -> Bottom CTA Launch Dashboard button exits demo mode', async ({ page }) => {
    // 1. Visit demo
    await page.goto('/demo', { waitUntil: 'domcontentloaded' });
    await page.waitForURL('**/app');

    const demoBanner = page.locator('.demo-banner');
    await expect(demoBanner).toBeVisible({ timeout: 10000 });

    // 2. Return to home
    await page.goto('/', { waitUntil: 'domcontentloaded' });
    await expect(demoBanner).toHaveCount(0);

    // 3. Click Bottom CTA "Launch Dashboard & Tabs"
    const ctaLaunchBtn = page.locator('.marketing-page > .hero-actions a', { hasText: 'Launch Dashboard & Tabs' }).last();
    await ctaLaunchBtn.click();
    await page.waitForURL('**/app');

    // 4. Verify in real application: demo banner is absent
    await expect(demoBanner).toHaveCount(0);
    await expect(page.locator('app-dashboard')).toBeVisible();
  });

  test('Edge Case 3: Live Demo -> Visit Theory/Blog -> Navigation Header Launch Dashboard exits demo mode', async ({ page }) => {
    // 1. Enter demo
    await page.goto('/demo', { waitUntil: 'domcontentloaded' });
    await page.waitForURL('**/app');

    const demoBanner = page.locator('.demo-banner');
    await expect(demoBanner).toBeVisible({ timeout: 10000 });

    // 2. Navigate to theory page (/blog)
    await page.goto('/blog', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('app-blog-list')).toBeVisible();
    await expect(demoBanner).toHaveCount(0);

    // 3. Click "Launch Dashboard" from the top navigation header
    const navLaunchBtn = page.locator('.marketing-header .btn-launch');
    await navLaunchBtn.click();
    await page.waitForURL('**/app');

    // 4. Verify demo banner is absent on /app
    await expect(demoBanner).toHaveCount(0);
    await expect(page.locator('app-dashboard')).toBeVisible();
  });

  test('Edge Case 4: Exit Demo button from yellow banner returns to home and clears demo mode', async ({ page }) => {
    // 1. Visit demo
    await page.goto('/demo', { waitUntil: 'domcontentloaded' });
    await page.waitForURL('**/app');

    const demoBanner = page.locator('.demo-banner');
    await expect(demoBanner).toBeVisible({ timeout: 10000 });

    // 2. Click Exit demo
    await page.locator('.demo-exit').click();
    await page.waitForURL('**/');

    // Verify back on landing page and banner is not present
    await expect(page.locator('app-landing')).toBeVisible();
    await expect(demoBanner).toHaveCount(0);

    // 3. Now click Launch Dashboard from the navigation header
    await page.locator('.marketing-header .btn-launch').click();
    await page.waitForURL('**/app');

    // 4. Verify in real application, demo banner is not present
    await expect(demoBanner).toHaveCount(0);
  });

  test('Edge Case 5: In-session browser reload on /app preserves demo mode while inside the workspace', async ({ page }) => {
    // 1. Enter demo mode
    await page.goto('/demo', { waitUntil: 'domcontentloaded' });
    await page.waitForURL('**/app');

    const demoBanner = page.locator('.demo-banner');
    await expect(demoBanner).toBeVisible({ timeout: 10000 });

    // 2. Reload the page while staying inside the application
    await page.reload({ waitUntil: 'domcontentloaded' });

    // 3. Verify demo session persists for the active workspace tab
    await expect(demoBanner).toBeVisible({ timeout: 10000 });
    await expect(demoBanner).toContainText('Sample data for a fictional squad');
  });

  test('Edge Case 6: Neither Voice-to-Text bar nor Demo Banner are displayed on theory and marketing routes', async ({ page }) => {
    // Enter demo first to have session flag primed
    await page.goto('/demo', { waitUntil: 'domcontentloaded' });
    await page.waitForURL('**/app');

    const captureBar = '.voice-bar';
    const demoBanner = '.demo-banner';

    // Verify they are visible in /app
    await expect(page.locator(captureBar)).toBeVisible();
    await expect(page.locator(demoBanner)).toBeVisible();

    // Verify they are NOT rendered on public marketing & theory pages
    const publicRoutes = ['/', '/blog', '/about', '/contact', '/privacy-policy', '/terms'];
    for (const route of publicRoutes) {
      await page.goto(route, { waitUntil: 'domcontentloaded' });
      await expect(page.locator(captureBar), `Voice bar should be hidden on ${route}`).toHaveCount(0);
      await expect(page.locator(demoBanner), `Demo banner should be hidden on ${route}`).toHaveCount(0);
    }
  });

  test('Edge Case 7: Clean visitor directly navigating to /app does NOT show demo banner and is in real workspace', async ({ page }) => {
    await page.goto('/app', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('app-dashboard')).toBeVisible();

    // Verify demo banner is completely absent
    const demoBanner = page.locator('.demo-banner');
    await expect(demoBanner).toHaveCount(0);
  });

  test('Edge Case 8: Demo mode banner persists across workspace sub-routes (/app -> /app/board-connection)', async ({ page }) => {
    // 1. Enter demo mode
    await page.goto('/demo', { waitUntil: 'domcontentloaded' });
    await page.waitForURL('**/app');

    const demoBanner = page.locator('.demo-banner');
    await expect(demoBanner).toBeVisible({ timeout: 10000 });

    // 2. Navigate to Board Connection sub-route inside /app
    await page.goto('/app/board-connection', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('app-board-connection')).toBeVisible();

    // 3. Demo banner must remain visible on workspace sub-routes
    await expect(demoBanner).toBeVisible();
    await expect(demoBanner).toContainText('Sample data for a fictional squad');
  });

  test('Edge Case 9: User in real mode on /app who navigates to /demo switches cleanly into demo mode', async ({ page }) => {
    // 1. Start in real mode on /app
    await page.goto('/app', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('app-dashboard')).toBeVisible();
    await expect(page.locator('.demo-banner')).toHaveCount(0);

    // 2. Navigate to /demo
    await page.goto('/demo', { waitUntil: 'domcontentloaded' });
    await page.waitForURL('**/app');

    // 3. Demo banner is now active
    const demoBanner = page.locator('.demo-banner');
    await expect(demoBanner).toBeVisible({ timeout: 10000 });
    await expect(demoBanner).toContainText('Sample data for a fictional squad');
  });

  test('Edge Case 10: Temporary visit to /about from demo mode preserves demo mode when browser back is used', async ({ page }) => {
    // 1. Enter demo
    await page.goto('/demo', { waitUntil: 'domcontentloaded' });
    await page.waitForURL('**/app');

    const demoBanner = page.locator('.demo-banner');
    await expect(demoBanner).toBeVisible({ timeout: 10000 });

    // 2. User clicks or navigates to /about
    await page.goto('/about', { waitUntil: 'domcontentloaded' });
    await expect(page.locator('app-about')).toBeVisible();
    // Banner is suppressed on theory page
    await expect(demoBanner).toHaveCount(0);

    // 3. User hits browser Back to return to their demo session
    await page.goBack({ waitUntil: 'domcontentloaded' });
    await page.waitForURL('**/app');

    // 4. Returning to workspace restores the active demo session
    await expect(demoBanner).toBeVisible({ timeout: 10000 });
    await expect(demoBanner).toContainText('Sample data for a fictional squad');
  });
});

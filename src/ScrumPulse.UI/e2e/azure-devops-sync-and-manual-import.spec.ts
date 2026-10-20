import { test, expect } from '@playwright/test';
import { unlockScrumMaster } from './helpers';

test.describe('Azure DevOps Sync & Manual CSV Import End-to-End', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/app', { waitUntil: 'domcontentloaded' });
    await unlockScrumMaster(page);
    await page.goto('/app/board-connection', { waitUntil: 'domcontentloaded' });
  });

  test.afterEach(async ({ page }) => {
    try {
      await page.request.delete('/api/board-connections');
    } catch {
      // Ignore if no connection existed
    }
  });

  test('Manual CSV Import: Uploads real Azure DevOps export, displays imported counts, and idempotently updates on re-upload', async ({ page }) => {
    await expect(page.getByRole('heading', { name: 'Import from a CSV export' })).toBeVisible();

    const fileInput = page.locator('[data-testid="import-file"]');
    await expect(fileInput).toBeAttached();

    // 1. Initial CSV import with 2 unique work items
    const id1 = Date.now();
    const id2 = id1 + 1;
    const id3 = id1 + 2;

    const initialCsv = [
      'ID,Work Item Type,Title,State,Story Points,Assigned To',
      `${id1},User Story,"Implement OAuth2 Authentication Flow",Active,5,`,
      `${id2},Bug,"Fix Memory Leak in Data Streamer",Closed,3,`
    ].join('\n');

    await fileInput.setInputFiles({
      name: 'ado-backlog-export.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(initialCsv, 'utf-8')
    });

    // 2. Verify sync result panel appears with correct metrics
    const syncResult = page.locator('[data-testid="sync-result"]');
    await expect(syncResult).toBeVisible({ timeout: 10000 });
    await expect(syncResult).toContainText('Items imported');
    await expect(syncResult.locator('dd').first()).toHaveText('2');
    await expect(syncResult.locator('dd').nth(1)).toHaveText('2'); // 2 newly created

    // 3. Verify connection status panel updates to show file import status
    const statusPanel = page.locator('[data-testid="connection-status"]');
    await expect(statusPanel).toContainText('Imported 2 item(s) from a file');

    // 4. Re-upload with an updated CSV (id1 updated, id2 unchanged, id3 added)
    const updatedCsv = [
      'ID,Work Item Type,Title,State,Story Points,Assigned To',
      `${id1},User Story,"Implement OAuth2 Authentication Flow",Closed,8,`,
      `${id2},Bug,"Fix Memory Leak in Data Streamer",Closed,3,`,
      `${id3},Task,"Setup Continuous Integration Pipeline",Active,2,`
    ].join('\n');

    await fileInput.setInputFiles({
      name: 'ado-backlog-export-v2.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(updatedCsv, 'utf-8')
    });

    // Verify 3 items imported in total, but only 1 newly created (idempotent upsert)
    await expect(syncResult).toBeVisible({ timeout: 10000 });
    await expect(syncResult.locator('dd').first()).toHaveText('3');
    await expect(syncResult.locator('dd').nth(1)).toHaveText('1');

    // 5. Navigate back to dashboard and verify work items are accessible
    await page.locator('a.btn-nav-return').click();
    await page.waitForURL('**/app');
    await expect(page.locator('app-dashboard')).toBeVisible();
  });

  test('Manual CSV Import: Detects and surfaces unmapped board states with state mapping advice', async ({ page }) => {
    const fileInput = page.locator('[data-testid="import-file"]');
    await expect(fileInput).toBeAttached();

    const id1 = Date.now() + 10;
    const id2 = id1 + 1;

    // Upload a CSV with a bespoke unmapped board state
    const csvWithUnmappedState = [
      'ID,Title,State,Story Points',
      `${id1},"Security Review for API Keys",In Security Review,3`,
      `${id2},"Legacy Code Cleanup",Active,2`
    ].join('\n');

    await fileInput.setInputFiles({
      name: 'custom-process-export.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csvWithUnmappedState, 'utf-8')
    });

    const syncResult = page.locator('[data-testid="sync-result"]');
    await expect(syncResult).toBeVisible({ timeout: 10000 });

    // Verify unmapped state warning callout appears
    const unmappedStatesCallout = page.locator('[data-testid="unmapped-states"]');
    await expect(unmappedStatesCallout).toBeVisible();
    await expect(unmappedStatesCallout).toContainText('In Security Review');
    await expect(unmappedStatesCallout).toContainText('Board columns with no mapping');

    // Also verify warnings callout contains the advice
    const warningsCallout = page.locator('[data-testid="sync-warnings"]');
    await expect(warningsCallout).toBeVisible();
    await expect(warningsCallout).toContainText('In Security Review');

    // Verify state mapping table exists to allow mapping the state
    await expect(page.locator('[data-testid="state-mapping"]')).toBeVisible();
  });

  test('Manual CSV Import: Detects and surfaces unmatched board assignees', async ({ page }) => {
    // 1. Configure connection with EveryoneInAreaPath assignee filter so external assignees are tracked
    await page.locator('[data-testid="organization-url"]').fill('https://dev.azure.com/contoso-delivery');
    await page.locator('[data-testid="project-name"]').fill('Core Banking');
    await page.locator('[data-testid="personal-access-token"]').fill('pat-valid-token-secret-12345');
    await page.locator('[data-testid="assignee-filter"]').selectOption('EveryoneInAreaPath');
    await page.locator('[data-testid="save-connection"]').click();
    await expect(page.locator('.board-alert.is-success')).toBeVisible({ timeout: 10000 });

    const fileInput = page.locator('[data-testid="import-file"]');
    await expect(fileInput).toBeAttached();

    const id1 = Date.now() + 20;

    // Upload a CSV with an external assignee not on the squad roster
    const csvWithUnmatchedAssignee = [
      'ID,Title,State,Assigned To',
      `${id1},"Vendor Integration Task",Active,External Consultant <external@vendor.example>`
    ].join('\n');

    await fileInput.setInputFiles({
      name: 'vendor-export.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(csvWithUnmatchedAssignee, 'utf-8')
    });

    const syncResult = page.locator('[data-testid="sync-result"]');
    await expect(syncResult).toBeVisible({ timeout: 10000 });

    // Verify unmatched assignees callout appears
    const unmatchedCallout = page.locator('[data-testid="unmatched-assignees"]');
    await expect(unmatchedCallout).toBeVisible();
    await expect(unmatchedCallout).toContainText('External Consultant');
  });

  test('Manual CSV Import: Rejects non-CSV files and displays appropriate error banner', async ({ page }) => {
    const fileInput = page.locator('[data-testid="import-file"]');
    await expect(fileInput).toBeAttached();

    // Upload a non-CSV file (e.g. .txt or .json)
    await fileInput.setInputFiles({
      name: 'notes.txt',
      mimeType: 'text/plain',
      buffer: Buffer.from('Just plain text', 'utf-8')
    });

    const errorBanner = page.locator('[data-testid="error-banner"]');
    await expect(errorBanner).toBeVisible({ timeout: 10000 });
    await expect(errorBanner).toContainText(/Only a \.csv export is supported|Please select a valid \.csv file|could not be imported/i);
  });

  test('Manual CSV Import: Handles header-only CSV gracefully with clear warning', async ({ page }) => {
    const fileInput = page.locator('[data-testid="import-file"]');
    await expect(fileInput).toBeAttached();

    const headerOnlyCsv = 'ID,Work Item Type,Title,State,Story Points\n';

    await fileInput.setInputFiles({
      name: 'headers-only.csv',
      mimeType: 'text/csv',
      buffer: Buffer.from(headerOnlyCsv, 'utf-8')
    });

    const syncResult = page.locator('[data-testid="sync-result"]');
    await expect(syncResult).toBeVisible({ timeout: 10000 });
    await expect(syncResult.locator('dd').first()).toHaveText('0');

    const warnings = page.locator('[data-testid="sync-warnings"]');
    await expect(warnings).toBeVisible();
    await expect(warnings).toContainText('The file contained no rows');
  });

  test('Azure DevOps Sync: Configures connection, enables sync buttons, runs sync, and supports deletion', async ({ page }) => {
    // Intercept external Azure DevOps network calls so test runs reliably and fast
    await page.route(/\/api\/board-connections\/test/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          succeeded: true,
          message: 'Azure DevOps accepted the stored token.'
        })
      });
    });

    await page.route(/\/api\/board-connections\/sync/, async (route) => {
      await route.fulfill({
        status: 200,
        contentType: 'application/json',
        body: JSON.stringify({
          importedCount: 5,
          createdCount: 2,
          unmappedStates: [],
          unmatchedAssignees: [],
          warnings: [],
          syncedAtUtc: new Date().toISOString()
        })
      });
    });

    // 1. Fill in Azure DevOps configuration form
    await page.locator('[data-testid="organization-url"]').fill('https://dev.azure.com/contoso-delivery');
    await page.locator('[data-testid="project-name"]').fill('Core Banking');
    await page.locator('[data-testid="personal-access-token"]').fill('pat-valid-token-secret-12345');
    await page.locator('[data-testid="content-policy"]').selectOption('MetricsOnly');
    await page.locator('[data-testid="assignee-filter"]').selectOption('EveryoneInAreaPath');

    // 2. Save the connection
    const saveBtn = page.locator('[data-testid="save-connection"]');
    const savePromise = page.waitForResponse(
      (res) => res.url().includes('/api/board-connections') && res.request().method() === 'PUT'
    );
    await saveBtn.click();
    await savePromise;

    // 3. Verify success message and connection status panel
    await expect(page.locator('.board-alert.is-success')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('.board-alert.is-success')).toContainText('Connection saved');

    const statusPanel = page.locator('[data-testid="connection-status"]');
    await expect(statusPanel).toContainText('Connected');
    await expect(statusPanel).toContainText('https://dev.azure.com/contoso-delivery');
    await expect(statusPanel).toContainText('Core Banking');

    // 4. Verify Sync now and Full resync buttons are now enabled
    const syncNowBtn = page.locator('[data-testid="sync-now"]');
    const fullResyncBtn = page.locator('[data-testid="full-resync"]');
    const testConnectionBtn = page.locator('[data-testid="test-connection"]');

    await expect(syncNowBtn).toBeEnabled();
    await expect(fullResyncBtn).toBeEnabled();
    await expect(testConnectionBtn).toBeEnabled();

    // 5. Test connection
    const testPromise = page.waitForResponse((res) => res.url().includes('/api/board-connections/test'));
    await testConnectionBtn.click();
    await testPromise;
    await expect(page.locator('[data-testid="test-result"]')).toBeVisible({ timeout: 10000 });
    await expect(page.locator('[data-testid="test-result"]')).toContainText('Azure DevOps accepted the stored token');

    // 6. Trigger live sync and await completion
    const syncPromise = page.waitForResponse((res) => res.url().includes('/api/board-connections/sync'));
    const loadAfterSyncPromise = page.waitForResponse(
      (res) => res.url().includes('/api/board-connections') && res.request().method() === 'GET'
    );
    await syncNowBtn.click();
    await syncPromise;
    await loadAfterSyncPromise;
    await expect(syncNowBtn).toBeEnabled({ timeout: 15000 });
    await expect(fullResyncBtn).toBeEnabled({ timeout: 15000 });
    await expect(page.locator('[data-testid="sync-result"]')).toBeVisible();

    // 7. Trigger full resync and await completion
    await expect(fullResyncBtn).toBeEnabled({ timeout: 15000 });
    const resyncPromise = page.waitForResponse((res) => res.url().includes('/api/board-connections/sync'));
    const loadAfterResyncPromise = page.waitForResponse(
      (res) => res.url().includes('/api/board-connections') && res.request().method() === 'GET'
    );
    await fullResyncBtn.click();
    await resyncPromise;
    await loadAfterResyncPromise;
    await expect(syncNowBtn).toBeEnabled({ timeout: 15000 });
    await expect(fullResyncBtn).toBeEnabled({ timeout: 15000 });
    await expect(page.locator('[data-testid="sync-result"]')).toBeVisible();

    // 8. Delete connection
    const deleteBtn = page.locator('[data-testid="delete-connection"]');
    await expect(deleteBtn).toBeVisible({ timeout: 10000 });
    await deleteBtn.click();

    const deleteConfirm = page.locator('[data-testid="delete-confirm"]');
    await expect(deleteConfirm).toBeVisible({ timeout: 10000 });

    // Confirm deletion
    const deletePromise = page.waitForResponse(
      (res) => res.url().includes('/api/board-connections') && res.request().method() === 'DELETE'
    );
    await page.locator('[data-testid="delete-confirm-yes"]').click();
    await deletePromise;

    // Status returns to "No board connected"
    await expect(page.locator('.board-alert.is-success')).toContainText('Connection removed');
    await expect(statusPanel).toContainText('No board connected');
    await expect(syncNowBtn).toBeDisabled();
  });

  test('API Boundary: Rejects live sync with 409 Conflict when no board is connected', async ({ page }) => {
    // Ensure any existing board connection is removed before asserting rejection
    await page.request.delete('/api/board-connections');

    // Call /api/board-connections/sync via authenticated request
    const response = await page.request.post('/api/board-connections/sync');
    expect(response.status()).toBe(409);

    const body = await response.json();
    expect(body.title).toContain('No board connected');
  });

  test('API Boundary: Manual CSV import rejects empty payload with 400 Bad Request', async ({ page }) => {
    const response = await page.request.post('/api/board-connections/import', {
      multipart: {
        file: {
          name: 'empty.csv',
          mimeType: 'text/csv',
          buffer: Buffer.from('')
        }
      }
    });

    expect(response.status()).toBe(400);
    const body = await response.json();
    expect(body.detail).toContain('Choose the CSV exported from your Azure DevOps query');
  });
});

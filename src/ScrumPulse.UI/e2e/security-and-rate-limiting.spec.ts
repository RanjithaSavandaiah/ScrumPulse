import { test, expect } from '@playwright/test';

test.describe('Security Hardening & Rate Limiting Verification', () => {

  test('should enforce OWASP security headers and suppress server fingerprints', async ({ request }) => {
    // Make a request to the healthz endpoint
    const response = await request.get('/healthz');
    expect(response.ok()).toBeTruthy();

    const headers = response.headers();

    // 1. Mandatory OWASP Security Headers
    expect(headers['x-content-type-options']).toBe('nosniff');
    expect(headers['x-frame-options']).toBe('DENY');
    expect(headers['x-xss-protection']).toBe('1; mode=block');
    expect(headers['referrer-policy']).toBe('strict-origin-when-cross-origin');
    expect(headers['content-security-policy']).toBeDefined();
    expect(headers['content-security-policy']).toContain("default-src 'self'");
    expect(headers['permissions-policy']).toBeDefined();
    expect(headers['permissions-policy']).toContain('camera=()');

    // 2. Server Fingerprint Suppression
    expect(headers['x-powered-by']).toBeUndefined();
    if (headers['server']) {
      expect(headers['server'].toLowerCase()).not.toContain('kestrel');
    }
  });

  test('should enforce rate limiting on auth endpoints with HTTP 429 after limit exceeded', async ({ request }) => {
    // Auth test partition allows 3 requests per window with 0 queue limit.
    // Rapidly sending 6 requests triggers 429 Too Many Requests on the test partition
    // without exhausting permits for subsequent tests in the suite.
    const responses = await Promise.all(
      Array.from({ length: 6 }, () =>
        request.post('/api/auth/verify-pin', {
          data: { pin: '0000' },
          headers: {
            'Content-Type': 'application/json',
            'X-Test-Rate-Limit': 'true'
          }
        })
      )
    );

    const statuses = responses.map(r => r.status());
    const has429 = statuses.includes(429);
    expect(has429).toBeTruthy();
  });

  test('should not disclose raw SQL or internal exception traces on error', async ({ request }) => {
    // Send invalid ID format to trigger bad request / error handling
    const response = await request.get('/api/workitems/invalid-guid-value');
    const body = await response.text();

    // Verify sensitive keywords are NOT disclosed
    expect(body.toLowerCase()).not.toContain('microsoft.entityframeworkcore');
    expect(body.toLowerCase()).not.toContain('sqliteexception');
    expect(body.toLowerCase()).not.toContain('sqlcommand');
    expect(body.toLowerCase()).not.toContain('connectionstring');
    expect(body.toLowerCase()).not.toContain('stack trace');
  });
});

import { expect, test } from '@playwright/test';

/**
 * Asserts the API's security boundaries against the running server.
 *
 * These exist because the unit tests covering the same rules passed while production was
 * vulnerable. The rules had been relaxed in the controller and middleware to make the SPA
 * work after the browser stopped sending the session cookie, and nothing exercised the real
 * HTTP surface to notice.
 *
 * Driven through `request` rather than a browser page: an attacker uses curl, not the UI, and
 * the UI cannot demonstrate that a forged header is ignored.
 */

const SQUAD_DATA_ENDPOINTS = [
  '/api/standups',
  '/api/workitems',
  '/api/blockers',
  '/api/teammembers',
  '/api/monthlyfeedback'
];

test.describe('API security boundaries', () => {
  test('squad data requires a session', async ({ request }) => {
    for (const endpoint of SQUAD_DATA_ENDPOINTS) {
      const response = await request.get(endpoint);

      expect(
        [401, 403],
        `${endpoint} must not serve squad data without a session`
      ).toContain(response.status());
    }
  });

  test('a forged X-Team-Id header grants nothing', async ({ request }) => {
    // The original vulnerability: tenant identity taken from a caller-controlled header.
    const response = await request.get('/api/standups', {
      headers: { 'X-Team-Id': '00000000-0000-0000-0000-000000000001' }
    });

    expect([401, 403]).toContain(response.status());
  });

  test('a forged X-User-Role header does not grant Scrum Master', async ({ request }) => {
    const response = await request.delete('/api/workitems/00000000-0000-0000-0000-000000000001', {
      headers: { 'X-User-Role': 'ScrumMaster' }
    });

    expect([401, 403]).toContain(response.status());
  });

  test('the PIN alone cannot reach a squad without a session', async ({ request }) => {
    // verify-pin elevates an existing membership. It became anonymous and took the team from
    // a header, which meant one global PIN granted Scrum Master on any squad.
    const response = await request.post('/api/auth/verify-pin', {
      data: { pin: '1234' },
      headers: { 'X-Team-Id': '00000000-0000-0000-0000-000000000001' }
    });

    expect(response.status(), 'Elevation must require an existing squad session').toBe(401);
  });

  test('a browser User-Agent does not substitute for a session', async ({ request }) => {
    // The fallback branch checked for a User-Agent header, which every browser sends, and
    // then granted Scrum Master on the first active squad it found.
    const response = await request.post('/api/auth/verify-pin', {
      data: { pin: '1234' },
      headers: {
        'User-Agent': 'Mozilla/5.0 (Windows NT 10.0; Win64; x64)',
        'sec-ch-ua': '"Chromium";v="120"'
      }
    });

    expect(response.status()).toBe(401);
  });

  test('an unknown join code is rejected', async ({ request }) => {
    const response = await request.post('/api/auth/join', {
      data: { joinCode: 'NOT-A-REAL-CODE', displayName: 'Mallory' }
    });

    expect(response.status()).toBe(401);
  });

  test('the join endpoint validates its input', async ({ request }) => {
    const response = await request.post('/api/auth/join', {
      data: { joinCode: '', displayName: '' }
    });

    expect(response.status()).toBe(400);
  });

  test('session state is reported honestly to an anonymous caller', async ({ request }) => {
    const response = await request.get('/api/auth/session');

    expect(response.ok()).toBeTruthy();
    expect((await response.json()).isAuthenticated).toBe(false);
  });

  test('any issued session cookie cannot be read by script', async ({ request }) => {
    // HttpOnly is what stops an XSS bug exfiltrating a Scrum Master session, and
    // SameSite=Strict is what removes the CSRF exposure on the mutating endpoints.
    const response = await request.post('/api/auth/join', {
      data: { joinCode: 'NOT-A-REAL-CODE', displayName: 'Mallory' }
    });

    const setCookie = response.headers()['set-cookie'];

    if (setCookie) {
      expect(setCookie.toLowerCase()).toContain('httponly');
      expect(setCookie.toLowerCase()).toContain('samesite=strict');
    }
  });

  test('repeated PIN attempts are rate limited', async ({ request }) => {
    // A four digit PIN is only ten thousand combinations; without a limit it falls quickly.
    const statuses: number[] = [];

    for (let attempt = 0; attempt < 12; attempt++) {
      const response = await request.post('/api/auth/verify-pin', { data: { pin: '0000' } });
      statuses.push(response.status());
    }

    expect(
      statuses.some((status) => status === 429),
      'Expected the auth rate limiter to reject a burst of PIN attempts'
    ).toBeTruthy();
  });
});

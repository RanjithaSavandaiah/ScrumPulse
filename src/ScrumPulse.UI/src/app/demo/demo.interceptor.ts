import { HttpErrorResponse, HttpInterceptorFn, HttpResponse } from '@angular/common/http';
import { inject } from '@angular/core';
import { Observable, delay, of, throwError } from 'rxjs';
import { DemoModeService } from './demo-mode.service';
import {
  DEMO_BLOCKERS,
  DEMO_MEMBERS,
  DEMO_SPRINTS,
  DEMO_STANDUPS,
  DEMO_WORK_ITEMS
} from './demo-fixtures';

/**
 * Serves fixture data instead of calling the API while demo mode is active.
 *
 * Implemented in the client rather than as a seeded demo tenant on the server, for two
 * reasons. A public writable tenant is an attack surface and a vandalism target - the demo a
 * reviewer sees should not be editable by whoever found it first. And a client-side demo
 * needs no session, so it works on a cold Render instance without waiting for the database.
 *
 * Reads return fixtures. Writes are refused with a clear message rather than silently
 * appearing to succeed, because a demo that accepts a change and loses it on refresh is more
 * confusing than one that says it is read-only.
 */

/** Simulated latency. Instant responses look fake; this reads as a fast backend. */
const SIMULATED_LATENCY_MS = 180;

/** Endpoint fragment to fixture. Matched on substring so query strings do not matter. */
const FIXTURE_ROUTES: ReadonlyArray<{ fragment: string; payload: unknown }> = [
  { fragment: '/api/teammembers', payload: DEMO_MEMBERS },
  { fragment: '/api/sprints', payload: DEMO_SPRINTS },
  { fragment: '/api/workitems', payload: DEMO_WORK_ITEMS },
  { fragment: '/api/blockers', payload: DEMO_BLOCKERS },
  { fragment: '/api/standups', payload: DEMO_STANDUPS }
];

export const demoInterceptor: HttpInterceptorFn = (request, next) => {
  const demoMode = inject(DemoModeService);

  if (!demoMode.isActive() || !request.url.includes('/api/')) {
    return next(request);
  }

  if (request.method !== 'GET') {
    return refuseWrite();
  }

  const route = FIXTURE_ROUTES.find((candidate) =>
    request.url.toLowerCase().includes(candidate.fragment)
  );

  // Endpoints without a fixture return an empty collection rather than falling through to the
  // real API. Falling through would make a tab appear to work while quietly reading live data
  // and, on a tab that writes, could reach the database.
  const payload = route?.payload ?? [];

  return of(new HttpResponse({ status: 200, body: payload })).pipe(delay(SIMULATED_LATENCY_MS));
};

function refuseWrite(): Observable<never> {
  return throwError(
    () =>
      new HttpErrorResponse({
        status: 403,
        statusText: 'Demo mode',
        error: {
          title: 'Read-only demo',
          detail:
            'This is a demonstration with sample data, so changes are not saved. Join a squad to use ScrumPulse with your own team.'
        }
      })
  ).pipe(delay(SIMULATED_LATENCY_MS));
}

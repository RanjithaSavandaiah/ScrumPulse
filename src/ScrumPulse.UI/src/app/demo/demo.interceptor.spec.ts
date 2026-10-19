import { HttpClient, HttpErrorResponse, provideHttpClient, withInterceptors } from '@angular/common/http';
import {
  HttpTestingController,
  provideHttpClientTesting
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { demoInterceptor } from './demo.interceptor';
import { DemoModeService } from './demo-mode.service';

/**
 * Covers the boundary that keeps the public demo safe.
 *
 * Two properties matter. While demo mode is on, no request may reach the real API - a leak
 * would mean a stranger's browser reading or writing live squad data. While it is off, the
 * interceptor must be completely transparent, or every normal user silently gets fixtures.
 */
describe('demoInterceptor', () => {
  let httpClient: HttpClient;
  let httpMock: HttpTestingController;
  let demoMode: DemoModeService;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(withInterceptors([demoInterceptor])),
        provideHttpClientTesting()
      ]
    });

    httpClient = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
    demoMode = TestBed.inject(DemoModeService);
  });

  afterEach(() => {
    demoMode.disable();
    httpMock.verify();
  });

  describe('when demo mode is off', () => {
    it('lets API requests through untouched', async () => {
      const response = firstValueFrom(httpClient.get('/api/sprints'));

      const request = httpMock.expectOne('/api/sprints');
      request.flush([{ id: 'real-sprint' }]);

      expect(await response).toEqual([{ id: 'real-sprint' }] as never);
    });

    it('lets writes through untouched', async () => {
      const response = firstValueFrom(httpClient.post('/api/standups', { todayPlan: 'x' }));

      httpMock.expectOne('/api/standups').flush({ id: 'created' });

      expect(await response).toEqual({ id: 'created' } as never);
    });
  });

  describe('when demo mode is on', () => {
    beforeEach(() => demoMode.enable());

    it('serves fixtures without reaching the API', async () => {
      const members = await firstValueFrom(httpClient.get<unknown[]>('/api/teammembers'));

      // verify() in afterEach fails if anything reached the backend.
      expect(members.length).toBeGreaterThan(0);
    });

    it('returns a populated squad so the demo does not look broken', async () => {
      const sprints = await firstValueFrom(httpClient.get<{ isActive: boolean }[]>('/api/sprints'));

      expect(sprints.length).toBeGreaterThan(0);
      expect(sprints.some((sprint) => sprint.isActive)).toBeTrue();
    });

    it('refuses writes rather than appearing to accept them', async () => {
      // Silently discarding a write is more confusing than refusing it: the user watches
      // their change vanish on the next refresh with no explanation.
      await expectAsync(
        firstValueFrom(httpClient.post('/api/standups', { todayPlan: 'x' }))
      ).toBeRejected();
    });

    it('explains why the write was refused', async () => {
      try {
        await firstValueFrom(httpClient.put('/api/workitems/1', {}));
        fail('Expected the write to be refused.');
      } catch (error) {
        const httpError = error as HttpErrorResponse;
        expect(httpError.status).toBe(403);
        expect(httpError.error.detail).toContain('not saved');
      }
    });

    it('returns an empty collection for endpoints without a fixture', async () => {
      // Falling through to the real API would make an unfixtured tab quietly read live data.
      const kudos = await firstValueFrom(httpClient.get<unknown[]>('/api/kudos'));

      expect(kudos).toEqual([]);
    });

    it('ignores requests that are not API calls', async () => {
      const response = firstValueFrom(httpClient.get('/assets/config.json'));

      httpMock.expectOne('/assets/config.json').flush({ ok: true });

      expect(await response).toEqual({ ok: true } as never);
    });
  });

  describe('demo mode state', () => {
    it('starts inactive', () => {
      expect(demoMode.isActive()).toBeFalse();
    });

    it('survives within the session once enabled', () => {
      demoMode.enable();
      expect(demoMode.isActive()).toBeTrue();

      // A fresh injector reads the same session storage, which is what keeps demo mode
      // active while the visitor moves between tabs inside the application.
      TestBed.resetTestingModule();
      TestBed.configureTestingModule({
        providers: [provideHttpClient(withInterceptors([demoInterceptor])), provideHttpClientTesting()]
      });

      expect(TestBed.inject(DemoModeService).isActive()).toBeTrue();
    });
  });
});

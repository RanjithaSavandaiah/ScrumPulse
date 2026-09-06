import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideMockActions } from '@ngrx/effects/testing';
import { Observable, of } from 'rxjs';
import { PullRequestsEffects } from './pull-requests.effects';
import { PullRequestActions } from './pull-requests.actions';
import { PullRequestLog, DeveloperPrMetrics } from '../../models/scrum.models';

describe('PullRequestsEffects', () => {
  let effects: PullRequestsEffects;
  let actions$: Observable<any>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        PullRequestsEffects,
        provideMockActions(() => actions$),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    effects = TestBed.inject(PullRequestsEffects);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('loadPullRequests$ should dispatch loadPullRequestsSuccess on successful HTTP GET', (done) => {
    const mockLogs: PullRequestLog[] = [
      {
        id: 'pr-1',
        prNumber: '#PR-101',
        prTitle: 'Feature: OAuth',
        prUrl: 'https://github.com/org/repo/pull/101',
        totalCommentsCount: 5,
        actionableCommentsCount: 3,
        reviewSummary: 'Approved',
        reviewStatus: 'Approved',
        authorId: 'dev-1',
        authorName: 'Priya Sharma',
        createdAtUtc: new Date().toISOString()
      }
    ];

    actions$ = of(PullRequestActions.loadPullRequests({}));

    effects.loadPullRequests$.subscribe(action => {
      expect(action).toEqual(PullRequestActions.loadPullRequestsSuccess({ prLogs: mockLogs }));
      done();
    });

    const req = httpMock.expectOne('/api/pullrequests');
    expect(req.request.method).toBe('GET');
    req.flush(mockLogs);
  });

  it('loadDeveloperPrMetrics$ should dispatch loadDeveloperPrMetricsSuccess on HTTP success', (done) => {
    const mockMetrics: DeveloperPrMetrics[] = [
      {
        developerId: 'dev-1',
        developerName: 'Priya Sharma',
        developerRole: 'Developer',
        developerAvatar: 'PS',
        totalPrsCreated: 4,
        totalCommentsReceived: 12,
        actionableCommentsReceived: 8,
        actionabilityRatePercentage: 67,
        avgCommentsPerPr: 3.0,
        prs: []
      }
    ];

    actions$ = of(PullRequestActions.loadDeveloperPrMetrics({}));

    effects.loadDeveloperPrMetrics$.subscribe(action => {
      expect(action).toEqual(PullRequestActions.loadDeveloperPrMetricsSuccess({ metrics: mockMetrics }));
      done();
    });

    const req = httpMock.expectOne('/api/pullrequests/developer-metrics');
    expect(req.request.method).toBe('GET');
    req.flush(mockMetrics);
  });

  it('createPullRequestLog$ should dispatch createPullRequestLogSuccess on HTTP POST', (done) => {
    const newLog: PullRequestLog = {
      id: 'pr-new',
      prNumber: '#PR-202',
      prTitle: 'Bugfix: Circuit Breaker',
      prUrl: 'https://github.com/org/repo/pull/202',
      totalCommentsCount: 2,
      actionableCommentsCount: 1,
      reviewSummary: 'LGTM',
      reviewStatus: 'Approved',
      authorId: 'dev-1',
      authorName: 'Priya Sharma',
      createdAtUtc: new Date().toISOString()
    };

    actions$ = of(PullRequestActions.createPullRequestLog({ request: newLog }));

    effects.createPullRequestLog$.subscribe(action => {
      expect(action).toEqual(PullRequestActions.createPullRequestLogSuccess({ log: newLog }));
      done();
    });

    const req = httpMock.expectOne('/api/pullrequests');
    expect(req.request.method).toBe('POST');
    req.flush(newLog);
  });

  it('deletePullRequestLog$ should dispatch deletePullRequestLogSuccess on HTTP DELETE', (done) => {
    const prId = 'pr-to-delete';

    actions$ = of(PullRequestActions.deletePullRequestLog({ id: prId }));

    effects.deletePullRequestLog$.subscribe(action => {
      expect(action).toEqual(PullRequestActions.deletePullRequestLogSuccess({ id: prId }));
      done();
    });

    const req = httpMock.expectOne(`/api/pullrequests/${prId}`);
    expect(req.request.method).toBe('DELETE');
    req.flush({});
  });
});

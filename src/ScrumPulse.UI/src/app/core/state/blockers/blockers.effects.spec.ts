import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideMockActions } from '@ngrx/effects/testing';
import { Observable, of } from 'rxjs';
import { BlockersEffects } from './blockers.effects';
import { BlockerActions } from './blockers.actions';
import { Blocker } from '../../models/scrum.models';

describe('BlockersEffects', () => {
  let effects: BlockersEffects;
  let actions$: Observable<any>;
  let httpMock: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        BlockersEffects,
        provideMockActions(() => actions$),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    effects = TestBed.inject(BlockersEffects);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('loadBlockers$ should dispatch loadBlockersSuccess on HTTP success', (done) => {
    const mockBlockers: Blocker[] = [
      {
        id: 'blocker-1',
        title: 'Firewall blocking Redis cluster port',
        description: 'Need firewall rule approval from SecOps',
        category: 'EnvironmentAccess',
        slaHoursLimit: 4,
        raisedById: 'user-1',
        raisedByName: 'Priya Sharma',
        raisedAtUtc: new Date().toISOString(),
        isResolved: false,
        hoursWaiting: 1.5,
        isSlaBreached: false
      }
    ];

    actions$ = of(BlockerActions.loadBlockers());

    effects.loadBlockers$.subscribe(action => {
      expect(action).toEqual(BlockerActions.loadBlockersSuccess({ blockers: mockBlockers }));
      done();
    });

    const req = httpMock.expectOne('/api/blockers');
    expect(req.request.method).toBe('GET');
    req.flush(mockBlockers);
  });

  it('createBlocker$ should dispatch createBlockerSuccess on HTTP POST', (done) => {
    const newBlocker: Blocker = {
      id: 'blocker-new',
      title: 'Database connection timeout',
      description: 'Connection pool exhausted under load test',
      category: 'TechLeadArchitecture',
      slaHoursLimit: 8,
      raisedById: 'user-2',
      raisedByName: 'Arun Kumar',
      raisedAtUtc: new Date().toISOString(),
      isResolved: false,
      hoursWaiting: 0,
      isSlaBreached: false
    };

    actions$ = of(BlockerActions.createBlocker({ blocker: newBlocker }));

    effects.createBlocker$.subscribe(action => {
      expect(action).toEqual(BlockerActions.createBlockerSuccess({ blocker: newBlocker }));
      done();
    });

    const req = httpMock.expectOne('/api/blockers');
    expect(req.request.method).toBe('POST');
    req.flush(newBlocker);
  });

  it('resolveBlocker$ should dispatch resolveBlockerSuccess on HTTP POST /resolve', (done) => {
    const resolvedBlocker: Blocker = {
      id: 'blocker-1',
      title: 'Firewall port',
      description: 'Approved',
      category: 'EnvironmentAccess',
      slaHoursLimit: 4,
      raisedById: 'user-1',
      raisedByName: 'Priya Sharma',
      raisedAtUtc: new Date().toISOString(),
      isResolved: true,
      hoursWaiting: 2.0,
      isSlaBreached: false,
      resolutionNotes: 'SecOps approved rule'
    };

    actions$ = of(BlockerActions.resolveBlocker({ id: 'blocker-1', notes: 'SecOps approved rule' }));

    effects.resolveBlocker$.subscribe(action => {
      expect(action).toEqual(BlockerActions.resolveBlockerSuccess({ blocker: resolvedBlocker }));
      done();
    });

    const req = httpMock.expectOne('/api/blockers/blocker-1/resolve');
    expect(req.request.method).toBe('POST');
    req.flush(resolvedBlocker);
  });

  it('deleteBlocker$ should dispatch deleteBlockerSuccess on HTTP DELETE', (done) => {
    const blockerId = 'blocker-to-delete';

    actions$ = of(BlockerActions.deleteBlocker({ id: blockerId }));

    effects.deleteBlocker$.subscribe(action => {
      expect(action).toEqual(BlockerActions.deleteBlockerSuccess({ id: blockerId }));
      done();
    });

    const req = httpMock.expectOne(`/api/blockers/${blockerId}`);
    expect(req.request.method).toBe('DELETE');
    req.flush({});
  });
});

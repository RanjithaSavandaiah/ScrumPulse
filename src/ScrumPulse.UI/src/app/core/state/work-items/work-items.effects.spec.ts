import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideMockActions } from '@ngrx/effects/testing';
import { Observable, of } from 'rxjs';
import { WorkItemsEffects } from './work-items.effects';
import { WorkItemActions } from './work-items.actions';
import { WorkItem } from '../../models/scrum.models';

describe('WorkItemsEffects', () => {
  let effects: WorkItemsEffects;
  let actions$: Observable<any>;
  let httpMock: HttpTestingController;

  const baseItem: WorkItem = {
    id: 'wi-1',
    key: 'SP-101',
    title: 'Build Distributed Tenant Interceptor',
    description: 'Intercept multi-tenant requests and apply tenant routing',
    type: 'UserStory',
    priority: 'High',
    status: 'InProgress',
    storyPoints: 5,
    estimatedHours: 12,
    createdAtUtc: new Date().toISOString(),
    dorAcceptanceCriteriaDefined: true,
    dorDependenciesIdentified: true,
    dorWireframeAvailable: true,
    dodUnitTestsPassed: true,
    dodPeerReviewCompleted: true,
    dodMergedToMaster: true,
    dodStagingVerified: true,
    isEscapedDefect: false
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        WorkItemsEffects,
        provideMockActions(() => actions$),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });

    effects = TestBed.inject(WorkItemsEffects);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('loadWorkItems$ should dispatch loadWorkItemsSuccess on HTTP success', (done) => {
    const mockItems: WorkItem[] = [baseItem];

    actions$ = of(WorkItemActions.loadWorkItems({}));

    effects.loadWorkItems$.subscribe(action => {
      expect(action).toEqual(WorkItemActions.loadWorkItemsSuccess({ items: mockItems }));
      done();
    });

    const req = httpMock.expectOne('/api/workitems');
    expect(req.request.method).toBe('GET');
    req.flush(mockItems);
  });

  it('createWorkItem$ should dispatch createWorkItemSuccess on HTTP POST', (done) => {
    const newItem: WorkItem = {
      ...baseItem,
      id: 'wi-new',
      key: 'SP-102',
      title: 'Automate Playwright Test Suite',
      type: 'TaskPbi',
      priority: 'Critical',
      status: 'Backlog',
      storyPoints: 3,
      estimatedHours: 8
    };

    actions$ = of(WorkItemActions.createWorkItem({ item: newItem }));

    effects.createWorkItem$.subscribe(action => {
      expect(action).toEqual(WorkItemActions.createWorkItemSuccess({ item: newItem }));
      done();
    });

    const req = httpMock.expectOne('/api/workitems');
    expect(req.request.method).toBe('POST');
    req.flush(newItem);
  });

  it('advanceStage$ should dispatch advanceWorkItemStageSuccess on HTTP POST /advance-stage', (done) => {
    const advancedItem: WorkItem = {
      ...baseItem,
      status: 'PrCreated'
    };

    actions$ = of(WorkItemActions.advanceWorkItemStage({ id: 'wi-1', targetStatus: 'PrCreated' }));

    effects.advanceStage$.subscribe(action => {
      expect(action).toEqual(WorkItemActions.advanceWorkItemStageSuccess({ item: advancedItem }));
      done();
    });

    const req = httpMock.expectOne('/api/workitems/wi-1/advance-stage');
    expect(req.request.method).toBe('POST');
    expect(req.request.body).toEqual({ targetStatus: 'PrCreated' });
    req.flush(advancedItem);
  });

  it('deleteWorkItem$ should dispatch deleteWorkItemSuccess on HTTP DELETE', (done) => {
    const itemId = 'wi-to-delete';

    actions$ = of(WorkItemActions.deleteWorkItem({ id: itemId }));

    effects.deleteWorkItem$.subscribe(action => {
      expect(action).toEqual(WorkItemActions.deleteWorkItemSuccess({ id: itemId }));
      done();
    });

    const req = httpMock.expectOne(`/api/workitems/${itemId}`);
    expect(req.request.method).toBe('DELETE');
    req.flush({});
  });
});

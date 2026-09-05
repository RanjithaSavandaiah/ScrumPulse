import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { Store, provideStore } from '@ngrx/store';
import { ReportExportService, ExportFilterOptions } from './report-export.service';
import { ScrumStateService } from './scrum-state.service';
import { appReducers, WorkItemActions, PullRequestActions } from '../state';
import { WorkItem, PullRequestLog } from '../models/scrum.models';

describe('ReportExportService', () => {
  let service: ReportExportService;
  let store: Store;

  const mockWorkItem: WorkItem = {
    id: 'w-1',
    key: 'SP-10',
    title: 'Export functionality',
    description: '',
    type: 'UserStory',
    status: 'Done',
    storyPoints: 5,
    priority: 'High',
    assigneeId: 'm-1',
    assigneeName: 'Alice',
    sprintId: 's-1',
    createdAtUtc: '2026-09-01T00:00:00Z',
    dorAcceptanceCriteriaDefined: true,
    dorDependenciesIdentified: true,
    dorWireframeAvailable: true,
    dodUnitTestsPassed: true,
    dodPeerReviewCompleted: true,
    dodMergedToMaster: true,
    dodStagingVerified: true,
    isEscapedDefect: false
  };

  const mockPr: PullRequestLog = {
    id: 'pr-1',
    authorId: 'm-1',
    authorName: 'Alice',
    prNumber: 'PR-100',
    prTitle: 'Export PDF',
    prUrl: '',
    totalCommentsCount: 3,
    actionableCommentsCount: 1,
    reviewSummary: '',
    reviewStatus: 'Merged',
    sprintId: 's-1',
    createdAtUtc: '2026-09-02T00:00:00Z'
  };

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        ReportExportService,
        ScrumStateService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideStore(appReducers)
      ]
    });

    service = TestBed.inject(ReportExportService);
    store = TestBed.inject(Store);

    store.dispatch(WorkItemActions.loadWorkItemsSuccess({ items: [mockWorkItem] }));
    store.dispatch(PullRequestActions.loadPullRequestsSuccess({ prLogs: [mockPr] }));
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should filter data for ALL scope', () => {
    const options: ExportFilterOptions = {
      memberId: 'ALL',
      timeScopeType: 'ALL'
    };

    const data = service.filterData(options);
    expect(data.workItems.length).toBe(1);
    expect(data.prLogs.length).toBe(1);
  });

  it('should filter data for specific sprint', () => {
    const options: ExportFilterOptions = {
      memberId: 'ALL',
      timeScopeType: 'SPRINT',
      sprintId: 's-1'
    };

    const data = service.filterData(options);
    expect(data.workItems.length).toBe(1);
    expect(data.prLogs.length).toBe(1);

    const nonExistent = service.filterData({ ...options, sprintId: 's-99' });
    expect(nonExistent.workItems.length).toBe(0);
    expect(nonExistent.prLogs.length).toBe(0);
  });

  it('should filter data by member ID', () => {
    const dataMatch = service.filterData({
      memberId: 'm-1',
      timeScopeType: 'ALL'
    });
    expect(dataMatch.workItems.length).toBe(1);

    const dataNoMatch = service.filterData({
      memberId: 'm-99',
      timeScopeType: 'ALL'
    });
    expect(dataNoMatch.workItems.length).toBe(0);
  });

  it('should filter data for MONTH scope', () => {
    const data = service.filterData({
      memberId: 'ALL',
      timeScopeType: 'MONTH',
      month: '2026-09'
    });
    expect(data.workItems.length).toBe(1);
    expect(data.prLogs.length).toBe(1);

    const dataDifferentMonth = service.filterData({
      memberId: 'ALL',
      timeScopeType: 'MONTH',
      month: '2026-01'
    });
    expect(dataDifferentMonth.workItems.length).toBe(0);
    expect(dataDifferentMonth.prLogs.length).toBe(0);
  });
});

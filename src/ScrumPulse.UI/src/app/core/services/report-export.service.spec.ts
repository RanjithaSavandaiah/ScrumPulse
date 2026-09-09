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

  describe('calculateStandupCompliance', () => {
    it('should calculate expected days, missed days, and compliance rate excluding approved leaves and weekends', () => {
      // 2026-09-01 is Tuesday, 2026-09-04 is Friday (4 weekdays: 01, 02, 03, 04)
      const options: ExportFilterOptions = {
        memberId: 'm-1',
        timeScopeType: 'CUSTOM',
        startDate: '2026-09-01',
        endDate: '2026-09-04'
      };

      const standups: any[] = [
        { teamMemberId: 'm-1', standupDate: '2026-09-01T09:00:00Z', yesterdaySummary: 'Done 1', todayPlan: 'Plan 1' },
        { teamMemberId: 'm-1', standupDate: '2026-09-02T09:00:00Z', yesterdaySummary: 'Done 2', todayPlan: 'Plan 2' }
      ];

      const leaves: any[] = [
        { teamMemberId: 'm-1', startDate: '2026-09-03T00:00:00Z', endDate: '2026-09-03T23:59:59Z', isApproved: true }
      ];

      const compliance = service.calculateStandupCompliance('m-1', options, standups, leaves, []);

      // Expected: 4 weekdays minus 1 leave day = 3 expected days
      expect(compliance.expectedDays).toBe(3);
      expect(compliance.leaveDays).toBe(1);
      // Logged: 2 days (Sep 1 and Sep 2)
      expect(compliance.loggedDays).toBe(2);
      // Missed: 1 day (Sep 4, since Sep 3 was leave)
      expect(compliance.missedDays).toBe(1);
      expect(compliance.complianceRate).toBe(67); // Math.round(2 / 3 * 100) = 67%
      expect(compliance.missedDates).toEqual(['2026-09-04']);
    });

    it('should report 100% compliance when member logs updates on all working days not on leave', () => {
      const options: ExportFilterOptions = {
        memberId: 'm-1',
        timeScopeType: 'CUSTOM',
        startDate: '2026-09-01',
        endDate: '2026-09-02'
      };

      const standups: any[] = [
        { teamMemberId: 'm-1', standupDate: '2026-09-01T09:00:00Z' },
        { teamMemberId: 'm-1', standupDate: '2026-09-02T09:00:00Z' }
      ];

      const compliance = service.calculateStandupCompliance('m-1', options, standups, [], []);
      expect(compliance.expectedDays).toBe(2);
      expect(compliance.loggedDays).toBe(2);
      expect(compliance.missedDays).toBe(0);
      expect(compliance.complianceRate).toBe(100);
      expect(compliance.missedDates.length).toBe(0);
    });
  });

  describe('buildWorkItemsRows', () => {
    it('should format all 6 stage timestamps in Excel rows when present', () => {
      const itemWithStages: WorkItem = {
        ...mockWorkItem,
        pickedUpAtUtc: '2026-09-01T10:00:00Z',
        prCreatedAtUtc: '2026-09-02T11:00:00Z',
        prApprovedAtUtc: '2026-09-02T15:30:00Z',
        prMergedAtUtc: '2026-09-03T09:00:00Z',
        qaStartedAtUtc: '2026-09-03T10:30:00Z',
        completedAtUtc: '2026-09-04T16:00:00Z'
      };

      const rows = service.buildWorkItemsRows([itemWithStages], 'Alice (Developer)');
      expect(rows.length).toBe(1);
      const row = rows[0];

      expect(row['Picked Up At']).toContain('2026');
      expect(row['PR Created At']).toContain('2026');
      expect(row['PR Approved At']).toContain('2026');
      expect(row['PR Merged At']).toContain('2026');
      expect(row['QA Started At']).toContain('2026');
      expect(row['Completed At']).toContain('2026');
    });

    it('should return empty string for timestamps when stages are not yet reached', () => {
      const pendingItem: WorkItem = {
        ...mockWorkItem,
        pickedUpAtUtc: undefined,
        prCreatedAtUtc: undefined,
        prApprovedAtUtc: undefined,
        prMergedAtUtc: undefined,
        qaStartedAtUtc: undefined,
        completedAtUtc: undefined
      };

      const rows = service.buildWorkItemsRows([pendingItem], 'Alice (Developer)');
      expect(rows.length).toBe(1);
      const row = rows[0];

      expect(row['Picked Up At']).toBe('');
      expect(row['PR Created At']).toBe('');
      expect(row['PR Approved At']).toBe('');
      expect(row['PR Merged At']).toBe('');
      expect(row['QA Started At']).toBe('');
      expect(row['Completed At']).toBe('');
    });
  });
});

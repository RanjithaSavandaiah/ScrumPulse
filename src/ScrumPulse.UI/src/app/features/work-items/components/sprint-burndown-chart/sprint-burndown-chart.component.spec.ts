import { ComponentFixture, TestBed } from '@angular/core/testing';
import { SprintBurndownChartComponent } from './sprint-burndown-chart.component';
import { Sprint, WorkItem, TeamLeave, TeamMember } from '../../../../core/models/scrum.models';

describe('SprintBurndownChartComponent', () => {
  let component: SprintBurndownChartComponent;
  let fixture: ComponentFixture<SprintBurndownChartComponent>;

  const mockSprint: Sprint = {
    id: 'sprint-1',
    name: 'Sprint 20',
    goal: 'Build burndown and capacity',
    startDate: '2026-09-01T00:00:00Z',
    endDate: '2026-09-14T00:00:00Z',
    isActive: true,
    committedStoryPoints: 30,
    deliveredStoryPoints: 10,
    confidenceScore: 4.5,
    dailyWorkingHours: 8.5
  };

  const mockMembers: TeamMember[] = [
    { id: 'm-1', name: 'Dev 1', email: 'dev1@test.com', role: 'Developer', avatar: '', location: 'BLR', timeZone: 'IST', activeWipLimit: 3, isActive: true },
    { id: 'm-2', name: 'Dev 2', email: 'dev2@test.com', role: 'Developer', avatar: '', location: 'BLR', timeZone: 'IST', activeWipLimit: 3, isActive: true }
  ];

  const mockLeaves: TeamLeave[] = [
    {
      id: 'l-1',
      teamMemberId: 'm-1',
      teamMemberName: 'Dev 1',
      startDate: '2026-09-02T00:00:00Z',
      endDate: '2026-09-02T00:00:00Z',
      leaveSlot: 'FullDay',
      totalDays: 1,
      reason: 'Personal',
      leaveType: 'Privilege Leave',
      location: 'Local',
      isApproved: true
    }
  ];

  const mockWorkItems: WorkItem[] = [
    {
      id: 'w-1',
      key: 'SP-1',
      title: 'Item 1',
      description: 'Desc',
      type: 'UserStory',
      status: 'Done',
      priority: 'Medium',
      storyPoints: 10,
      sprintId: 'sprint-1',
      isEscapedDefect: false,
      createdAtUtc: '2026-09-01T00:00:00Z',
      completedAtUtc: '2026-09-05T00:00:00Z',
      dorAcceptanceCriteriaDefined: true,
      dorDependenciesIdentified: true,
      dorWireframeAvailable: true,
      dodUnitTestsPassed: true,
      dodPeerReviewCompleted: true,
      dodMergedToMaster: true,
      dodStagingVerified: true
    },
    {
      id: 'w-2',
      key: 'SP-2',
      title: 'Item 2',
      description: 'Desc',
      type: 'UserStory',
      status: 'InProgress',
      priority: 'High',
      storyPoints: 20,
      sprintId: 'sprint-1',
      isEscapedDefect: false,
      createdAtUtc: '2026-09-01T00:00:00Z',
      dorAcceptanceCriteriaDefined: true,
      dorDependenciesIdentified: true,
      dorWireframeAvailable: true,
      dodUnitTestsPassed: false,
      dodPeerReviewCompleted: false,
      dodMergedToMaster: false,
      dodStagingVerified: false
    }
  ];

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SprintBurndownChartComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(SprintBurndownChartComponent);
    component = fixture.componentInstance;
    component.sprint = { ...mockSprint };
    component.members = [...mockMembers];
    component.leaves = [...mockLeaves];
    component.workItems = [...mockWorkItems];
    fixture.detectChanges();
  });

  it('should create and compute sprint capacity analysis', () => {
    expect(component).toBeTruthy();
    const capacity = component.capacityAnalysis();
    expect(capacity.memberCount).toBe(2);
    expect(capacity.workingDays).toBeGreaterThan(0);
    expect(capacity.totalLeaveDays).toBe(1);
    expect(capacity.grossHours).toBeGreaterThan(0);
    expect(capacity.netAvailableHours).toBeLessThan(capacity.grossHours);
    expect(capacity.committedPoints).toBe(30);
    expect(capacity.deliveredPoints).toBe(10);
    expect(capacity.remainingPoints).toBe(20);
    expect(capacity.leaveBreakdown.length).toBe(1);
  });

  it('should generate SVG path coordinates and burndown points', () => {
    const burndown = component.burndownData();
    expect(burndown.days.length).toBeGreaterThan(0);
    expect(burndown.idealPath).toContain('M ');
    expect(burndown.getX(0)).toBeDefined();
    expect(burndown.getY(10)).toBeDefined();
    expect(['Ahead', 'OnTrack', 'Behind', 'Completed']).toContain(burndown.paceStatus);
  });
});

import { selectAllSprints, selectActiveSprint } from './sprints/sprints.selectors';
import { selectAllWorkItems, selectWorkItemsLoading } from './work-items/work-items.selectors';
import { selectPullRequestLogs, selectDeveloperPrMetrics } from './pull-requests/pull-requests.selectors';
import { selectAllMembers, selectCurrentRole, selectIsDarkMode } from './team-members/team-members.selectors';
import { selectAllBlockers, selectActiveBlockersCount } from './blockers/blockers.selectors';
import { selectDailyStandups } from './standups/standups.selectors';
import { selectTeamLeaves, selectSprintCapacity } from './leaves/leaves.selectors';
import { selectMonthlyFeedbacks } from './reviews/reviews.selectors';
import { selectRetroCards, selectRetroActions } from './retros/retros.selectors';
import { selectKudos } from './kudos/kudos.selectors';
import { selectTechDebtItems, selectTechTalkLogs } from './tech-hub/tech-hub.selectors';
import {
  Blocker,
  DailyStandup,
  KudosCard,
  MonthlyFeedback,
  PullRequestLog,
  RetroActionItem,
  RetroCard,
  Sprint,
  TeamLeave,
  TeamMember,
  TechDebtItem,
  TechTalkLog,
  WorkItem
} from '../models/scrum.models';

describe('Modular NgRx Selectors', () => {
  it('sprints selectors should project state correctly', () => {
    const mockSprint: Sprint = {
      id: 's-1',
      name: 'Sprint 25',
      goal: 'Goal',
      startDate: '2026-08-20',
      endDate: '2026-09-03',
      isActive: true,
      committedStoryPoints: 30,
      deliveredStoryPoints: 0,
      confidenceScore: 9
    };
    const state = { sprints: [mockSprint], activeSprint: mockSprint, activeSprintId: 's-1', loading: false, error: null };
    expect(selectAllSprints.projector(state)).toEqual([mockSprint]);
    expect(selectActiveSprint.projector(state)).toBe(mockSprint);
  });

  it('work items selectors should project workItems and loading state', () => {
    const mockItem: WorkItem = {
      id: 'w-1',
      key: 'SP-1',
      title: 'PBI 1',
      description: '',
      type: 'UserStory',
      status: 'InProgress',
      storyPoints: 5,
      priority: 'High',
      createdAtUtc: new Date().toISOString(),
      dorAcceptanceCriteriaDefined: true,
      dorDependenciesIdentified: true,
      dorWireframeAvailable: true,
      dodUnitTestsPassed: false,
      dodPeerReviewCompleted: false,
      dodMergedToMaster: false,
      dodStagingVerified: false,
      isEscapedDefect: false
    };
    const state = { items: [mockItem], loading: false, error: null };
    expect(selectAllWorkItems.projector(state)).toEqual([mockItem]);
    expect(selectWorkItemsLoading.projector(state)).toBeFalse();
  });

  it('pull requests selectors should project logs and developer metrics', () => {
    const mockLog: PullRequestLog = {
      id: 'pr-1',
      authorId: 'm-1',
      authorName: 'Alice',
      prNumber: 'PR-10',
      prTitle: 'Feat',
      prUrl: '',
      totalCommentsCount: 4,
      actionableCommentsCount: 2,
      reviewSummary: '',
      reviewStatus: 'Approved',
      createdAtUtc: new Date().toISOString()
    };
    const state = { prLogs: [mockLog], developerPrMetrics: [], loading: false, error: null };
    expect(selectPullRequestLogs.projector(state)).toEqual([mockLog]);
    expect(selectDeveloperPrMetrics.projector(state)).toEqual([]);
  });

  it('team members selectors should return currentRole and darkMode', () => {
    const state = { members: [], currentRole: 'Developer' as const, darkMode: true, loading: false, error: null };
    expect(selectAllMembers.projector(state)).toEqual([]);
    expect(selectCurrentRole.projector(state)).toBe('Developer');
    expect(selectIsDarkMode.projector(state)).toBeTrue();
  });

  it('blockers selectors should calculate active blocker count', () => {
    const mockBlocker: Blocker = {
      id: 'b-1',
      title: 'Blocker',
      description: 'Desc',
      category: 'EnvironmentAccess',
      slaHoursLimit: 24,
      raisedById: 'm-1',
      raisedAtUtc: new Date().toISOString(),
      isResolved: false,
      hoursWaiting: 1,
      isSlaBreached: false
    };
    const state = { blockers: [mockBlocker], loading: false, error: null };
    expect(selectAllBlockers.projector(state)).toEqual([mockBlocker]);
    expect(selectActiveBlockersCount.projector([mockBlocker])).toBe(1);
  });

  it('standups selectors should project daily standups', () => {
    const state = { standups: [], loading: false, error: null };
    expect(selectDailyStandups.projector(state)).toEqual([]);
  });

  it('leaves selectors should project team leaves and sprint capacity', () => {
    const state = { leaves: [], capacity: null, loading: false, error: null };
    expect(selectTeamLeaves.projector(state)).toEqual([]);
    expect(selectSprintCapacity.projector(state)).toBeNull();
  });

  it('reviews selectors should project monthly feedbacks', () => {
    const state = { feedbacks: [], loading: false, error: null };
    expect(selectMonthlyFeedbacks.projector(state)).toEqual([]);
  });

  it('retros selectors should project cards and actions', () => {
    const state = { cards: [], actions: [], loading: false, error: null };
    expect(selectRetroCards.projector(state)).toEqual([]);
    expect(selectRetroActions.projector(state)).toEqual([]);
  });

  it('kudos selectors should project kudos', () => {
    const state = { kudos: [], loading: false, error: null };
    expect(selectKudos.projector(state)).toEqual([]);
  });

  it('tech-hub selectors should project debt and tech talks', () => {
    const state = { techDebt: [], techTalks: [], loading: false, error: null };
    expect(selectTechDebtItems.projector(state)).toEqual([]);
    expect(selectTechTalkLogs.projector(state)).toEqual([]);
  });
});

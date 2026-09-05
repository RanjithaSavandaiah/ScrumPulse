import { sprintsReducer, initialSprintsState } from './sprints/sprints.reducer';
import { SprintActions } from './sprints/sprints.actions';
import { pullRequestsReducer, initialPullRequestsState } from './pull-requests/pull-requests.reducer';
import { PullRequestActions } from './pull-requests/pull-requests.actions';
import { teamMembersReducer, initialTeamMembersState } from './team-members/team-members.reducer';
import { TeamMemberActions } from './team-members/team-members.actions';
import { blockersReducer, initialBlockersState } from './blockers/blockers.reducer';
import { BlockerActions } from './blockers/blockers.actions';
import { standupsReducer, initialStandupsState } from './standups/standups.reducer';
import { StandupActions } from './standups/standups.actions';
import { leavesReducer, initialLeavesState } from './leaves/leaves.reducer';
import { LeaveActions } from './leaves/leaves.actions';
import { reviewsReducer, initialReviewsState } from './reviews/reviews.reducer';
import { ReviewActions } from './reviews/reviews.actions';
import { retrosReducer, initialRetrosState } from './retros/retros.reducer';
import { RetroActions } from './retros/retros.actions';
import { kudosReducer, initialKudosState } from './kudos/kudos.reducer';
import { KudosActions } from './kudos/kudos.actions';
import { techHubReducer, initialTechHubState } from './tech-hub/tech-hub.reducer';
import { TechHubActions } from './tech-hub/tech-hub.actions';
import { workItemsReducer, initialWorkItemsState } from './work-items/work-items.reducer';
import { WorkItemActions } from './work-items/work-items.actions';
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

describe('Modular NgRx Reducers', () => {
  describe('Sprints Reducer', () => {
    it('should set loading true on loadSprints', () => {
      const state = sprintsReducer(initialSprintsState, SprintActions.loadSprints());
      expect(state.loading).toBeTrue();
    });

    it('should set sprints on loadSprintsSuccess', () => {
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
      const state = sprintsReducer(initialSprintsState, SprintActions.loadSprintsSuccess({ sprints: [mockSprint] }));
      expect(state.sprints.length).toBe(1);
      expect(state.activeSprintId).toBe('s-1');
      expect(state.loading).toBeFalse();
    });

    it('should set active sprint on activateSprintSuccess', () => {
      const mockSprint: Sprint = {
        id: 's-2',
        name: 'Sprint 26',
        goal: 'Goal',
        startDate: '2026-09-04',
        endDate: '2026-09-18',
        isActive: true,
        committedStoryPoints: 35,
        deliveredStoryPoints: 0,
        confidenceScore: 8
      };
      const state = sprintsReducer(initialSprintsState, SprintActions.activateSprintSuccess({ sprint: mockSprint }));
      expect(state.activeSprintId).toBe('s-2');
      expect(state.activeSprint).toBe(mockSprint);
    });
  });

  describe('Pull Requests Reducer', () => {
    it('should append log on createPullRequestLogSuccess', () => {
      const mockPr: PullRequestLog = {
        id: 'pr-1',
        authorId: 'm-1',
        authorName: 'Kaushik (Developer)',
        prNumber: 'PR-101',
        prTitle: 'Feature',
        prUrl: 'https://url',
        totalCommentsCount: 5,
        actionableCommentsCount: 2,
        reviewSummary: 'Summary',
        reviewStatus: 'Approved',
        createdAtUtc: new Date().toISOString()
      };
      const state = pullRequestsReducer(initialPullRequestsState, PullRequestActions.createPullRequestLogSuccess({ log: mockPr }));
      expect(state.prLogs.length).toBe(1);
      expect(state.prLogs[0].prNumber).toBe('PR-101');
    });

    it('should populate prLogs on loadPullRequestsSuccess', () => {
      const state = pullRequestsReducer(initialPullRequestsState, PullRequestActions.loadPullRequestsSuccess({ prLogs: [] }));
      expect(state.prLogs).toEqual([]);
      expect(state.loading).toBeFalse();
    });
  });

  describe('Team Members Reducer', () => {
    it('should change currentRole on setCurrentRole', () => {
      const state = teamMembersReducer(initialTeamMembersState, TeamMemberActions.setCurrentRole({ role: 'Developer' }));
      expect(state.currentRole).toBe('Developer');
    });

    it('should toggle dark mode', () => {
      const state = teamMembersReducer(initialTeamMembersState, TeamMemberActions.toggleDarkMode());
      expect(state.darkMode).toBeFalse();
    });

    it('should append team member on createTeamMemberSuccess', () => {
      const mockMember: TeamMember = {
        id: 'tm-1',
        name: 'Eve',
        email: 'eve@scrum.io',
        role: 'Developer',
        location: 'Offshore',
        timeZone: 'IST',
        avatar: 'EV',
        activeWipLimit: 3
      };
      const state = teamMembersReducer(initialTeamMembersState, TeamMemberActions.createTeamMemberSuccess({ member: mockMember }));
      expect(state.members.length).toBe(1);
      expect(state.members[0].name).toBe('Eve');
    });
  });

  describe('Blockers Reducer', () => {
    const mockBlocker: Blocker = {
      id: 'b-1',
      title: 'Db unreachable',
      description: '',
      category: 'EnvironmentAccess',
      slaHoursLimit: 8,
      raisedById: 'm-1',
      raisedAtUtc: new Date().toISOString(),
      isResolved: false,
      hoursWaiting: 1,
      isSlaBreached: false
    };

    it('should load blockers on loadBlockersSuccess', () => {
      const state = blockersReducer(initialBlockersState, BlockerActions.loadBlockersSuccess({ blockers: [mockBlocker] }));
      expect(state.blockers.length).toBe(1);
      expect(state.loading).toBeFalse();
    });

    it('should add blocker on createBlockerSuccess', () => {
      const state = blockersReducer(initialBlockersState, BlockerActions.createBlockerSuccess({ blocker: mockBlocker }));
      expect(state.blockers.length).toBe(1);
    });

    it('should update blocker on updateBlockerSuccess', () => {
      const init = { ...initialBlockersState, blockers: [mockBlocker] };
      const updated = { ...mockBlocker, title: 'Db reached' };
      const state = blockersReducer(init, BlockerActions.updateBlockerSuccess({ blocker: updated }));
      expect(state.blockers[0].title).toBe('Db reached');
    });

    it('should remove blocker on deleteBlockerSuccess', () => {
      const init = { ...initialBlockersState, blockers: [mockBlocker] };
      const state = blockersReducer(init, BlockerActions.deleteBlockerSuccess({ id: 'b-1' }));
      expect(state.blockers.length).toBe(0);
    });
  });

  describe('Standups Reducer', () => {
    const mockStandup: DailyStandup = {
      id: 'st-1',
      teamMemberId: 'm-1',
      teamMemberName: 'Alice',
      teamMemberAvatar: 'AL',
      standupDate: '2026-09-05',
      yesterdaySummary: 'Auth',
      todayPlan: 'Testing',
      moodScore: 5
    };

    it('should handle loadStandupsSuccess', () => {
      const state = standupsReducer(initialStandupsState, StandupActions.loadStandupsSuccess({ standups: [mockStandup] }));
      expect(state.standups.length).toBe(1);
      expect(state.loading).toBeFalse();
    });

    it('should handle submitStandupSuccess', () => {
      const state = standupsReducer(initialStandupsState, StandupActions.submitStandupSuccess({ standup: mockStandup }));
      expect(state.standups.length).toBe(1);
    });

    it('should handle deleteStandupSuccess', () => {
      const init = { ...initialStandupsState, standups: [mockStandup] };
      const state = standupsReducer(init, StandupActions.deleteStandupSuccess({ id: 'st-1' }));
      expect(state.standups.length).toBe(0);
    });
  });

  describe('Leaves Reducer', () => {
    const mockLeave: TeamLeave = {
      id: 'l-1',
      teamMemberId: 'm-1',
      teamMemberName: 'Alice',
      startDate: '2026-09-10',
      endDate: '2026-09-12',
      reason: 'Vacation',
      leaveType: 'Privilege Leave',
      location: 'Bangalore',
      isApproved: true,
      totalDays: 3
    };

    it('should handle loadLeavesSuccess', () => {
      const state = leavesReducer(initialLeavesState, LeaveActions.loadLeavesSuccess({ leaves: [mockLeave] }));
      expect(state.leaves.length).toBe(1);
    });

    it('should handle submitLeaveSuccess and deleteLeaveSuccess', () => {
      let state = leavesReducer(initialLeavesState, LeaveActions.submitLeaveSuccess({ leave: mockLeave }));
      expect(state.leaves.length).toBe(1);

      state = leavesReducer(state, LeaveActions.deleteLeaveSuccess({ id: 'l-1' }));
      expect(state.leaves.length).toBe(0);
    });
  });

  describe('Reviews Reducer', () => {
    const mockFeedback: MonthlyFeedback = {
      id: 'f-1',
      teamMemberId: 'm-1',
      teamMemberName: 'Alice',
      monthYear: '2026-09',
      scrumMasterFeedback: 'Good',
      cdlFeedback: 'Solid',
      clientFeedback: 'Great',
      selfReflection: 'Nice',
      smRating: 5,
      happinessIndex: 9,
      actionItems: '',
      nextMonthGoals: '',
      createdAtUtc: new Date().toISOString()
    };

    it('should handle loadFeedbacksSuccess', () => {
      const state = reviewsReducer(initialReviewsState, ReviewActions.loadFeedbacksSuccess({ feedbacks: [mockFeedback] }));
      expect(state.feedbacks.length).toBe(1);
    });

    it('should handle submitFeedbackSuccess and deleteFeedbackSuccess', () => {
      let state = reviewsReducer(initialReviewsState, ReviewActions.submitFeedbackSuccess({ feedback: mockFeedback }));
      expect(state.feedbacks.length).toBe(1);

      state = reviewsReducer(state, ReviewActions.deleteFeedbackSuccess({ id: 'f-1' }));
      expect(state.feedbacks.length).toBe(0);
    });
  });

  describe('Retros Reducer', () => {
    const mockCard: RetroCard = {
      id: 'c-1',
      category: 'WentWell',
      content: 'CI speedup',
      isAnonymous: false,
      upvotesCount: 2
    };

    const mockAction: RetroActionItem = {
      id: 'a-1',
      title: 'Upgrade Node',
      isCompleted: false
    };

    it('should handle loadRetrosSuccess', () => {
      const state = retrosReducer(initialRetrosState, RetroActions.loadRetrosSuccess({ cards: [mockCard], actions: [mockAction] }));
      expect(state.cards.length).toBe(1);
      expect(state.actions.length).toBe(1);
    });

    it('should handle createRetroCardSuccess, voteRetroCard, and deleteRetroCardSuccess', () => {
      let state = retrosReducer(initialRetrosState, RetroActions.createRetroCardSuccess({ card: mockCard }));
      expect(state.cards.length).toBe(1);

      state = retrosReducer(state, RetroActions.voteRetroCard({ id: 'c-1' }));
      expect(state.cards[0].upvotesCount).toBe(3);

      state = retrosReducer(state, RetroActions.deleteRetroCardSuccess({ id: 'c-1' }));
      expect(state.cards.length).toBe(0);
    });
  });

  describe('Kudos Reducer', () => {
    const mockKudos: KudosCard = {
      id: 'k-1',
      senderId: 'm-1',
      senderName: 'Alice',
      receiverId: 'm-2',
      receiverName: 'Bob',
      badge: 'GoalCrusher',
      message: 'Great sprint',
      reactionEmojis: { clap: 1 },
      createdAtUtc: new Date().toISOString()
    };

    it('should handle loadKudosSuccess and giveKudosSuccess', () => {
      let state = kudosReducer(initialKudosState, KudosActions.loadKudosSuccess({ kudos: [mockKudos] }));
      expect(state.kudos.length).toBe(1);

      const mockKudos2 = { ...mockKudos, id: 'k-2' };
      state = kudosReducer(state, KudosActions.giveKudosSuccess({ kudos: mockKudos2 }));
      expect(state.kudos.length).toBe(2);
    });
  });

  describe('TechHub Reducer', () => {
    const mockDebt: TechDebtItem = {
      id: 'td-1',
      title: 'Refactor auth',
      description: '',
      severity: 'High',
      estimatedHours: 8,
      status: 'Identified'
    };

    const mockTalk: TechTalkLog = {
      id: 'tt-1',
      topic: 'Signals',
      presenterId: 'm-1',
      talkDate: '2026-09-01',
      durationMinutes: 30
    };

    it('should handle loadTechHubSuccess', () => {
      const state = techHubReducer(initialTechHubState, TechHubActions.loadTechHubSuccess({ techDebt: [mockDebt], techTalks: [mockTalk] }));
      expect(state.techDebt.length).toBe(1);
      expect(state.techTalks.length).toBe(1);
    });

    it('should handle deleteTechDebtSuccess and deleteTechTalkSuccess', () => {
      let state = techHubReducer(initialTechHubState, TechHubActions.loadTechHubSuccess({ techDebt: [mockDebt], techTalks: [mockTalk] }));
      state = techHubReducer(state, TechHubActions.deleteTechDebtSuccess({ id: 'td-1' }));
      expect(state.techDebt.length).toBe(0);

      state = techHubReducer(state, TechHubActions.deleteTechTalkSuccess({ id: 'tt-1' }));
      expect(state.techTalks.length).toBe(0);
    });
  });

  describe('WorkItems Reducer', () => {
    const mockItem: WorkItem = {
      id: 'wi-1',
      key: 'SP-1',
      title: 'Task 1',
      description: '',
      type: 'TaskPbi',
      status: 'Backlog',
      storyPoints: 5,
      priority: 'Medium',
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

    it('should handle loadWorkItemsSuccess', () => {
      const state = workItemsReducer(initialWorkItemsState, WorkItemActions.loadWorkItemsSuccess({ items: [mockItem] }));
      expect(state.items.length).toBe(1);
      expect(state.loading).toBeFalse();
    });

    it('should handle createWorkItemSuccess and deleteWorkItemSuccess', () => {
      let state = workItemsReducer(initialWorkItemsState, WorkItemActions.createWorkItemSuccess({ item: mockItem }));
      expect(state.items.length).toBe(1);

      state = workItemsReducer(state, WorkItemActions.deleteWorkItemSuccess({ id: 'wi-1' }));
      expect(state.items.length).toBe(0);
    });
  });
});

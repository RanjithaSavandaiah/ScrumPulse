import { sprintsReducer, initialSprintsState } from './sprints/sprints.reducer';
import { SprintActions } from './sprints/sprints.actions';
import { pullRequestsReducer, initialPullRequestsState } from './pull-requests/pull-requests.reducer';
import { PullRequestActions } from './pull-requests/pull-requests.actions';
import { teamMembersReducer, initialTeamMembersState } from './team-members/team-members.reducer';
import { TeamMemberActions } from './team-members/team-members.actions';
import { PullRequestLog, Sprint, TeamMember } from '../models/scrum.models';

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
  });
});

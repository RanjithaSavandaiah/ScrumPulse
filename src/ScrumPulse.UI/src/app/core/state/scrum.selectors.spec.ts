import { selectAllSprints, selectActiveSprint } from './sprints/sprints.selectors';
import { selectAllBlockers, selectActiveBlockersCount } from './blockers/blockers.selectors';
import { selectPullRequestLogs } from './pull-requests/pull-requests.selectors';
import { selectAllMembers, selectCurrentRole } from './team-members/team-members.selectors';
import { Blocker, PullRequestLog, Sprint, TeamMember } from '../models/scrum.models';

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
    expect(selectActiveBlockersCount.projector([mockBlocker])).toBe(1);
  });

  it('team members selectors should return currentRole', () => {
    const state = { members: [], currentRole: 'Developer' as const, darkMode: true, loading: false, error: null };
    expect(selectCurrentRole.projector(state)).toBe('Developer');
  });
});

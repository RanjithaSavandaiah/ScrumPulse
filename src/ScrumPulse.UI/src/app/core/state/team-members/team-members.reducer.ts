import { createReducer, on } from '@ngrx/store';
import { RoleType, TeamMember } from '../../models/scrum.models';
import { TeamMemberActions } from './team-members.actions';

export interface TeamMembersState {
  members: TeamMember[];
  currentRole: RoleType;
  darkMode: boolean;
  loading: boolean;
  error: string | null;
}

export const initialTeamMembersState: TeamMembersState = {
  members: [],
  currentRole: 'Developer',
  darkMode: true,
  loading: false,
  error: null
};

export const teamMembersReducer = createReducer(
  initialTeamMembersState,
  on(TeamMemberActions.loadTeamMembers, state => ({ ...state, loading: true, error: null })),
  on(TeamMemberActions.loadTeamMembersSuccess, (state, { members }) => ({ ...state, members, loading: false })),
  on(TeamMemberActions.loadTeamMembersFailure, (state, { error }) => ({ ...state, loading: false, error })),
  on(TeamMemberActions.createTeamMemberSuccess, (state, { member }) => ({
    ...state,
    members: [...state.members, member]
  })),
  on(TeamMemberActions.deleteTeamMemberSuccess, (state, { id }) => ({
    ...state,
    members: state.members.filter(m => m.id !== id)
  })),
  on(TeamMemberActions.setCurrentRole, (state, { role }) => ({ ...state, currentRole: role })),
  on(TeamMemberActions.toggleDarkMode, state => ({ ...state, darkMode: !state.darkMode }))
);

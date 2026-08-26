import { createFeatureSelector, createSelector } from '@ngrx/store';
import { TeamMembersState } from './team-members.reducer';

export const selectTeamMembersState = createFeatureSelector<TeamMembersState>('teamMembers');

export const selectAllMembers = createSelector(selectTeamMembersState, state => state.members);
export const selectCurrentRole = createSelector(selectTeamMembersState, state => state.currentRole);
export const selectIsDarkMode = createSelector(selectTeamMembersState, state => state.darkMode);
export const selectTeamMembersLoading = createSelector(selectTeamMembersState, state => state.loading);

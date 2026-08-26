import { createFeatureSelector, createSelector } from '@ngrx/store';
import { LeavesState } from './leaves.reducer';

export const selectLeavesState = createFeatureSelector<LeavesState>('leaves');

export const selectTeamLeaves = createSelector(selectLeavesState, state => state.leaves);
export const selectSprintCapacity = createSelector(selectLeavesState, state => state.capacity);
export const selectLeavesLoading = createSelector(selectLeavesState, state => state.loading);

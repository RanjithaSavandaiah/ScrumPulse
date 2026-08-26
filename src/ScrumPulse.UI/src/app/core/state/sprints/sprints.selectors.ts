import { createFeatureSelector, createSelector } from '@ngrx/store';
import { SprintsState } from './sprints.reducer';

export const selectSprintsState = createFeatureSelector<SprintsState>('sprints');

export const selectAllSprints = createSelector(selectSprintsState, state => state.sprints);
export const selectActiveSprint = createSelector(selectSprintsState, state => state.activeSprint);
export const selectActiveSprintId = createSelector(selectSprintsState, state => state.activeSprintId);
export const selectSprintsLoading = createSelector(selectSprintsState, state => state.loading);

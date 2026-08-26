import { createFeatureSelector, createSelector } from '@ngrx/store';
import { StandupsState } from './standups.reducer';

export const selectStandupsState = createFeatureSelector<StandupsState>('standups');

export const selectDailyStandups = createSelector(selectStandupsState, state => state.standups);
export const selectStandupsLoading = createSelector(selectStandupsState, state => state.loading);

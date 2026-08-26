import { createFeatureSelector, createSelector } from '@ngrx/store';
import { TechHubState } from './tech-hub.reducer';

export const selectTechHubState = createFeatureSelector<TechHubState>('techHub');

export const selectTechDebtItems = createSelector(selectTechHubState, state => state.techDebt);
export const selectTechTalkLogs = createSelector(selectTechHubState, state => state.techTalks);
export const selectTechHubLoading = createSelector(selectTechHubState, state => state.loading);

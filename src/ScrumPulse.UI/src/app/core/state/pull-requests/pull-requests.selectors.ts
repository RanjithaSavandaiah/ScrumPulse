import { createFeatureSelector, createSelector } from '@ngrx/store';
import { PullRequestsState } from './pull-requests.reducer';

export const selectPullRequestsState = createFeatureSelector<PullRequestsState>('pullRequests');

export const selectPullRequestLogs = createSelector(selectPullRequestsState, state => state.prLogs);
export const selectDeveloperPrMetrics = createSelector(selectPullRequestsState, state => state.developerPrMetrics);
export const selectPullRequestsLoading = createSelector(selectPullRequestsState, state => state.loading);

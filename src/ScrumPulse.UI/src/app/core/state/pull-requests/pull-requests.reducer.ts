import { createReducer, on } from '@ngrx/store';
import { DeveloperPrMetrics, PullRequestLog } from '../../models/scrum.models';
import { PullRequestActions } from './pull-requests.actions';

export interface PullRequestsState {
  prLogs: PullRequestLog[];
  developerPrMetrics: DeveloperPrMetrics[];
  loading: boolean;
  error: string | null;
}

export const initialPullRequestsState: PullRequestsState = {
  prLogs: [],
  developerPrMetrics: [],
  loading: false,
  error: null
};

export const pullRequestsReducer = createReducer(
  initialPullRequestsState,
  on(PullRequestActions.loadPullRequests, state => ({ ...state, loading: true, error: null })),
  on(PullRequestActions.loadPullRequestsSuccess, (state, { prLogs }) => ({ ...state, prLogs, loading: false })),
  on(PullRequestActions.loadPullRequestsFailure, (state, { error }) => ({ ...state, loading: false, error })),
  on(PullRequestActions.loadDeveloperPrMetricsSuccess, (state, { metrics }) => ({
    ...state,
    developerPrMetrics: metrics
  })),
  on(PullRequestActions.createPullRequestLogSuccess, (state, { log }) => ({
    ...state,
    prLogs: [log, ...state.prLogs]
  })),
  on(PullRequestActions.deletePullRequestLogSuccess, (state, { id }) => ({
    ...state,
    prLogs: state.prLogs.filter(p => p.id !== id)
  }))
);

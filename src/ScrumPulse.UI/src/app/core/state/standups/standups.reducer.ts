import { createReducer, on } from '@ngrx/store';
import { DailyStandup } from '../../models/scrum.models';
import { StandupActions } from './standups.actions';

export interface StandupsState {
  standups: DailyStandup[];
  loading: boolean;
  error: string | null;
}

export const initialStandupsState: StandupsState = {
  standups: [],
  loading: false,
  error: null
};

export const standupsReducer = createReducer(
  initialStandupsState,
  on(StandupActions.loadStandups, state => ({ ...state, loading: true, error: null })),
  on(StandupActions.loadStandupsSuccess, (state, { standups }) => ({ ...state, standups, loading: false })),
  on(StandupActions.loadStandupsFailure, (state, { error }) => ({ ...state, loading: false, error })),
  on(StandupActions.submitStandupSuccess, (state, { standup }) => ({
    ...state,
    standups: [standup, ...state.standups]
  })),
  on(StandupActions.updateStandupSuccess, (state, { standup }) => ({
    ...state,
    standups: state.standups.map(s => s.id === standup.id ? standup : s)
  })),
  on(StandupActions.deleteStandupSuccess, (state, { id }) => ({
    ...state,
    standups: state.standups.filter(s => s.id !== id)
  })),
  on(StandupActions.clearAllStandupsSuccess, state => ({
    ...state,
    standups: []
  }))
);

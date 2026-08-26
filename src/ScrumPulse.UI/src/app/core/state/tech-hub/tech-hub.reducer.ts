import { createReducer, on } from '@ngrx/store';
import { TechDebtItem, TechTalkLog } from '../../models/scrum.models';
import { TechHubActions } from './tech-hub.actions';

export interface TechHubState {
  techDebt: TechDebtItem[];
  techTalks: TechTalkLog[];
  loading: boolean;
  error: string | null;
}

export const initialTechHubState: TechHubState = {
  techDebt: [],
  techTalks: [],
  loading: false,
  error: null
};

export const techHubReducer = createReducer(
  initialTechHubState,
  on(TechHubActions.loadTechHub, state => ({ ...state, loading: true, error: null })),
  on(TechHubActions.loadTechHubSuccess, (state, { techDebt, techTalks }) => ({
    ...state,
    techDebt,
    techTalks,
    loading: false
  })),
  on(TechHubActions.loadTechHubFailure, (state, { error }) => ({ ...state, loading: false, error })),
  on(TechHubActions.createTechDebtSuccess, (state, { item }) => ({
    ...state,
    techDebt: [item, ...state.techDebt]
  })),
  on(TechHubActions.updateTechDebtSuccess, (state, { item }) => ({
    ...state,
    techDebt: state.techDebt.map(t => (t.id === item.id ? item : t))
  })),
  on(TechHubActions.deleteTechDebtSuccess, (state, { id }) => ({
    ...state,
    techDebt: state.techDebt.filter(t => t.id !== id)
  })),
  on(TechHubActions.resolveTechDebtSuccess, (state, { item }) => ({
    ...state,
    techDebt: state.techDebt.map(t => (t.id === item.id ? item : t))
  })),
  on(TechHubActions.logTechTalkSuccess, (state, { log }) => ({
    ...state,
    techTalks: [log, ...state.techTalks]
  })),
  on(TechHubActions.updateTechTalkSuccess, (state, { log }) => ({
    ...state,
    techTalks: state.techTalks.map(t => (t.id === log.id ? log : t))
  })),
  on(TechHubActions.deleteTechTalkSuccess, (state, { id }) => ({
    ...state,
    techTalks: state.techTalks.filter(t => t.id !== id)
  }))
);

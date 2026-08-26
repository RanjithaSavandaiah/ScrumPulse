import { createReducer, on } from '@ngrx/store';
import { Sprint } from '../../models/scrum.models';
import { SprintActions } from './sprints.actions';

export interface SprintsState {
  sprints: Sprint[];
  activeSprintId: string | null;
  activeSprint: Sprint | null;
  loading: boolean;
  error: string | null;
}

export const initialSprintsState: SprintsState = {
  sprints: [],
  activeSprintId: null,
  activeSprint: null,
  loading: false,
  error: null
};

export const sprintsReducer = createReducer(
  initialSprintsState,
  on(SprintActions.loadSprints, state => ({ ...state, loading: true, error: null })),
  on(SprintActions.loadSprintsSuccess, (state, { sprints }) => {
    const active = sprints.find(s => s.isActive) || sprints[0] || null;
    return {
      ...state,
      sprints,
      activeSprint: active,
      activeSprintId: active?.id || null,
      loading: false
    };
  }),
  on(SprintActions.loadSprintsFailure, (state, { error }) => ({ ...state, loading: false, error })),
  on(SprintActions.createSprintSuccess, (state, { sprint }) => {
    const updated = sprint.isActive
      ? [sprint, ...state.sprints.map(s => ({ ...s, isActive: false }))]
      : [sprint, ...state.sprints];
    return {
      ...state,
      sprints: updated,
      activeSprint: sprint.isActive ? sprint : state.activeSprint,
      activeSprintId: sprint.isActive ? sprint.id : state.activeSprintId
    };
  }),
  on(SprintActions.updateSprintSuccess, (state, { sprint }) => ({
    ...state,
    sprints: state.sprints.map(s => (s.id === sprint.id ? sprint : s)),
    activeSprint: state.activeSprintId === sprint.id ? sprint : state.activeSprint
  })),
  on(SprintActions.activateSprintSuccess, (state, { sprint }) => ({
    ...state,
    sprints: state.sprints.map(s => ({ ...s, isActive: s.id === sprint.id })),
    activeSprint: sprint,
    activeSprintId: sprint.id
  })),
  on(SprintActions.deleteSprintSuccess, (state, { sprintId }) => {
    const remaining = state.sprints.filter(s => s.id !== sprintId);
    const nextActive = state.activeSprintId === sprintId ? (remaining[0] || null) : state.activeSprint;
    return {
      ...state,
      sprints: remaining,
      activeSprint: nextActive,
      activeSprintId: nextActive?.id || null
    };
  })
);

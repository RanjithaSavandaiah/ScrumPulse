import { createReducer, on } from '@ngrx/store';
import { WorkItem } from '../../models/scrum.models';
import { WorkItemActions } from './work-items.actions';
import { SprintActions } from '../sprints/sprints.actions';

export interface WorkItemsState {
  items: WorkItem[];
  loading: boolean;
  error: string | null;
}

export const initialWorkItemsState: WorkItemsState = {
  items: [],
  loading: false,
  error: null
};

export const workItemsReducer = createReducer(
  initialWorkItemsState,
  on(WorkItemActions.loadWorkItems, state => ({ ...state, loading: true, error: null })),
  on(WorkItemActions.loadWorkItemsSuccess, (state, { items }) => ({
    ...state,
    items,
    loading: false
  })),
  on(WorkItemActions.loadWorkItemsFailure, (state, { error }) => ({ ...state, loading: false, error })),
  on(WorkItemActions.createWorkItemSuccess, (state, { item }) => ({
    ...state,
    items: [item, ...state.items]
  })),
  on(WorkItemActions.updateWorkItemSuccess, (state, { item }) => ({
    ...state,
    items: state.items.map(workItem => (workItem.id === item.id ? item : workItem))
  })),
  on(WorkItemActions.deleteWorkItemSuccess, (state, { id }) => ({
    ...state,
    items: state.items.filter(workItem => workItem.id !== id)
  })),
  on(WorkItemActions.advanceWorkItemStageSuccess, (state, { item }) => ({
    ...state,
    items: state.items.map(workItem => (workItem.id === item.id ? item : workItem))
  })),
  on(WorkItemActions.updateQualityGatesSuccess, (state, { item }) => ({
    ...state,
    items: state.items.map(workItem => (workItem.id === item.id ? item : workItem))
  })),
  on(SprintActions.deleteSprintSuccess, (state, { sprintId }) => ({
    ...state,
    items: state.items.map(workItem => (workItem.sprintId === sprintId ? { ...workItem, sprintId: undefined } : workItem))
  }))
);

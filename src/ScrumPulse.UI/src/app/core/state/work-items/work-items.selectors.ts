import { createFeatureSelector, createSelector } from '@ngrx/store';
import { WorkItemsState } from './work-items.reducer';

export const selectWorkItemsState = createFeatureSelector<WorkItemsState>('workItems');

export const selectAllWorkItems = createSelector(selectWorkItemsState, state => state.items);
export const selectWorkItemsBySprint = (sprintId: string) =>
  createSelector(selectAllWorkItems, items =>
    sprintId === 'ALL' ? items : items.filter(item => item.sprintId === sprintId)
  );
export const selectWorkItemsLoading = createSelector(selectWorkItemsState, state => state.loading);

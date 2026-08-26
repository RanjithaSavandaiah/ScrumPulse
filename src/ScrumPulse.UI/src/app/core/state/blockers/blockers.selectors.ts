import { createFeatureSelector, createSelector } from '@ngrx/store';
import { BlockersState } from './blockers.reducer';

export const selectBlockersState = createFeatureSelector<BlockersState>('blockers');

export const selectAllBlockers = createSelector(selectBlockersState, state => state.blockers);
export const selectActiveBlockers = createSelector(selectAllBlockers, blockers => blockers.filter(b => !b.isResolved));
export const selectActiveBlockersCount = createSelector(selectActiveBlockers, active => active.length);
export const selectBlockersLoading = createSelector(selectBlockersState, state => state.loading);

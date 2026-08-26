import { createFeatureSelector, createSelector } from '@ngrx/store';
import { RetrosState } from './retros.reducer';

export const selectRetrosState = createFeatureSelector<RetrosState>('retros');

export const selectRetroCards = createSelector(selectRetrosState, state => state.cards);
export const selectRetroActions = createSelector(selectRetrosState, state => state.actions);
export const selectRetrosLoading = createSelector(selectRetrosState, state => state.loading);

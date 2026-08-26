import { createFeatureSelector, createSelector } from '@ngrx/store';
import { KudosState } from './kudos.reducer';

export const selectKudosState = createFeatureSelector<KudosState>('kudos');

export const selectKudos = createSelector(selectKudosState, state => state.kudos);
export const selectKudosLoading = createSelector(selectKudosState, state => state.loading);

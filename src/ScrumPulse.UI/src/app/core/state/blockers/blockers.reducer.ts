import { createReducer, on } from '@ngrx/store';
import { Blocker } from '../../models/scrum.models';
import { BlockerActions } from './blockers.actions';

export interface BlockersState {
  blockers: Blocker[];
  loading: boolean;
  error: string | null;
}

export const initialBlockersState: BlockersState = {
  blockers: [],
  loading: false,
  error: null
};

export const blockersReducer = createReducer(
  initialBlockersState,
  on(BlockerActions.loadBlockers, state => ({ ...state, loading: true, error: null })),
  on(BlockerActions.loadBlockersSuccess, (state, { blockers }) => ({ ...state, blockers, loading: false })),
  on(BlockerActions.loadBlockersFailure, (state, { error }) => ({ ...state, loading: false, error })),
  on(BlockerActions.createBlockerSuccess, (state, { blocker }) => ({
    ...state,
    blockers: [blocker, ...state.blockers]
  })),
  on(BlockerActions.updateBlockerSuccess, (state, { blocker }) => ({
    ...state,
    blockers: state.blockers.map(blockerItem => (blockerItem.id === blocker.id ? blocker : blockerItem))
  })),
  on(BlockerActions.deleteBlockerSuccess, (state, { id }) => ({
    ...state,
    blockers: state.blockers.filter(blockerItem => blockerItem.id !== id)
  })),
  on(BlockerActions.resolveBlockerSuccess, (state, { blocker }) => ({
    ...state,
    blockers: state.blockers.map(blockerItem => (blockerItem.id === blocker.id ? blocker : blockerItem))
  }))
);

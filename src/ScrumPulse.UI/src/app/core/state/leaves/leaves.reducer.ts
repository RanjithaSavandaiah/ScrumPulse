import { createReducer, on } from '@ngrx/store';
import { SprintCapacity, TeamLeave } from '../../models/scrum.models';
import { LeaveActions } from './leaves.actions';

export interface LeavesState {
  leaves: TeamLeave[];
  capacity: SprintCapacity | null;
  loading: boolean;
  error: string | null;
}

export const initialLeavesState: LeavesState = {
  leaves: [],
  capacity: null,
  loading: false,
  error: null
};

export const leavesReducer = createReducer(
  initialLeavesState,
  on(LeaveActions.loadLeaves, state => ({ ...state, loading: true, error: null })),
  on(LeaveActions.loadLeavesSuccess, (state, { leaves }) => ({ ...state, leaves, loading: false })),
  on(LeaveActions.loadLeavesFailure, (state, { error }) => ({ ...state, loading: false, error })),
  on(LeaveActions.loadCapacitySuccess, (state, { capacity }) => ({ ...state, capacity })),
  on(LeaveActions.submitLeaveSuccess, (state, { leave }) => ({
    ...state,
    leaves: [leave, ...state.leaves]
  })),
  on(LeaveActions.updateLeaveSuccess, (state, { leave }) => ({
    ...state,
    leaves: state.leaves.map(l => l.id === leave.id ? leave : l)
  })),
  on(LeaveActions.deleteLeaveSuccess, (state, { id }) => ({
    ...state,
    leaves: state.leaves.filter(l => l.id !== id)
  }))
);

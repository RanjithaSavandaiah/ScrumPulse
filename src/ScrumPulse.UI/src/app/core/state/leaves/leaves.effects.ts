import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { of } from 'rxjs';
import { catchError, map, mergeMap, switchMap } from 'rxjs/operators';
import { LeaveActions } from './leaves.actions';
import { SprintCapacity, TeamLeave } from '../../models/scrum.models';

@Injectable()
export class LeavesEffects {
  private actions$ = inject(Actions);
  private http = inject(HttpClient);
  private apiUrl = '/api';

  loadLeaves$ = createEffect(() =>
    this.actions$.pipe(
      ofType(LeaveActions.loadLeaves),
      switchMap(() =>
        this.http.get<TeamLeave[]>(`${this.apiUrl}/leaves`).pipe(
          map(leaves => LeaveActions.loadLeavesSuccess({ leaves })),
          catchError(err => of(LeaveActions.loadLeavesFailure({ error: err.message })))
        )
      )
    )
  );

  loadCapacity$ = createEffect(() =>
    this.actions$.pipe(
      ofType(LeaveActions.loadCapacity),
      switchMap(({ sprintId }) =>
        this.http.get<SprintCapacity>(`${this.apiUrl}/leaves/capacity/${sprintId}`).pipe(
          map(capacity => LeaveActions.loadCapacitySuccess({ capacity })),
          catchError(err => of(LeaveActions.loadCapacityFailure({ error: err.message })))
        )
      )
    )
  );

  submitLeave$ = createEffect(() =>
    this.actions$.pipe(
      ofType(LeaveActions.submitLeave),
      mergeMap(({ leave }) =>
        this.http.post<TeamLeave>(`${this.apiUrl}/leaves`, leave).pipe(
          map(saved => LeaveActions.submitLeaveSuccess({ leave: saved })),
          catchError(err => of(LeaveActions.loadLeavesFailure({ error: err.message || 'Failed to submit leave' })))
        )
      )
    )
  );

  updateLeave$ = createEffect(() =>
    this.actions$.pipe(
      ofType(LeaveActions.updateLeave),
      mergeMap(({ id, leave }) =>
        this.http.put<TeamLeave>(`${this.apiUrl}/leaves/${id}`, leave).pipe(
          map(updated => LeaveActions.updateLeaveSuccess({ leave: updated })),
          catchError(err => of(LeaveActions.loadLeavesFailure({ error: err.message || 'Failed to update leave' })))
        )
      )
    )
  );

  deleteLeave$ = createEffect(() =>
    this.actions$.pipe(
      ofType(LeaveActions.deleteLeave),
      mergeMap(({ id }) =>
        this.http.delete(`${this.apiUrl}/leaves/${id}`).pipe(
          map(() => LeaveActions.deleteLeaveSuccess({ id })),
          catchError(err => of(LeaveActions.loadLeavesFailure({ error: err.message || 'Failed to delete leave' })))
        )
      )
    )
  );
}

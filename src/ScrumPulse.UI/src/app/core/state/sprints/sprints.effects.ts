import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { of } from 'rxjs';
import { catchError, map, mergeMap, switchMap } from 'rxjs/operators';
import { SprintActions } from './sprints.actions';
import { Sprint } from '../../models/scrum.models';

@Injectable()
export class SprintsEffects {
  private actions$ = inject(Actions);
  private http = inject(HttpClient);
  private apiUrl = '/api';

  loadSprints$ = createEffect(() =>
    this.actions$.pipe(
      ofType(SprintActions.loadSprints),
      switchMap(() =>
        this.http.get<Sprint[]>(`${this.apiUrl}/sprints`).pipe(
          map(sprints => SprintActions.loadSprintsSuccess({ sprints })),
          catchError(err => of(SprintActions.loadSprintsFailure({ error: err.message })))
        )
      )
    )
  );

  createSprint$ = createEffect(() =>
    this.actions$.pipe(
      ofType(SprintActions.createSprint),
      mergeMap(({ sprint }) =>
        this.http.post<Sprint>(`${this.apiUrl}/sprints`, sprint).pipe(
          map(created => SprintActions.createSprintSuccess({ sprint: created })),
          catchError(err => {
            console.error('[SprintsEffects] Failed to create sprint:', err);
            return of();
          })
        )
      )
    )
  );

  updateSprint$ = createEffect(() =>
    this.actions$.pipe(
      ofType(SprintActions.updateSprint),
      mergeMap(({ id, sprint }) =>
        this.http.put<Sprint>(`${this.apiUrl}/sprints/${id}`, sprint).pipe(
          map(updated => SprintActions.updateSprintSuccess({ sprint: updated })),
          catchError(err => {
            console.error('[SprintsEffects] Failed to update sprint:', err);
            return of();
          })
        )
      )
    )
  );

  activateSprint$ = createEffect(() =>
    this.actions$.pipe(
      ofType(SprintActions.activateSprint),
      mergeMap(({ sprintId }) =>
        this.http.post<Sprint>(`${this.apiUrl}/sprints/${sprintId}/activate`, {}).pipe(
          map(active => SprintActions.activateSprintSuccess({ sprint: active })),
          catchError(err => {
            console.error('[SprintsEffects] Failed to activate sprint:', err);
            return of();
          })
        )
      )
    )
  );

  deleteSprint$ = createEffect(() =>
    this.actions$.pipe(
      ofType(SprintActions.deleteSprint),
      mergeMap(({ sprintId }) =>
        this.http.delete(`${this.apiUrl}/sprints/${sprintId}`).pipe(
          map(() => SprintActions.deleteSprintSuccess({ sprintId })),
          catchError(err => {
            console.error('[SprintsEffects] Failed to delete sprint:', err);
            return of();
          })
        )
      )
    )
  );
}

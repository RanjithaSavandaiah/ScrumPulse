import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { of } from 'rxjs';
import { catchError, map, mergeMap, switchMap } from 'rxjs/operators';
import { SprintActions } from './sprints.actions';
import { Sprint } from '../../models/scrum.models';
import { NotificationService } from '../../services/notification.service';

@Injectable()
export class SprintsEffects {
  private actions$ = inject(Actions);
  private http = inject(HttpClient);
  private notification = inject(NotificationService);
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
          map(created => {
            this.notification.showSuccess('Sprint Created', `Sprint "${created.name}" added successfully.`, 'calendar');
            return SprintActions.createSprintSuccess({ sprint: created });
          }),
          catchError(err => {
            console.error('[SprintsEffects] Failed to create sprint:', err);
            const msg = err?.error?.detail || err?.error?.title || (err?.error?.errors ? Object.values(err.error.errors).flat().join(', ') : err?.message) || 'Failed to create sprint';
            this.notification.showError('Sprint Validation Error', msg, 'alert-triangle');
            return of(SprintActions.createSprintFailure({ error: msg }));
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
          map(updated => {
            this.notification.showSuccess('Sprint Updated', `Sprint "${updated.name}" updated successfully.`, 'calendar');
            return SprintActions.updateSprintSuccess({ sprint: updated });
          }),
          catchError(err => {
            console.error('[SprintsEffects] Failed to update sprint:', err);
            const msg = err?.error?.detail || err?.error?.title || (err?.error?.errors ? Object.values(err.error.errors).flat().join(', ') : err?.message) || 'Failed to update sprint';
            this.notification.showError('Sprint Update Failed', msg, 'alert-triangle');
            return of(SprintActions.updateSprintFailure({ error: msg }));
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
            const msg = err?.error?.detail || err?.error?.title || err?.message || 'Failed to activate sprint';
            this.notification.showError('Sprint Activation Failed', msg, 'alert-triangle');
            return of(SprintActions.activateSprintFailure({ error: msg }));
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
          map(() => {
            this.notification.showSuccess('Sprint Deleted', 'Sprint board was deleted.', 'trash-2');
            return SprintActions.deleteSprintSuccess({ sprintId });
          }),
          catchError(err => {
            console.error('[SprintsEffects] Failed to delete sprint:', err);
            const msg = err?.error?.detail || err?.error?.title || err?.message || 'Failed to delete sprint';
            this.notification.showError('Sprint Delete Failed', msg, 'alert-triangle');
            return of(SprintActions.deleteSprintFailure({ error: msg }));
          })
        )
      )
    )
  );
}

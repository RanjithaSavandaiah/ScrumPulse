import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { of } from 'rxjs';
import { catchError, map, mergeMap, switchMap } from 'rxjs/operators';
import { BlockerActions } from './blockers.actions';
import { Blocker } from '../../models/scrum.models';
import { NotificationService } from '../../services/notification.service';

@Injectable()
export class BlockersEffects {
  private actions$ = inject(Actions);
  private http = inject(HttpClient);
  private notification = inject(NotificationService);
  private apiUrl = '/api';

  loadBlockers$ = createEffect(() =>
    this.actions$.pipe(
      ofType(BlockerActions.loadBlockers),
      switchMap(() =>
        this.http.get<Blocker[]>(`${this.apiUrl}/blockers`).pipe(
          map(blockers => BlockerActions.loadBlockersSuccess({ blockers })),
          catchError(err => of(BlockerActions.loadBlockersFailure({ error: err.message })))
        )
      )
    )
  );

  createBlocker$ = createEffect(() =>
    this.actions$.pipe(
      ofType(BlockerActions.createBlocker),
      mergeMap(({ blocker }) =>
        this.http.post<Blocker>(`${this.apiUrl}/blockers`, blocker).pipe(
          map(created => BlockerActions.createBlockerSuccess({ blocker: created })),
          catchError(err => {
            console.error('[BlockersEffects] Failed to create blocker:', err);
            const msg = err?.error?.detail || err?.error?.title || (err?.error?.errors ? Object.values(err.error.errors).flat().join(', ') : err?.message) || 'Failed to create blocker';
            this.notification.showError('Validation Error', msg, 'alert-triangle');
            return of();
          })
        )
      )
    )
  );

  updateBlocker$ = createEffect(() =>
    this.actions$.pipe(
      ofType(BlockerActions.updateBlocker),
      mergeMap(({ id, blocker }) =>
        this.http.put<Blocker>(`${this.apiUrl}/blockers/${id}`, blocker).pipe(
          map(updated => BlockerActions.updateBlockerSuccess({ blocker: updated })),
          catchError(err => {
            console.error('[BlockersEffects] Failed to update blocker:', err);
            const msg = err?.error?.detail || err?.error?.title || (err?.error?.errors ? Object.values(err.error.errors).flat().join(', ') : err?.message) || 'Failed to update blocker';
            this.notification.showError('Validation Error', msg, 'alert-triangle');
            return of();
          })
        )
      )
    )
  );

  deleteBlocker$ = createEffect(() =>
    this.actions$.pipe(
      ofType(BlockerActions.deleteBlocker),
      mergeMap(({ id }) =>
        this.http.delete(`${this.apiUrl}/blockers/${id}`).pipe(
          map(() => BlockerActions.deleteBlockerSuccess({ id })),
          catchError(err => {
            console.error('[BlockersEffects] Failed to delete blocker:', err);
            return of();
          })
        )
      )
    )
  );

  resolveBlocker$ = createEffect(() =>
    this.actions$.pipe(
      ofType(BlockerActions.resolveBlocker),
      mergeMap(({ id, notes, blockedHours }) =>
        this.http.post<Blocker>(`${this.apiUrl}/blockers/${id}/resolve`, { resolutionNotes: notes || '', blockedHours }).pipe(
          map(resolved => BlockerActions.resolveBlockerSuccess({ blocker: resolved })),
          catchError(err => {
            console.error('[BlockersEffects] Failed to resolve blocker:', err);
            return of();
          })
        )
      )
    )
  );
}

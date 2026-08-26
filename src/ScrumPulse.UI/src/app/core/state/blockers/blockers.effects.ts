import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { of } from 'rxjs';
import { catchError, map, mergeMap, switchMap } from 'rxjs/operators';
import { BlockerActions } from './blockers.actions';
import { Blocker } from '../../models/scrum.models';

@Injectable()
export class BlockersEffects {
  private actions$ = inject(Actions);
  private http = inject(HttpClient);
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
          catchError(() => of())
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
          catchError(() => of())
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
          catchError(() => of())
        )
      )
    )
  );

  resolveBlocker$ = createEffect(() =>
    this.actions$.pipe(
      ofType(BlockerActions.resolveBlocker),
      mergeMap(({ id, notes }) =>
        this.http.post<Blocker>(`${this.apiUrl}/blockers/${id}/resolve`, { resolutionNotes: notes || '' }).pipe(
          map(resolved => BlockerActions.resolveBlockerSuccess({ blocker: resolved })),
          catchError(() => of())
        )
      )
    )
  );
}

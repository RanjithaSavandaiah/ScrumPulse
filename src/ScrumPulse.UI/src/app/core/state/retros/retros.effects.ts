import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { forkJoin, of } from 'rxjs';
import { catchError, map, mergeMap, switchMap } from 'rxjs/operators';
import { RetroActions } from './retros.actions';
import { RetroActionItem, RetroCard } from '../../models/scrum.models';

@Injectable()
export class RetrosEffects {
  private actions$ = inject(Actions);
  private http = inject(HttpClient);
  private apiUrl = '/api';

  loadRetros$ = createEffect(() =>
    this.actions$.pipe(
      ofType(RetroActions.loadRetros),
      switchMap(({ sprintId }) => {
        const cardsUrl = sprintId ? `${this.apiUrl}/retrospectives/cards?sprintId=${sprintId}` : `${this.apiUrl}/retrospectives/cards`;
        const actionsUrl = sprintId ? `${this.apiUrl}/retrospectives/actions?sprintId=${sprintId}` : `${this.apiUrl}/retrospectives/actions`;

        return forkJoin({
          cards: this.http.get<RetroCard[]>(cardsUrl).pipe(
            catchError(err => {
              console.error('[RetrosEffects] Failed to load retro cards:', err);
              return of([]);
            })
          ),
          actions: this.http.get<RetroActionItem[]>(actionsUrl).pipe(
            catchError(err => {
              console.error('[RetrosEffects] Failed to load retro actions:', err);
              return of([]);
            })
          )
        }).pipe(
          map(res => RetroActions.loadRetrosSuccess({ cards: res.cards, actions: res.actions })),
          catchError(err => of(RetroActions.loadRetrosFailure({ error: err.message })))
        );
      })
    )
  );

  createRetroCard$ = createEffect(() =>
    this.actions$.pipe(
      ofType(RetroActions.createRetroCard),
      mergeMap(({ card }) =>
        this.http.post<RetroCard>(`${this.apiUrl}/retrospectives/cards`, card).pipe(
          map(saved => RetroActions.createRetroCardSuccess({ card: saved })),
          catchError(err => of(RetroActions.loadRetrosFailure({ error: err.message })))
        )
      )
    )
  );

  updateRetroCard$ = createEffect(() =>
    this.actions$.pipe(
      ofType(RetroActions.updateRetroCard),
      mergeMap(({ id, card }) =>
        this.http.put<RetroCard>(`${this.apiUrl}/retrospectives/cards/${id}`, card).pipe(
          map(saved => RetroActions.updateRetroCardSuccess({ card: saved })),
          catchError(err => of(RetroActions.loadRetrosFailure({ error: err.message })))
        )
      )
    )
  );

  deleteRetroCard$ = createEffect(() =>
    this.actions$.pipe(
      ofType(RetroActions.deleteRetroCard),
      mergeMap(({ id }) =>
        this.http.delete(`${this.apiUrl}/retrospectives/cards/${id}`).pipe(
          map(() => RetroActions.deleteRetroCardSuccess({ id })),
          catchError(err => of(RetroActions.loadRetrosFailure({ error: err.message })))
        )
      )
    )
  );

  voteRetroCard$ = createEffect(() =>
    this.actions$.pipe(
      ofType(RetroActions.voteRetroCard),
      mergeMap(({ id }) =>
        this.http.post<RetroCard>(`${this.apiUrl}/retrospectives/cards/${id}/vote`, {}).pipe(
          map(updated => RetroActions.voteRetroCardSuccess({ card: updated })),
          catchError(err => of(RetroActions.loadRetrosFailure({ error: err.message })))
        )
      )
    )
  );

  createRetroAction$ = createEffect(() =>
    this.actions$.pipe(
      ofType(RetroActions.createRetroAction),
      mergeMap(({ action }) =>
        this.http.post<RetroActionItem>(`${this.apiUrl}/retrospectives/actions`, action).pipe(
          map(saved => RetroActions.createRetroActionSuccess({ action: saved })),
          catchError(err => of(RetroActions.loadRetrosFailure({ error: err.message })))
        )
      )
    )
  );

  updateRetroAction$ = createEffect(() =>
    this.actions$.pipe(
      ofType(RetroActions.updateRetroAction),
      mergeMap(({ id, action }) =>
        this.http.put<RetroActionItem>(`${this.apiUrl}/retrospectives/actions/${id}`, action).pipe(
          map(saved => RetroActions.updateRetroActionSuccess({ action: saved })),
          catchError(err => of(RetroActions.loadRetrosFailure({ error: err.message })))
        )
      )
    )
  );

  deleteRetroAction$ = createEffect(() =>
    this.actions$.pipe(
      ofType(RetroActions.deleteRetroAction),
      mergeMap(({ id }) =>
        this.http.delete(`${this.apiUrl}/retrospectives/actions/${id}`).pipe(
          map(() => RetroActions.deleteRetroActionSuccess({ id })),
          catchError(err => of(RetroActions.loadRetrosFailure({ error: err.message })))
        )
      )
    )
  );

  toggleRetroAction$ = createEffect(() =>
    this.actions$.pipe(
      ofType(RetroActions.toggleRetroAction),
      mergeMap(({ id }) =>
        this.http.post<RetroActionItem>(`${this.apiUrl}/retrospectives/actions/${id}/toggle`, {}).pipe(
          map(updated => RetroActions.toggleRetroActionSuccess({ action: updated })),
          catchError(err => of(RetroActions.loadRetrosFailure({ error: err.message })))
        )
      )
    )
  );
}

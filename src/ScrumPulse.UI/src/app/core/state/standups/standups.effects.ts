import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Actions, createEffect, ofType } from '@ngrx/effects';
import { of } from 'rxjs';
import { catchError, map, mergeMap, switchMap } from 'rxjs/operators';
import { StandupActions } from './standups.actions';
import { DailyStandup } from '../../models/scrum.models';

@Injectable()
export class StandupsEffects {
  private actions$ = inject(Actions);
  private http = inject(HttpClient);
  private apiUrl = '/api';

  loadStandups$ = createEffect(() =>
    this.actions$.pipe(
      ofType(StandupActions.loadStandups),
      switchMap(() =>
        this.http.get<DailyStandup[]>(`${this.apiUrl}/standups`).pipe(
          map(standups => StandupActions.loadStandupsSuccess({ standups })),
          catchError(err => of(StandupActions.loadStandupsFailure({ error: err.message })))
        )
      )
    )
  );

  submitStandup$ = createEffect(() =>
    this.actions$.pipe(
      ofType(StandupActions.submitStandup),
      mergeMap(({ standup }) =>
        this.http.post<DailyStandup>(`${this.apiUrl}/standups`, standup).pipe(
          map(saved => StandupActions.submitStandupSuccess({ standup: saved })),
          catchError(() => of())
        )
      )
    )
  );

  updateStandup$ = createEffect(() =>
    this.actions$.pipe(
      ofType(StandupActions.updateStandup),
      mergeMap(({ id, standup }) =>
        this.http.put<DailyStandup>(`${this.apiUrl}/standups/${id}`, standup).pipe(
          map(updated => StandupActions.updateStandupSuccess({ standup: updated })),
          catchError(() => of())
        )
      )
    )
  );

  deleteStandup$ = createEffect(() =>
    this.actions$.pipe(
      ofType(StandupActions.deleteStandup),
      mergeMap(({ id }) =>
        this.http.delete(`${this.apiUrl}/standups/${id}`).pipe(
          map(() => StandupActions.deleteStandupSuccess({ id })),
          catchError(() => of())
        )
      )
    )
  );

  clearAllStandups$ = createEffect(() =>
    this.actions$.pipe(
      ofType(StandupActions.clearAllStandups),
      mergeMap(() =>
        this.http.delete(`${this.apiUrl}/standups/clear-all`).pipe(
          map(() => StandupActions.clearAllStandupsSuccess()),
          catchError(() => of())
        )
      )
    )
  );
}
